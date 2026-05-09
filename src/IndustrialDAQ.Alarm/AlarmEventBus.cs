// File: AlarmEventBus.cs  Module: Alarm Engine  Author: IndustrialDAQ Team
using System.Threading.Channels;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Alarm;

/// <summary>
/// 报警事件总线 — 使用 Channel&lt;T&gt; 实现发布订阅模式。
/// 支持多个消费者（UI、通知服务、历史存储）订阅报警事件。
/// </summary>
public sealed class AlarmEventBus
{
    private readonly Channel<AlarmEvent> _channel = Channel.CreateUnbounded<AlarmEvent>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    /// <summary>
    /// 发布报警事件。
    /// </summary>
    /// <param name="alarmEvent">报警事件。</param>
    /// <returns>是否成功发布。</returns>
    public bool Publish(AlarmEvent alarmEvent)
    {
        ArgumentNullException.ThrowIfNull(alarmEvent);
        return _channel.Writer.TryWrite(alarmEvent);
    }

    /// <summary>
    /// 订阅报警事件流。
    /// </summary>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>报警事件异步可枚举。</returns>
    public IAsyncEnumerable<AlarmEvent> Subscribe(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// 完成事件发布（关闭通道）。
    /// </summary>
    public void Complete()
    {
        _channel.Writer.Complete();
    }
}

/// <summary>
/// 报警事件类型。
/// </summary>
public enum AlarmEventType : byte
{
    /// <summary>报警触发。</summary>
    Triggered = 0,

    /// <summary>报警确认。</summary>
    Acknowledged = 1,

    /// <summary>报警恢复。</summary>
    Cleared = 2
}

/// <summary>
/// 报警事件 — 包含报警事件的完整信息。
/// </summary>
public sealed class AlarmEvent
{
    /// <summary>事件类型。</summary>
    public AlarmEventType EventType { get; init; }

    /// <summary>报警 ID。</summary>
    public string AlarmId { get; init; } = string.Empty;

    /// <summary>报警规则。</summary>
    public AlarmRule Rule { get; init; } = null!;

    /// <summary>报警记录。</summary>
    public AlarmRecord Record { get; init; } = null!;

    /// <summary>触发值。</summary>
    public double TriggerValue { get; init; }

    /// <summary>事件时间 (UTC)。</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>报警状态。</summary>
    public AlarmState State { get; init; }
}
