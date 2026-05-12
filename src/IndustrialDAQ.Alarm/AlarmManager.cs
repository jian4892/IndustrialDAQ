// File: AlarmManager.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警管理服务 — 协调报警引擎、事件总线和历史存储。
/// 作为 <see cref="IHostedService"/> 运行，订阅报警事件并持久化到数据库。
/// 提供统一的报警管理 API。
/// </summary>
public sealed class AlarmManager : IHostedService
{
    private readonly AlarmEngine _engine;
    private readonly AlarmEventBus _eventBus;
    private readonly AlarmHistoryRepository _repository;
    private readonly ILogger<AlarmManager> _logger;

    /// <summary>实时报警列表（线程安全）。</summary>
    private readonly ConcurrentDictionary<string, AlarmRecord> _activeAlarms = new();

    /// <summary>报警事件消费任务。</summary>
    private Task? _consumeTask;
    private CancellationTokenSource? _cts;

    /// <summary>报警事件触发（供 UI 订阅）。</summary>
    public event EventHandler<AlarmEventArgs>? AlarmTriggered;

    /// <summary>报警确认事件（供 UI 订阅）。</summary>
    public event EventHandler<AlarmEventArgs>? AlarmAcknowledged;

    /// <summary>报警恢复事件（供 UI 订阅）。</summary>
    public event EventHandler<AlarmEventArgs>? AlarmCleared;

    /// <summary>实时报警列表变更事件。</summary>
    public event EventHandler? ActiveAlarmsChanged;

    /// <summary>
    /// 初始化报警管理服务。
    /// </summary>
    public AlarmManager(AlarmEngine engine, AlarmEventBus eventBus,
        AlarmHistoryRepository repository, ILogger<AlarmManager> logger)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 注册报警规则。
    /// </summary>
    public void RegisterRule(AlarmRule rule)
    {
        _engine.RegisterRule(rule);
    }

    /// <summary>
    /// 批量注册报警规则。
    /// </summary>
    public void RegisterRules(IEnumerable<AlarmRule> rules)
    {
        _engine.RegisterRules(rules);
    }

    /// <summary>
    /// 确认报警。
    /// </summary>
    /// <param name="alarmId">报警 ID。</param>
    /// <returns>是否成功确认。</returns>
    public bool AcknowledgeAlarm(string alarmId)
    {
        // 查找对应的规则 ID
        var ruleId = _activeAlarms.Values
            .FirstOrDefault(a => a.Id == alarmId)?.RuleId;

        if (ruleId is not null)
        {
            return _engine.AcknowledgeAlarm(ruleId);
        }
        return false;
    }

    /// <summary>
    /// 确认所有活跃报警。
    /// </summary>
    public void AcknowledgeAllAlarms()
    {
        var activeAlarms = _activeAlarms.Values
            .Where(a => a.Status == AlarmStatus.Active)
            .ToList();

        foreach (var alarm in activeAlarms)
        {
            AcknowledgeAlarm(alarm.Id);
        }
    }

