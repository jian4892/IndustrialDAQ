// File: TrendAlarmLine.cs  Module: Trend  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;

namespace IndustrialDAQ.Trend;

/// <summary>
/// 趋势报警线 — 在趋势图上显示的水平报警线。
/// </summary>
public sealed class TrendAlarmLine
{
    /// <summary>关联的 Tag ID。</summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>关联的 Tag 名称。</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>报警线 Y 值（阈值）。</summary>
    public double Value { get; init; }

    /// <summary>报警级别。</summary>
    public AlarmSeverity Severity { get; init; }

    /// <summary>报警线颜色（十六进制）。</summary>
    public string Color { get; init; } = "#EF4444";

    /// <summary>报警线标签。</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>报警类型（High/Low 等）。</summary>
    public AlarmType AlarmType { get; init; }
}
