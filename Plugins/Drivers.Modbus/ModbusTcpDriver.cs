// File: ModbusTcpDriver.cs
using System.Net.Sockets;
using IndustrialDAQ.Core.Interfaces;
using IndustrialDAQ.Core.Models;
using Modbus.Device;

namespace Drivers.Modbus;

public sealed class ModbusTcpDriver : IProtocolDriver
{
    private TcpClient? _tcpClient;
    private IModbusMaster? _master;
    private bool _connected;
    private readonly string _ipAddress;
    private readonly int _port;
    private readonly int _timeoutMs;
    private readonly byte _slaveId;

    public string DriverType => "Modbus";
    public bool IsConnected => _connected;

    public ModbusTcpDriver()
    {
        _ipAddress = string.Empty;
        _port = 502;
        _timeoutMs = 3000;
        _slaveId = 1;
    }

    public ModbusTcpDriver(DeviceConfig config)
    {
        _ipAddress = config.IpAddress;
        _port = config.Port > 0 ? config.Port : 502;
        _timeoutMs = config.TimeoutMs > 0 ? config.TimeoutMs : 3000;
        _slaveId = config.StationAddress;
    }

    public async Task ConnectAsync(CancellationToken ct = default)
    {
        if (_connected) return;
        ct.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(_ipAddress))
            throw new InvalidOperationException("Modbus 驱动未配置 IP 地址");

        _tcpClient = new TcpClient
        {
            ReceiveTimeout = _timeoutMs,
            SendTimeout = _timeoutMs
        };

