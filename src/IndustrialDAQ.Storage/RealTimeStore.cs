// File: RealTimeStore.cs  Module: Storage Engine  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using System.Threading.Channels;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Storage;

/// <summary>
/// 实时数据库 — 使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 维护
/// 所有测点的最新值。提供广播模式的变更通知，支持多个订阅者同时接收数据。
/// </summary>
public sealed class RealTimeStore
{
    private readonly ConcurrentDictionary<string, TagValue> _store = new();

    /// <summary>订阅者列表（线程安全）。</summary>
    private readonly ConcurrentDictionary<string, Channel<TagValue>> _subscribers = new();

    /// <summary>订阅者计数器。</summary>
    private long _subscriberCounter;

    /// <summary>当前实时缓存的测点总数。</summary>
    public int Count => _store.Count;

    /// <summary>
    /// 更新或添加一个测点值，并广播给所有订阅者。
    /// </summary>
    /// <param name="value">测点实时值</param>
    public void Update(TagValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _store[value.TagId] = value;

        // 广播给所有订阅者
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(value);
        }
    }

    /// <summary>
    /// 订阅变更通知流（广播模式）。
    /// 每个订阅者都会收到所有更新，互不影响。
    /// </summary>
    /// <returns>变更通知流的 ChannelReader</returns>
    public ChannelReader<TagValue> Subscribe()
    {
        var channel = Channel.CreateUnbounded<TagValue>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        string subscriberId = $"sub-{Interlocked.Increment(ref _subscriberCounter)}";
        _subscribers[subscriberId] = channel;

        return channel.Reader;
    }

    /// <summary>
    /// 取消订阅。
    /// </summary>
    /// <param name="reader">之前订阅的 ChannelReader</param>
    public void Unsubscribe(ChannelReader<TagValue> reader)
    {
        // 查找并移除对应的订阅者
        var toRemove = _subscribers.FirstOrDefault(kvp => kvp.Value.Reader == reader);
        if (toRemove.Key is not null)
        {
            _subscribers.TryRemove(toRemove.Key, out _);
            toRemove.Value.Writer.Complete();
        }
    }

    /// <summary>
    /// 获取单个测点的最新值。
    /// </summary>
    /// <returns>测点值，不存在时返回 null</returns>
    public TagValue? TryGetValue(string tagId)
    {
        _store.TryGetValue(tagId, out TagValue? value);
        return value;
    }

    /// <summary>
    /// 获取所有测点的最新值快照。
    /// </summary>
    public IReadOnlyCollection<TagValue> GetAll()
    {
        return _store.Values.ToList().AsReadOnly();
    }
}
