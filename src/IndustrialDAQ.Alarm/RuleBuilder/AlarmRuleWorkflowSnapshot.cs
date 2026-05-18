namespace IndustrialDAQ.Alarm.RuleBuilder;

/// <summary>
/// Immutable runtime snapshot of compiled alarm workflows.
/// Hot reload builds a new snapshot and swaps the reference in RuleEngineService.
/// </summary>
public sealed class AlarmRuleWorkflowSnapshot
{
    private readonly IReadOnlyDictionary<string, AlarmRuleWorkflow> _byRuleId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AlarmRuleWorkflow>> _byTagId;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<AlarmRuleWorkflow>> _byResourcePath;

    private AlarmRuleWorkflowSnapshot(
        IReadOnlyDictionary<string, AlarmRuleWorkflow> byRuleId,
        IReadOnlyDictionary<string, IReadOnlyList<AlarmRuleWorkflow>> byTagId,
        IReadOnlyDictionary<string, IReadOnlyList<AlarmRuleWorkflow>> byResourcePath,
        long version,
        string compiledHash)
    {
        _byRuleId = byRuleId;
        _byTagId = byTagId;
        _byResourcePath = byResourcePath;
        Version = version;
        CompiledHash = compiledHash;
    }

    public static AlarmRuleWorkflowSnapshot Empty { get; } = new(
        new Dictionary<string, AlarmRuleWorkflow>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<AlarmRuleWorkflow>>(StringComparer.OrdinalIgnoreCase),
        new Dictionary<string, IReadOnlyList<AlarmRuleWorkflow>>(StringComparer.OrdinalIgnoreCase),
        0,
        string.Empty);

    public long Version { get; }

    public string CompiledHash { get; }

    public IReadOnlyCollection<AlarmRuleWorkflow> Workflows => _byRuleId.Values.ToArray();

    public static AlarmRuleWorkflowSnapshot Build(IEnumerable<AlarmRuleWorkflow> workflows)
    {
        var workflowList = workflows.ToArray();
        var byRuleId = new Dictionary<string, AlarmRuleWorkflow>(StringComparer.OrdinalIgnoreCase);

        foreach (var workflow in workflowList)
        {
            if (!byRuleId.TryAdd(workflow.Definition.RuleId, workflow))
            {
                throw new InvalidOperationException($"Duplicate compiled alarm workflow '{workflow.Definition.RuleId}'.");
            }
        }

        var byTagId = workflowList
            .Where(static workflow => !string.IsNullOrWhiteSpace(workflow.Definition.TagId))
            .GroupBy(static workflow => workflow.Definition.TagId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<AlarmRuleWorkflow>)group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var byResourcePath = workflowList
            .Where(static workflow => workflow.Definition.TargetResourcePath is not null)
            .GroupBy(static workflow => workflow.Definition.TargetResourcePath!.Value.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyList<AlarmRuleWorkflow>)group.ToArray(),
                StringComparer.OrdinalIgnoreCase);

        var version = workflowList.Length == 0 ? 0 : workflowList.Max(static workflow => workflow.Version);
        var compiledHash = string.Join('|', workflowList
            .OrderBy(static workflow => workflow.Definition.RuleId, StringComparer.OrdinalIgnoreCase)
            .Select(static workflow => workflow.CompiledHash));

        return new AlarmRuleWorkflowSnapshot(byRuleId, byTagId, byResourcePath, version, compiledHash);
    }

    public AlarmRuleWorkflow? FindByRuleId(string ruleId)
    {
        return _byRuleId.TryGetValue(ruleId, out var workflow) ? workflow : null;
    }

    public IReadOnlyList<AlarmRuleWorkflow> FindByTagId(string tagId)
    {
        return _byTagId.TryGetValue(tagId, out var workflows) ? workflows : [];
    }

    public IReadOnlyList<AlarmRuleWorkflow> FindByResourcePath(string resourcePath)
    {
        var normalized = Core.ResourceTree.ResourcePath.Normalize(resourcePath);
        return _byResourcePath.TryGetValue(normalized, out var workflows) ? workflows : [];
    }
}
