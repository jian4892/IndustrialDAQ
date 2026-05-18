// File: AlarmEngine.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警引擎 — 订阅实时数据库变更流，根据报警规则对测点值进行阈值判断，
/// 管理报警生命周期（触发→确认→清除），通过 <see cref="AlarmEventBus"/> 发布报警事件。
/// 支持回滞防抖和冷却时间，防止报警风暴。
/// 使用状态机管理报警状态转换，只在状态变化时触发事件。
/// 作为 <see cref="IHostedService"/> 运行。
/// </summary>
public sealed class AlarmEngine : IHostedService
{
    private readonly RealTimeStore _store;
    private readonly AlarmEventBus _eventBus;
    private readonly ILogger<AlarmEngine> _logger;

    /// <summary>所有报警规则（线程安全字典）。</summary>
    private readonly ConcurrentDictionary<string, AlarmDefinition> _rules = new();

    /// <summary>规则 → 报警实例（管理状态机）。</summary>
    private readonly ConcurrentDictionary<string, AlarmInstance> _instances = new();

    /// <summary>报警 ID 计数器。</summary>
    private long _alarmIdCounter;

    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    /// <summary>
    /// 初始化报警引擎。
    /// </summary>
    public AlarmEngine(RealTimeStore store, AlarmEventBus eventBus, ILogger<AlarmEngine> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 注册报警规则。
    /// </summary>
    public void RegisterRule(AlarmDefinition rule)
    {
        _rules[rule.RuleId] = rule;

        // 创建报警实例并订阅状态变化事件
        string alarmId = $"ALM-{Interlocked.Increment(ref _alarmIdCounter):D6}";
        var instance = new AlarmInstance(rule, alarmId);
        instance.StateChanged += OnAlarmStateChanged;
        _instances[rule.RuleId] = instance;

        _logger.LogInformation("已注册报警规则: {RuleId} [{TagName} {AlarmType}] {Title}",
            rule.RuleId, rule.TagName, rule.AlarmType, rule.Title);
    }

    /// <summary>
    /// 批量注册报警规则。
    /// </summary>
    public void RegisterRules(IEnumerable<AlarmDefinition> rules)
    {
        foreach (var rule in rules) RegisterRule(rule);
    }

    /// <summary>
    /// 确认报警。
    /// </summary>
    /// <param name="ruleId">规则 ID。</param>
    /// <returns>是否成功确认。</returns>
    public bool AcknowledgeAlarm(string ruleId)
    {
        if (_instances.TryGetValue(ruleId, out AlarmInstance? instance))
        {
            return instance.Acknowledge();
        }
        return false;
    }

    /// <summary>
    /// 获取所有活跃报警。
    /// </summary>
    public IReadOnlyList<AlarmInstance> GetActiveAlarms()
    {
        return _instances.Values
            .Where(i => i.CurrentState == AlarmState.Active || i.CurrentState == AlarmState.Acknowledged)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 获取所有报警规则。
    /// </summary>
    public IReadOnlyList<AlarmDefinition> GetRules()
    {
        return _rules.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// 获取所有报警实例。
    /// </summary>
    public IReadOnlyList<AlarmInstance> GetAllInstances()
    {
        return _instances.Values.ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("报警引擎已启动 ({Count} 条规则)", _rules.Count);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("报警引擎正在停止...");
        _cts?.Cancel();

        if (_consumeTask is not null)
        {
            try { await _consumeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }

        _eventBus.Complete();
        _cts?.Dispose();
        _logger.LogInformation("报警引擎已停止");
    }

    /// <summary>
    /// 主消费循环 — 监听实时数据变更，触发相关规则评估。
    /// </summary>
    private async Task ConsumeAsync(CancellationToken ct)
    {
        _logger.LogDebug("报警引擎消费循环已启动，当前注册规则数: {Count}", _rules.Count);
        try
        {
            var reader = _store.Subscribe();
            await foreach (TagValue value in reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();

                // 跳过质量不良的数据
                if (value.Quality == Quality.Bad)
                {
                    _logger.LogTrace("跳过质量不良数据: {TagId} = {Value}", value.TagId, value.Value);
                    continue;
                }

                // 查找关联此测点的报警规则
                var matchedRules = _rules.Values
                    .Where(r => r.Enabled && r.TagId == value.TagId)
                    .ToList();

                if (matchedRules.Count > 0)
                {
                    _logger.LogDebug("测点 {TagId} = {Value} 匹配到 {Count} 条规则",
                        value.TagId, value.Value, matchedRules.Count);
                }

                foreach (AlarmDefinition rule in matchedRules)
                {
                    if (_instances.TryGetValue(rule.RuleId, out AlarmInstance? instance))
                    {
                        await EvaluateInstanceAsync(instance, value);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // 正常退出
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报警引擎消费循环异常");
        }
    }

    /// <summary>
    /// 评估报警实例，处理状态转换。
    /// </summary>
    private async Task EvaluateInstanceAsync(AlarmInstance instance, TagValue value)
    {
        try
        {
            if (!TryConvertToDouble(value.Value, out double currentValue))
            {
                _logger.LogTrace("无法转换值: {TagId} = {Value}", value.TagId, value.Value);
                return;
            }

            _logger.LogDebug("评估规则 {RuleId}: {TagName} = {Value}, 表达式 = {Expression}, 类型 = {AlarmType}",
                instance.Definition.RuleId, instance.Definition.TagName, currentValue,
                instance.Definition.ConditionExpression, instance.Definition.AlarmType);

            bool stateChanged = await instance.EvaluateAsync(currentValue);

            if (stateChanged)
            {
                string severityLabel = instance.Definition.Severity switch
                {
                    AlarmSeverity.Critical => "严重",
                    AlarmSeverity.Warning => "警告",
                    _ => "信息"
                };

                string stateLabel = instance.CurrentState switch
                {
                    AlarmState.Active => "触发",
                    AlarmState.Acknowledged => "已确认",
                    AlarmState.Normal => "已恢复",
                    _ => "未知"
                };

                _logger.LogWarning("[{Severity}] {AlarmId}: {Title} — {TagName}={Value}, 状态: {State}",
                    severityLabel, instance.AlarmId, instance.Definition.Title,
                    instance.Definition.TagName, currentValue, stateLabel);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "评估报警实例 {AlarmId} 时发生异常", instance.AlarmId);
        }
    }

    /// <summary>
    /// 处理报警状态变化事件。
    /// </summary>
    private void OnAlarmStateChanged(object? sender, AlarmInstanceStateChangedEventArgs e)
    {
        try
        {
            var eventType = e.NewState switch
            {
                AlarmState.Active => AlarmEventType.Triggered,
                AlarmState.Acknowledged => AlarmEventType.Acknowledged,
                AlarmState.Normal => AlarmEventType.Cleared,
                _ => AlarmEventType.Triggered
            };

            // 每次报警触发生成新的 AlarmId，避免重复
            string alarmId;
            if (e.NewState == AlarmState.Active)
            {
                alarmId = $"ALM-{Interlocked.Increment(ref _alarmIdCounter):D6}";
                // 将新 ID 存入实例，供后续 Acknowledged/Normal 复用
                if (sender is AlarmInstance instance)
                    instance.CurrentOccurrenceId = alarmId;
            }
            else
            {
                alarmId = e.AlarmId;
                // Normal 状态清除事件 ID
                if (e.NewState == AlarmState.Normal && sender is AlarmInstance instance)
                    instance.CurrentOccurrenceId = null;
            }

            _logger.LogInformation("报警状态变化: AlarmId={AlarmId}, {OldState} -> {NewState}, 事件={EventType}, 触发值={Value}",
                alarmId, e.OldState, e.NewState, eventType, e.TriggerValue);

            var alarmStatus = e.NewState switch
            {
                AlarmState.Active => AlarmStatus.Active,
                AlarmState.Acknowledged => AlarmStatus.Acknowledged,
                AlarmState.Normal => AlarmStatus.Cleared,
                _ => AlarmStatus.Active
            };

            string message = BuildMessage(e.Definition, e.TriggerValue ?? 0, e.NewState);

            // 创建报警记录
            var record = new AlarmRecord
            {
                Id = alarmId,
                RuleId = e.Definition.RuleId,
                Severity = e.Definition.Severity,
                Source = e.Definition.Source,
                Title = e.Definition.Title,
                Message = message,
                TagId = e.Definition.TagId,
                TagName = e.Definition.TagName,
                TriggerValue = e.TriggerValue ?? 0,

                OccurredAt = e.Timestamp,
                Status = alarmStatus,
                AcknowledgedAt = e.NewState == AlarmState.Acknowledged ? e.Timestamp : null,
                ClearedAt = e.NewState == AlarmState.Normal ? e.Timestamp : null
            };

            // 发布到事件总线
            var alarmEvent = new AlarmEvent
            {
                EventType = eventType,
                AlarmId = alarmId,
                Rule = e.Definition,
                Record = record,
                TriggerValue = e.TriggerValue ?? 0,
                Timestamp = e.Timestamp,
                State = e.NewState
            };

            bool published = _eventBus.Publish(alarmEvent);
            if (!published)
            {
                _logger.LogWarning("报警事件发布失败: AlarmId={AlarmId}, EventType={EventType}", e.AlarmId, eventType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "处理报警状态变化事件时发生异常");
        }
    }

    /// <summary>
    /// 构建报警消息，替换消息模板中的占位符。
    /// </summary>
    private static string BuildMessage(AlarmDefinition rule, double currentValue, AlarmState state)
    {
        string prefix = state switch
        {
            AlarmState.Active => "[触发]",
            AlarmState.Acknowledged => "[已确认]",
            AlarmState.Normal => "[已恢复]",
            _ => ""
        };

        string baseMessage = string.IsNullOrWhiteSpace(rule.MessageTemplate)
            ? $"{rule.TagName} 当前值: {currentValue:F2}, 表达式: {rule.ConditionExpression}"
            : rule.MessageTemplate
                .Replace("{TagName}", rule.TagName)
                .Replace("{Value}", currentValue.ToString("F2"))
                .Replace("{Expression}", rule.ConditionExpression)
                .Replace("{Delay}", rule.DelaySeconds.ToString());

        return $"{prefix} {baseMessage}";
    }

    /// <summary>
    /// 安全尝试将 object 值转为 double。
    /// </summary>
    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return true;
            case float f: result = f; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case short s: result = s; return true;
            case byte b: result = b; return true;
            case ushort us: result = us; return true;
            case uint ui: result = ui; return true;
            case bool b: result = b ? 1.0 : 0.0; return true;
            default:
                result = 0;
                return false;
        }
    }
}
