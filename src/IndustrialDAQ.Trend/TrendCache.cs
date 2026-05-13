// File: TrendCache.cs  Module: Trend  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Trend;

/// <summary>
/// 趋势数据点。
/// </summary>
public readonly record struct TrendPoint(DateTime Timestamp, double Value, Quality Quality);

/// <summary>
/// 趋势缓存 — 线程安全的环形缓冲区，存储单个 Tag 的趋势数据。
/// 支持按时间窗口查询和全量查询。
/// </summary>
public sealed class TrendCache
{
    private readonly TrendPoint[] _buffer;
    private int _head;
    private int _count;
    private readonly object _lock = new();

    /// <summary>缓冲区容量。</summary>
    public int Capacity { get; }

    /// <summary>当前数据点数量。</summary>
    public int Count { get { lock (_lock) return _count; } }

    /// <summary>关联的 Tag ID。</summary>
    public string TagId { get; }

    /// <summary>
    /// 初始化趋势缓存。
    /// </summary>
    /// <param name="tagId">关联的 Tag ID。</param>
    /// <param name="capacity">缓冲区容量。</param>
    public TrendCache(string tagId, int capacity = 3600)
    {
        TagId = tagId;
        Capacity = capacity > 0 ? capacity : 3600;
        _buffer = new TrendPoint[Capacity];
    }

    /// <summary>
    /// 添加数据点（线程安全）。
    /// </summary>
    public void Add(TrendPoint point)
    {
        lock (_lock)
        {
            _buffer[_head] = point;
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }
    }

    /// <summary>
    /// 获取指定时间窗口内的数据点。
    /// </summary>
    /// <param name="seconds">时间窗口（秒）。</param>
    /// <returns>窗口内的数据点数组，按时间排序。</returns>
    public TrendPoint[] GetWindow(int seconds)
    {
        lock (_lock)
        {
            if (_count == 0) return [];

            var cutoff = DateTime.UtcNow.AddSeconds(-seconds);
            var result = new List<TrendPoint>(_count);

            // 从最旧的点开始遍历
            int start = _count < Capacity ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                int idx = (start + i) % Capacity;
                if (_buffer[idx].Timestamp >= cutoff)
                {
                    result.Add(_buffer[idx]);
                }
            }

            return result.ToArray();
        }
    }

    /// <summary>
    /// 获取所有数据点。
    /// </summary>
    public TrendPoint[] GetAll()
    {
        lock (_lock)
        {
            if (_count == 0) return [];

            var result = new TrendPoint[_count];
            int start = _count < Capacity ? 0 : _head;
            for (int i = 0; i < _count; i++)
            {
                result[i] = _buffer[(start + i) % Capacity];
            }
            return result;
        }
    }

    /// <summary>
    /// 获取最新的数据点。
    /// </summary>
    public TrendPoint? GetLatest()
    {
        lock (_lock)
        {
            if (_count == 0) return null;
            int idx = (_head - 1 + Capacity) % Capacity;
            return _buffer[idx];
        }
    }

    /// <summary>
    /// 清空缓存。
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _head = 0;
            _count = 0;
        }
    }
}
