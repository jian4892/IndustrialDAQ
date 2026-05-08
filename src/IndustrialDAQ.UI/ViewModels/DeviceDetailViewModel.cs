// File: DeviceDetailViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using System.Windows;
using IndustrialDAQ.Acquisition;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using IndustrialDAQ.UI.Models;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Commands;
using IndustrialDAQ.UI.Events;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 设备详情 ViewModel — 展示单个设备的完整测点数据和状态信息。
/// </summary>
public class DeviceDetailViewModel : BindableBase, IDestructible
{
    private readonly RealTimeStore _realTimeStore;
    private readonly AcquisitionHost _acquisitionHost;
    private readonly IDialogService _dialogService;
    private readonly IEventAggregator _eventAggregator;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, TagDisplayItem> _itemLookup = new();

    /// <summary>设备分组列表（树状结构）。</summary>
    public ObservableCollection<DeviceGroup> DeviceGroups { get; } = new();

    private readonly Dictionary<string, string> _tagToDeviceId = new();
    private readonly Dictionary<string, DeviceGroup> _deviceLookup = new();

    private string _deviceName = "反应釜 #1";
    /// <summary>当前设备名称。</summary>
    public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

    private string _deviceStatus = "运行中";
    /// <summary>设备状态文本。</summary>
    public string DeviceStatus { get => _deviceStatus; set => SetProperty(ref _deviceStatus, value); }

    private string _deviceStatusColor = "#10B981";
    /// <summary>设备状态颜色。</summary>
    public string DeviceStatusColor { get => _deviceStatusColor; set => SetProperty(ref _deviceStatusColor, value); }

    private string _ipAddress = "192.168.1.101";
    /// <summary>设备 IP 地址。</summary>
    public string IpAddress { get => _ipAddress; set => SetProperty(ref _ipAddress, value); }

    private string _protocol = "Mock";
    /// <summary>通信协议。</summary>
    public string Protocol { get => _protocol; set => SetProperty(ref _protocol, value); }

    private string _cycleTime = "500 ms";
    /// <summary>采集周期。</summary>
    public string CycleTime { get => _cycleTime; set => SetProperty(ref _cycleTime, value); }

    private string _lastError = "无";
    /// <summary>最后错误信息。</summary>
    public string LastError { get => _lastError; set => SetProperty(ref _lastError, value); }

    public DelegateCommand<TagDisplayItem> WriteTagCommand { get; }

    public DeviceDetailViewModel(RealTimeStore realTimeStore, AcquisitionHost acquisitionHost, IDialogService dialogService, IEventAggregator eventAggregator)
    {
        _realTimeStore = realTimeStore ?? throw new ArgumentNullException(nameof(realTimeStore));
        _acquisitionHost = acquisitionHost ?? throw new ArgumentNullException(nameof(acquisitionHost));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _eventAggregator = eventAggregator ?? throw new ArgumentNullException(nameof(eventAggregator));
        _cts = new CancellationTokenSource();
        
        WriteTagCommand = new DelegateCommand<TagDisplayItem>(OnWriteTag);
        
        InitializeDeviceGroups();

        _ = SubscribeToChangesAsync(_cts.Token);
        _ = StartConnectionCheckLoop(_cts.Token);
    }

