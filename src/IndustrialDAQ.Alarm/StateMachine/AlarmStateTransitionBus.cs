using System.Threading.Channels;

namespace IndustrialDAQ.Alarm.StateMachine;

/// <summary>
/// Bounded channel for alarm state transitions.
/// </summary>
public sealed class AlarmStateTransitionBus : IAlarmStateTransitionBus
{
    private readonly Channel<AlarmStateTransition> _channel;

    public AlarmStateTransitionBus(int capacity = 50_000)
    {
        _channel = Channel.CreateBounded<AlarmStateTransition>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public ChannelReader<AlarmStateTransition> Reader => _channel.Reader;

    public async ValueTask PublishAsync(
        AlarmStateTransition transition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transition);
        await _channel.Writer.WriteAsync(transition, cancellationToken).ConfigureAwait(false);
    }
}
