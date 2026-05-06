namespace IndustrialDAQ.Core.Models;

/// <summary>
/// Configuration for a single data-source device.
/// </summary>
public sealed class DeviceConfig
{
    /// <summary>Unique device identifier.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable device name (e.g. "Boiler #3 PLC").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Driver type key used to resolve an <see cref="Interfaces.IProtocolDriver"/>.
    /// Examples: "Modbus", "OpcUA", "S7".
    /// </summary>
    public string DriverType { get; init; } = string.Empty;

    /// <summary>IP address or hostname of the device.</summary>
    public string IpAddress { get; init; } = string.Empty;

    /// <summary>TCP port number.</summary>
    public int Port { get; init; }

    /// <summary>Slave / station address (Modbus) or session id (OPC UA).</summary>
    public byte StationAddress { get; init; } = 1;

    /// <summary>S7 PLC CPU 插槽号（默认 1，S7-300 通常为 2）。</summary>
    public short CpuSlot { get; init; } = 1;

    /// <summary>Polling cycle interval in milliseconds.</summary>
    public int CycleTimeMs { get; init; } = 1000;

    /// <summary>Per-read timeout in milliseconds.</summary>
    public int TimeoutMs { get; init; } = 3000;

    /// <summary>Maximum reconnection attempts before raising an alarm.</summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>Tag points belonging to this device.</summary>
    public List<TagPoint> Tags { get; init; } = new();
}
