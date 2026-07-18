using IndustrialDAQ.Core.Authorization;
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndustrialDAQ.Infrastructure.Authorization;

public sealed class AuthorizationRepository : IAuthorizationRepository
{
    private readonly IDbContextFactory<DaqDbContext> _contextFactory;

    public AuthorizationRepository(IDbContextFactory<DaqDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<IReadOnlyList<PermissionPolicy>> LoadPoliciesAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.PermissionPolicies
            .AsNoTracking()
            .Where(static policy => policy.IsEnabled)
            .OrderBy(static policy => policy.ResourcePath)
            .ThenByDescending(static policy => policy.Priority)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(static entity => entity.ToDomain()).ToArray();
    }

    public async Task UpsertPolicyAsync(
        PermissionPolicy policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.PermissionPolicies
            .FirstOrDefaultAsync(item => item.Id == policy.Id, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            context.PermissionPolicies.Add(PermissionPolicyEntity.FromDomain(policy));
        }
        else
        {
            PermissionPolicyEntity.Apply(policy, entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DisablePolicyAsync(string policyId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.PermissionPolicies
            .FirstOrDefaultAsync(policy => policy.Id == policyId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return;
        }

        entity.IsEnabled = false;
        entity.Version++;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
