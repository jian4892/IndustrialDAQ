// File: DataPointConfigItem.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using IndustrialDAQ.Core.Models;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 数据点配置项 — 在新增设备模板对话框中，用于配置单个数据点的报警/趋势模板。
/// </summary>
public class DataPointConfigItem : BindableBase
{
    /// <summary>数据点名称。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>数据类型。</summary>
    public TagDataType DataType { get; set; }

    /// <summary>数据类型显示文本。</summary>
    public string DataTypeText => DataType.ToString();

    /// <summary>原始 Tag ID（用于生成模板 ID）。</summary>
    public string SourceTagId { get; set; } = string.Empty;

    private string _unit = string.Empty;
    /// <summary>工程单位。</summary>
    public string Unit { get => _unit; set => SetProperty(ref _unit, value); }

    private string? _selectedAlarmTemplateId;
    /// <summary>选中的报警模板 ID。</summary>
    public string? SelectedAlarmTemplateId
    {
        get => _selectedAlarmTemplateId;
        set => SetProperty(ref _selectedAlarmTemplateId, value);
    }

    private bool _enableTrend = true;
    /// <summary>是否启用趋势模板。</summary>
    public bool EnableTrend { get => _enableTrend; set => SetProperty(ref _enableTrend, value); }

    /// <summary>可选的报警模板列表。</summary>
    public ObservableCollection<AlarmTemplateOption> AlarmTemplateOptions { get; } = [];

    /// <summary>当前选中的报警模板选项。</summary>
    private AlarmTemplateOption? _selectedAlarmOption;
    public AlarmTemplateOption? SelectedAlarmOption
    {
        get => _selectedAlarmOption;
        set
        {
            if (SetProperty(ref _selectedAlarmOption, value))
                SelectedAlarmTemplateId = value?.TemplateId;
        }
    }
}

/// <summary>
/// 报警模板下拉选项。
/// </summary>
public class AlarmTemplateOption
{
    public string TemplateId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "无";
    public AlarmTemplate? Template { get; init; }

    public static AlarmTemplateOption None { get; } = new()
    {
        TemplateId = string.Empty,
        DisplayName = "（无报警）",
        Template = null
    };
}
