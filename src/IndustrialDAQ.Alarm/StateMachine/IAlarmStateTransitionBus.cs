using System.Threading.Channels;

namespace IndustrialDAQ.Alarm.StateMachine;

public interface IAlarmStateTransitionBus
{
    ChannelReader<AlarmStateTransition> Reader { get; }

    ValueTask PublishAsync(AlarmStateTransition transition, CancellationToken cancellationToken = default);
}
