using System.Threading.Channels;

namespace IndustrialDAQ.Alarm.Center;

/// <summary>
/// Event outlet for UI, MQTT, Redis and other alarm subscribers.
/// </summary>
public sealed class AlarmCenterEventBus : IAlarmCenterEventBus
{
    private readonly Channel<AlarmCenterEvent> _channel;

    public AlarmCenterEventBus(int capacity = 50_000)
    {
        _channel = Channel.CreateBounded<AlarmCenterEvent>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public ChannelReader<AlarmCenterEvent> Reader => _channel.Reader;

    public async ValueTask PublishAsync(
        AlarmCenterEvent alarmEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(alarmEvent);
        await _channel.Writer.WriteAsync(alarmEvent, cancellationToken).ConfigureAwait(false);
    }
}
