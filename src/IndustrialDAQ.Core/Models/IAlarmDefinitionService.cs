namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 面向运行时的报警定义服务接口。
/// 它公开了一个不可变的快照，使得规则构建和热重载可以在不锁定每个标签事件评估的情况下切换定义集。
/// </summary>
public interface IAlarmDefinitionService
{
    /// <summary>
    /// 获取当前的报警定义运行时快照。
    /// </summary>
    AlarmDefinitionSnapshot Current { get; }

    /// <summary>
    /// 从持久化存储中异步重载报警定义并发布新快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新发布的快照。</returns>
    Task<AlarmDefinitionSnapshot> ReloadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规则 ID 在当前快照中查找报警定义。
    /// </summary>
    /// <param name="ruleId">报警规则 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>找到的报警定义，否则为 null。</returns>
    ValueTask<AlarmDefinition?> FindByRuleIdAsync(string ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据标签 ID 在当前快照中查找关联的所有报警定义。
    /// </summary>
    /// <param name="tagId">标签 ID。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>关联的报警定义列表。</returns>
    ValueTask<IReadOnlyList<AlarmDefinition>> FindByTagIdAsync(string tagId, CancellationToken cancellationToken = default);
}
