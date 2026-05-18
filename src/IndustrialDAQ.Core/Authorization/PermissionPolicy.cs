using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Core.Authorization;

/// <summary>
/// Database-backed authorization policy.
/// ResourcePath is hierarchical and can be inherited by child resources.
/// </summary>
public sealed record PermissionPolicy
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public PermissionSubjectType SubjectType { get; init; }

    public string SubjectId { get; init; } = string.Empty;

    public ResourcePath ResourcePath { get; init; }

    public string Action { get; init; } = string.Empty;

    public PermissionEffect Effect { get; init; } = PermissionEffect.Deny;

    public bool Inherit { get; init; } = true;

    public int Priority { get; init; }

    public string? ConditionJson { get; init; }

    public bool IsEnabled { get; init; } = true;

    public long Version { get; init; } = 1;

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; init; } = DateTime.UtcNow;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SubjectId))
        {
            throw new InvalidOperationException("Permission policy requires SubjectId.");
        }

        if (string.IsNullOrWhiteSpace(Action))
        {
            throw new InvalidOperationException("Permission policy requires Action.");
        }
    }
}
