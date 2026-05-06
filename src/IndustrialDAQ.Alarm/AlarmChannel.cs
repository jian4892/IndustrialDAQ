// File: AlarmChannel.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using System.Threading.Channels;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警事件管道 — 封装 <see cref="Channel{T}"/> 用于将报警记录推送给 UI 和通知层。
/// 无界通道，生产者不阻塞。
/// </summary>
public sealed class AlarmChannel
{
    private readonly Channel<AlarmRecord> _channel = Channel.CreateUnbounded<AlarmRecord>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

    /// <summary>消费者读取端，由 UI（AlarmRecordViewModel）和通知服务消费。</summary>
    public ChannelReader<AlarmRecord> Reader => _channel.Reader;

    /// <summary>生产者写入端，由 <see cref="AlarmEngine"/> 写入报警事件。</summary>
    public ChannelWriter<AlarmRecord> Writer => _channel.Writer;
}
