using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.StateMachine;

/// <summary>
/// Snapshot of one alarm rule's runtime state.
/// </summary>
public sealed record AlarmRuntimeState
{
    public required string RuleId { get; init; }

    public string AlarmCode { get; init; } = string.Empty;

    public string? OccurrenceId { get; init; }

    public AlarmState State { get; init; } = AlarmState.Normal;

    public DateTimeOffset? PendingSince { get; init; }

    public DateTimeOffset? ActivatedAt { get; init; }

    public DateTimeOffset? ClearedAt { get; init; }

    public DateTimeOffset? AcknowledgedAt { get; init; }

    public object? LastValue { get; init; }
}
