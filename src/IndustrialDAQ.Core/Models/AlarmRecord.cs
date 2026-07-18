namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 报警记录领域模型 — 记录一次报警的完整生命周期。
/// 重构后增加完整规则快照，可准确回答：
/// 哪个规则、哪个资源、哪个数据点、什么值、什么时候触发、从什么状态变成什么状态。
/// </summary>
public sealed class AlarmRecord
{
    /// <summary>报警唯一标识。</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>报警规则 ID（关联 AlarmDefinition）。</summary>
    public string RuleId { get; init; } = string.Empty;

    /// <summary>工程报警代码。</summary>
    public string AlarmCode { get; init; } = string.Empty;

    /// <summary>报警级别。</summary>
    public AlarmSeverity Severity { get; init; }

    /// <summary>报警来源设备名称。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>报警标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>报警详细消息（含当前值等上下文）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>关联测点 ID（运行时解析后的 TagId）。</summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>关联测点名称。</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>目标资源路径。</summary>
    public string TargetResourcePath { get; init; } = string.Empty;

    /// <summary>触发时的测点值。</summary>
    public double TriggerValue { get; init; }

    /// <summary>报警类型。</summary>
    public AlarmType AlarmType { get; init; }

    /// <summary>发生时间 (UTC)。</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>报警状态。</summary>
    public AlarmStatus Status { get; set; }

    /// <summary>确认时间 (UTC)。</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>清除时间 (UTC)。</summary>
    public DateTime? ClearedAt { get; set; }

    // ── 状态转换追踪字段 ──

    /// <summary>报警发生标识（格式: ALM-{ruleId}-{timestamp}）。</summary>
    public string OccurrenceId { get; init; } = string.Empty;

    /// <summary>转换前的状态。</summary>
    public string FromState { get; init; } = string.Empty;

    /// <summary>转换后的状态。</summary>
    public string ToState { get; init; } = string.Empty;
}