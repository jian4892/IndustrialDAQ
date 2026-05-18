namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 报警定义的不可变运行时快照。
/// 报警引擎的热重载操作会构建新快照并原子地进行替换。
/// </summary>
public sealed class AlarmDefinitionSnapshot
{
    private readonly IReadOnlyDictionary<string, AlarmDefinition> _byRuleId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AlarmDefinition>> _byTagId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AlarmDefinition>> _byResourcePath;

    private AlarmDefinitionSnapshot(
        IReadOnlyDictionary<string, AlarmDefinition> byRuleId,
        IReadOnlyDictionary<string, IReadOnlyList<AlarmDefinition>> byTagId,
        IReadOnlyDictionary<string, IReadOnlyList<AlarmDefinition>> byResourcePath,
        long version)
    {
        _byRuleId = byRuleId;
        _byTagId = byTagId;
        _byResourcePath = byResourcePath;
        Version = version;
    }

    /// <summary>
    /// 获取空快照。
    /// </summary>
    public static AlarmDefinitionSnapshot Empty { get; } = new(
        new Dictionary<string, AlarmDefinition>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<AlarmDefinition>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<AlarmDefinition>>(StringComparer.OrdinalIgnoreCase),
        0);

    /// <summary>
    /// 获取快照版本号。
    /// </summary>
    public long Version { get; }

    /// <summary>
    /// 获取快照中的所有报警定义。
    /// </summary>
    public IReadOnlyCollection<AlarmDefinition> Definitions => _byRuleId.Values.ToArray();

    /// <summary>
    /// 从报警定义集合构建新的运行时快照。
    /// </summary>
    /// <param name="definitions">报警定义集合。</param>
    /// <returns>构建的报警定义快照。</returns>
    /// <exception cref="InvalidOperationException">当存在重复的 RuleId 时抛出。</exception>
    public static AlarmDefinitionSnapshot Build(IEnumerable<AlarmDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var enabledDefinitions = definitions
            .Where(static definition => definition.IsRuntimeEnabled)
            .ToArray();

        foreach (var definition in enabledDefinitions)
        {
            definition.Validate();
        }

        var byRuleId = new Dictionary<string, AlarmDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in enabledDefinitions)
        {
            if (!byRuleId.TryAdd(definition.RuleId, definition))
            {
                throw new InvalidOperationException($"重复的报警规则 ID '{definition.RuleId}'。");
            }
        }

        var byTagId = enabledDefinitions
            .Where(static definition => !string.IsNullOrWhiteSpace(definition.TagId))
            .GroupBy(static definition => definition.TagId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<AlarmDefinition>)group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var byResourcePath = enabledDefinitions
            .Where(static definition => definition.TargetResourcePath is not null)
            .GroupBy(static definition => definition.TargetResourcePath!.Value.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<AlarmDefinition>)group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var version = enabledDefinitions.Length == 0 ? 0 : enabledDefinitions.Max(static definition => definition.Version);
        return new AlarmDefinitionSnapshot(byRuleId, byTagId, byResourcePath, version);
    }

    /// <summary>
    /// 根据规则 ID 查找报警定义。
    /// </summary>
    /// <param name="ruleId">报警规则 ID。</param>
    /// <returns>找到的报警定义，否则为 null。</returns>
    public AlarmDefinition? FindByRuleId(string ruleId)
    {
        return _byRuleId.TryGetValue(ruleId, out var definition) ? definition : null;
    }

    /// <summary>
    /// 根据标签 ID 查找关联的所有报警定义。
    /// </summary>
    /// <param name="tagId">标签 ID。</param>
    /// <returns>关联的报警定义列表。</returns>
    public IReadOnlyList<AlarmDefinition> FindByTagId(string tagId)
    {
        return _byTagId.TryGetValue(tagId, out var definitions) ? definitions : [];
    }

    /// <summary>
    /// 根据资源路径查找关联的所有报警定义。
    /// </summary>
    /// <param name="resourcePath">资源路径字符串。</param>
    /// <returns>关联的报警定义列表。</returns>
    public IReadOnlyList<AlarmDefinition> FindByResourcePath(string resourcePath)
    {
        var normalized = ResourceTree.ResourcePath.Normalize(resourcePath);
        return _byResourcePath.TryGetValue(normalized, out var definitions) ? definitions : [];
    }
}
