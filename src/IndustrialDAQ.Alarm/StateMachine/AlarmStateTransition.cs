using IndustrialDAQ.Alarm.RuleEngine;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.StateMachine;

/// <summary>
/// State transition emitted by the industrial alarm state machine.
/// AlarmCenter consumes these transitions to update current alarms, persist
/// history and publish UI/MQTT/Redis notifications.
/// </summary>
public sealed record AlarmStateTransition
{
    public required string OccurrenceId { get; init; }

    public required string RuleId { get; init; }

    public string AlarmCode { get; init; } = string.Empty;

    public required AlarmDefinition Definition { get; init; }

    public AlarmState FromState { get; init; }

    public AlarmState ToState { get; init; }

    public object? Value { get; init; }

    public string? OperatorId { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;

    public AlarmRuleSignal? Signal { get; init; }
}
