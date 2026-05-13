// File: CreateDeviceDialogViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using Prism.Commands;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 创建设备配置对话框 ViewModel — 用户输入设备名称、IP 地址、端口等信息后生成 JSON 配置文件。
/// </summary>
public class CreateDeviceDialogViewModel : BindableBase, IDialogAware
{
    private string _deviceName = string.Empty;
    public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

    private string _driverType = string.Empty;
    public string DriverType { get => _driverType; set => SetProperty(ref _driverType, value); }

    private string _ipAddress = "192.168.1.100";
    public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }

    private string _port = "4840";
    public string Port { get => _port; set => SetProperty(ref _port, value); }

    private string _cycleTimeMs = "500";
    public string CycleTimeMs { get => _cycleTimeMs; set => SetProperty(ref _cycleTimeMs, value); }

    private string _timeoutMs = "3000";
    public string TimeoutMs { get => _timeoutMs; set => SetProperty(ref _timeoutMs, value); }

    private string _retryCount = "3";
    public string RetryCount { get => _retryCount; set => SetProperty(ref _retryCount, value); }

    private string _errorMessage = string.Empty;
    public string ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (SetProperty(ref _errorMessage, value))
                RaisePropertyChanged(nameof(HasError));
        }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public string Title => "生成设备配置";

    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand CancelCommand { get; }

    public DialogCloseListener RequestClose { get; }

    public CreateDeviceDialogViewModel()
    {
        ConfirmCommand = new DelegateCommand(OnConfirm);
        CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
    }

    private void OnConfirm()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(DeviceName))
        {
            ErrorMessage = "请输入设备名称";
            return;
        }
        if (string.IsNullOrWhiteSpace(IpAddress))
        {
            ErrorMessage = "请输入 IP 地址";
            return;
        }
        if (!int.TryParse(Port, out int port) || port < 1 || port > 65535)
        {
            ErrorMessage = "端口号必须是 1-65535 之间的整数";
            return;
        }
        if (!int.TryParse(CycleTimeMs, out int cycle) || cycle < 100)
        {
            ErrorMessage = "采集周期必须 >= 100 ms";
            return;
        }
        if (!int.TryParse(TimeoutMs, out int timeout) || timeout < 1000)
        {
            ErrorMessage = "超时时间必须 >= 1000 ms";
            return;
        }
        if (!int.TryParse(RetryCount, out int retry) || retry < 0)
        {
            ErrorMessage = "重试次数必须 >= 0";
            return;
        }

        var parameters = new DialogParameters
        {
            { "DeviceName", DeviceName.Trim() },
            { "IpAddress", IpAddress.Trim() },
            { "Port", port },
            { "CycleTimeMs", cycle },
            { "TimeoutMs", timeout },
            { "RetryCount", retry }
        };
        RequestClose.Invoke(parameters, ButtonResult.OK);
    }

    public bool CanCloseDialog() => true;
    public void OnDialogClosed() { }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        if (parameters.TryGetValue("TemplateName", out string name))
            DeviceName = name;
        if (parameters.TryGetValue("DriverType", out string driver))
        {
            DriverType = driver;
            Port = driver switch
            {
                "OpcUA" => "4840",
                "Modbus" => "502",
                "S7" => "102",
                _ => "502"
            };
        }
    }
}
