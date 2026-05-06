namespace IndustrialDAQ.Core.Models;

/// <summary>
/// Definition of a single data point within a device.
/// </summary>
public sealed class TagPoint
{
    /// <summary>Unique tag identifier within the system.</summary>
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Human-readable tag name (e.g. "Temp_Reactor_01").</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Protocol-specific address string.
    /// Modbus: "40001"; OPC UA: "ns=3;s=Temperature".
    /// </summary>
    public string Address { get; init; } = string.Empty;

    /// <summary>Expected data type of the value.</summary>
    public TagDataType DataType { get; init; } = TagDataType.Float32;

    /// <summary>Override of the parent device scan rate (0 = inherit).</summary>
    public int ScanRateMs { get; init; } = 0;

    /// <summary>Deadband / threshold for change detection. Values within this range are not re-published.</summary>
    public double Deadband { get; init; } = 0.0;

    /// <summary>访问权限 — 只读、只写或读写。</summary>
    public TagAccess Access { get; init; } = TagAccess.Read;

    /// <summary>Description for documentation purposes.</summary>
    public string Description { get; init; } = string.Empty;
}

public enum TagDataType : byte
{
    Bool = 1,
    Int16 = 2,
    Int32 = 3,
    Float32 = 4,
    Float64 = 5,
    String = 6,
    Int64 = 7,
    UInt16 = 8,
    UInt32 = 9
}
