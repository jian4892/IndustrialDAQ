// File: DeviceControlItem.cs  Module: UI (Models)  Author: IndustrialDAQ Team
using IndustrialDAQ.Core.Models;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.Models;

/// <summary>
/// 设备控制显示模型 — 用于设备启停面板的 MVVM 绑定。
/// </summary>
public class DeviceControlItem : BindableBase
{
    /// <summary>设备唯一标识。</summary>
    public string DeviceId { get; }

    /// <summary>设备配置（用于重启时恢复参数）。</summary>
    public DeviceConfig Config { get; }

    private string _deviceName = string.Empty;
    /// <summary>设备名称。</summary>
    public string DeviceName
    {
        get => _deviceName;
        set => SetProperty(ref _deviceName, value);
    }

    private bool _isRunning;
    /// <summary>设备是否正在运行。</summary>
    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (SetProperty(ref _isRunning, value))
            {
                StatusText = value ? "运行中" : "已停止";
                StatusColor = value ? "#4CAF50" : "#9E9E9E";
            }
        }
    }

    private string _statusText = "已停止";
    /// <summary>状态文本。</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _statusColor = "#9E9E9E";
    /// <summary>状态指示颜色（Hex）。</summary>
    public string StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }

    /// <summary>
    /// 初始化设备控制项。
    /// </summary>
    public DeviceControlItem(string deviceId, DeviceConfig config)
    {
        DeviceId = deviceId ?? throw new ArgumentNullException(nameof(deviceId));
        Config = config ?? throw new ArgumentNullException(nameof(config));
        DeviceName = config.Name;
    }
}
