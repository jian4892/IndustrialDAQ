// File: TrendDataStore.cs  Module: Trend  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Trend;

/// <summary>
/// 趋势数据存储 — 管理多个 Tag 的趋势缓存。
/// 线程安全，支持动态添加/移除 Tag。
/// </summary>
public sealed class TrendDataStore
{
    private readonly ConcurrentDictionary<string, TrendCache> _caches = new();
    private readonly ConcurrentDictionary<string, TrendTemplate> _templates = new();
    private readonly int _defaultCapacity;

    /// <summary>当前跟踪的所有 Tag ID。</summary>
    public IReadOnlyCollection<string> TrackedTagIds => _caches.Keys.ToList().AsReadOnly();

    /// <summary>新数据点添加事件（tagId, point）。</summary>
    public event Action<string, TrendPoint>? DataPointAdded;

    /// <summary>
    /// 初始化趋势数据存储。
    /// </summary>
    /// <param name="defaultCapacity">默认缓冲区容量。</param>
    public TrendDataStore(int defaultCapacity = 3600)
    {
        _defaultCapacity = defaultCapacity;
    }

    /// <summary>
    /// 注册 Tag 并创建缓存。
    /// </summary>
    public TrendCache RegisterTag(string tagId, TrendTemplate? template = null)
    {
        int capacity = template?.BufferCapacity ?? _defaultCapacity;
        var cache = _caches.GetOrAdd(tagId, id => new TrendCache(id, capacity));
        if (template is not null)
            _templates[tagId] = template;
        return cache;
    }

    /// <summary>
    /// 移除 Tag 及其缓存。
    /// </summary>
    public void UnregisterTag(string tagId)
    {
        _caches.TryRemove(tagId, out _);
        _templates.TryRemove(tagId, out _);
    }

    /// <summary>
    /// 添加数据点到指定 Tag 的缓存。
    /// </summary>
    public void Add(string tagId, TrendPoint point)
    {
        if (_caches.TryGetValue(tagId, out var cache))
        {
            cache.Add(point);
            DataPointAdded?.Invoke(tagId, point);
        }
    }

    /// <summary>
    /// 获取指定 Tag 的缓存。
    /// </summary>
    public TrendCache? GetCache(string tagId)
    {
        _caches.TryGetValue(tagId, out var cache);
        return cache;
    }

    /// <summary>
    /// 获取指定 Tag 的趋势模板。
    /// </summary>
    public TrendTemplate? GetTemplate(string tagId)
    {
        _templates.TryGetValue(tagId, out var template);
        return template;
    }

    /// <summary>
    /// 清空所有缓存。
    /// </summary>
    public void ClearAll()
    {
        foreach (var cache in _caches.Values)
            cache.Clear();
    }
}
