using IndustrialDAQ.Core.Authorization;
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndustrialDAQ.Infrastructure.Authorization;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbContextFactory<DaqDbContext> _contextFactory;

    public UserRepository(IDbContextFactory<DaqDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<User?> FindByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<User?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToModel(entity);
    }

    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entities = await context.Users
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToModel).ToList();
    }

    public async Task UpsertAsync(User user, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id, cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            entity = new UserEntity { Id = user.Id };
            MapToEntity(user, entity);
            context.Users.Add(entity);
        }
        else
        {
            MapToEntity(user, entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken).ConfigureAwait(false);
        if (entity is not null)
        {
            context.Users.Remove(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static User MapToModel(UserEntity entity)
    {
        return new User
        {
            Id = entity.Id,
            Username = entity.Username,
            PasswordHash = entity.PasswordHash,
            RealName = entity.RealName,
            Roles = string.IsNullOrWhiteSpace(entity.Roles) 
                ? new List<string>() 
                : entity.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
            CreatedAtUtc = entity.CreatedAtUtc,
            IsActive = entity.IsActive
        };
    }

    private static void MapToEntity(User model, UserEntity entity)
    {
        entity.Username = model.Username;
        entity.PasswordHash = model.PasswordHash;
        entity.RealName = model.RealName;
        entity.Roles = string.Join(',', model.Roles);
        entity.CreatedAtUtc = model.CreatedAtUtc;
        entity.IsActive = model.IsActive;
    }
}
