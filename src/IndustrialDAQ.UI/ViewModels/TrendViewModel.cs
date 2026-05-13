// File: TrendViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using IndustrialDAQ.Alarm;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Trend;
using IndustrialDAQ.UI.Models;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using SkiaSharp;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 趋势页面 ViewModel — 支持实时/历史模式、多 Tag、报警线、暂停。
/// </summary>
public class TrendViewModel : BindableBase, IDestructible
{
    private readonly TrendEngine _trendEngine;
    private readonly AlarmManager _alarmManager;

    // 颜色池
    private static readonly string[] Colors =
        ["#3B82F6", "#10B981", "#F59E0B", "#EF4444", "#8B5CF6", "#EC4899", "#06B6D4", "#84CC16"];

    // ─── LiveCharts 绑定 ───

    /// <summary>趋势曲线系列。</summary>
    public ObservableCollection<ISeries> Series { get; } = [];

    /// <summary>报警线系列（水平线）。</summary>
    public ObservableCollection<ISeries> AlarmLineSeries { get; } = [];

    /// <summary>X 轴配置（时间轴）。</summary>
    public Axis[] XAxes { get; }

    /// <summary>Y 轴配置（支持双轴）。</summary>
    public Axis[] YAxes { get; }

    /// <summary>图例画刷。</summary>
    public SolidColorPaint LegendPaint { get; }

    // ─── Tag 选择 ───

    /// <summary>可选 Tag 列表。</summary>
    public ObservableCollection<TrendTagItem> AvailableTags { get; } = [];

    // ─── 控制 ───

    private bool _isPaused;
    /// <summary>是否暂停趋势。</summary>
    public bool IsPaused { get => _isPaused; set { if (SetProperty(ref _isPaused, value)) RaisePropertyChanged(nameof(PauseButtonText)); } }

    /// <summary>暂停按钮文本。</summary>
    public string PauseButtonText => IsPaused ? "▶ 恢复" : "⏸ 暂停";

    private bool _isRealTimeMode = true;
    /// <summary>是否为实时模式（false = 历史模式）。</summary>
    public bool IsRealTimeMode { get => _isRealTimeMode; set { if (SetProperty(ref _isRealTimeMode, value)) RaisePropertyChanged(nameof(ModeButtonText)); } }

    /// <summary>模式切换按钮文本。</summary>
    public string ModeButtonText => IsRealTimeMode ? "🔄 历史模式" : "🔄 实时模式";

    private string _statusText = "实时模式";
    /// <summary>状态栏文本。</summary>
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    // ─── 命令 ───

    public DelegateCommand TogglePauseCommand { get; }
    public DelegateCommand SwitchModeCommand { get; }
    public DelegateCommand<TrendTagItem> ToggleTagCommand { get; }

    // 内部 Series 映射
    private readonly Dictionary<string, LineSeries<ObservablePoint>> _seriesMap = [];

