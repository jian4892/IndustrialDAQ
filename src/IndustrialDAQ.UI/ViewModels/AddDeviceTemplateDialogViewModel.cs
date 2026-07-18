// File: AddDeviceTemplateDialogViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using IndustrialDAQ.Acquisition;
using IndustrialDAQ.Core.Models;
using Prism.Commands;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 新增设备模板对话框 ViewModel — 从已连接设备创建设备模板。
/// 用户选择设备后，为每个数据点配置报警模板和趋势模板。
/// </summary>
public class AddDeviceTemplateDialogViewModel : BindableBase, IDialogAware
{
    private readonly AcquisitionHost _acquisitionHost;

    // ─── 设备选择 ───

    public ObservableCollection<DeviceOption> AvailableDevices { get; } = [];

    private DeviceOption? _selectedDevice;
    public DeviceOption? SelectedDevice
    {
        get => _selectedDevice;
        set
        {
            if (SetProperty(ref _selectedDevice, value) && value is not null)
                OnDeviceSelected(value.Config);
        }
    }

    // ─── 模板信息 ───

    private string _templateName = string.Empty;
    public string TemplateName { get => _templateName; set => SetProperty(ref _templateName, value); }

    private string _driverType = string.Empty;
    public string DriverType { get => _driverType; set => SetProperty(ref _driverType, value); }

    // ─── 数据点配置 ───

    public ObservableCollection<DataPointConfigItem> DataPointConfigs { get; } = [];

    // ─── 状态 ───

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

    public string Title => "新增设备模板";

    public DelegateCommand ConfirmCommand { get; }
    public DelegateCommand CancelCommand { get; }

    public DialogCloseListener RequestClose { get; }

    public AddDeviceTemplateDialogViewModel(AcquisitionHost acquisitionHost)
    {
        _acquisitionHost = acquisitionHost;

        ConfirmCommand = new DelegateCommand(OnConfirm);
        CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));

        LoadAvailableDevices();
    }

    private void LoadAvailableDevices()
    {
        var devices = _acquisitionHost.GetDevices();
        foreach (var device in devices)
            AvailableDevices.Add(new DeviceOption(device));
    }

    private void OnDeviceSelected(DeviceConfig config)
    {
        TemplateName = config.Name;
        DriverType = config.DriverType;

        DataPointConfigs.Clear();

        // 构建报警模板选项列表
        var alarmOptions = new List<AlarmTemplateOption> { AlarmTemplateOption.None };
        foreach (var (id, template) in AlarmTemplateFactory.All)
        {
            alarmOptions.Add(new AlarmTemplateOption
            {
                TemplateId = id,
                DisplayName = template.Name,
                Template = template
            });
        }

        foreach (var tag in config.Tags)
        {
            var item = new DataPointConfigItem
            {
                Name = tag.Name,
                DataType = tag.DataType,
                SourceTagId = tag.Id,
                Unit = tag.Description ?? string.Empty
            };

            foreach (var option in alarmOptions)
                item.AlarmTemplateOptions.Add(option);

            // 根据数据类型自动选择匹配的报警模板
            var matchingAlarm = alarmOptions.FirstOrDefault(o =>
                o.Template is not null && o.Template.ApplicableDataType == tag.DataType);
            if (matchingAlarm is not null)
                item.SelectedAlarmOption = matchingAlarm;

            DataPointConfigs.Add(item);
        }
    }

    private void OnConfirm()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(TemplateName))
        {
            ErrorMessage = "请输入模板名称";
            return;
        }

        if (DataPointConfigs.Count == 0)
        {
            ErrorMessage = "没有数据点可配置";
            return;
        }

        // 构建 DeviceTemplate 领域模型
        var templateId = $"tpl-{TemplateName.Trim().ToLowerInvariant().Replace(" ", "-")}-{DateTime.Now:yyyyMMddHHmmss}";
        var dataPoints = new List<DataPointTemplate>();

        // 趋势曲线颜色池（循环分配）
        string[] trendColors = ["#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6",
                                 "#EC4899", "#06B6D4", "#F97316", "#84CC16", "#6366F1"];
        int colorIndex = 0;

        foreach (var config in DataPointConfigs)
        {
            AlarmTemplate? alarmTemplate = null;
            if (config.SelectedAlarmOption?.Template is not null)
                alarmTemplate = config.SelectedAlarmOption.Template;

            TrendTemplate? trendTemplate = null;
            if (config.EnableTrend)
            {
                trendTemplate = new TrendTemplate
                {
                    TemplateId = $"trend-{config.SourceTagId}",
                    Name = $"{config.Name} 趋势",
                    Unit = config.Unit,
                    YMin = double.NaN,
                    YMax = double.NaN,
                    BufferCapacity = 3600,
                    WindowSeconds = 300,
                    LineColor = trendColors[colorIndex % trendColors.Length],
                    ShowAlarmLines = alarmTemplate is not null,
                    StrokeThickness = 2,
                    ShowGeometry = config.DataType == TagDataType.Bool
                };
                colorIndex++;
            }

            dataPoints.Add(new DataPointTemplate
            {
                TemplateId = $"dp-{config.SourceTagId}",
                Name = config.Name,
                DataType = config.DataType,
                Unit = config.Unit,
                AlarmTemplate = alarmTemplate,
                TrendTemplate = trendTemplate
            });
        }

        var template = new DeviceTemplate
        {
            TemplateId = templateId,
            Name = TemplateName.Trim(),
            DriverType = DriverType,
            DataPoints = dataPoints
        };

        var parameters = new DialogParameters
        {
            { "Template", template }
        };
        RequestClose.Invoke(parameters, ButtonResult.OK);
    }

    public bool CanCloseDialog() => true;
    public void OnDialogClosed() { }

    public void OnDialogOpened(IDialogParameters parameters)
    {
        // 无需传入参数，直接从 AcquisitionHost 获取设备列表
    }
}

/// <summary>
/// 设备下拉选项。
/// </summary>
public class DeviceOption
{
    public DeviceConfig Config { get; }
    public string DisplayName => $"{Config.Name} [{Config.DriverType}]";
    public string DeviceId => Config.Id;

    public DeviceOption(DeviceConfig config)
    {
        Config = config;
    }
}
