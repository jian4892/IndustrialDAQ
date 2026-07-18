using IndustrialDAQ.Core.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 报警历史持久化实体。
/// 重构后增加完整规则快照字段：AlarmCode、TargetResourcePath、OccurrenceId、FromState、ToState。
/// </summary>
[Table("AlarmHistories")]
public sealed class AlarmHistoryEntity
{
    /// <summary>主键（自增）。</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>报警 ID（短标识）。</summary>
    [Required]
    [MaxLength(64)]
    public string AlarmId { get; set; } = string.Empty;

    /// <summary>规则 ID。</summary>
    [Required]
    [MaxLength(128)]
    public string RuleId { get; set; } = string.Empty;

    /// <summary>工程报警代码。</summary>
    [MaxLength(128)]
    public string AlarmCode { get; set; } = string.Empty;

    /// <summary>报警类型。</summary>
    [MaxLength(32)]
    public string AlarmType { get; set; } = nameof(IndustrialDAQ.Core.Models.AlarmType.High);

    /// <summary>报警级别。</summary>
    [Required]
    [MaxLength(32)]
    public string Severity { get; set; } = nameof(AlarmSeverity.Warning);

    /// <summary>报警状态。</summary>
    [Required]
    [MaxLength(32)]
    public string Status { get; set; } = nameof(AlarmStatus.Active);

    /// <summary>报警来源。</summary>
    [MaxLength(256)]
    public string Source { get; set; } = string.Empty;

    /// <summary>报警标题。</summary>
    [MaxLength(256)]
    public string Title { get; set; } = string.Empty;

    /// <summary>报警消息。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>关联测点 ID。</summary>
    [MaxLength(128)]
    public string TagId { get; set; } = string.Empty;

    /// <summary>关联测点名称。</summary>
    [MaxLength(256)]
    public string TagName { get; set; } = string.Empty;

    /// <summary>目标资源路径。</summary>
    [MaxLength(512)]
    public string TargetResourcePath { get; set; } = string.Empty;

    /// <summary>触发值。</summary>
    public double TriggerValue { get; set; }

    /// <summary>阈值（兼容旧版查询）。</summary>
    public double Threshold { get; set; }

    /// <summary>发生时间 (UTC)。</summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    /// <summary>确认时间 (UTC)。</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>清除时间 (UTC)。</summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>创建时间 (UTC)。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── 状态转换追踪字段 ──

    /// <summary>报警发生标识。</summary>
    [MaxLength(128)]
    public string OccurrenceId { get; set; } = string.Empty;

    /// <summary>转换前的状态。</summary>
    [MaxLength(32)]
    public string FromState { get; set; } = string.Empty;

    /// <summary>转换后的状态。</summary>
    [MaxLength(32)]
    public string ToState { get; set; } = string.Empty;

    /// <summary>
    /// 从领域模型创建实体。
    /// </summary>
    public static AlarmHistoryEntity FromDomain(AlarmRecord record)
    {
        return new AlarmHistoryEntity
        {
            AlarmId = record.Id,
            RuleId = record.RuleId,
            AlarmCode = record.AlarmCode,
            AlarmType = record.AlarmType.ToString(),
            Severity = record.Severity.ToString(),
            Status = record.Status.ToString(),
            Source = record.Source,
            Title = record.Title,
            Message = record.Message,
            TagId = record.TagId,
            TagName = record.TagName,
            TargetResourcePath = record.TargetResourcePath,
            TriggerValue = record.TriggerValue,
            OccurredAt = record.OccurredAt,
            AcknowledgedAt = record.AcknowledgedAt,
            ClearedAt = record.ClearedAt,
            OccurrenceId = record.OccurrenceId,
            FromState = record.FromState,
            ToState = record.ToState,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 转换为领域模型。
    /// </summary>
    public AlarmRecord ToDomain()
    {
        return new AlarmRecord
        {
            Id = AlarmId,
            RuleId = RuleId,
            AlarmCode = AlarmCode,
            Severity = Enum.TryParse<AlarmSeverity>(Severity, out var sev) ? sev : AlarmSeverity.Warning,
            Source = Source,
            Title = Title,
            Message = Message,
            TagId = TagId,
            TagName = TagName,
            TargetResourcePath = TargetResourcePath,
            TriggerValue = TriggerValue,
            AlarmType = Enum.TryParse<AlarmType>(AlarmType, out var at) ? at :  Core.Models.AlarmType.High,
            OccurredAt = OccurredAt,
            Status = Enum.TryParse<AlarmStatus>(Status, out var st) ? st : AlarmStatus.Active,
            AcknowledgedAt = AcknowledgedAt,
            ClearedAt = ClearedAt,
            OccurrenceId = OccurrenceId,
            FromState = FromState,
            ToState = ToState
        };
    }
}