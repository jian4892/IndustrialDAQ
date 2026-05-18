using System.Collections.Concurrent;
using IndustrialDAQ.Alarm.StateMachine;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Alarm.Center;

/// <summary>
/// 工业报警中心。
/// 作为运行时当前报警的权威记录系统：消费状态转换信号，更新活跃报警视图，写入历史记录，
/// 并为 UI、MQTT 或 Redis 适配器发布报警中心事件。
/// </summary>
public sealed class AlarmCenter : IAlarmCenter
{
    private readonly IAlarmStateTransitionBus _transitionBus;
    private readonly IAlarmStateMachineService _stateMachineService;
    private readonly IAlarmCenterEventBus _eventBus;
    private readonly AlarmHistoryRepository _historyRepository;
    private readonly ILogger<AlarmCenter> _logger;
    /// <summary> 当前活跃报警记录集合，以 OccurrenceId 为键 </summary>
    private readonly ConcurrentDictionary<string, AlarmRecord> _currentAlarms = new(StringComparer.OrdinalIgnoreCase);
    /// <summary> 发生标识与规则 ID 的映射表 </summary>
    private readonly ConcurrentDictionary<string, string> _occurrenceToRule = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _cts;
    private Task? _consumeTask;

    /// <summary>
    /// 初始化报警中心的新实例。
    /// </summary>
    public AlarmCenter(
        IAlarmStateTransitionBus transitionBus,
        IAlarmStateMachineService stateMachineService,
        IAlarmCenterEventBus eventBus,
        AlarmHistoryRepository historyRepository,
        ILogger<AlarmCenter> logger)
    {
        _transitionBus = transitionBus ?? throw new ArgumentNullException(nameof(transitionBus));
        _stateMachineService = stateMachineService ?? throw new ArgumentNullException(nameof(stateMachineService));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary> 启动报警中心消费任务 </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _consumeTask = Task.Run(() => ConsumeAsync(_cts.Token), _cts.Token);
        _logger.LogInformation("报警中心已启动。");
        return Task.CompletedTask;
    }

    /// <summary> 停止报警中心服务 </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("报警中心正在停止...");
        _cts?.Cancel();

