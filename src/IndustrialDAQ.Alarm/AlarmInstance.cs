// File: AlarmInstance.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警运行实例 — 管理单个报警规则的运行时状态。
/// 包含状态机、触发值历史、冷却时间控制等。
/// </summary>
public sealed class AlarmInstance
{
    private readonly AlarmRule _rule;
    private readonly AlarmStateMachine _stateMachine;
    private readonly object _lock = new();

    /// <summary>报警唯一标识。</summary>
    public string AlarmId { get; }

    /// <summary>关联的报警规则。</summary>
    public AlarmRule Rule => _rule;

    /// <summary>状态机。</summary>
    public AlarmStateMachine StateMachine => _stateMachine;

    /// <summary>当前状态。</summary>
    public AlarmState CurrentState => _stateMachine.CurrentState;

    /// <summary>首次触发时间 (UTC)。</summary>
    public DateTime? FirstTriggeredAt { get; private set; }

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
    /// <param name="rule">报警规则。</param>
    /// <param name="alarmId">报警唯一标识。</param>
    public AlarmInstance(AlarmRule rule, string alarmId)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        AlarmId = alarmId ?? throw new ArgumentNullException(nameof(alarmId));
        _stateMachine = new AlarmStateMachine();

        // 订阅状态机事件
        _stateMachine.StateChanged += OnStateMachineStateChanged;
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
                case AlarmState.Active:
                    FirstTriggeredAt ??= e.Timestamp;
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
                    break;
            }

            // 触发实例状态变化事件
            StateChanged?.Invoke(this, new AlarmInstanceStateChangedEventArgs(
                AlarmId, _rule, e.OldState, e.NewState,
                e.TriggerValue, e.Timestamp));
        }
    }

    /// <summary>
    /// 评估当前值，触发状态转换。
    /// </summary>
    /// <param name="currentValue">当前值。</param>
    /// <returns>是否发生了状态变化。</returns>
    public bool Evaluate(double currentValue)
    {
        lock (_lock)
        {
            bool conditionMet = EvaluateCondition(currentValue);
            bool isAlarming = _stateMachine.CurrentState == AlarmState.Active ||
                             _stateMachine.CurrentState == AlarmState.Acknowledged;

            // 调试日志
            System.Diagnostics.Debug.WriteLine(
                $"[AlarmInstance] {Rule.RuleId}: Value={currentValue}, ConditionMet={conditionMet}, " +
                $"IsAlarming={isAlarming}, State={_stateMachine.CurrentState}, " +
                $"Threshold={Rule.Threshold}, HighHigh={Rule.HighHighThreshold}");

            if (conditionMet && !isAlarming)
            {
                // 检查冷却时间
                if (LastTriggeredAt.HasValue)
                {
                    double elapsed = (DateTime.UtcNow - LastTriggeredAt.Value).TotalSeconds;
                    if (elapsed < _rule.CooldownSeconds)
                    {
                        System.Diagnostics.Debug.WriteLine($"[AlarmInstance] {Rule.RuleId}: 冷却中，剩余 {Rule.CooldownSeconds - elapsed:F1} 秒");
                        return false;
                    }
                }

                // 转换到活跃状态
                var result = _stateMachine.TryTransition(AlarmState.Active, currentValue);
                System.Diagnostics.Debug.WriteLine($"[AlarmInstance] {Rule.RuleId}: 状态转换结果={result}");
                return result;
            }
            else if (!conditionMet && isAlarming)
            {
                // 检查滞回条件
                if (ShouldClear(currentValue))
                {
                    return _stateMachine.TryTransition(AlarmState.Normal, currentValue);
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 确认报警。
    /// </summary>
    /// <returns>是否成功确认。</returns>
    public bool Acknowledge()
    {
        return _stateMachine.TryTransition(AlarmState.Acknowledged);
    }

    /// <summary>
    /// 评估报警条件是否满足。
    /// </summary>
    private bool EvaluateCondition(double value) => _rule.AlarmType switch
    {
        AlarmType.High => value > _rule.Threshold,
        AlarmType.Low => value < _rule.Threshold,
        AlarmType.HighHigh => value > (_rule.HighHighThreshold > 0 ? _rule.HighHighThreshold : _rule.Threshold),
        AlarmType.LowLow => value < (_rule.LowLowThreshold > 0 ? _rule.LowLowThreshold : _rule.Threshold),
        AlarmType.Bool => Convert.ToBoolean(value),
        _ => false
    };

    /// <summary>
    /// 检查是否应该清除报警（考虑滞回）。
    /// </summary>
    private bool ShouldClear(double value) => _rule.AlarmType switch
    {
        AlarmType.High => value <= _rule.Threshold - _rule.Hysteresis,
        AlarmType.Low => value >= _rule.Threshold + _rule.Hysteresis,
        AlarmType.HighHigh => value <= (_rule.HighHighThreshold > 0 ? _rule.HighHighThreshold : _rule.Threshold) - _rule.Hysteresis,
        AlarmType.LowLow => value >= (_rule.LowLowThreshold > 0 ? _rule.LowLowThreshold : _rule.Threshold) + _rule.Hysteresis,
        AlarmType.Bool => !Convert.ToBoolean(value),
        _ => false
    };

    /// <summary>
    /// 生成报警记录。
    /// </summary>
    /// <param name="status">报警状态。</param>
    /// <param name="message">报警消息。</param>
    /// <returns>报警记录。</returns>
    public AlarmRecord ToAlarmRecord(AlarmStatus status, string message)
    {
        return new AlarmRecord
        {
            Id = AlarmId,
            RuleId = _rule.RuleId,
            Severity = _rule.Severity,
            Source = _rule.Source,
            Title = _rule.Title,
            Message = message,
            TagId = _rule.TagId,
            TagName = _rule.TagName,
            TriggerValue = LastTriggerValue,
            Threshold = _rule.Threshold,
            OccurredAt = FirstTriggeredAt ?? DateTime.UtcNow,
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
    /// <summary>报警 ID。</summary>
    public string AlarmId { get; }

    /// <summary>报警规则。</summary>
    public AlarmRule Rule { get; }

    /// <summary>旧状态。</summary>
    public AlarmState OldState { get; }

    /// <summary>新状态。</summary>
    public AlarmState NewState { get; }

    /// <summary>触发值。</summary>
    public double? TriggerValue { get; }

    /// <summary>状态变化时间 (UTC)。</summary>
    public DateTime Timestamp { get; }

    public AlarmInstanceStateChangedEventArgs(string alarmId, AlarmRule rule,
        AlarmState oldState, AlarmState newState, double? triggerValue, DateTime timestamp)
    {
        AlarmId = alarmId;
        Rule = rule;
        OldState = oldState;
        NewState = newState;
        TriggerValue = triggerValue;
        Timestamp = timestamp;
    }
}
