// File: DataPointTemplateEntity.cs  Module: Infrastructure (Entities)  Author: IndustrialDAQ Team
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 数据点模板实体 — 用于 SQLite 持久化存储设备模板中的数据点配置。
/// 每条记录对应设备模板中的一个数据点，包含可选的报警模板和趋势模板关联。
/// </summary>
[Table("data_point_templates")]
public sealed class DataPointTemplateEntity
{
    /// <summary>自增主键。</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>所属设备模板 ID（外键）。</summary>
    public long DeviceTemplateId { get; set; }

    /// <summary>数据点模板唯一标识。</summary>
    [Required]
    [MaxLength(64)]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>数据点名称。</summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>数据类型（TagDataType 枚举的字节值）。</summary>
    public byte DataType { get; set; }

    /// <summary>工程单位。</summary>
    [MaxLength(32)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>关联的报警模板 ID（alarm_templates 表的 TemplateId），null 表示无报警。</summary>
    [MaxLength(64)]
    public string? AlarmTemplateId { get; set; }

    /// <summary>关联的趋势模板 ID（trend_templates 表的 TemplateId），null 表示无趋势配置。</summary>
    [MaxLength(64)]
    public string? TrendTemplateId { get; set; }
}
