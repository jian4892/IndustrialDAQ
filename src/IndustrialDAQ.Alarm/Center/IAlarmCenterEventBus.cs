using System.Threading.Channels;

namespace IndustrialDAQ.Alarm.Center;

public interface IAlarmCenterEventBus
{
    ChannelReader<AlarmCenterEvent> Reader { get; }

    ValueTask PublishAsync(AlarmCenterEvent alarmEvent, CancellationToken cancellationToken = default);
}
