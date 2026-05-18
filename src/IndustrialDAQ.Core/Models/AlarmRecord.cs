namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 报警记录领域模型 — 记录一次报警的完整生命周期。
/// </summary>
public sealed class AlarmRecord
{
    /// <summary>报警唯一标识。</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>报警规则 ID（关联 AlarmRule）。</summary>
    public string RuleId { get; init; } = string.Empty;

    /// <summary>报警级别。</summary>
    public AlarmSeverity Severity { get; init; }

    /// <summary>报警来源设备名称。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>报警标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>报警详细消息（含当前值等上下文）。</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>关联测点 ID。</summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>关联测点名称。</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>触发时的测点值。</summary>
    public double TriggerValue { get; init; }


    /// <summary>发生时间 (UTC)。</summary>
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>报警状态。</summary>
    public AlarmStatus Status { get; set; }

    /// <summary>确认时间 (UTC)。</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>清除时间 (UTC)。</summary>
    public DateTime? ClearedAt { get; set; }
}