        await _tcpClient.ConnectAsync(_ipAddress, _port, ct);
        _master = ModbusIpMaster.CreateIp(_tcpClient);
        _connected = true;
    }

    public Task DisconnectAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _connected = false;
        _master?.Dispose();
        _master = null;
        _tcpClient?.Close();
        _tcpClient?.Dispose();
        _tcpClient = null;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TagValue>> ReadTagsAsync(
        IEnumerable<TagPoint> tags, CancellationToken ct = default)
    {
        if (!_connected || _master is null)
            throw new InvalidOperationException("Modbus 驱动未连接");

        ct.ThrowIfCancellationRequested();
        var tagList = tags.ToList();
        if (tagList.Count == 0) return Array.Empty<TagValue>();

        var groups = tagList
            .Where(tag => tag.Access != TagAccess.Write)
            .Select(tag => (Tag: tag, Entry: ParseAddress(tag.Address)))
            .GroupBy(x => x.Entry.FunctionCode)
            .ToList();

        var timestamp = DateTimeOffset.UtcNow;
        var resultMap = new Dictionary<string, TagValue>(tagList.Count);

        foreach (var group in groups)
        {
            try
            {
                var functionCode = group.Key;
                var sortedEntries = group.OrderBy(x => x.Entry.StartAddress).ToList();
                var mergedRanges = MergeAdjacentAddresses(sortedEntries, functionCode);

                foreach (var range in mergedRanges)
                {
                    ushort start = range.StartAddress;
                    ushort count = range.Count;

                    ushort[] rawValues = functionCode switch
                    {
                        ModbusFunctionCode.ReadCoils =>
                            (await _master.ReadCoilsAsync(_slaveId, start, count))
                            .Select(b => b ? (ushort)1 : (ushort)0).ToArray(),

                        ModbusFunctionCode.ReadDiscreteInputs =>
                            (await _master.ReadInputsAsync(_slaveId, start, count))
                            .Select(b => b ? (ushort)1 : (ushort)0).ToArray(),

                        ModbusFunctionCode.ReadInputRegisters =>
                            await _master.ReadInputRegistersAsync(_slaveId, start, count),

                        ModbusFunctionCode.ReadHoldingRegisters =>
                            await _master.ReadHoldingRegistersAsync(_slaveId, start, count),

                        _ => throw new NotSupportedException($"不支持的功能码: {functionCode}")
                    };

                    foreach (var (tag, entry) in range.Tags)
                    {
                        int offset = entry.StartAddress - start;
                        object? value = functionCode is ModbusFunctionCode.ReadCoils or ModbusFunctionCode.ReadDiscreteInputs
                            ? rawValues[offset] == 1
                            : ExtractTypedValue(rawValues, offset, tag.DataType);

                        resultMap[tag.Id] = new TagValue
                        {
                            TagId = tag.Id,
                            TagName = tag.Name,
                            Value = value,
                            Quality = Quality.Good,
                            Timestamp = timestamp,
                            DataType = MapToType(tag.DataType)
                        };
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (ex is IOException or SocketException or ObjectDisposedException or TimeoutException or InvalidOperationException)
                {
                    _connected = false;
                    throw;
                }

                foreach (var (tag, _) in group)
                {
                    resultMap[tag.Id] = new TagValue
                    {
                        TagId = tag.Id,
                        TagName = tag.Name,
                        Value = null,
                        Quality = Quality.Bad,
                        Timestamp = timestamp,
                        DataType = MapToType(tag.DataType)
                    };
                }
            }
        }

        return tagList
            .Select(t => resultMap.TryGetValue(t.Id, out var v)
                ? v
                : new TagValue { TagId = t.Id, TagName = t.Name, Quality = Quality.Bad, Timestamp = timestamp })
            .ToList();
    }

    public async Task WriteTagAsync(TagPoint tag, object value, CancellationToken ct = default)
    {
        if (!_connected || _master is null)
            throw new InvalidOperationException("Modbus 驱动未连接");

        ct.ThrowIfCancellationRequested();
        if (tag.Access == TagAccess.Read)
            throw new InvalidOperationException($"标签 {tag.Name} 为只读，不可写入");

        var entry = ParseAddress(tag.Address);

        try
        {
            switch (entry.FunctionCode)
            {
                case ModbusFunctionCode.ReadCoils:
                    if (value is bool b)
                        await _master.WriteSingleCoilAsync(_slaveId, entry.StartAddress, b);
                    else
                        await _master.WriteSingleCoilAsync(_slaveId, entry.StartAddress, Convert.ToBoolean(value));
                    break;

                case ModbusFunctionCode.ReadHoldingRegisters:
                    ushort[] registerValues = PackRegisterValues(value, tag.DataType);
                    if (registerValues.Length == 1)
                        await _master.WriteSingleRegisterAsync(_slaveId, entry.StartAddress, registerValues[0]);
                    else
                        await _master.WriteMultipleRegistersAsync(_slaveId, entry.StartAddress, registerValues);
                    break;

                default:
                    throw new NotSupportedException($"不支持写入功能码 {entry.FunctionCode}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (ex is IOException or SocketException or ObjectDisposedException or TimeoutException or InvalidOperationException)
            {
                _connected = false;
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    // ─── 地址解析 ───

    private record ModbusAddressEntry(ModbusFunctionCode FunctionCode, ushort StartAddress);

    /// <summary>
    /// 解析 Modbus 地址字符串。
    /// 支持格式: "HR:0", "IR:1", "CO:2", "DI:3", "40001", "0x0000"
    /// </summary>
    private static ModbusAddressEntry ParseAddress(string address)
    {
        string cleaned = address.Trim().ToUpperInvariant();

        // 支持 HR:0, IR:1, CO:2, DI:3 格式
        if (cleaned.Contains(':'))
        {
            var parts = cleaned.Split(':');
            if (parts.Length == 2 && ushort.TryParse(parts[1], out ushort offset))
            {
                return parts[0] switch
                {
                    "HR" => new ModbusAddressEntry(ModbusFunctionCode.ReadHoldingRegisters, offset),
                    "IR" => new ModbusAddressEntry(ModbusFunctionCode.ReadInputRegisters, offset),
                    "CO" or "C" => new ModbusAddressEntry(ModbusFunctionCode.ReadCoils, offset),
                    "DI" or "D" => new ModbusAddressEntry(ModbusFunctionCode.ReadDiscreteInputs, offset),
                    _ => throw new ArgumentException($"不支持的 Modbus 地址前缀: {parts[0]}")
                };
            }
        }

        // 去掉前缀
        string numeric = cleaned
            .Replace("HR", "").Replace("IR", "").Replace("CO", "")
            .Replace("DI", "").Replace("H", "").Replace("C", "").Replace("D", "");

        if (numeric.Length == 0)
            throw new ArgumentException($"无效的 Modbus 地址: {address}");

        // 16 进制
        if (numeric.StartsWith("0X"))
        {
            ushort hexVal = Convert.ToUInt16(numeric, 16);
            return new ModbusAddressEntry(ModbusFunctionCode.ReadHoldingRegisters, hexVal);
        }

        // 传统 5 位地址格式
        if (numeric.Length == 5 && numeric.All(char.IsDigit))
        {
            int prefix = numeric[0] - '0';
            ushort offset = checked((ushort)(ushort.Parse(numeric.Substring(1)) - 1));
            return prefix switch
            {
                0 => new ModbusAddressEntry(ModbusFunctionCode.ReadCoils, offset),
                1 => new ModbusAddressEntry(ModbusFunctionCode.ReadDiscreteInputs, offset),
                3 => new ModbusAddressEntry(ModbusFunctionCode.ReadInputRegisters, offset),
                4 => new ModbusAddressEntry(ModbusFunctionCode.ReadHoldingRegisters, offset),
                _ => throw new ArgumentException($"不支持的 Modbus 地址前缀: {prefix}")
            };
        }

        // 6 位格式
        if (ushort.TryParse(numeric, out ushort num) && num >= 10000)
        {
            int prefix = num / 10000;
            ushort offset = checked((ushort)((num % 10000) - 1));
            return prefix switch
            {
                0 => new ModbusAddressEntry(ModbusFunctionCode.ReadCoils, offset),
                1 => new ModbusAddressEntry(ModbusFunctionCode.ReadDiscreteInputs, offset),
                3 => new ModbusAddressEntry(ModbusFunctionCode.ReadInputRegisters, offset),
                4 => new ModbusAddressEntry(ModbusFunctionCode.ReadHoldingRegisters, offset),
                _ => throw new ArgumentException($"不支持的 Modbus 地址前缀: {prefix}")
            };
        }

        // 纯数字，默认 Holding Register
        if (!ushort.TryParse(numeric, out num))
            throw new ArgumentException($"无法解析 Modbus 地址: {address}");

        return new ModbusAddressEntry(ModbusFunctionCode.ReadHoldingRegisters, num);
    }

    private static List<ModbusReadRange> MergeAdjacentAddresses(
        List<(TagPoint Tag, ModbusAddressEntry Entry)> sortedEntries,
        ModbusFunctionCode functionCode)
    {
        const ushort maxGap = 8;
        const ushort maxBatch = 120;

        var ranges = new List<ModbusReadRange>();
        if (sortedEntries.Count == 0) return ranges;

        ushort rangeStart = sortedEntries[0].Entry.StartAddress;
        ushort rangeEnd = rangeStart;
        var rangeTags = new List<(TagPoint, ModbusAddressEntry)> { sortedEntries[0] };

        for (int i = 1; i < sortedEntries.Count; i++)
        {
            var (tag, entry) = sortedEntries[i];
            ushort required = (ushort)(entry.StartAddress + RegistersNeeded(tag.DataType) - 1);

            if (entry.StartAddress <= rangeEnd + maxGap &&
                required - rangeStart + 1 <= maxBatch)
            {
                rangeEnd = Math.Max(rangeEnd, required);
            }
            else
            {
                ranges.Add(new ModbusReadRange(rangeStart,
                    checked((ushort)(rangeEnd - rangeStart + 1)), rangeTags));
                rangeStart = entry.StartAddress;
                rangeEnd = required;
                rangeTags = new List<(TagPoint, ModbusAddressEntry)> { (tag, entry) };
                continue;
            }
            rangeTags.Add((tag, entry));
        }

        ranges.Add(new ModbusReadRange(rangeStart,
            checked((ushort)(rangeEnd - rangeStart + 1)), rangeTags));

        return ranges;
    }

    private record ModbusReadRange(
        ushort StartAddress, ushort Count, List<(TagPoint Tag, ModbusAddressEntry Entry)> Tags);

    // ─── 数据类型转换 ───

    /// <summary>
    /// 关键修改：支持 Little Endian 和 Big Endian
    /// </summary>
    private static object? ExtractTypedValue(ushort[] registers, int offset, TagDataType dataType)
    {
        return dataType switch
        {
            TagDataType.Bool => registers[offset] != 0,
            TagDataType.Int16 => (short)registers[offset],
            TagDataType.UInt16 => registers[offset],
            TagDataType.Int32 => (int)PackTwoRegistersLittleEndian(registers, offset),
            TagDataType.UInt32 => PackTwoRegistersLittleEndian(registers, offset),
            TagDataType.Float32 => RegistersToFloatLittleEndian(registers, offset),  // ← 改成 Little Endian
            TagDataType.Float64 => RegistersToDoubleLittleEndian(registers, offset),
            TagDataType.Int64 => (long)PackFourRegistersLittleEndian(registers, offset),
            TagDataType.String => RegistersToString(registers, offset, 20),
            _ => registers[offset]
        };
    }

    // ─── Little Endian 转换（匹配 Python 模拟器）───

    private static uint PackTwoRegistersLittleEndian(ushort[] regs, int offset)
        => ((uint)regs[offset + 1] << 16) | regs[offset];

    private static ulong PackFourRegistersLittleEndian(ushort[] regs, int offset)
        => ((ulong)regs[offset + 3] << 48) | ((ulong)regs[offset + 2] << 32)
           | ((ulong)regs[offset + 1] << 16) | regs[offset];

    /// <summary>
    /// Little Endian: 寄存器 [offset] = 低 16 位, [offset+1] = 高 16 位
    /// 字节顺序: 低字节在前
    /// </summary>
    private static float RegistersToFloatLittleEndian(ushort[] regs, int offset)
    {
        byte[] bytes = new byte[4];
        bytes[0] = (byte)(regs[offset] & 0xFF);      // 低字节
        bytes[1] = (byte)(regs[offset] >> 8);        // 高字节
        bytes[2] = (byte)(regs[offset + 1] & 0xFF);  // 低字节
        bytes[3] = (byte)(regs[offset + 1] >> 8);     // 高字节
        return BitConverter.ToSingle(bytes, 0);
    }

    private static double RegistersToDoubleLittleEndian(ushort[] regs, int offset)
    {
        byte[] bytes = new byte[8];
        for (int i = 0; i < 4; i++)
        {
            bytes[i * 2] = (byte)(regs[offset + i] & 0xFF);
            bytes[i * 2 + 1] = (byte)(regs[offset + i] >> 8);
        }
        return BitConverter.ToDouble(bytes, 0);
    }

    // ─── 字符串转换 ───

    private static string RegistersToString(ushort[] regs, int offset, int maxRegs)
    {
        var chars = new List<char>();
        for (int i = 0; i < maxRegs && offset + i < regs.Length; i++)
        {
            ushort r = regs[offset + i];
            if (r == 0) break;
            char lo = (char)(r & 0xFF);
            if (lo != 0) chars.Add(lo);
            char hi = (char)(r >> 8);
            if (hi != 0) chars.Add(hi);
        }
        return new string(chars.ToArray());
    }

    // ─── 写入打包 ───

    private static ushort[] PackRegisterValues(object value, TagDataType dataType)
    {
        byte[] bytes = dataType switch
        {
            TagDataType.Int16 => BitConverter.GetBytes(Convert.ToInt16(value)),
            TagDataType.UInt16 => BitConverter.GetBytes(Convert.ToUInt16(value)),
            TagDataType.Int32 => BitConverter.GetBytes(Convert.ToInt32(value)),
            TagDataType.UInt32 => BitConverter.GetBytes(Convert.ToUInt32(value)),
            TagDataType.Float32 => BitConverter.GetBytes(Convert.ToSingle(value)),
            TagDataType.Float64 => BitConverter.GetBytes(Convert.ToDouble(value)),
            TagDataType.Int64 => BitConverter.GetBytes(Convert.ToInt64(value)),
            _ => throw new NotSupportedException($"不支持的 Modbus 写入类型: {dataType}")
        };

        int regCount = (bytes.Length + 1) / 2;
        ushort[] regs = new ushort[regCount];

        // Little Endian: 低字节在前
        for (int i = 0; i < regCount; i++)
        {
            int byteIdx = i * 2;
            regs[i] = (ushort)(bytes[byteIdx] |
                (byteIdx + 1 < bytes.Length ? bytes[byteIdx + 1] << 8 : 0));
        }
        return regs;
    }

    private static ushort RegistersNeeded(TagDataType dataType) => dataType switch
    {
        TagDataType.Bool or TagDataType.Int16 or TagDataType.UInt16 => 1,
        TagDataType.Int32 or TagDataType.UInt32 or TagDataType.Float32 => 2,
        TagDataType.Float64 or TagDataType.Int64 => 4,
        TagDataType.String => 10,
        _ => 1
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

    private enum ModbusFunctionCode : byte
    {
        ReadCoils = 1,
        ReadDiscreteInputs = 2,
        ReadInputRegisters = 4,
        ReadHoldingRegisters = 3
    }
}