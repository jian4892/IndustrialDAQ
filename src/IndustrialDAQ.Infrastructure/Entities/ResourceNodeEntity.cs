using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IndustrialDAQ.Infrastructure.Entities;

/// <summary>
/// 运行时资源树的 EF Core 实体。
/// 该表设计为通用型：设备、标签、菜单项、报警、规则和授权目标都共享相同的资源路径模型。
/// </summary>
[Table("resource_nodes")]
public sealed class ResourceNodeEntity
{
    /// <summary> 获取或设置主键 ID。 </summary>
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary> 获取或设置父节点 ID。 </summary>
    [MaxLength(64)]
    public string? ParentId { get; set; }

    /// <summary> 获取或设置规格化的资源路径。 </summary>
    [Required]
    [MaxLength(512)]
    public string ResourcePath { get; set; } = string.Empty;

    /// <summary> 获取或设置节点内部名称。 </summary>
    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary> 获取或设置节点显示名称。 </summary>
    [Required]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary> 获取或设置资源类型字符串。 </summary>
    [Required]
    [MaxLength(32)]
    public string ResourceType { get; set; } = string.Empty;

    /// <summary> 获取或设置排序权重。 </summary>
    public int SortOrder { get; set; }

    /// <summary> 获取或设置一个值，指示该节点是否启用。 </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary> 获取或设置关联的元数据 JSON。 </summary>
    public string? MetadataJson { get; set; }

    /// <summary> 获取或设置版本号。 </summary>
    public long Version { get; set; } = 1;

    /// <summary> 获取或设置创建时间（UTC）。 </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary> 获取或设置更新时间（UTC）。 </summary>
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
