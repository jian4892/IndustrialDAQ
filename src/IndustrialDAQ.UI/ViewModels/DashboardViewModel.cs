// File: DashboardViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using System.Windows;
using IndustrialDAQ.Acquisition;
using IndustrialDAQ.Storage;
using IndustrialDAQ.UI.Models;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Prism.Mvvm;
using Prism.Navigation;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 实时仪表板 ViewModel — 提供首页仪表盘的数据绑定，
/// 包含顶部卡片数据、生产流程工位状态以及实时报警栏数据。
/// </summary>
public class DashboardViewModel : BindableBase, IDestructible
{
    private readonly RealTimeStore _realTimeStore;
    private readonly AcquisitionHost _acquisitionHost;
    private CancellationTokenSource? _cts;

    // ─── 顶部属性 ───

    private int _totalYield;
    public int TotalYield
    {
        get => _totalYield;
        set => SetProperty(ref _totalYield, value);
    }

    private double _yieldRate;
    public double YieldRate
    {
        get => _yieldRate;
        set => SetProperty(ref _yieldRate, value);
    }

    private double _energyConsumption;
    public double EnergyConsumption
    {
        get => _energyConsumption;
        set => SetProperty(ref _energyConsumption, value);
    }

    private string _systemStatus = "Running";
    public string SystemStatus
    {
        get => _systemStatus;
        set => SetProperty(ref _systemStatus, value);
    }

    private string _currentUser = "Admin";
    public string CurrentUser
    {
        get => _currentUser;
        set => SetProperty(ref _currentUser, value);
    }

    private string _currentTime = string.Empty;
    public string CurrentTime
    {
        get => _currentTime;
        set => SetProperty(ref _currentTime, value);
    }

    private int _alarmCount;
    public int AlarmCount
    {
        get => _alarmCount;
        set => SetProperty(ref _alarmCount, value);
    }

    // ─── 集合 ───

    /// <summary>生产线工位集合。</summary>
    public ObservableCollection<StationModel> Stations { get; } = new();

    /// <summary>实时报警栏集合。</summary>
    public ObservableCollection<string> RealTimeAlarms { get; } = new();

    /// <summary>
    /// 初始化仪表板 ViewModel。
    /// </summary>
    public DashboardViewModel(RealTimeStore realTimeStore, AcquisitionHost acquisitionHost)
    {
        _realTimeStore = realTimeStore ?? throw new ArgumentNullException(nameof(realTimeStore));
        _acquisitionHost = acquisitionHost ?? throw new ArgumentNullException(nameof(acquisitionHost));
        _cts = new CancellationTokenSource();

        InitializeMockData();
        StartClock(_cts.Token);
    }

    /// <inheritdoc />
    public void Destroy()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }

    private void InitializeMockData()
    {
        // 顶部卡片数据
        TotalYield = 3847;
        YieldRate = 98.2;
        EnergyConsumption = 12450.5; // 用具体数字替代省略号
        AlarmCount = 2;

        // 初始化工位
        var colorGreen = new SolidColorPaint(SKColor.Parse("#10B981")) { StrokeThickness = 2 };
        var colorYellow = new SolidColorPaint(SKColor.Parse("#F59E0B")) { StrokeThickness = 2 };

        Stations.Add(new StationModel
        {
            StationId = "S1",
            Name = "进瓶区 (传送带)",
            Status = StationStatus.Running,
            PrimaryStatName = "产量",
            PrimaryStatValue = "856 (瓶)",
            PlcCount = 1
        });

        Stations.Add(new StationModel
        {
            StationId = "S2",
            Name = "灌装站 (电磁阀)",
            Status = StationStatus.Running,
            PrimaryStatName = "OEE",
            PrimaryStatValue = "82%",
            PlcCount = 2,
            SparklineSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Values = new double[] { 70, 75, 82, 80, 85, 82 },
                    GeometrySize = 0,
                    Fill = null,
                    Stroke = colorGreen
                }
            }
        });

        Stations.Add(new StationModel
        {
            StationId = "S3",
            Name = "封盖站 (气缸)",
            Status = StationStatus.Standby,
            PrimaryStatName = "产量",
            PrimaryStatValue = "120 (瓶)",
            PlcCount = 2,
            SparklineSeries = new ObservableCollection<ISeries>
            {
                new LineSeries<double>
                {
                    Values = new double[] { 100, 110, 105, 120, 118, 120 },
                    GeometrySize = 0,
                    Fill = null,
                    Stroke = colorYellow
                }
            }
        });

        Stations.Add(new StationModel
        {
            StationId = "S4",
            Name = "贴标站 (电机)",
            Status = StationStatus.Fault,
            PrimaryStatName = "故障代码",
            PrimaryStatValue = "E-002",
            PlcCount = 1
        });

        Stations.Add(new StationModel
        {
            StationId = "S5",
            Name = "出瓶区 (计数)",
            Status = StationStatus.NotStarted,
            PrimaryStatName = "产量",
            PrimaryStatValue = "0 (瓶)",
            PlcCount = 0,
            IsLast = true
        });

        // 实时报警栏
        RealTimeAlarms.Add("[08:45:10] 贴标站-PLC1 通信中断 (Unack)");
        RealTimeAlarms.Add("[08:42:33] 封盖站 等待物料补给 (Ack)");
        RealTimeAlarms.Add("[08:29:45] 进瓶区状态 正常 (Ack)");
        RealTimeAlarms.Add("[08:29:45] 灌装站状态 正常 (Ack)");
    }

    private async void StartClock(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                await Task.Delay(1000, ct);
            }
        }
        catch (TaskCanceledException)
        {
            // 忽略任务取消异常
        }
    }
}