    private async Task StartConnectionCheckLoop(CancellationToken ct)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                foreach (var group in DeviceGroups)
                {
                    var driver = _acquisitionHost.GetDriver(group.DeviceId);
                    bool isConnected = driver?.IsConnected ?? false;
                    
                    foreach (var tag in group.Tags)
                    {
                        if (isConnected)
                        {
                            // 恢复连接：Bad -> Good
                            if (tag.Quality == "Bad") tag.Quality = "Good";
                        }
                        else
                        {
                            // 断开连接：非 Init -> Bad
                            if (tag.Quality != "Bad" && tag.Quality != "Init")
                            {
                                tag.Quality = "Bad";
                            }
                        }
                    }
                }
                });
            }
        }
        catch (OperationCanceledException) { }
    }

    private void InitializeDeviceGroups()
    {
        var devices = _acquisitionHost.GetDevices();
        foreach (var device in devices)
        {
            var group = new DeviceGroup { DeviceId = device.Id, DeviceName = device.Name };
            _deviceLookup[device.Id] = group;
            DeviceGroups.Add(group);

            foreach (var tag in device.Tags)
            {
                _tagToDeviceId[tag.Id] = device.Id;

                // 初始化占位项，使用户即使还没收到数据也能看到并展开树结构
                var item = new TagDisplayItem(tag.Id)
                {
                    TagName = tag.Name,
                    Description = tag.Description,
                    Value = "-",
                    Quality = "Init",
                    Timestamp = "-",
                    CanWrite = tag.Access != TagAccess.Read
                };
                _itemLookup[tag.Id] = item;
                group.Tags.Add(item);
            }
        }
    }

    /// <inheritdoc />
    public void Destroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private async Task SubscribeToChangesAsync(CancellationToken ct)
    {
        try
        {
            await foreach (TagValue value in _realTimeStore.ChangeStream.ReadAllAsync(ct)
                .ConfigureAwait(false))
            {
                Application.Current?.Dispatcher.Invoke(() => UpdateOrAdd(value));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private void UpdateOrAdd(TagValue value)
    {
        if (_itemLookup.TryGetValue(value.TagId, out TagDisplayItem? item))
        {
            item.TagName = value.TagName;
            item.Value = value.Value?.ToString() ?? "-";
            item.Quality = value.Quality.ToString();
            item.Timestamp = value.Timestamp.LocalDateTime.ToString("HH:mm:ss.fff");
        }
        else
        {
            if (!_tagToDeviceId.TryGetValue(value.TagId, out string? deviceId))
            {
                // 如果是动态增加的测点，尝试查找所属设备
                foreach (var device in _acquisitionHost.GetDevices())
                {
                    if (device.Tags.Any(t => t.Id == value.TagId))
                    {
                        deviceId = device.Id;
                        _tagToDeviceId[value.TagId] = deviceId;
                        break;
                    }
                }
            }

            if (deviceId != null && _deviceLookup.TryGetValue(deviceId, out var group))
            {
                bool canWrite = false;
                var device = _acquisitionHost.GetDevices().FirstOrDefault(d => d.Id == deviceId);
                var tag = device?.Tags.FirstOrDefault(t => t.Id == value.TagId);
                if (tag != null)
                {
                    canWrite = tag.Access != TagAccess.Read;
                }

                item = new TagDisplayItem(value.TagId)
                {
                    TagName = value.TagName,
                    Value = value.Value?.ToString() ?? "-",
                    Quality = value.Quality.ToString(),
                    Timestamp = value.Timestamp.LocalDateTime.ToString("HH:mm:ss.fff"),
                    CanWrite = canWrite
                };
                _itemLookup[value.TagId] = item;
                group.Tags.Add(item);
            }
        }
    }

    private void OnWriteTag(TagDisplayItem? item)
    {
        if (item == null) return;
        
        DeviceConfig? targetDevice = null;
        TagPoint? targetTag = null;
        
        foreach (var device in _acquisitionHost.GetDevices())
        {
            targetTag = device.Tags.FirstOrDefault(t => t.Id == item.TagId);
            if (targetTag != null)
            {
                targetDevice = device;
                break;
            }
        }
        
        if (targetTag == null || targetDevice == null) return;

        var parameters = new DialogParameters
        {
            { "TagName", string.IsNullOrWhiteSpace(item.Description) ? item.TagName : item.Description },
            { "DataType", targetTag.DataType },
            { "CurrentValue", item.Value }
        };

        _dialogService.ShowDialog("WriteTagDialog", parameters, result =>
        {
            if (result.Result != ButtonResult.OK) return;

            string stringValue = result.Parameters.GetValue<string>("ResultValue");
            if (string.IsNullOrWhiteSpace(stringValue)) return;

            object? writeValue = null;
            try
            {
                writeValue = targetTag.DataType switch
                {
                    TagDataType.Bool => bool.Parse(stringValue),
                    TagDataType.Int16 => short.Parse(stringValue),
                    TagDataType.Int32 => int.Parse(stringValue),
                    TagDataType.Float32 => float.Parse(stringValue),
                    TagDataType.Float64 => double.Parse(stringValue),
                    TagDataType.UInt16 => ushort.Parse(stringValue),
                    TagDataType.UInt32 => uint.Parse(stringValue),
                    TagDataType.String => stringValue,
                    _ => stringValue
                };
            }
            catch
            {
                _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
                {
                    Title = "写入失败",
                    Message = $"写入格式不正确，无法转换为 {targetTag.DataType}",
                    Type = NotificationType.Error
                });
                return;
            }

            var driver = _acquisitionHost.GetDriver(targetDevice.Id);
            if (driver != null)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await driver.WriteTagAsync(targetTag, writeValue, CancellationToken.None);
                        _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
                        {
                            Title = "写入成功",
                            Message = $"测点 [{targetTag.Name}] 写入指令已下发",
                            Type = NotificationType.Success
                        });
                    }
                    catch (Exception ex)
                    {
                        _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
                        {
                            Title = "写入错误",
                            Message = ex.Message,
                            Type = NotificationType.Error
                        });
                    }
                });
            }
            else
            {
                _eventAggregator.GetEvent<NotificationEvent>().Publish(new NotificationMessage
                {
                    Title = "写入失败",
                    Message = "无法获取设备驱动实例，请检查设备是否连接",
                    Type = NotificationType.Error
                });
            }
        });
    }
}

/// <summary>
/// 设备分组项，用于 TreeView。
/// </summary>
public class DeviceGroup : BindableBase
{
    private string _deviceName = string.Empty;
    public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

    private string _deviceId = string.Empty;
    public string DeviceId { get => _deviceId; set => SetProperty(ref _deviceId, value); }

    public ObservableCollection<TagDisplayItem> Tags { get; } = new();
}
