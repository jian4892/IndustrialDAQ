namespace IndustrialDAQ.Alarm;

/// <summary>
/// 工业报警状态枚举 — 报警实例的完整生命周期状态。
/// 由 AlarmStateMachineService 驱动状态转换。
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