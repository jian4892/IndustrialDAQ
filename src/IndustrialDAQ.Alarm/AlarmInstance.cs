// File: AlarmInstance.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;
using RulesEngine.Models;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警运行实例 — 管理单个报警规则的运行时状态。
/// 包含状态机、触发值历史、冷却时间控制、并使用 RulesEngine 执行条件判断。
/// </summary>
public sealed class AlarmInstance
{
    private readonly AlarmDefinition _definition;
    private readonly AlarmStateMachine _stateMachine;
    private readonly RulesEngine.RulesEngine _rulesEngine;
    private readonly object _lock = new();

    /// <summary>报警唯一标识（规则级别，用于内部管理）。</summary>
    public string AlarmId { get; }

    /// <summary>当前报警事件 ID（每次触发生成新 ID，清除后重置）。</summary>
    public string? CurrentOccurrenceId { get; internal set; }

    /// <summary>关联的报警定义。</summary>
    public AlarmDefinition Definition => _definition;

    /// <summary>状态机。</summary>
    public AlarmStateMachine StateMachine => _stateMachine;

    /// <summary>当前状态。</summary>
    public AlarmState CurrentState => _stateMachine.CurrentState;

    /// <summary>首次触发时间 (UTC)（进入 Pending 的时间）。</summary>
    public DateTime? FirstTriggeredAt { get; private set; }

    /// <summary>激活时间 (UTC)（进入 Active 的时间）。</summary>
    public DateTime? ActivatedAt { get; private set; }

    /// <summary>最近触发时间 (UTC)。</summary>
    public DateTime? LastTriggeredAt { get; private set; }

    /// <summary>确认时间 (UTC)。</summary>
    public DateTime? AcknowledgedAt { get; private set; }

    /// <summary>恢复时间 (UTC)。</summary>
    public DateTime? ClearedAt { get; private set; }

    /// <summary>最近触发值。</summary>
    public double LastTriggerValue { get; private set; }

    /// <summary>触发次数。</summary>
    public int TriggerCount { get; private set; }

    /// <summary>状态变化事件。</summary>
    public event EventHandler<AlarmInstanceStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 初始化报警实例。
    /// </summary>
    public AlarmInstance(AlarmDefinition definition, string alarmId)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        AlarmId = alarmId ?? throw new ArgumentNullException(nameof(alarmId));
        _stateMachine = new AlarmStateMachine();

        // 订阅状态机事件
        _stateMachine.StateChanged += OnStateMachineStateChanged;

