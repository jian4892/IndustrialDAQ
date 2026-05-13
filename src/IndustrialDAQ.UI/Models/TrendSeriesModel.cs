// File: TrendSeriesModel.cs  Module: UI (Models)  Author: IndustrialDAQ Team
using Prism.Mvvm;

namespace IndustrialDAQ.UI.Models;

/// <summary>
/// 趋势 Tag 选择项 — 用于 UI 绑定 Tag 的勾选状态。
/// </summary>
public class TrendTagItem : BindableBase
{
    /// <summary>Tag ID。</summary>
    public string TagId { get; init; } = string.Empty;

    /// <summary>Tag 名称。</summary>
    public string TagName { get; init; } = string.Empty;

    /// <summary>工程单位。</summary>
    public string Unit { get; init; } = string.Empty;

    private bool _isSelected;
    /// <summary>是否选中显示趋势。</summary>
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    /// <summary>曲线颜色（十六进制）。</summary>
    public string Color { get; init; } = "#3B82F6";
}
