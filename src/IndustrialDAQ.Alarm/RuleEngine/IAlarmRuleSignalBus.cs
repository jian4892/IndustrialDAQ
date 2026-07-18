using System.Threading.Channels;

namespace IndustrialDAQ.Alarm.RuleEngine;

/// <summary>
/// Asynchronous channel for rule evaluation signals.
/// Downstream services such as AlarmStateMachine and AlarmCenter subscribe to
/// this bus without coupling themselves to RulesEngine.
/// </summary>
public interface IAlarmRuleSignalBus
{
    ChannelReader<AlarmRuleSignal> Reader { get; }

    ValueTask PublishAsync(AlarmRuleSignal signal, CancellationToken cancellationToken = default);
}
