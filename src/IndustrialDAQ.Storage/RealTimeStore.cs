// File: RealTimeStore.cs  Module: Storage Engine  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using System.Threading.Channels;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Storage;

/// <summary>
/// 实时数据库 — 使用 <see cref="ConcurrentDictionary{TKey,TValue}"/> 维护
/// 所有测点的最新值。提供变更通知流，供 UI 和告警层订阅。
/// </summary>
public sealed class RealTimeStore
{
    private readonly ConcurrentDictionary<string, TagValue> _store = new();
    private readonly Channel<TagValue> _changeChannel = Channel.CreateUnbounded<TagValue>(
        new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

    /// <summary>
    /// 变更通知流 — 当任意测点被更新时，新的 <see cref="TagValue"/> 会写入此管道。
    /// 订阅方（UI、告警层）可通过 <c>await foreach</c> 消费。
    /// </summary>
    public ChannelReader<TagValue> ChangeStream => _changeChannel.Reader;

    /// <summary>当前实时缓存的测点总数。</summary>
    public int Count => _store.Count;

    /// <summary>
    /// 更新或添加一个测点值，并通过变更管道通知订阅者。
    /// </summary>
    /// <param name="value">测点实时值</param>
    public void Update(TagValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        _store[value.TagId] = value;

        // 非阻塞写入变更管道，若管道满则丢弃通知（变更通知允许丢失，实时值总是最新）
        _changeChannel.Writer.TryWrite(value);
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
