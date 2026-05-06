// File: DriverFactory.cs  Module: Infrastructure  Author: IndustrialDAQ Team
using System.Collections.Concurrent;
using IndustrialDAQ.Core.Interfaces;
using IndustrialDAQ.Core.Models;
using Microsoft.Extensions.Logging;

namespace IndustrialDAQ.Infrastructure;

/// <summary>
/// 驱动工厂实现 — 管理 <see cref="IProtocolDriver"/> 的注册和创建。
/// 支持运行时动态注册新驱动类型，满足热扩展需求。
/// </summary>
public sealed class DriverFactory : IDriverFactory
{
    private readonly ConcurrentDictionary<string, Func<DeviceConfig, CancellationToken, Task<IProtocolDriver>>> _registry = new();
    private readonly ILogger<DriverFactory> _logger;

    /// <summary>
    /// 初始化驱动工厂。
    /// </summary>
    public DriverFactory(ILogger<DriverFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyCollection<string> RegisteredDriverTypes =>
        _registry.Keys.ToList().AsReadOnly();

    /// <inheritdoc />
    public async Task<IProtocolDriver> CreateDriverAsync(DeviceConfig device, CancellationToken ct = default)
    {
        if (!_registry.TryGetValue(device.DriverType, out var factory))
        {
            throw new InvalidOperationException(
                $"未注册的驱动类型: \"{device.DriverType}\"。可用类型: {string.Join(", ", _registry.Keys)}");
        }

        _logger.LogInformation("正在为设备 {DeviceName} 创建驱动 (类型: {DriverType})",
            device.Name, device.DriverType);

        IProtocolDriver driver = await factory(device, ct).ConfigureAwait(false);
        return driver;
    }

    /// <inheritdoc />
    public void RegisterDriver(
        string driverType,
        Func<DeviceConfig, CancellationToken, Task<IProtocolDriver>> factory)
    {
        if (string.IsNullOrWhiteSpace(driverType))
            throw new ArgumentException("驱动类型名称不能为空", nameof(driverType));

        ArgumentNullException.ThrowIfNull(factory);

        if (!_registry.TryAdd(driverType, factory))
        {
            throw new InvalidOperationException($"驱动类型 \"{driverType}\" 已注册，请先调用 RemoveDriver");
        }

        _logger.LogInformation("驱动类型 \"{DriverType}\" 已注册", driverType);
    }

    /// <inheritdoc />
    public bool RemoveDriver(string driverType)
    {
        if (_registry.TryRemove(driverType, out _))
        {
            _logger.LogInformation("驱动类型 \"{DriverType}\" 已移除", driverType);
            return true;
        }

        return false;
    }
}
