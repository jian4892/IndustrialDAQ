namespace IndustrialDAQ.Core.Authorization;

/// <summary>
/// User plus inherited role/group identities used in authorization checks.
/// </summary>
public sealed record PermissionSubject
{
    public required string UserId { get; init; }

    public IReadOnlySet<string> RoleIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> GroupIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool IsSystem { get; init; }

    public IEnumerable<(PermissionSubjectType Type, string Id)> EnumerateIdentities()
    {
        if (IsSystem)
        {
            yield return (PermissionSubjectType.System, "System");
        }

        if (!string.IsNullOrWhiteSpace(UserId))
        {
            yield return (PermissionSubjectType.User, UserId);
        }

        foreach (var roleId in RoleIds)
        {
            yield return (PermissionSubjectType.Role, roleId);
        }

        foreach (var groupId in GroupIds)
        {
            yield return (PermissionSubjectType.Group, groupId);
        }
    }
}
