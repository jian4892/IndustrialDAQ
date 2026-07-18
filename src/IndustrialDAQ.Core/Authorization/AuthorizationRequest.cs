using IndustrialDAQ.Core.ResourceTree;

namespace IndustrialDAQ.Core.Authorization;

public sealed record AuthorizationRequest
{
    public required PermissionSubject Subject { get; init; }

    public required ResourcePath ResourcePath { get; init; }

    public required string Action { get; init; }

    public IReadOnlyDictionary<string, object?> Context { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