    public TrendViewModel(TrendEngine trendEngine, AlarmManager alarmManager)
    {
        _trendEngine = trendEngine;
        _alarmManager = alarmManager;

        // 配置 X 轴（时间轴）
        var typeface = SKTypeface.FromFamilyName("Microsoft YaHei",
            SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
            ?? SKTypeface.Default;
        var darkText = new SolidColorPaint(new SKColor(0x9C, 0xA3, 0xAF)) { SKTypeface = typeface };
        var darkSeparator = new SolidColorPaint(new SKColor(0x37, 0x41, 0x51)) { StrokeThickness = 0.5f };
        LegendPaint = darkText;

        XAxes =
        [
            new Axis
            {
                Name = "时间",
                NameTextSize = 12,
                NamePaint = darkText,
                LabelsPaint = darkText,
                SeparatorsPaint = darkSeparator,
                TextSize = 10,
                Labeler = value =>
                {
                    var dt = new DateTime((long)value);
                    return dt.ToString("HH:mm:ss");
                },
                UnitWidth = TimeSpan.FromSeconds(1).Ticks,
                MinStep = TimeSpan.FromSeconds(1).Ticks
            }
        ];

        YAxes =
        [
            new Axis
            {
                NameTextSize = 12,
                NamePaint = darkText,
                LabelsPaint = darkText,
                SeparatorsPaint = darkSeparator,
                TextSize = 10,
                Position = LiveChartsCore.Measure.AxisPosition.Start
            }
        ];

        // 命令
        TogglePauseCommand = new DelegateCommand(() =>
        {
            IsPaused = !IsPaused;
            StatusText = IsPaused ? "已暂停" : (IsRealTimeMode ? "实时模式" : "历史模式");
        });

        SwitchModeCommand = new DelegateCommand(() =>
        {
            IsRealTimeMode = !IsRealTimeMode;
            StatusText = IsRealTimeMode ? "实时模式" : "历史模式";
        });

        ToggleTagCommand = new DelegateCommand<TrendTagItem>(item =>
        {
            if (item is null) return;
            item.IsSelected = !item.IsSelected;
            UpdateSeriesVisibility(item);
        });

        // 订阅数据刷新事件
        _trendEngine.DataRefreshed += OnDataRefreshed;
        _alarmManager.AlarmTriggered += OnAlarmTriggered;

        // 加载可用 Tag
        LoadAvailableTags();
    }

    /// <inheritdoc />
    public void Destroy()
    {
        _trendEngine.DataRefreshed -= OnDataRefreshed;
        _alarmManager.AlarmTriggered -= OnAlarmTriggered;
    }

    /// <summary>
    /// 加载可选 Tag 列表。
    /// </summary>
    private void LoadAvailableTags()
    {
        int colorIdx = 0;
        foreach (var tagId in _trendEngine.DataStore.TrackedTagIds)
        {
            var template = _trendEngine.DataStore.GetTemplate(tagId);
            string color = template?.LineColor ?? Colors[colorIdx % Colors.Length];
            colorIdx++;

            AvailableTags.Add(new TrendTagItem
            {
                TagId = tagId,
                TagName = tagId.Replace("tag-", "").Replace("-", "."),
                Unit = template?.Unit ?? "",
                Color = color,
                IsSelected = true
            });

            // 创建 Series
            var series = new LineSeries<ObservablePoint>
            {
                Name = tagId.Replace("tag-", "").Replace("-", "."),
                Values = new ObservableCollection<ObservablePoint>(),
                GeometrySize = template?.ShowGeometry == true ? 6 : 0,
                Stroke = new SolidColorPaint(SKColor.Parse(color)) { StrokeThickness = (float)(template?.StrokeThickness ?? 2) },
                Fill = null,
                LineSmoothness = 0
            };

            _seriesMap[tagId] = series;
            Series.Add(series);
        }

        // 加载报警线
        LoadAlarmLines();
    }

    /// <summary>
    /// 加载报警线到图表。
    /// </summary>
    private void LoadAlarmLines()
    {
        foreach (var line in _trendEngine.AlarmLines)
        {
            AlarmLineSeries.Add(new LineSeries<ObservablePoint>
            {
                Name = line.Label,
                Values = new ObservableCollection<ObservablePoint>
                {
                    new(XAxes[0].MinLimit ?? 0, line.Value),
                    new(XAxes[0].MaxLimit ?? DateTime.UtcNow.Ticks, line.Value)
                },
                Stroke = new SolidColorPaint(SKColor.Parse(line.Color)) { StrokeThickness = 1 },
                Fill = null,
                GeometrySize = 0,
                ScalesYAt = 0
            });
        }
    }

    /// <summary>
    /// 数据刷新回调 — 从 TrendCache 读取数据更新 Series。
    /// </summary>
    private void OnDataRefreshed()
    {
        if (IsPaused) return;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var windowSeconds = 300; // 默认 5 分钟窗口

            foreach (var tagItem in AvailableTags)
            {
                if (!tagItem.IsSelected) continue;
                if (!_seriesMap.TryGetValue(tagItem.TagId, out var series)) continue;
                if (series.Values is not ObservableCollection<ObservablePoint> values) continue;

                var cache = _trendEngine.DataStore.GetCache(tagItem.TagId);
                if (cache is null) continue;

                var template = _trendEngine.DataStore.GetTemplate(tagItem.TagId);
                windowSeconds = template?.WindowSeconds ?? windowSeconds;

                var points = cache.GetWindow(windowSeconds);
                if (points.Length == 0) continue;

                // 更新数据
                values.Clear();
                foreach (var p in points)
                {
                    values.Add(new ObservablePoint(p.Timestamp.Ticks, p.Value));
                }
            }

            // 更新状态
            StatusText = IsPaused ? "已暂停" :
                $"实时模式 | {AvailableTags.Count(t => t.IsSelected)} 个 Tag | {DateTime.Now:HH:mm:ss}";
        });
    }

    /// <summary>
    /// 报警触发事件 — 添加报警点标记。
    /// </summary>
    private void OnAlarmTriggered(object? sender, AlarmEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            StatusText = $"报警: {e.Record.Title} — {e.Record.TagName} = {e.Record.TriggerValue:F1}";
        });
    }

    /// <summary>
    /// 更新 Series 可见性。
    /// </summary>
    private void UpdateSeriesVisibility(TrendTagItem item)
    {
        if (_seriesMap.TryGetValue(item.TagId, out var series))
        {
            series.IsVisible = item.IsSelected;
        }
    }
}
