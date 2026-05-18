using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using IndustrialDAQ.Core.Authorization;
using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Infrastructure.Entities;

[Table("permission_policies")]
public sealed class PermissionPolicyEntity
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    [MaxLength(32)]
    public string SubjectType { get; set; } = nameof(PermissionSubjectType.Role);

    [Required]
    [MaxLength(128)]
    public string SubjectId { get; set; } = string.Empty;

    [Required]
    [MaxLength(512)]
    public string ResourcePath { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    public string Effect { get; set; } = nameof(PermissionEffect.Deny);

    public bool Inherit { get; set; } = true;

    public int Priority { get; set; }

    public string? ConditionJson { get; set; }

    public bool IsEnabled { get; set; } = true;

    public long Version { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public PermissionPolicy ToDomain() => new()
    {
        Id = Id,
        SubjectType = ParseEnum(SubjectType, PermissionSubjectType.Role),
        SubjectId = SubjectId,
        ResourcePath = new ResourcePath(ResourcePath),
        Action = Action,
        Effect = ParseEnum(Effect, PermissionEffect.Deny),
        Inherit = Inherit,
        Priority = Priority,
        ConditionJson = ConditionJson,
        IsEnabled = IsEnabled,
        Version = Version,
        CreatedAtUtc = CreatedAtUtc,
        UpdatedAtUtc = UpdatedAtUtc
    };

    public static PermissionPolicyEntity FromDomain(PermissionPolicy policy)
    {
        var entity = new PermissionPolicyEntity
        {
            Id = string.IsNullOrWhiteSpace(policy.Id) ? Guid.NewGuid().ToString("N") : policy.Id,
            CreatedAtUtc = policy.CreatedAtUtc == default ? DateTime.UtcNow : policy.CreatedAtUtc
        };

        Apply(policy, entity);
        return entity;
    }

    public static void Apply(PermissionPolicy policy, PermissionPolicyEntity entity)
    {
        policy.Validate();

        entity.SubjectType = policy.SubjectType.ToString();
        entity.SubjectId = policy.SubjectId;
        entity.ResourcePath = policy.ResourcePath.Value;
        entity.Action = policy.Action;
        entity.Effect = policy.Effect.ToString();
        entity.Inherit = policy.Inherit;
        entity.Priority = policy.Priority;
        entity.ConditionJson = policy.ConditionJson;
        entity.IsEnabled = policy.IsEnabled;
        entity.Version = Math.Max(1, policy.Version);
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback)
        where TEnum : struct
    {
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }
}
