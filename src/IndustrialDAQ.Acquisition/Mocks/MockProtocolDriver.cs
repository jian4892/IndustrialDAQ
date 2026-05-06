// File: MockProtocolDriver.cs  Module: Acquisition Engine (Mock)  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Interfaces;
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Acquisition.Mocks;

/// <summary>
/// 模拟协议驱动 — 用于开发调试和采集循环验证。
/// 不依赖真实硬件，返回正弦波模拟数据，并随机注入质量劣化。
/// </summary>
public sealed class MockProtocolDriver : IProtocolDriver
{
    private readonly Random _rng = new();
    private bool _connected;
    private DateTimeOffset _startTime;

    /// <inheritdoc />
    public string DriverType => "Mock";

    /// <inheritdoc />
    public bool IsConnected => _connected;

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        // 模拟 5% 概率连接失败（用于测试重连逻辑）
        if (_rng.NextDouble() < 0.05)
            throw new InvalidOperationException("模拟连接失败：设备无响应");

        _connected = true;
        _startTime = DateTimeOffset.UtcNow;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _connected = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<TagValue>> ReadTagsAsync(IEnumerable<TagPoint> tags, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_connected)
            throw new InvalidOperationException("驱动未连接");

        // 模拟 3% 概率读取失败
        if (_rng.NextDouble() < 0.03)
            throw new TimeoutException("模拟读取超时");

        var elapsed = (DateTimeOffset.UtcNow - _startTime).TotalSeconds;

        var readableTags = tags;
        var results = new List<TagValue>(tags is ICollection<TagPoint> coll ? coll.Count : 0);

        foreach (TagPoint tag in readableTags)
        {
            Quality quality = DetermineQuality();
            object? value = quality == Quality.Bad
                ? null
                : GenerateSimulatedValue(tag, elapsed);

            results.Add(new TagValue
            {
                TagId = tag.Id,
                TagName = tag.Name,
                Value = value,
                Quality = quality,
                Timestamp = DateTimeOffset.UtcNow,
                DataType = MapToType(tag.DataType)
            });
        }

        return Task.FromResult<IReadOnlyList<TagValue>>(results);
    }

    /// <inheritdoc />
    public Task WriteTagAsync(TagPoint tag, object value, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_connected)
            throw new InvalidOperationException("驱动未连接");

        if (tag.Access == TagAccess.Read)
            throw new InvalidOperationException($"标签 {tag.Name} 为只读，不可写入");

        // 模拟写入：仅记录，不实际存储
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _connected = false;
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 根据运行时间生成模拟数据：基于正弦波叠加随机噪声。
    /// </summary>
    private static object? GenerateSimulatedValue(TagPoint tag, double elapsedSeconds)
    {
        double baseValue = Math.Sin(elapsedSeconds * 0.5 + tag.Name.GetHashCode() % 100 * 0.1) * 50.0 + 75.0;
        double noise = Random.Shared.NextDouble() * 4.0 - 2.0; // ±2 随机噪声
        double raw = Math.Round(baseValue + noise, 3);

        return tag.DataType switch
        {
            TagDataType.Bool => raw > 75.0,
            TagDataType.Int16 => (short)raw,
            TagDataType.Int32 => (int)raw,
            TagDataType.Int64 => (long)raw,
            TagDataType.UInt16 => (ushort)Math.Max(0, raw),
            TagDataType.UInt32 => (uint)Math.Max(0, raw),
            TagDataType.Float32 => (float)raw,
            TagDataType.Float64 => raw,
            TagDataType.String => raw.ToString("F2"),
            _ => raw
        };
    }

    /// <summary>
    /// 按概率分布决定本次采集的质量码：
    /// ~90% Good, ~7% Uncertain, ~3% Bad。
    /// </summary>
    private Quality DetermineQuality()
    {
        double roll = _rng.NextDouble();
        return roll switch
        {
            < 0.90 => Quality.Good,
            < 0.97 => Quality.Uncertain,
            _ => Quality.Bad
        };
    }

    /// <summary>
    /// 将 <see cref="TagDataType"/> 映射为 CLR 类型。
    /// </summary>
    private static Type MapToType(TagDataType dataType) => dataType switch
    {
        TagDataType.Bool => typeof(bool),
        TagDataType.Int16 => typeof(short),
        TagDataType.Int32 => typeof(int),
        TagDataType.Int64 => typeof(long),
        TagDataType.UInt16 => typeof(ushort),
        TagDataType.UInt32 => typeof(uint),
        TagDataType.Float32 => typeof(float),
        TagDataType.Float64 => typeof(double),
        TagDataType.String => typeof(string),
        _ => typeof(object)
    };
}
