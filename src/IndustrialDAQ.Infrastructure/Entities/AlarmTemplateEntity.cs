// File: AlarmTemplateEntity.cs  Module: Infrastructure (Entities)  Author: IndustrialDAQ Team
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 报警模板实体 — 用于 SQLite 持久化存储报警模板。
/// </summary>
[Table("alarm_templates")]
public sealed class AlarmTemplateEntity
{
    /// <summary>自增主键。</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>模板唯一标识。</summary>
    [Required]
    [MaxLength(64)]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>模板名称。</summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>适用数据类型（TagDataType 枚举字节值）。</summary>
    public byte ApplicableDataType { get; set; }

    /// <summary>工程单位。</summary>
    [MaxLength(32)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>高限阈值。</summary>
    public double HighThreshold { get; set; }

    /// <summary>高高限阈值。</summary>
    public double HighHighThreshold { get; set; }

    /// <summary>低限阈值。</summary>
    public double LowThreshold { get; set; }

    /// <summary>低低限阈值。</summary>
    public double LowLowThreshold { get; set; }

    /// <summary>滞回值（死区）。</summary>
    public double Hysteresis { get; set; }

    /// <summary>默认报警级别。</summary>
    public byte Severity { get; set; }

    /// <summary>冷却时间（秒）。</summary>
    public int CooldownSeconds { get; set; }

    /// <summary>支持的报警类型（JSON 数组序列化）。</summary>
    public string SupportedAlarmTypesJson { get; set; } = "[]";

    /// <summary>是否内置模板。</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>创建时间 (UTC)。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
