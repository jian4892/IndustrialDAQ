// File: DeviceCardItem.cs  Module: UI (Models)  Author: IndustrialDAQ Team
using Prism.Mvvm;

namespace IndustrialDAQ.UI.Models;

/// <summary>
/// 设备状态卡片显示模型 — 用于生产监控面板的设备状态卡片绑定。
/// </summary>
public class DeviceCardItem : BindableBase
{
    private string _deviceName = string.Empty;
    /// <summary>设备名称。</summary>
    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    private string _statusText = "正常";
    /// <summary>状态文本（正常 / 警告 / 已断开）。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _statusType = "success";
    /// <summary>状态类型：success / warning / disconnected，用于前端样式选择。</summary>
    public string StatusType
    {
        get => _statusType;
        set => SetProperty(ref _statusType, value);
    }

    /// <summary>
    /// 初始化设备卡片项。
    /// </summary>
    public DeviceCardItem(string deviceName, string statusText, string statusType)
    {
        _deviceName = deviceName;
        _statusText = statusText;
        _statusType = statusType;
    }
}
