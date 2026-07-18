using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm.StateMachine;

internal sealed class AlarmStateContext
{
    public AlarmStateContext(AlarmDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }

    public AlarmDefinition Definition { get; set; }

    public AlarmState State { get; set; } = AlarmState.Normal;

    public string? OccurrenceId { get; set; }

    public DateTimeOffset? PendingSince { get; set; }

    public DateTimeOffset? ActivatedAt { get; set; }

    public DateTimeOffset? ClearedAt { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; set; }

    public object? LastValue { get; set; }

    public AlarmRuntimeState ToSnapshot() => new()
    {
        RuleId = Definition.RuleId,
        AlarmCode = Definition.AlarmCode,
        OccurrenceId = OccurrenceId,
        State = State,
        PendingSince = PendingSince,
        ActivatedAt = ActivatedAt,
        ClearedAt = ClearedAt,
        AcknowledgedAt = AcknowledgedAt,
        LastValue = LastValue
    };
}
