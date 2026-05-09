namespace IndustrialDAQ.Core.Models;

/// <summary>
/// 报警规则配置 — 定义触发报警的条件、级别和消息模板。
/// 支持 High, Low, HighHigh, LowLow, Bool 五种报警类型。
/// </summary>
public sealed class AlarmRule
{
    /// <summary>规则唯一标识。</summary>
    public string RuleId { get; init; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>监控的测点 ID。</summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>监控的测点名称（用于日志和消息）。</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>报警类型。</summary>
    public AlarmType AlarmType { get; init; } = AlarmType.High;

    /// <summary>比较运算符 ("&gt;", "&lt;", "&gt;=", "&lt;=", "==", "!=")。仅用于自定义阈值类型。</summary>
    public string Operator { get; init; } = ">";

    /// <summary>报警阈值（用于 High/Low 类型）。</summary>
    public double Threshold { get; init; }

    /// <summary>高高限阈值（用于 HighHigh 类型）。</summary>
    public double HighHighThreshold { get; init; }

    /// <summary>低低限阈值（用于 LowLow 类型）。</summary>
    public double LowLowThreshold { get; init; }

    /// <summary>回滞值 — 值需回到阈值 ± Hysteresis 范围内才会清除报警，防止抖动。</summary>
    public double Hysteresis { get; init; } = 0;

    /// <summary>报警严重程度。</summary>
    public AlarmSeverity Severity { get; init; } = AlarmSeverity.Warning;

    /// <summary>报警标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>报警消息模板。支持占位符: {TagName}, {Value}, {Threshold}, {Operator}。</summary>
    public string MessageTemplate { get; init; } = string.Empty;

    /// <summary>报警来源设备名称。</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>是否启用此规则。</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>冷却时间（秒）— 同一规则两次报警的最小间隔，防止重复告警。</summary>
    public int CooldownSeconds { get; init; } = 60;
}
