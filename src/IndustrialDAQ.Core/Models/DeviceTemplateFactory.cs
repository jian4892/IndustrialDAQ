// File: DeviceTemplateFactory.cs  Module: Core (Models)  Author: IndustrialDAQ Team
namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 设备模板工厂 — 提供常见工业设备的预定义模板。
/// 每个模板包含完整的数据点配置及其关联的报警和趋势模板。
/// </summary>
public static class DeviceTemplateFactory
{
    /// <summary>S7-1500 灌装产线 PLC（OpcUA 协议）。</summary>
    public static DeviceTemplate S71500FillingLine()
    {
        var tempAlarm = AlarmTemplateFactory.Temperature();
        var levelAlarm = AlarmTemplateFactory.Level("mL", 800, 700, 100, 50, 30, 60);
        var speedAlarm = AlarmTemplateFactory.MotorSpeed("m/min", 25, 5, 2, 15);
        var estopAlarm = AlarmTemplateFactory.MotorRunning(5);

        return new DeviceTemplate
        {
            TemplateId = "s7-1500-filling",
            Name = "S7-1500 灌装产线 PLC",
            DriverType = "OpcUA",
            DataPoints =
            [
                new DataPointTemplate
                {
                    TemplateId = "dp-filling-level",
                    Name = "灌装液位",
                    DataType = TagDataType.Float32,
                    Unit = "mL",
                    AlarmTemplate = levelAlarm,
                    TrendTemplate = new TrendTemplate
                    {
                        TemplateId = "trend-filling-level",
                        Name = "灌装液位趋势",
                        Unit = "mL",
                        YMin = 0, YMax = 1000,
                        BufferCapacity = 3600, WindowSeconds = 300,
                        LineColor = "#3B82F6", ShowAlarmLines = true,
                        StrokeThickness = 2, ShowGeometry = false
                    }
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-conveyor-speed",
                    Name = "传送速度",
                    DataType = TagDataType.Float32,
                    Unit = "m/min",
                    AlarmTemplate = speedAlarm,
                    TrendTemplate = new TrendTemplate
                    {
                        TemplateId = "trend-conveyor-speed",
                        Name = "传送速度趋势",
                        Unit = "m/min",
                        YMin = 0, YMax = 50,
                        BufferCapacity = 3600, WindowSeconds = 300,
                        LineColor = "#10B981", ShowAlarmLines = true,
                        StrokeThickness = 2, ShowGeometry = false
                    }
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-line-estop",
                    Name = "急停按钮",
                    DataType = TagDataType.Bool,
                    Unit = "",
                    AlarmTemplate = estopAlarm,
                    TrendTemplate = new TrendTemplate
                    {
                        TemplateId = "trend-estop",
                        Name = "急停状态趋势",
                        Unit = "",
                        YMin = 0, YMax = 1,
                        BufferCapacity = 600, WindowSeconds = 60,
                        LineColor = "#EF4444", ShowAlarmLines = false,
                        StrokeThickness = 2, ShowGeometry = true
                    }
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-line-running",
                    Name = "产线运行状态",
                    DataType = TagDataType.Bool,
                    Unit = ""
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-filling-valve",
                    Name = "灌装阀状态",
                    DataType = TagDataType.Bool,
                    Unit = ""
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-line-count",
                    Name = "总产量计数",
                    DataType = TagDataType.Int32,
                    Unit = "件",
                    TrendTemplate = new TrendTemplate
                    {
                        TemplateId = "trend-line-count",
                        Name = "产量趋势",
                        Unit = "件",
                        YMin = 0, YMax = double.NaN,
                        BufferCapacity = 3600, WindowSeconds = 600,
                        LineColor = "#F59E0B", ShowAlarmLines = false,
                        StrokeThickness = 2, ShowGeometry = false
                    }
                }
            ]
        };
    }