    /// <summary>
    /// 获取实时报警列表。
    /// </summary>
    public IReadOnlyList<AlarmRecord> GetActiveAlarms()
    {
        return _activeAlarms.Values
            .OrderByDescending(a => a.OccurredAt)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 获取报警历史记录。
    /// </summary>
    public async Task<(IReadOnlyList<AlarmRecord> Records, int TotalCount)> GetHistoryAsync(
        int pageNumber = 1, int pageSize = 50,
        AlarmStatus? status = null, AlarmSeverity? severity = null,
        string? tagId = null, DateTime? startTime = null, DateTime? endTime = null,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetHistoryAsync(pageNumber, pageSize, status, severity,
            tagId, startTime, endTime, cancellationToken);
    }

    /// <summary>
    /// 获取报警统计信息。
    /// </summary>
    public async Task<AlarmStatistics> GetStatisticsAsync(
        DateTime startTime, DateTime endTime,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetStatisticsAsync(startTime, endTime, cancellationToken);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _consumeTask = Task.Run(() => ConsumeEventsAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("报警管理服务已启动");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("报警管理服务正在停止...");
        _cts?.Cancel();

        if (_consumeTask is not null)
        {
            try { await _consumeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _cts?.Dispose();
        _logger.LogInformation("报警管理服务已停止");
    }

    /// <summary>
    /// 消费报警事件，更新实时列表并持久化到数据库。
    /// </summary>
    private async Task ConsumeEventsAsync(CancellationToken ct)
    {
        _logger.LogDebug("报警管理服务消费循环已启动");
        try
        {
            await foreach (var alarmEvent in _eventBus.Subscribe(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                _logger.LogDebug("收到报警事件: {AlarmId}, 类型: {EventType}, 规则: {RuleId}",
                    alarmEvent.AlarmId, alarmEvent.EventType, alarmEvent.Rule.RuleId);

                try
                {
                    await ProcessAlarmEventAsync(alarmEvent, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理报警事件失败: {AlarmId}", alarmEvent.AlarmId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报警管理服务消费循环异常");
        }
    }

    /// <summary>
    /// 处理单个报警事件。
    /// </summary>
    private async Task ProcessAlarmEventAsync(AlarmEvent alarmEvent, CancellationToken ct)
    {
        var record = alarmEvent.Record;

        _logger.LogInformation("处理报警事件: AlarmId={AlarmId}, EventType={EventType}, RuleId={RuleId}",
            alarmEvent.AlarmId, alarmEvent.EventType, alarmEvent.Rule.RuleId);

        switch (alarmEvent.EventType)
        {
            case AlarmEventType.Triggered:
                // 检查是否已存在相同规则的活跃报警
                var existingAlarm = _activeAlarms.Values
                    .FirstOrDefault(a => a.RuleId == record.RuleId &&
                                        (a.Status == AlarmStatus.Active || a.Status == AlarmStatus.Acknowledged));

                if (existingAlarm is not null)
                {
                    // 已存在活跃报警，不创建新记录，只触发UI刷新
                    _logger.LogDebug("已存在活跃报警 {AlarmId}，跳过重复保存", existingAlarm.Id);
                    AlarmTriggered?.Invoke(this, new AlarmEventArgs(existingAlarm));
                }
                else
                {
                    // 新报警，添加到实时列表并保存到数据库
                    _activeAlarms[alarmEvent.AlarmId] = record;
                    _logger.LogInformation("保存新报警到数据库: AlarmId={AlarmId}", alarmEvent.AlarmId);
                    await _repository.SaveAsync(record, alarmEvent.Rule.AlarmType, ct);
                    AlarmTriggered?.Invoke(this, new AlarmEventArgs(record));
                }
                break;

            case AlarmEventType.Acknowledged:
                // 更新实时列表
                if (_activeAlarms.TryGetValue(alarmEvent.AlarmId, out var ackedAlarm))
                {
                    ackedAlarm.Status = AlarmStatus.Acknowledged;
                    ackedAlarm.AcknowledgedAt = alarmEvent.Timestamp;
                }
                // 更新数据库
                _logger.LogInformation("更新报警为已确认: AlarmId={AlarmId}", alarmEvent.AlarmId);
                await _repository.UpdateStatusAsync(alarmEvent.AlarmId,
                    AlarmStatus.Acknowledged, alarmEvent.Timestamp, null, ct);
                // 触发事件
                AlarmAcknowledged?.Invoke(this, new AlarmEventArgs(record));
                break;

            case AlarmEventType.Cleared:
                // 从实时列表移除
                bool removed = _activeAlarms.TryRemove(alarmEvent.AlarmId, out _);
                _logger.LogInformation("报警清除: AlarmId={AlarmId}, 从活跃列表移除={Removed}", alarmEvent.AlarmId, removed);
                // 更新数据库
                try
                {
                    await _repository.UpdateStatusAsync(alarmEvent.AlarmId,
                        AlarmStatus.Cleared, null, alarmEvent.Timestamp, ct);
                    _logger.LogInformation("数据库已更新报警状态为已清除: AlarmId={AlarmId}", alarmEvent.AlarmId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "更新数据库报警状态失败: AlarmId={AlarmId}", alarmEvent.AlarmId);
                }
                // 触发事件
                AlarmCleared?.Invoke(this, new AlarmEventArgs(record));
                break;
        }

        // 通知实时列表变更
        ActiveAlarmsChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 报警事件参数。
/// </summary>
public sealed class AlarmEventArgs : EventArgs
{
    /// <summary>报警记录。</summary>
    public AlarmRecord Record { get; }

    public AlarmEventArgs(AlarmRecord record)
    {
        Record = record ?? throw new ArgumentNullException(nameof(record));
    }
}
