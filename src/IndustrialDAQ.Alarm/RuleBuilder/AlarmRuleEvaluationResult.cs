using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.RuleBuilder;

/// <summary>
/// Result of evaluating one compiled alarm workflow against a tag value.
/// RuleEngineService will use this result to drive AlarmStateMachine.
/// </summary>
public sealed record AlarmRuleEvaluationResult
{
    public required AlarmDefinition Definition { get; init; }

    public bool IsSuppressed { get; init; }

    public bool IsTriggered { get; init; }

    public bool IsCleared { get; init; }

    public object? Value { get; init; }

    public DateTimeOffset Timestamp { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];
}