    /// <summary>Modbus 模拟设备。</summary>
    public static DeviceTemplate ModbusSimulator()
    {
        var levelAlarm = AlarmTemplateFactory.Level("mL", 800, 700, 100, 50, 30, 60);
        var speedAlarm = AlarmTemplateFactory.MotorSpeed("m/min", 25, 5, 2, 15);

        return new DeviceTemplate
        {
            TemplateId = "modbus-sim",
            Name = "Modbus 模拟设备",
            DriverType = "Modbus",
            DataPoints =
            [
                new DataPointTemplate
                {
                    TemplateId = "dp-mb-level",
                    Name = "模拟液位",
                    DataType = TagDataType.Float32,
                    Unit = "mL",
                    AlarmTemplate = levelAlarm,
                    TrendTemplate = new TrendTemplate
                    {
                        TemplateId = "trend-mb-level",
                        Name = "模拟液位趋势",
                        Unit = "mL",
                        YMin = 0, YMax = 1000,
                        BufferCapacity = 3600, WindowSeconds = 300,
                        LineColor = "#8B5CF6", ShowAlarmLines = true,
                        StrokeThickness = 2, ShowGeometry = false
                    }
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-mb-speed",
                    Name = "模拟速度",
                    DataType = TagDataType.Float32,
                    Unit = "m/min",
                    AlarmTemplate = speedAlarm,
                    TrendTemplate = new TrendTemplate
                    {
                        TemplateId = "trend-mb-speed",
                        Name = "模拟速度趋势",
                        Unit = "m/min",
                        YMin = 0, YMax = 50,
                        BufferCapacity = 3600, WindowSeconds = 300,
                        LineColor = "#EC4899", ShowAlarmLines = true,
                        StrokeThickness = 2, ShowGeometry = false
                    }
                },
                new DataPointTemplate
                {
                    TemplateId = "dp-mb-count",
                    Name = "模拟计数",
                    DataType = TagDataType.Int32,
                    Unit = "件"
                }
            ]
        };
    }

    /// <summary>温度监测设备（多路温度传感器）。</summary>
    public static DeviceTemplate TemperatureMonitor()
    {
        var tempAlarm = AlarmTemplateFactory.Temperature("°C", 150, 120, 10, 5, 5, 60);

        return new DeviceTemplate
        {
            TemplateId = "temp-monitor",
            Name = "温度监测设备",
            DriverType = "Modbus",
            DataPoints =
            [
                CreateTempSensor("temp-1", "温度传感器 #1", "#EF4444", tempAlarm),
                CreateTempSensor("temp-2", "温度传感器 #2", "#3B82F6", tempAlarm),
                CreateTempSensor("temp-3", "温度传感器 #3", "#10B981", tempAlarm),
                CreateTempSensor("temp-4", "温度传感器 #4", "#F59E0B", tempAlarm)
            ]
        };
    }

    /// <summary>压力监测设备。</summary>
    public static DeviceTemplate PressureMonitor()
    {
        var pressureAlarm = AlarmTemplateFactory.Pressure("bar", 16, 12, 2, 1, 0.5, 30);

        return new DeviceTemplate
        {
            TemplateId = "pressure-monitor",
            Name = "压力监测设备",
            DriverType = "Modbus",
            DataPoints =
            [
                CreatePressureSensor("press-1", "压力传感器 #1", "#06B6D4", pressureAlarm),
                CreatePressureSensor("press-2", "压力传感器 #2", "#8B5CF6", pressureAlarm)
            ]
        };
    }

    /// <summary>所有预定义设备模板集合。</summary>
    public static IReadOnlyDictionary<string, DeviceTemplate> All { get; } =
        new Dictionary<string, DeviceTemplate>
        {
            ["s7-1500-filling"] = S71500FillingLine(),
            ["modbus-sim"] = ModbusSimulator(),
            ["temp-monitor"] = TemperatureMonitor(),
            ["pressure-monitor"] = PressureMonitor()
        };

    // ── 辅助方法 ──

    private static DataPointTemplate CreateTempSensor(string id, string name,
        string color, AlarmTemplate alarm)
    {
        return new DataPointTemplate
        {
            TemplateId = $"dp-{id}",
            Name = name,
            DataType = TagDataType.Float32,
            Unit = "°C",
            AlarmTemplate = alarm,
            TrendTemplate = new TrendTemplate
            {
                TemplateId = $"trend-{id}",
                Name = $"{name}趋势",
                Unit = "°C",
                YMin = 0, YMax = 200,
                BufferCapacity = 3600, WindowSeconds = 300,
                LineColor = color, ShowAlarmLines = true,
                StrokeThickness = 2, ShowGeometry = false
            }
        };
    }

    private static DataPointTemplate CreatePressureSensor(string id, string name,
        string color, AlarmTemplate alarm)
    {
        return new DataPointTemplate
        {
            TemplateId = $"dp-{id}",
            Name = name,
            DataType = TagDataType.Float32,
            Unit = "bar",
            AlarmTemplate = alarm,
            TrendTemplate = new TrendTemplate
            {
                TemplateId = $"trend-{id}",
                Name = $"{name}趋势",
                Unit = "bar",
                YMin = 0, YMax = 20,
                BufferCapacity = 3600, WindowSeconds = 300,
                LineColor = color, ShowAlarmLines = true,
                StrokeThickness = 2, ShowGeometry = false
            }
        };
    }
}
