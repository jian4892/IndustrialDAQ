// File: DeviceTemplateEntity.cs  Module: Infrastructure (Entities)  Author: IndustrialDAQ Team
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 设备模板实体 — 用于 SQLite 持久化存储设备模板。
/// 区分内置模板（从 DeviceTemplateFactory 生成）和用户自定义模板。
/// </summary>
[Table("device_templates")]
public sealed class DeviceTemplateEntity
{
    /// <summary>自增主键。</summary>
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    /// <summary>模板唯一标识（如 "tpl-s7-1500-filling"）。</summary>
    [Required]
    [MaxLength(64)]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>模板名称。</summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>协议类型（OpcUA / Modbus / S7）。</summary>
    [Required]
    [MaxLength(32)]
    public string DriverType { get; set; } = string.Empty;

    /// <summary>是否内置模板（true = 从 DeviceTemplateFactory 生成，false = 用户创建）。</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>创建时间 (UTC)。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
