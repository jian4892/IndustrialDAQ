// File: TrendTemplateEntity.cs  Module: Infrastructure (Entities)  Author: IndustrialDAQ Team
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 趋势模板实体 — 用于 SQLite 持久化存储趋势模板。
/// </summary>
[Table("trend_templates")]
public sealed class TrendTemplateEntity
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

    /// <summary>工程单位。</summary>
    [MaxLength(32)]
    public string Unit { get; set; } = string.Empty;

    /// <summary>Y 轴最小值（null 表示自动）。</summary>
    public double? YMin { get; set; }

    /// <summary>Y 轴最大值（null 表示自动）。</summary>
    public double? YMax { get; set; }

    /// <summary>环形缓冲区容量。</summary>
    public int BufferCapacity { get; set; } = 3600;

    /// <summary>时间窗口（秒）。</summary>
    public int WindowSeconds { get; set; } = 300;

    /// <summary>曲线颜色（十六进制）。</summary>
    [MaxLength(16)]
    public string LineColor { get; set; } = "#3B82F6";

    /// <summary>是否显示报警线。</summary>
    public bool ShowAlarmLines { get; set; } = true;

    /// <summary>曲线线宽。</summary>
    public double StrokeThickness { get; set; } = 2;

    /// <summary>是否显示数据点。</summary>
    public bool ShowGeometry { get; set; }

    /// <summary>是否内置模板。</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>创建时间 (UTC)。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