        // 初始化 RulesEngine
        var workflow = new Workflow
        {
            WorkflowName = "AlarmWorkflow",
            Rules = new List<Rule>
            {
                new Rule
                {
                    RuleName = "Condition",
                    Expression = _definition.ConditionExpression,
                    RuleExpressionType = RuleExpressionType.LambdaExpression
                }
            }
        };
        _rulesEngine = new RulesEngine.RulesEngine(new[] { workflow });
    }

    /// <summary>
    /// 处理状态机状态变化。
    /// </summary>
    private void OnStateMachineStateChanged(object? sender, AlarmStateChangedEventArgs e)
    {
        lock (_lock)
        {
            // 更新时间戳
            switch (e.NewState)
            {
                case AlarmState.Pending:
                    FirstTriggeredAt = e.Timestamp;
                    break;
                case AlarmState.Active:
                    ActivatedAt = e.Timestamp;
                    LastTriggeredAt = e.Timestamp;
                    TriggerCount++;
                    if (e.TriggerValue.HasValue)
                        LastTriggerValue = e.TriggerValue.Value;
                    break;
                case AlarmState.Acknowledged:
                    AcknowledgedAt = e.Timestamp;
                    break;
                case AlarmState.Normal:
                    ClearedAt = e.Timestamp;
                    FirstTriggeredAt = null; // 重置防抖时间
                    break;
            }

            // 只有进入 Active, Acknowledged, Normal 才向外广播事件（Pending 是内部防抖状态）
            if (e.NewState != AlarmState.Pending || e.OldState == AlarmState.Pending)
            {
                // 使用当前事件 ID
                string occurrenceId = CurrentOccurrenceId ?? AlarmId;

                // 触发实例状态变化事件
                StateChanged?.Invoke(this, new AlarmInstanceStateChangedEventArgs(
                    occurrenceId, _definition, e.OldState, e.NewState,
                    e.TriggerValue, e.Timestamp));
            }
        }
    }

    /// <summary>
    /// 评估当前值，触发状态转换。
    /// </summary>
    public async ValueTask<bool> EvaluateAsync(double currentValue)
    {
        bool conditionMet;
        try
        {
            var results = await _rulesEngine.ExecuteAllRulesAsync("AlarmWorkflow", new RuleParameter("Value", currentValue));
            conditionMet = results.Any(r => r.IsSuccess);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AlarmInstance] {AlarmId}: 表达式计算失败: {ex.Message}");
            return false;
        }

        lock (_lock)
        {
            bool isAlarming = _stateMachine.CurrentState == AlarmState.Active ||
                              _stateMachine.CurrentState == AlarmState.Acknowledged;
            bool isPending = _stateMachine.CurrentState == AlarmState.Pending;

            if (conditionMet)
            {
                if (!isAlarming && !isPending)
                {
                    // 检查冷却时间
                    if (ClearedAt.HasValue)
                    {
                        double elapsed = (DateTime.UtcNow - ClearedAt.Value).TotalSeconds;
                        if (elapsed < _definition.CooldownSeconds)
                        {
                            return false;
                        }
                    }

                    // 正常 -> 待定
                    return _stateMachine.TryTransition(AlarmState.Pending, currentValue);
                }
                else if (isPending)
                {
                    // 检查是否超过延时
                    if (FirstTriggeredAt.HasValue && (DateTime.UtcNow - FirstTriggeredAt.Value).TotalSeconds >= _definition.DelaySeconds)
                    {
                        // 待定 -> 激活
                        return _stateMachine.TryTransition(AlarmState.Active, currentValue);
                    }
                }
            }
            else
            {
                // 条件不满足
                if (isPending)
                {
                    // 在延时期间数据恢复正常，退回 Normal
                    return _stateMachine.TryTransition(AlarmState.Normal, currentValue);
                }
                else if (isAlarming)
                {
                    // 检查是否需要确认
                    if (_definition.RequireAck && _stateMachine.CurrentState != AlarmState.Acknowledged)
                    {
                        // 如果需要确认且尚未确认，保持活跃（或者进入 UnacknowledgedNormal，但目前沿用状态机，可保持 Active 或特殊处理，此处暂不自动恢复，必须操作员确认）
                        return false; 
                    }

                    // 检查滞回条件 (Hysteresis)
                    // 由于只有布尔表达式，我们需要再算一遍反向条件，或者简单点：如果不满足，再判断是否跨过了死区
                    // 如果简单处理：conditionMet 为 false，我们假定已经不在报警区间，但如果有 Hysteresis，我们需要防止抖动
                    // 更好的处理方式是：条件不满足时，如果设置了 Hysteresis > 0，我们使用一个反向逻辑，但纯表达式难以知道是偏大还是偏小
                    // 既然 "死区和回滞在警报引擎中实现"，可以在这里做处理：如果与 LastTriggerValue 差异未超过 Hysteresis，则不恢复
                    if (_definition.Hysteresis > 0 && LastTriggerValue != 0)
                    {
                        if (Math.Abs(currentValue - LastTriggerValue) <= _definition.Hysteresis)
                        {
                            return false; // 还在死区内，不恢复
                        }
                    }

                    return _stateMachine.TryTransition(AlarmState.Normal, currentValue);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 确认报警。
    /// </summary>
    public bool Acknowledge()
    {
        return _stateMachine.TryTransition(AlarmState.Acknowledged);
    }

    /// <summary>
    /// 生成报警记录。
    /// </summary>
    public AlarmRecord ToAlarmRecord(AlarmStatus status, string message)
    {
        return new AlarmRecord
        {
            Id = AlarmId,
            RuleId = _definition.RuleId,
            Severity = _definition.Severity,
            Source = _definition.Source,
            Title = _definition.Title,
            Message = message,
            TagId = _definition.TagId,
            TagName = _definition.TagName,
            TriggerValue = LastTriggerValue,

            OccurredAt = ActivatedAt ?? DateTime.UtcNow,
            Status = status,
            AcknowledgedAt = AcknowledgedAt,
            ClearedAt = ClearedAt
        };
    }

    /// <summary>
    /// 重置实例状态。
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _stateMachine.Reset();
            FirstTriggeredAt = null;
            ActivatedAt = null;
            LastTriggeredAt = null;
            AcknowledgedAt = null;
            ClearedAt = null;
            LastTriggerValue = 0;
            TriggerCount = 0;
        }
    }
}

/// <summary>
/// 报警实例状态变化事件参数。
/// </summary>
public sealed class AlarmInstanceStateChangedEventArgs : EventArgs
{
    public string AlarmId { get; }
    public AlarmDefinition Definition { get; }
    public AlarmState OldState { get; }
    public AlarmState NewState { get; }
    public double? TriggerValue { get; }
    public DateTime Timestamp { get; }

    public AlarmInstanceStateChangedEventArgs(string alarmId, AlarmDefinition definition,
        AlarmState oldState, AlarmState newState, double? triggerValue, DateTime timestamp)
    {
        AlarmId = alarmId;
        Definition = definition;
        OldState = oldState;
        NewState = newState;
        TriggerValue = triggerValue;
        Timestamp = timestamp;
    }
}
