// File: AlarmTemplateFactory.cs  Module: Core (Models)  Author: IndustrialDAQ Team
namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 报警模板工厂 — 提供常见工业场景的预定义报警模板。
/// </summary>
public static class AlarmTemplateFactory
{
    /// <summary>
    /// 温度报警模板 — 高高/高/低/低低四限报警。
    /// </summary>
    public static AlarmTemplate Temperature(string unit = "°C",
        double hh = 150, double h = 120, double l = 10, double ll = 5,
        double hysteresis = 5, int cooldown = 60)
    {
        return new AlarmTemplate
        {
            TemplateId = "temp-hh-h-l-ll",
            Name = $"温度报警模板 ({unit})",
            ApplicableDataType = TagDataType.Float32,
            Unit = unit,
            HighHighThreshold = hh,
            HighThreshold = h,
            LowThreshold = l,
            LowLowThreshold = ll,
            Hysteresis = hysteresis,
            Severity = AlarmSeverity.Warning,
            CooldownSeconds = cooldown,
            SupportedAlarmTypes = [AlarmType.HighHigh, AlarmType.High, AlarmType.Low, AlarmType.LowLow]
        };
    }

    /// <summary>
    /// 压力报警模板 — 高高/高/低/低低四限报警。
    /// </summary>
    public static AlarmTemplate Pressure(string unit = "bar",
        double hh = 16, double h = 12, double l = 2, double ll = 1,
        double hysteresis = 0.5, int cooldown = 30)
    {
        return new AlarmTemplate
        {
            TemplateId = "pressure-hh-h-l-ll",
            Name = $"压力报警模板 ({unit})",
            ApplicableDataType = TagDataType.Float32,
            Unit = unit,
            HighHighThreshold = hh,
            HighThreshold = h,
            LowThreshold = l,
            LowLowThreshold = ll,
            Hysteresis = hysteresis,
            Severity = AlarmSeverity.Warning,
            CooldownSeconds = cooldown,
            SupportedAlarmTypes = [AlarmType.HighHigh, AlarmType.High, AlarmType.Low, AlarmType.LowLow]
        };
    }

    /// <summary>
    /// 电机运行状态模板 — Bool 类型报警。
    /// </summary>
    public static AlarmTemplate MotorRunning(int cooldown = 5)
    {
        return new AlarmTemplate
        {
            TemplateId = "motor-running",
            Name = "电机运行状态报警",
            ApplicableDataType = TagDataType.Bool,
            Unit = "",
            Severity = AlarmSeverity.Critical,
            CooldownSeconds = cooldown,
            SupportedAlarmTypes = [AlarmType.Bool]
        };
    }

    /// <summary>
    /// 电机速度报警模板 — 高/低限报警。
    /// </summary>
    public static AlarmTemplate MotorSpeed(string unit = "rpm",
        double h = 3000, double l = 100,
        double hysteresis = 50, int cooldown = 30)
    {
        return new AlarmTemplate
        {
            TemplateId = "motor-speed",
            Name = $"电机速度报警模板 ({unit})",
            ApplicableDataType = TagDataType.Float32,
            Unit = unit,
            HighThreshold = h,
            LowThreshold = l,
            Hysteresis = hysteresis,
            Severity = AlarmSeverity.Warning,
            CooldownSeconds = cooldown,
            SupportedAlarmTypes = [AlarmType.High, AlarmType.Low]
        };
    }

    /// <summary>
    /// 通讯故障模板 — Bool 类型，连接断开时报警。
    /// </summary>
    public static AlarmTemplate CommunicationFail(int cooldown = 10)
    {
        return new AlarmTemplate
        {
            TemplateId = "comm-fail",
            Name = "通讯故障报警",
            ApplicableDataType = TagDataType.Bool,
            Unit = "",
            Severity = AlarmSeverity.Critical,
            CooldownSeconds = cooldown,
            SupportedAlarmTypes = [AlarmType.Bool]
        };
    }

    /// <summary>
    /// 液位报警模板 — 高高/高/低/低低四限报警。
    /// </summary>
    public static AlarmTemplate Level(string unit = "mm",
        double hh = 800, double h = 700, double l = 100, double ll = 50,
        double hysteresis = 30, int cooldown = 60)
    {
        return new AlarmTemplate
        {
            TemplateId = "level-hh-h-l-ll",
            Name = $"液位报警模板 ({unit})",
            ApplicableDataType = TagDataType.Float32,
            Unit = unit,
            HighHighThreshold = hh,
            HighThreshold = h,
            LowThreshold = l,
            LowLowThreshold = ll,
            Hysteresis = hysteresis,
            Severity = AlarmSeverity.Warning,
            CooldownSeconds = cooldown,
            SupportedAlarmTypes = [AlarmType.HighHigh, AlarmType.High, AlarmType.Low, AlarmType.LowLow]
        };
    }

    /// <summary>所有预定义模板集合。</summary>
    public static IReadOnlyDictionary<string, AlarmTemplate> All { get; } =
        new Dictionary<string, AlarmTemplate>
        {
            ["temp-hh-h-l-ll"] = Temperature(),
            ["pressure-hh-h-l-ll"] = Pressure(),
            ["motor-running"] = MotorRunning(),
            ["motor-speed"] = MotorSpeed(),
            ["comm-fail"] = CommunicationFail(),
            ["level-hh-h-l-ll"] = Level()
        };
}
