// File: AlarmStateMachine.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警状态机 — 管理单个报警实例的状态转换。
/// 状态转换规则：
/// Normal → Active (报警条件满足)
/// Active → Acknowledged (操作员确认)
/// Active → Normal (报警条件不满足，自动恢复)
/// Acknowledged → Normal (报警条件不满足，自动恢复)
/// 只在状态变化时触发事件，防止重复报警。
/// </summary>
public sealed class AlarmStateMachine
{
    private AlarmState _currentState = AlarmState.Normal;
    private readonly object _lock = new();

    /// <summary>当前状态。</summary>
    public AlarmState CurrentState
    {
        get { lock (_lock) return _currentState; }
    }

    /// <summary>状态变化事件 — 仅在状态真正变化时触发。</summary>
    public event EventHandler<AlarmStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// 尝试转换到新状态。
    /// </summary>
    /// <param name="newState">目标状态。</param>
    /// <param name="triggerValue">触发值（可选）。</param>
    /// <returns>是否成功转换（状态真正变化）。</returns>
    public bool TryTransition(AlarmState newState, double? triggerValue = null)
    {
        lock (_lock)
        {
            if (_currentState == newState)
                return false; // 状态未变化，不触发事件

            // 验证状态转换是否合法
            if (!IsValidTransition(_currentState, newState))
                return false;

            var oldState = _currentState;
            _currentState = newState;

            // 触发状态变化事件
            StateChanged?.Invoke(this, new AlarmStateChangedEventArgs(
                oldState, newState, triggerValue, DateTime.UtcNow));

            return true;
        }
    }

    /// <summary>
    /// 重置状态机到初始状态。
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentState = AlarmState.Normal;
        }
    }

    /// <summary>
    /// 验证状态转换是否合法。
    /// </summary>
    private static bool IsValidTransition(AlarmState from, AlarmState to) => (from, to) switch
    {
        (AlarmState.Normal, AlarmState.Pending) => true,
        (AlarmState.Normal, AlarmState.Active) => true,
        (AlarmState.Normal, AlarmState.Suppressed) => true,
        (AlarmState.Pending, AlarmState.Active) => true,
        (AlarmState.Pending, AlarmState.Normal) => true,
        (AlarmState.Pending, AlarmState.Suppressed) => true,
        (AlarmState.Active, AlarmState.Acknowledged) => true,
        (AlarmState.Active, AlarmState.Cleared) => true,
        (AlarmState.Active, AlarmState.Normal) => true,
        (AlarmState.Active, AlarmState.Suppressed) => true,
        (AlarmState.Active, AlarmState.Shelved) => true,
        (AlarmState.Acknowledged, AlarmState.Cleared) => true,
        (AlarmState.Acknowledged, AlarmState.Normal) => true,
        (AlarmState.Acknowledged, AlarmState.Suppressed) => true,
        (AlarmState.Cleared, AlarmState.Normal) => true,
        (AlarmState.Cleared, AlarmState.Acknowledged) => true,
        (AlarmState.Cleared, AlarmState.Active) => true,
        (AlarmState.Cleared, AlarmState.Suppressed) => true,
        (AlarmState.Suppressed, AlarmState.Normal) => true,
        (AlarmState.Suppressed, AlarmState.Pending) => true,
        (AlarmState.Suppressed, AlarmState.Active) => true,
        (AlarmState.Shelved, AlarmState.Active) => true,
        (AlarmState.Shelved, AlarmState.Normal) => true,
        _ => false
    };
}

/// <summary>
/// 报警状态枚举。
/// </summary>
public enum AlarmState : byte
{
    /// <summary> 正常 — 报警条件未满足。 </summary>
    Normal = 0,

    /// <summary> 待定 — 报警条件刚满足，正在防抖延时（DelayMs）中。 </summary>
    Pending = 1,

    /// <summary> 活跃 — 报警条件已满足，且已超过延时时间，等待操作员确认。 </summary>
    Active = 2,

    /// <summary> 已确认 — 操作员已确认报警，但报警条件仍然存在。 </summary>
    Acknowledged = 3,

    /// <summary> 已清除 — 报警条件已消失，但根据策略（如需确认）仍在等待最终关闭。 </summary>
    Cleared = 4,

    /// <summary> 抑制 — 由于维护、禁用、联锁或运行时策略而被抑制。 </summary>
    Suppressed = 5,

    /// <summary> 搁置 — 由操作员根据策略暂时搁置。 </summary>
    Shelved = 6
}

/// <summary>
/// 报警状态变化事件参数。
/// </summary>
public sealed class AlarmStateChangedEventArgs : EventArgs
{
    /// <summary>旧状态。</summary>
    public AlarmState OldState { get; }

    /// <summary>新状态。</summary>
    public AlarmState NewState { get; }

    /// <summary>触发值。</summary>
    public double? TriggerValue { get; }

    /// <summary>状态变化时间 (UTC)。</summary>
    public DateTime Timestamp { get; }

    public AlarmStateChangedEventArgs(AlarmState oldState, AlarmState newState,
        double? triggerValue, DateTime timestamp)
    {
        OldState = oldState;
        NewState = newState;
        TriggerValue = triggerValue;
        Timestamp = timestamp;
    }
}
