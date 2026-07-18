using System.Threading.Channels;

namespace IndustrialDAQ.Alarm.RuleEngine;

/// <summary>
/// Bounded async signal bus. Bounded capacity applies backpressure under alarm
/// storms instead of growing memory without limit.
/// </summary>
public sealed class AlarmRuleSignalBus : IAlarmRuleSignalBus
{
    private readonly Channel<AlarmRuleSignal> _channel;

    public AlarmRuleSignalBus(int capacity = 50_000)
    {
        _channel = Channel.CreateBounded<AlarmRuleSignal>(new BoundedChannelOptions(capacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public ChannelReader<AlarmRuleSignal> Reader => _channel.Reader;

    public async ValueTask PublishAsync(
        AlarmRuleSignal signal,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(signal);
        await _channel.Writer.WriteAsync(signal, cancellationToken).ConfigureAwait(false);
    }
}
