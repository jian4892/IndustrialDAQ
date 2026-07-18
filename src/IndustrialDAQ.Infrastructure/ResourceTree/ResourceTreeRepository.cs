using IndustrialDAQ.Core.ResourceTree;
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndustrialDAQ.Infrastructure.ResourceTree;

/// <summary>
/// 适用于 SQLite/PostgreSQL 的运行时资源树存储库实现。
/// 它仅负责将持久化记录转换为领域节点；验证和层级索引由 ResourceTreeSnapshot 处理。
/// </summary>
public sealed class ResourceTreeRepository : IResourceTreeRepository
{
    private readonly IDbContextFactory<DaqDbContext> _contextFactory;

    /// <summary>
    /// 初始化资源树存储库的新实例。
    /// </summary>
    /// <param name="contextFactory">数据库上下文工厂。</param>
    public ResourceTreeRepository(IDbContextFactory<DaqDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// 异步加载所有启用的资源节点。
    /// </summary>
    public async Task<IReadOnlyList<ResourceNode>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.ResourceNodes
            .AsNoTracking()
            .Where(static node => node.IsEnabled)
            .OrderBy(static node => node.ResourcePath)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToArray();
    }

    /// <summary>
    /// 根据路径查找资源节点。
    /// </summary>
    public async Task<ResourceNode?> FindByPathAsync(ResourcePath path, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.ResourceNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(node => node.ResourcePath == path.Value, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    /// <summary>
    /// 插入或更新资源节点。
    /// </summary>
    public async Task UpsertAsync(ResourceNode node, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var normalizedPath = node.Path.Value;
        var entity = await context.ResourceNodes
            .FirstOrDefaultAsync(item => item.ResourcePath == normalizedPath, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = ToEntity(node);
            context.ResourceNodes.Add(entity);
        }
        else
        {
            Apply(node, entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 递归删除指定路径下的所有资源节点。
    /// </summary>
    public async Task DeleteAsync(ResourcePath path, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var normalizedPath = path.Value;
        var entities = await context.ResourceNodes
            .Where(node => node.ResourcePath == normalizedPath ||
                           EF.Functions.Like(node.ResourcePath, normalizedPath + "/%"))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.ResourceNodes.RemoveRange(entities);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ResourceNode ToDomain(ResourceNodeEntity entity)
    {
        return new ResourceNode
        {
            Id = entity.Id,
            ParentId = entity.ParentId,
            Path = new ResourcePath(entity.ResourcePath),
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            ResourceType = Enum.TryParse<ResourceType>(entity.ResourceType, ignoreCase: true, out var resourceType)
                ? resourceType
                : ResourceType.Unknown,
            SortOrder = entity.SortOrder,
            IsEnabled = entity.IsEnabled,
            MetadataJson = entity.MetadataJson,
            Version = entity.Version,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static ResourceNodeEntity ToEntity(ResourceNode node)
    {
        var entity = new ResourceNodeEntity
        {
            Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString("N") : node.Id,
            CreatedAtUtc = node.CreatedAtUtc == default ? DateTime.UtcNow : node.CreatedAtUtc
        };

        Apply(node, entity);
        return entity;
    }

    private static void Apply(ResourceNode node, ResourceNodeEntity entity)
    {
        entity.ParentId = node.ParentId;
        entity.ResourcePath = node.Path.Value;
        entity.Name = string.IsNullOrWhiteSpace(node.Name) ? node.Path.Name : node.Name;
        entity.DisplayName = string.IsNullOrWhiteSpace(node.DisplayName) ? entity.Name : node.DisplayName;
        entity.ResourceType = node.ResourceType.ToString();
        entity.SortOrder = node.SortOrder;
        entity.IsEnabled = node.IsEnabled;
        entity.MetadataJson = node.MetadataJson;
        entity.Version = Math.Max(1, node.Version);
        entity.UpdatedAtUtc = DateTime.UtcNow;
    }
}
