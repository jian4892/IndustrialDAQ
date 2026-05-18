namespace IndustrialDAQ.Core.Authorization;

public interface IAuthorizationRepository
{
    Task<IReadOnlyList<PermissionPolicy>> LoadPoliciesAsync(CancellationToken cancellationToken = default);

    Task UpsertPolicyAsync(PermissionPolicy policy, CancellationToken cancellationToken = default);

    Task DisablePolicyAsync(string policyId, CancellationToken cancellationToken = default);
}
