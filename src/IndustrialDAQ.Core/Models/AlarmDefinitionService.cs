namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 将报警定义从持久化存储加载到不可变运行时快照中。
/// 该模式镜像了 ResourceTree 的热重载模式：构建、验证并原子发布。
/// </summary>
public sealed class AlarmDefinitionService : IAlarmDefinitionService
{
    private readonly IAlarmDefinitionRepository _repository;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private volatile AlarmDefinitionSnapshot _current = AlarmDefinitionSnapshot.Empty;

    /// <summary>
    /// 初始化报警定义服务的新实例。
    /// </summary>
    /// <param name="repository">报警定义存储库。</param>
    public AlarmDefinitionService(IAlarmDefinitionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// 获取当前的报警定义运行时快照。
    /// </summary>
    public AlarmDefinitionSnapshot Current => _current;

    /// <summary>
    /// 从存储库异步重载所有报警定义并发布新快照。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>新发布的报警定义快照。</returns>
    public async Task<AlarmDefinitionSnapshot> ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _reloadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var definitions = await _repository.LoadAllAsync(cancellationToken).ConfigureAwait(false);
            var next = AlarmDefinitionSnapshot.Build(definitions);
            _current = next;
            return next;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    /// <summary>
    /// 根据规则 ID 异步查找报警定义。
    /// </summary>
    public ValueTask<AlarmDefinition?> FindByRuleIdAsync(
        string ruleId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Current.FindByRuleId(ruleId));
    }

    /// <summary>
    /// 根据标签 ID 异步查找关联的所有报警定义。
    /// </summary>
    public ValueTask<IReadOnlyList<AlarmDefinition>> FindByTagIdAsync(
        string tagId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Current.FindByTagId(tagId));
    }
}
