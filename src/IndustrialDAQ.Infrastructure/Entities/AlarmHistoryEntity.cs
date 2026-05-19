// File: AlarmHistoryEntity.cs  Module: Infrastructure  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 报警历史实体 — 用于 SQLite 持久化存储报警记录。
/// </summary>
public sealed class AlarmHistoryEntity
{
    /// <summary>主键 ID。</summary>
    public long Id { get; set; }

    /// <summary>报警唯一标识。</summary>
    public string AlarmId { get; set; } = string.Empty;

    /// <summary>报警规则 ID。</summary>
    public string RuleId { get; set; } = string.Empty;

    /// <summary>报警类型。</summary>
    public AlarmType AlarmType { get; set; }

    /// <summary>报警严重程度。</summary>
    public AlarmSeverity Severity { get; set; }

    /// <summary>报警状态。</summary>
    public AlarmStatus Status { get; set; }

    /// <summary>报警来源设备名称。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>报警标题。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>报警详细消息。</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>关联测点 ID。</summary>
    public string TagId { get; set; } = string.Empty;

    /// <summary>关联测点名称。</summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>触发值。</summary>
    public double TriggerValue { get; set; }

    /// <summary>阈值（为了兼容旧版数据库架构保留）。</summary>
    public double Threshold { get; set; }

    /// <summary>发生时间 (UTC)。</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>确认时间 (UTC)。</summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>清除时间 (UTC)。</summary>
    public DateTime? ClearedAt { get; set; }

    /// <summary>创建时间 (UTC)。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 从领域模型转换。
    /// </summary>
    public static AlarmHistoryEntity FromDomain(AlarmRecord record, AlarmType alarmType)
    {
        return new AlarmHistoryEntity
        {
            AlarmId = record.Id,
            RuleId = record.RuleId,
            AlarmType = alarmType,
            Severity = record.Severity,
            Status = record.Status,
            Source = record.Source,
            Title = record.Title,
            Message = record.Message,
            TagId = record.TagId,
            TagName = record.TagName,
            TriggerValue = record.TriggerValue,
            Threshold = 0, // 为了兼容旧版架构的 NOT NULL 约束

            OccurredAt = record.OccurredAt,
            AcknowledgedAt = record.AcknowledgedAt,
            ClearedAt = record.ClearedAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// 转换为领域模型（时间转换为本地时间）。
    /// </summary>
    public AlarmRecord ToDomain()
    {
        return new AlarmRecord
        {
            Id = AlarmId,
            RuleId = RuleId,
            Severity = Severity,
            Source = Source,
            Title = Title,
            Message = Message,
            TagId = TagId,
            TagName = TagName,
            TriggerValue = TriggerValue,

            OccurredAt = DateTime.SpecifyKind(OccurredAt, DateTimeKind.Utc).ToLocalTime(),
            Status = Status,
            AcknowledgedAt = AcknowledgedAt.HasValue
                ? DateTime.SpecifyKind(AcknowledgedAt.Value, DateTimeKind.Utc).ToLocalTime()
                : null,
            ClearedAt = ClearedAt.HasValue
                ? DateTime.SpecifyKind(ClearedAt.Value, DateTimeKind.Utc).ToLocalTime()
                : null
        };
    }
}
