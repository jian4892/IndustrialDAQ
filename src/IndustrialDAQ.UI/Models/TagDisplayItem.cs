// File: TagDisplayItem.cs  Module: UI (Models)  Author: IndustrialDAQ Team
using Prism.Mvvm;

namespace IndustrialDAQ.UI.Models;

/// <summary>
/// 实时测点显示模型 — 可变的 MVVM 绑定对象，用于 DataGrid 实时更新。
/// 包装 <see cref="Core.Models.TagValue"/> 的不可变数据。
/// </summary>
public class TagDisplayItem : BindableBase
{
    /// <summary>测点唯一标识（不显示在 DataGrid 中）。</summary>
    public string TagId { get; }

    private string _tagName = string.Empty;
    /// <summary>测点名称。</summary>
    public string TagName
    {
        get => _tagName;
        set => SetProperty(ref _tagName, value);
    }

    private string _value = "-";
    /// <summary>当前值（格式化字符串）。</summary>
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    private string _quality = "Bad";
    /// <summary>质量码。</summary>
    public string Quality
    {
        get => _quality;
        set => SetProperty(ref _quality, value);
    }

    private string _timestamp = "-";
    /// <summary>采集时间戳。</summary>
    public string Timestamp
    {
        get => _timestamp;
        set => SetProperty(ref _timestamp, value);
    }

    private bool _isNumeric;
    /// <summary>是否为数值类型。</summary>
    public bool IsNumeric
    {
        get => _isNumeric;
        set => SetProperty(ref _isNumeric, value);
    }

    private string _description = string.Empty;
    /// <summary>测点描述。</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private bool _canWrite;
    /// <summary>是否可写。</summary>
    public bool CanWrite
    {
        get => _canWrite;
        set => SetProperty(ref _canWrite, value);
    }

    private bool _isTrendSelected;
    /// <summary>是否在趋势图中展示。</summary>
    public bool IsTrendSelected
    {
        get => _isTrendSelected;
        set => SetProperty(ref _isTrendSelected, value);
    }

    private bool _isGaugeSelected;
    /// <summary>是否在仪表中展示。</summary>
    public bool IsGaugeSelected
    {
        get => _isGaugeSelected;
        set => SetProperty(ref _isGaugeSelected, value);
    }

    private string _writeValue = string.Empty;
    /// <summary>准备写入的值。</summary>
    public string WriteValue
    {
        get => _writeValue;
        set => SetProperty(ref _writeValue, value);
    }

    /// <summary>
    /// 初始化显示模型。
    /// </summary>
    /// <param name="tagId">测点唯一标识</param>
    public TagDisplayItem(string tagId)
    {
        TagId = tagId ?? throw new ArgumentNullException(nameof(tagId));
    }
}
