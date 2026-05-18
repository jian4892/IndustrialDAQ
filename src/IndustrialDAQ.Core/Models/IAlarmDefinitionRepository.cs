namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 报警定义的持久化边界接口。
/// 报警定义存储在配置存储库中并加载到运行时快照；可执行的工作流则单独存储和构建。
/// </summary>
public interface IAlarmDefinitionRepository
{
    /// <summary>
    /// 异步加载数据库中定义的所有活跃报警规则。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>报警定义列表。</returns>
    Task<IReadOnlyList<AlarmDefinition>> LoadAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规则 ID 异步查找报警定义。
    /// </summary>
    /// <param name="ruleId">报警规则 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>找到的报警定义，否则为 null。</returns>
    Task<AlarmDefinition?> FindByRuleIdAsync(string ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 插入或更新报警定义。
    /// </summary>
    /// <param name="definition">要保存的报警定义。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task UpsertAsync(AlarmDefinition definition, CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步禁用指定的报警规则。
    /// </summary>
    /// <param name="ruleId">报警规则 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task DisableAsync(string ruleId, CancellationToken cancellationToken = default);
}
