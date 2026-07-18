using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace IndustrialDAQ.Infrastructure.Alarms;

/// <summary>
/// 报警定义配置存储库实现。
/// 它仅加载报警定义，工作流的具体实例化工作交给 RuleBuilder。
/// </summary>
public sealed class AlarmDefinitionRepository : IAlarmDefinitionRepository
{
    private readonly IDbContextFactory<DaqDbContext> _contextFactory;

    /// <summary>
    /// 初始化报警定义存储库的新实例。
    /// </summary>
    /// <param name="contextFactory">数据库上下文工厂。</param>
    public AlarmDefinitionRepository(IDbContextFactory<DaqDbContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    /// <summary>
    /// 异步加载所有启用的报警定义。
    /// </summary>
    public async Task<IReadOnlyList<AlarmDefinition>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entities = await context.AlarmDefinitions
            .AsNoTracking()
            .Where(static definition => definition.IsEnabled)
            .OrderBy(static definition => definition.TargetResourcePath)
            .ThenBy(static definition => definition.AlarmCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(static entity => entity.ToDomain()).ToArray();
    }

    /// <summary>
    /// 根据规则 ID 异步查找报警定义。
    /// </summary>
    public async Task<AlarmDefinition?> FindByRuleIdAsync(
        string ruleId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.AlarmDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(definition => definition.RuleId == ruleId, cancellationToken)
            .ConfigureAwait(false);

        return entity?.ToDomain();
    }

    /// <summary>
    /// 插入或更新报警定义。
    /// </summary>
    public async Task UpsertAsync(AlarmDefinition definition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition.Validate();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.AlarmDefinitions
            .FirstOrDefaultAsync(item => item.RuleId == definition.RuleId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            context.AlarmDefinitions.Add(AlarmDefinitionEntity.FromDomain(definition));
        }
        else
        {
            AlarmDefinitionEntity.Apply(definition, entity);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 异步禁用指定的报警规则。
    /// </summary>
    public async Task DisableAsync(string ruleId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = await context.AlarmDefinitions
            .FirstOrDefaultAsync(definition => definition.RuleId == ruleId, cancellationToken)
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
