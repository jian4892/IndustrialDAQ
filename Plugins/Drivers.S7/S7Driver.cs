// File: S7Driver.cs  Module: Plugins (Drivers.S7)  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Interfaces;
using IndustrialDAQ.Core.Models;
using S7.Net;
using S7.Net.Types;

namespace IndustrialDAQ.Drivers.S7;

/// <summary>
/// 西门子 S7 PLC 协议驱动 — 使用 S7netplus 库实现 IProtocolDriver。
/// 支持 S7-200/300/400/1200/1500 系列的 DB、M、I、Q 区读写。
/// </summary>
public sealed class S7Driver : IProtocolDriver
{
    private Plc? _plc;
    private bool _connected;
    private readonly CpuType _cpuType;
    private readonly string _ipAddress;
    private readonly short _rack;
    private readonly short _slot;

    public string DriverType => "S7";
    public bool IsConnected => _connected;

    /// <summary>
    /// 构造 S7 驱动（默认 S7-1200/1500）。
    /// </summary>
    public S7Driver()
    {
        _cpuType = CpuType.S71200;
        _ipAddress = string.Empty;
        _rack = 0;
        _slot = 1;
    }

    /// <summary>
    /// 带设备配置构造驱动。
    /// </summary>
    public S7Driver(DeviceConfig config)
    {
        _cpuType = ParseCpuType(config.DriverType);
        _ipAddress = config.IpAddress;
        _rack = 0;
        _slot = config.CpuSlot > 0 ? config.CpuSlot : (short)1;
    }

