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

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 设备详情 ViewModel — 展示单个设备的完整测点数据和状态信息。
/// </summary>
public class DeviceDetailViewModel : BindableBase, IDestructible
{
    private readonly RealTimeStore _realTimeStore;
    private readonly AcquisitionHost _acquisitionHost;
    private readonly IDialogService _dialogService;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, TagDisplayItem> _itemLookup = new();

    /// <summary>测点数据表。</summary>
    public ObservableCollection<TagDisplayItem> Tags { get; } = new();

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

    public DeviceDetailViewModel(RealTimeStore realTimeStore, AcquisitionHost acquisitionHost, IDialogService dialogService)
    {
        _realTimeStore = realTimeStore ?? throw new ArgumentNullException(nameof(realTimeStore));
        _acquisitionHost = acquisitionHost ?? throw new ArgumentNullException(nameof(acquisitionHost));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _cts = new CancellationTokenSource();
        
        WriteTagCommand = new DelegateCommand<TagDisplayItem>(OnWriteTag);

        _ = SubscribeToChangesAsync(_cts.Token);
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
            bool canWrite = false;
            foreach (var device in _acquisitionHost.GetDevices())
            {
                var tag = device.Tags.FirstOrDefault(t => t.Id == value.TagId);
                if (tag != null)
                {
                    canWrite = tag.Access != TagAccess.Read;
                    break;
                }
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
            Tags.Add(item);
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
                MessageBox.Show($"写入格式不正确，无法转换为 {targetTag.DataType}", "写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
                        Application.Current?.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show("写入指令已下发", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        });
                    }
                    catch (Exception ex)
                    {
                        Application.Current?.Dispatcher.Invoke(() =>
                            MessageBox.Show($"写入失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error));
                    }
                });
            }
            else
            {
                MessageBox.Show("无法获取设备驱动实例，请检查设备是否连接", "写入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });
    }
}
