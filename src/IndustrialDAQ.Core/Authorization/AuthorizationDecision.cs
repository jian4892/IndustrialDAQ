namespace IndustrialDAQ.Core.Authorization;

public sealed record AuthorizationDecision
{
    public bool IsAllowed { get; init; }

    public PermissionEffect Effect => IsAllowed ? PermissionEffect.Allow : PermissionEffect.Deny;

    public string Reason { get; init; } = string.Empty;

    public PermissionPolicy? MatchedPolicy { get; init; }

    public static AuthorizationDecision Allow(PermissionPolicy policy, string reason) => new()
    {
        IsAllowed = true,
        MatchedPolicy = policy,
        Reason = reason
    };

    public static AuthorizationDecision Deny(string reason, PermissionPolicy? policy = null) => new()
    {
        IsAllowed = false,
        MatchedPolicy = policy,
        Reason = reason
    };
}