    /// <inheritdoc />
    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connected) return;
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_ipAddress))
            throw new InvalidOperationException("S7 驱动未配置 IP 地址");

        _plc = new Plc(_cpuType, _ipAddress, _rack, _slot);

        try
        {
            await Task.Run(() => _plc.Open(), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _plc?.Close();
            _plc = null;
            throw new InvalidOperationException($"S7 PLC 连接失败: {_ipAddress}", ex);
        }

        _connected = true;
    }

    /// <inheritdoc />
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _connected = false;

        if (_plc is not null)
        {
            _plc.Close();
            _plc = null;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(
        IEnumerable<TagPoint> tags, CancellationToken ct = default)
    {
        if (!_connected || _plc is null)
            throw new InvalidOperationException("S7 驱动未连接");

        ct.ThrowIfCancellationRequested();

        var tagList = tags.Where(t => t.Access != TagAccess.Write).ToList();
        if (tagList.Count == 0) return Array.Empty<TagValue>();

        var timestamp = DateTimeOffset.UtcNow;
        var results = new List<TagValue>(tagList.Count);

        foreach (var tag in tagList)
        {
            try
            {
                var (dataType, dbNumber, startByteAdr, bitNumber, varType) = ParseAddress(tag.Address);

                object? rawValue;
                if (!string.IsNullOrEmpty(varType))
                {
                    // 类型化读取（位、字、双字等）
                    VarType vt = varType switch
                    {
                        "Bit" => VarType.Bit,
                        "Byte" => VarType.Byte,
                        "Word" => VarType.Word,
                        "DWord" => VarType.DWord,
                        _ => VarType.Byte
                    };
                    rawValue = await Task.Run(() =>
                        _plc.Read(dataType, dbNumber, startByteAdr, vt, 1, (byte)bitNumber), ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    // 字节数组读取
                    int byteLen = GetByteLength(tag.DataType);
                    rawValue = await Task.Run(() =>
                        _plc.ReadBytes(dataType, dbNumber, startByteAdr, byteLen), ct)
                        .ConfigureAwait(false);
                }

                object? value = rawValue switch
                {
                    byte[] bytes => ConvertFromBytes(bytes, tag.DataType),
                    _ => rawValue // 类型化读取直接返回正确类型
                };

                results.Add(new TagValue
                {
                    TagId = tag.Id,
                    TagName = tag.Name,
                    Value = value,
                    Quality = Quality.Good,
                    Timestamp = timestamp,
                    DataType = MapToType(tag.DataType)
                });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                results.Add(new TagValue
                {
                    TagId = tag.Id,
                    TagName = tag.Name,
                    Value = null,
                    Quality = Quality.Bad,
                    Timestamp = timestamp,
                    DataType = MapToType(tag.DataType)
                });
            }
        }

        return results;
    }

    /// <inheritdoc />
    public async Task WriteTagAsync(TagPoint tag, object value, CancellationToken ct = default)
    {
        if (!_connected || _plc is null)
            throw new InvalidOperationException("S7 驱动未连接");

        ct.ThrowIfCancellationRequested();

        if (tag.Access == TagAccess.Read)
            throw new InvalidOperationException($"标签 {tag.Name} 为只读，不可写入");

        var (dataType, dbNumber, startByteAdr, bitNumber, varType) = ParseAddress(tag.Address);
        object writeValue = ConvertWriteValue(value, tag.DataType);

        try
        {
            await Task.Run(() =>
            {
                if (!string.IsNullOrEmpty(varType) && varType == "Bit")
                {
                    _plc.Write(dataType, dbNumber, startByteAdr, writeValue, (byte)bitNumber);
                }
                else if (!string.IsNullOrEmpty(varType))
                {
                    // 使用类型化写入（Write 方法自动处理大小）
                    _plc.Write(dataType, dbNumber, startByteAdr, writeValue);
                }
                else
                {
                    // 使用字节数组写入
                    byte[] bytes = ConvertToBytes(writeValue, tag.DataType);
                    _plc.WriteBytes(dataType, dbNumber, startByteAdr, bytes);
                }
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new InvalidOperationException($"S7 写入失败: {tag.Address}", ex);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
    }

    // ─── 地址解析 ───

    /// <summary>
    /// 解析 Siemens S7 地址字符串。
    /// 支持格式:
    ///   "DB1.DBD0"   — DB1 双字从字节 0
    ///   "DB1.DBW2"   — DB1 字从字节 2
    ///   "DB1.DBB4"   — DB1 字节 4
    ///   "DB1.DBX0.0" — DB1 位 0.0
    ///   "M0.0"       — 存储器位
    ///   "MB0" / "MW0" / "MD0" — 存储器字节/字/双字
    ///   "I0.0" / "Q0.0" — 输入/输出位
    ///   "IB0" / "QB0" — 输入/输出字节
    /// </summary>
    private static (DataType DataType, int DbNumber, int StartByteAdr, int BitNumber, string VarType)
        ParseAddress(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("S7 地址不能为空");

        address = address.Trim().ToUpperInvariant();

        // DB 区
        if (address.StartsWith("DB"))
        {
            int dotIdx = address.IndexOf('.');
            if (dotIdx < 0) throw new ArgumentException($"无效 S7 DB 地址: {address}");

            if (!int.TryParse(address[2..dotIdx], out int dbNumber))
                throw new ArgumentException($"无法解析 DB 编号: {address}");

            string subAddr = address[(dotIdx + 1)..];

            if (subAddr.StartsWith("DBX"))
            {
                var parts = subAddr[3..].Split('.');
                if (parts.Length < 2) throw new ArgumentException($"无效位地址: {address}");
                return (DataType.DataBlock, dbNumber, int.Parse(parts[0]), int.Parse(parts[1]), "Bit");
            }
            if (subAddr.StartsWith("DBD"))
                return (DataType.DataBlock, dbNumber, int.Parse(subAddr[3..]), 0, "DWord");
            if (subAddr.StartsWith("DBW"))
                return (DataType.DataBlock, dbNumber, int.Parse(subAddr[3..]), 0, "Word");
            if (subAddr.StartsWith("DBB"))
                return (DataType.DataBlock, dbNumber, int.Parse(subAddr[3..]), 0, "Byte");

            return (DataType.DataBlock, dbNumber, int.Parse(subAddr), 0, string.Empty);
        }

        // 存储器区域
        if (address.StartsWith("M"))
        {
            if (address.Contains('.'))
            {
                var parts = address[1..].Split('.');
                if (parts.Length < 2) throw new ArgumentException($"无效存储器位: {address}");
                return (DataType.Memory, 0, int.Parse(parts[0]), int.Parse(parts[1]), "Bit");
            }
            if (address.StartsWith("MD"))
                return (DataType.Memory, 0, int.Parse(address[2..]), 0, "DWord");
            if (address.StartsWith("MW"))
                return (DataType.Memory, 0, int.Parse(address[2..]), 0, "Word");
            if (address.StartsWith("MB"))
                return (DataType.Memory, 0, int.Parse(address[2..]), 0, "Byte");
            return (DataType.Memory, 0, int.Parse(address[1..]), 0, string.Empty);
        }

        // 输入区
        if (address.StartsWith("I"))
        {
            if (address.Contains('.'))
            {
                var parts = address[1..].Split('.');
                if (parts.Length < 2) throw new ArgumentException($"无效输入位: {address}");
                return (DataType.Input, 0, int.Parse(parts[0]), int.Parse(parts[1]), "Bit");
            }
            if (address.StartsWith("IB"))
                return (DataType.Input, 0, int.Parse(address[2..]), 0, "Byte");
            if (address.StartsWith("IW"))
                return (DataType.Input, 0, int.Parse(address[2..]), 0, "Word");
            if (address.StartsWith("ID"))
                return (DataType.Input, 0, int.Parse(address[2..]), 0, "DWord");
            return (DataType.Input, 0, int.Parse(address[1..]), 0, string.Empty);
        }

        // 输出区
        if (address.StartsWith("Q"))
        {
            if (address.Contains('.'))
            {
                var parts = address[1..].Split('.');
                if (parts.Length < 2) throw new ArgumentException($"无效输出位: {address}");
                return (DataType.Output, 0, int.Parse(parts[0]), int.Parse(parts[1]), "Bit");
            }
            if (address.StartsWith("QB"))
                return (DataType.Output, 0, int.Parse(address[2..]), 0, "Byte");
            if (address.StartsWith("QW"))
                return (DataType.Output, 0, int.Parse(address[2..]), 0, "Word");
            if (address.StartsWith("QD"))
                return (DataType.Output, 0, int.Parse(address[2..]), 0, "DWord");
            return (DataType.Output, 0, int.Parse(address[1..]), 0, string.Empty);
        }

        throw new ArgumentException($"无法解析的 S7 地址: {address}");
    }

    // ─── 数据类型转换 ───

    /// <summary>
    /// 从原始字节数组转换为指定类型的值（S7 Big-Endian 字节序）。
    /// </summary>
    private static object? ConvertFromBytes(byte[] bytes, TagDataType dataType)
    {
        if (bytes.Length == 0) return null;

        // S7 PLC 使用 Big-Endian 字节序，BitConverter 在 x86 上使用 Little-Endian
        return dataType switch
        {
            TagDataType.Bool => bytes[0] != 0,
            TagDataType.Int16 => (short)((bytes[0] << 8) | bytes[1]),
            TagDataType.UInt16 => (ushort)((bytes[0] << 8) | bytes[1]),
            TagDataType.Int32 => (int)((uint)(bytes[0] << 24) | (uint)(bytes[1] << 16) | (uint)(bytes[2] << 8) | bytes[3]),
            TagDataType.UInt32 => (uint)(bytes[0] << 24) | (uint)(bytes[1] << 16) | (uint)(bytes[2] << 8) | bytes[3],
            TagDataType.Float32 => BigEndianToFloat(bytes),
            TagDataType.Float64 => BigEndianToDouble(bytes),
            TagDataType.Int64 => (long)BigEndianToUInt64(bytes),
            TagDataType.String => System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0'),
            _ => bytes
        };
    }

    /// <summary>
    /// 将 CLR 值转换为 S7 Big-Endian 字节数组。
    /// </summary>
    private static byte[] ConvertToBytes(object value, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => new[] { (byte)(Convert.ToBoolean(value) ? 1 : 0) },
            TagDataType.Int16 => Int16ToBigEndian(Convert.ToInt16(value)),
            TagDataType.UInt16 => UInt16ToBigEndian(Convert.ToUInt16(value)),
            TagDataType.Int32 => Int32ToBigEndian(Convert.ToInt32(value)),
            TagDataType.UInt32 => UInt32ToBigEndian(Convert.ToUInt32(value)),
            TagDataType.Float32 => FloatToBigEndian(Convert.ToSingle(value)),
            TagDataType.Float64 => DoubleToBigEndian(Convert.ToDouble(value)),
            TagDataType.Int64 => Int64ToBigEndian(Convert.ToInt64(value)),
            TagDataType.String => System.Text.Encoding.ASCII.GetBytes(
                (value.ToString() ?? string.Empty).PadRight(GetByteLength(dataType), '\0')),
            _ => Array.Empty<byte>()
        };
    }

    // Big-Endian 转换辅助方法 (S7 PLC 使用 Big-Endian)
    private static float BigEndianToFloat(byte[] bytes)
        => BitConverter.ToSingle(EnsureBigEndian(bytes, 4));

    private static double BigEndianToDouble(byte[] bytes)
        => BitConverter.ToDouble(EnsureBigEndian(bytes, 8));

    private static ulong BigEndianToUInt64(byte[] bytes)
    {
        var b = EnsureBigEndian(bytes, 8);
        return BitConverter.ToUInt64(b, 0);
    }

    private static byte[] EnsureBigEndian(byte[] bytes, int size)
    {
        if (BitConverter.IsLittleEndian)
        {
            var result = new byte[size];
            for (int i = 0; i < bytes.Length && i < size; i++)
                result[size - 1 - i] = bytes[i];
            return result;
        }
        var padded = new byte[size];
        Array.Copy(bytes, padded, Math.Min(bytes.Length, size));
        return padded;
    }

    private static byte[] Int16ToBigEndian(short v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] UInt16ToBigEndian(ushort v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] Int32ToBigEndian(int v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] UInt32ToBigEndian(uint v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] FloatToBigEndian(float v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] DoubleToBigEndian(double v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static byte[] Int64ToBigEndian(long v)
    {
        var b = BitConverter.GetBytes(v);
        if (BitConverter.IsLittleEndian) Array.Reverse(b);
        return b;
    }

    private static object ConvertWriteValue(object value, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => Convert.ToBoolean(value),
            TagDataType.Int16 => Convert.ToInt16(value),
            TagDataType.UInt16 => Convert.ToUInt16(value),
            TagDataType.Int32 => Convert.ToInt32(value),
            TagDataType.UInt32 => Convert.ToUInt32(value),
            TagDataType.Float32 => Convert.ToSingle(value),
            TagDataType.Float64 => Convert.ToDouble(value),
            TagDataType.String => value.ToString() ?? string.Empty,
            _ => value
        };
    }

    private static int GetByteLength(TagDataType dataType) => dataType switch
    {
        TagDataType.Bool => 1,
        TagDataType.Int16 or TagDataType.UInt16 => 2,
        TagDataType.Int32 or TagDataType.UInt32 or TagDataType.Float32 => 4,
        TagDataType.Int64 or TagDataType.Float64 => 8,
        TagDataType.String => 256,
        _ => 1
    };

    private static CpuType ParseCpuType(string driverType) => driverType.ToUpperInvariant() switch
    {
        "S7-200" or "S7200" => CpuType.S7200,
        "S7-300" or "S7300" => CpuType.S7300,
        "S7-400" or "S7400" => CpuType.S7400,
        "S7-1200" or "S71200" => CpuType.S71200,
        "S7-1500" or "S71500" => CpuType.S71500,
        _ => CpuType.S71200
    };

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
