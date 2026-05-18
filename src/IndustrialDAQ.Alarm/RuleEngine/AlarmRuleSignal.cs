using IndustrialDAQ.Alarm.RuleBuilder;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.RuleEngine;

/// <summary>
/// Signal emitted by RuleEngineService after evaluating one alarm workflow.
/// The signal is intentionally not an alarm event yet; AlarmStateMachine owns
/// Pending/Active/Cleared/Ack transitions.
/// </summary>
public sealed record AlarmRuleSignal
{
    public required AlarmDefinition Definition { get; init; }

    public required string RuleId { get; init; }

    public string AlarmCode { get; init; } = string.Empty;

    public string TagId { get; init; } = string.Empty;

    public string TagName { get; init; } = string.Empty;

    public object? Value { get; init; }

    public bool IsSuppressed { get; init; }

    public bool IsTriggered { get; init; }

    public bool IsCleared { get; init; }

    public string CompiledHash { get; init; } = string.Empty;

    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<string> Errors { get; init; } = [];

    public static AlarmRuleSignal FromEvaluation(
        AlarmRuleWorkflow workflow,
        AlarmRuleEvaluationResult result,
        TagValue tagValue)
    {
        return new AlarmRuleSignal
        {
            Definition = workflow.Definition,
            RuleId = workflow.Definition.RuleId,
            AlarmCode = workflow.Definition.AlarmCode,
            TagId = tagValue.TagId,
            TagName = tagValue.TagName,
            Value = tagValue.Value,
            IsSuppressed = result.IsSuppressed,
            IsTriggered = result.IsTriggered,
            IsCleared = result.IsCleared,
            CompiledHash = workflow.CompiledHash,
            Timestamp = tagValue.Timestamp,
            Errors = result.Errors
        };
    }
}
