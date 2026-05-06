// File: GaugeItem.cs  Module: UI (Models)  Author: IndustrialDAQ Team
using Prism.Mvvm;

namespace IndustrialDAQ.UI.Models;

/// <summary>
/// 动态仪表盘显示模型。
/// </summary>
public class GaugeItem : BindableBase
{
    private double _value;
    /// <summary>当前值。</summary>
    public double Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    private string _label = string.Empty;
    /// <summary>仪表标题（测点描述）。</summary>
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private string _unit = string.Empty;
    /// <summary>单位。</summary>
    public string Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    private string _gaugeColor = "#3B82F6";
    /// <summary>颜色。</summary>
    public string GaugeColor
    {
        get => _gaugeColor;
        set => SetProperty(ref _gaugeColor, value);
    }

    private double _maxValue = 100;
    /// <summary>最大量程。</summary>
    public double MaxValue
    {
        get => _maxValue;
        set => SetProperty(ref _maxValue, value);
    }

    private double _minValue = 0;
    /// <summary>最小量程。</summary>
    public double MinValue
    {
        get => _minValue;
        set => SetProperty(ref _minValue, value);
    }

    /// <summary>测点唯一标识。</summary>
    public string TagId { get; }

    public GaugeItem(string tagId)
    {
        TagId = tagId ?? throw new ArgumentNullException(nameof(tagId));
    }
}
