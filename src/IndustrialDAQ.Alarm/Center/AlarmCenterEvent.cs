using IndustrialDAQ.Alarm.StateMachine;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.Center;

/// <summary>
/// Event published by AlarmCenter after a state transition has been accepted
/// into the current alarm view and persisted when needed.
/// </summary>
public sealed record AlarmCenterEvent
{
    public required AlarmStateTransition Transition { get; init; }

    public required AlarmRecord Record { get; init; }

    public AlarmCenterEventType EventType { get; init; }
}

public enum AlarmCenterEventType : byte
{
    Raised = 0,
    Cleared = 1,
    Acknowledged = 2,
    Suppressed = 3,
    Shelved = 4,
    Closed = 5
}
