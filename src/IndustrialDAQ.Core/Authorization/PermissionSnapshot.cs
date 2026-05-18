namespace IndustrialDAQ.Core.Authorization;

/// <summary>
/// Immutable runtime snapshot of permission policies.
/// AuthorizationService evaluates against this snapshot without database calls.
/// </summary>
public sealed class PermissionSnapshot
{
    private readonly IReadOnlyList<PermissionPolicy> _policies;

    private PermissionSnapshot(IReadOnlyList<PermissionPolicy> policies, long version)
    {
        _policies = policies;
        Version = version;
    }

    public static PermissionSnapshot Empty { get; } = new([], 0);

    public long Version { get; }

    public IReadOnlyList<PermissionPolicy> Policies => _policies;

    public static PermissionSnapshot Build(IEnumerable<PermissionPolicy> policies)
    {
        ArgumentNullException.ThrowIfNull(policies);

        var enabled = policies
            .Where(static policy => policy.IsEnabled)
            .ToArray();

        foreach (var policy in enabled)
        {
            policy.Validate();
        }

        var version = enabled.Length == 0 ? 0 : enabled.Max(static policy => policy.Version);
        return new PermissionSnapshot(enabled, version);
    }

    public IReadOnlyList<PermissionPolicy> FindCandidates(AuthorizationRequest request)
    {
        var identities = request.Subject.EnumerateIdentities().ToHashSet();

        return _policies
            .Where(policy => identities.Contains((policy.SubjectType, policy.SubjectId)))
            .Where(policy => ActionMatches(policy.Action, request.Action))
            .Where(policy => ResourceMatches(policy, request.ResourcePath))
            .OrderByDescending(static policy => policy.Effect == PermissionEffect.Deny ? 1 : 0)
            .ThenByDescending(policy => policy.ResourcePath.Depth)
            .ThenByDescending(static policy => policy.Priority)
            .ToArray();
    }

    private static bool ActionMatches(string policyAction, string requestedAction)
    {
        return string.Equals(policyAction, requestedAction, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(policyAction, "*", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResourceMatches(PermissionPolicy policy, ResourceTree.ResourcePath requestedPath)
    {
        if (string.Equals(policy.ResourcePath.Value, requestedPath.Value, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (policy.ResourcePath.Value.EndsWith("/*", StringComparison.Ordinal))
        {
            var prefix = policy.ResourcePath.Value[..^2];
            return requestedPath.Value.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
        }

        return policy.Inherit && requestedPath.IsDescendantOf(policy.ResourcePath);
    }
}