        if (_consumeTask is not null)
        {
            try
            {
                await _consumeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts?.Dispose();
        _logger.LogInformation("报警中心已停止。");
    }

    /// <summary> 获取所有当前活跃报警 </summary>
    public IReadOnlyList<AlarmRecord> GetCurrentAlarms()
    {
        return _currentAlarms.Values
            .OrderByDescending(static alarm => alarm.OccurredAt)
            .ToArray();
    }

    /// <summary> 查找指定发生标识的活跃报警 </summary>
    public AlarmRecord? FindCurrentAlarm(string occurrenceId)
    {
        return _currentAlarms.TryGetValue(occurrenceId, out var record) ? record : null;
    }

    /// <summary> 确认报警 </summary>
    public async Task<bool> AcknowledgeAsync(
        string occurrenceId,
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        if (!_occurrenceToRule.TryGetValue(occurrenceId, out var ruleId))
        {
            return false;
        }

        return await _stateMachineService
            .AcknowledgeAsync(ruleId, operatorId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> 确认所有活跃报警 </summary>
    public async Task<int> AcknowledgeAllAsync(
        string operatorId,
        CancellationToken cancellationToken = default)
    {
        var occurrenceIds = _currentAlarms.Values
            .Where(static alarm => alarm.Status == AlarmStatus.Active || alarm.Status == AlarmStatus.Cleared)
            .Select(static alarm => alarm.Id)
            .ToArray();

        var count = 0;
        foreach (var occurrenceId in occurrenceIds)
        {
            if (await AcknowledgeAsync(occurrenceId, operatorId, cancellationToken).ConfigureAwait(false))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary> 消费状态迁移信号异步循环 </summary>
    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var transition in _transitionBus.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ProcessTransitionAsync(transition, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "报警中心消费循环异常。");
        }
    }

    /// <summary> 处理状态迁移，更新内存视图并归档历史 </summary>
    private async ValueTask ProcessTransitionAsync(
        AlarmStateTransition transition,
        CancellationToken cancellationToken)
    {
        var record = BuildRecord(transition);
        var eventType = MapEventType(transition);

        switch (transition.ToState)
        {
            case AlarmState.Active:
                // 新报警触发，加入活跃列表
                _currentAlarms[transition.OccurrenceId] = record;
                _occurrenceToRule[transition.OccurrenceId] = transition.RuleId;
                await SaveNewOccurrenceIfNeededAsync(record, transition, cancellationToken).ConfigureAwait(false);
                break;

            case AlarmState.Acknowledged:
                // 报警被确认，更新内存记录并持久化状态
                UpdateCurrentAlarm(transition.OccurrenceId, alarm =>
                {
                    alarm.Status = AlarmStatus.Acknowledged;
                    alarm.AcknowledgedAt = transition.OccurredAt.UtcDateTime;
                });
                await _historyRepository.UpdateStatusAsync(
                    transition.OccurrenceId,
                    AlarmStatus.Acknowledged,
                    transition.OccurredAt.UtcDateTime,
                    null,
                    cancellationToken).ConfigureAwait(false);
                break;

            case AlarmState.Cleared:
                // 报警条件清除，但仍在列表中（等待确认）
                UpdateCurrentAlarm(transition.OccurrenceId, alarm =>
                {
                    alarm.Status = AlarmStatus.Cleared;
                    alarm.ClearedAt = transition.OccurredAt.UtcDateTime;
                });
                break;

            case AlarmState.Normal:
                // 报警彻底关闭，从活跃列表中移除并更新历史记录
                if (_currentAlarms.TryRemove(transition.OccurrenceId, out var removed))
                {
                    removed.Status = AlarmStatus.Cleared;
                    removed.ClearedAt ??= transition.OccurredAt.UtcDateTime;
                    _occurrenceToRule.TryRemove(transition.OccurrenceId, out _);

                    await _historyRepository.UpdateStatusAsync(
                        transition.OccurrenceId,
                        AlarmStatus.Cleared,
                        removed.AcknowledgedAt,
                        removed.ClearedAt,
                        cancellationToken).ConfigureAwait(false);
                }
                break;

            case AlarmState.Suppressed:
            case AlarmState.Shelved:
                _currentAlarms[transition.OccurrenceId] = record;
                _occurrenceToRule[transition.OccurrenceId] = transition.RuleId;
                break;
        }

        // 向外发布报警中心事件（供 UI 刷新等）
        await _eventBus.PublishAsync(new AlarmCenterEvent
        {
            Transition = transition,
            Record = record,
            EventType = eventType
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "报警中心处理了状态迁移。报警={AlarmId}, 规则={RuleId}, {From}->{To}",
            transition.OccurrenceId,
            transition.RuleId,
            transition.FromState,
            transition.ToState);
    }

    private async Task SaveNewOccurrenceIfNeededAsync(
        AlarmRecord record,
        AlarmStateTransition transition,
        CancellationToken cancellationToken)
    {
        // 如果是从 Cleared 状态跳回来的（同一个发生标识），不需要存新记录
        if (transition.FromState == AlarmState.Cleared)
        {
            return;
        }

        await _historyRepository
            .SaveAsync(record, transition.Definition.AlarmType, cancellationToken)
            .ConfigureAwait(false);
    }

    private void UpdateCurrentAlarm(string occurrenceId, Action<AlarmRecord> update)
    {
        if (_currentAlarms.TryGetValue(occurrenceId, out var existing))
        {
            update(existing);
        }
    }

    /// <summary> 根据状态迁移信息构建报警记录模型 </summary>
    private static AlarmRecord BuildRecord(AlarmStateTransition transition)
    {
        var status = transition.ToState switch
        {
            AlarmState.Acknowledged => AlarmStatus.Acknowledged,
            AlarmState.Cleared or AlarmState.Normal => AlarmStatus.Cleared,
            _ => AlarmStatus.Active
        };

        var value = TryConvertToDouble(transition.Value, out var numericValue) ? numericValue : 0;
        var definition = transition.Definition;

        return new AlarmRecord
        {
            Id = transition.OccurrenceId,
            RuleId = transition.RuleId,
            Severity = definition.Severity,
            Source = string.IsNullOrWhiteSpace(definition.Source)
                ? definition.TargetResourcePath?.Value ?? definition.ResourcePath?.Value ?? definition.TagName
                : definition.Source,
            Title = definition.Title,
            Message = BuildMessage(definition, transition, value),
            TagId = definition.TagId,
            TagName = string.IsNullOrWhiteSpace(definition.TagName) ? transition.RuleId : definition.TagName,
            TriggerValue = value,
            OccurredAt = transition.OccurredAt.UtcDateTime,
            Status = status,
            AcknowledgedAt = transition.ToState == AlarmState.Acknowledged ? transition.OccurredAt.UtcDateTime : null,
            ClearedAt = transition.ToState is AlarmState.Cleared or AlarmState.Normal ? transition.OccurredAt.UtcDateTime : null
        };
    }

    /// <summary> 填充报警消息模板 </summary>
    private static string BuildMessage(
        AlarmDefinition definition,
        AlarmStateTransition transition,
        double value)
    {
        var template = string.IsNullOrWhiteSpace(definition.MessageTemplate)
            ? "{TagName} {Value}, {AlarmCode}, {Expression}"
            : definition.MessageTemplate;

        return template
            .Replace("{TagName}", definition.TagName, StringComparison.OrdinalIgnoreCase)
            .Replace("{Value}", value.ToString("F2"), StringComparison.OrdinalIgnoreCase)
            .Replace("{AlarmCode}", definition.AlarmCode, StringComparison.OrdinalIgnoreCase)
            .Replace("{ResourcePath}", definition.TargetResourcePath?.Value ?? definition.ResourcePath?.Value ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{Expression}", definition.ConditionExpression, StringComparison.OrdinalIgnoreCase)
            .Replace("{State}", transition.ToState.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static AlarmCenterEventType MapEventType(AlarmStateTransition transition)
    {
        return transition.ToState switch
        {
            AlarmState.Active => AlarmCenterEventType.Raised,
            AlarmState.Acknowledged => AlarmCenterEventType.Acknowledged,
            AlarmState.Cleared => AlarmCenterEventType.Cleared,
            AlarmState.Normal => AlarmCenterEventType.Closed,
            AlarmState.Suppressed => AlarmCenterEventType.Suppressed,
            AlarmState.Shelved => AlarmCenterEventType.Shelved,
            _ => AlarmCenterEventType.Raised
        };
    }

    private static bool TryConvertToDouble(object? value, out double result)
    {
        switch (value)
        {
            case double item:
                result = item;
                return true;
            case float item:
                result = item;
                return true;
            case decimal item:
                result = (double)item;
                return true;
            case int item:
                result = item;
                return true;
            case long item:
                result = item;
                return true;
            case short item:
                result = item;
                return true;
            case bool item:
                result = item ? 1 : 0;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
