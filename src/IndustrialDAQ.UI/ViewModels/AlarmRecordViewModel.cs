// File: AlarmRecordViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using System.Windows;
using IndustrialDAQ.Alarm;
using IndustrialDAQ.Core.Models;
using IndustrialDAQ.Storage;
using Prism.Commands;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 报警记录 ViewModel — 管理报警列表的展示、筛选和确认。
/// 订阅 AlarmManager 的实时报警事件，与真实数据管道集成。
/// </summary>
public class AlarmRecordViewModel : BindableBase
{
    private readonly AlarmManager _alarmManager;
    private readonly AlarmHistoryRepository _historyRepository;

    /// <summary>报警记录集合。</summary>
    public ObservableCollection<AlarmRecordItem> Alarms { get; } = new();

    /// <summary>筛选后的报警记录。</summary>
    public ObservableCollection<AlarmRecordItem> FilteredAlarms { get; } = new();

    private string _filterSeverity = "全部";
    /// <summary>当前筛选的报警级别。</summary>
    public string FilterSeverity
    {
        get => _filterSeverity;
        set { if (SetProperty(ref _filterSeverity, value)) ApplyFilter(); }
    }

    private string _filterStatus = "全部";
    /// <summary>当前筛选的状态。</summary>
    public string FilterStatus
    {
        get => _filterStatus;
        set { if (SetProperty(ref _filterStatus, value)) ApplyFilter(); }
    }

    private string _searchText = string.Empty;
    /// <summary>搜索文本。</summary>
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilter(); }
    }

    private int _totalCount;
    /// <summary>报警总数。</summary>
    public int TotalCount { get => _totalCount; set => SetProperty(ref _totalCount, value); }

    private int _activeCount;
    /// <summary>活跃报警数。</summary>
    public int ActiveCount { get => _activeCount; set => SetProperty(ref _activeCount, value); }

    /// <summary>确认报警命令。</summary>
    public DelegateCommand<AlarmRecordItem> AcknowledgeCommand { get; }

    /// <summary>确认全部报警命令。</summary>
    public DelegateCommand AcknowledgeAllCommand { get; }

    /// <summary>刷新命令。</summary>
    public DelegateCommand RefreshCommand { get; }

    public AlarmRecordViewModel(AlarmManager alarmManager, AlarmHistoryRepository historyRepository)
    {
        _alarmManager = alarmManager ?? throw new ArgumentNullException(nameof(alarmManager));
        _historyRepository = historyRepository ?? throw new ArgumentNullException(nameof(historyRepository));

        AcknowledgeCommand = new DelegateCommand<AlarmRecordItem>(
            item => AcknowledgeAlarm(item!));

        AcknowledgeAllCommand = new DelegateCommand(AcknowledgeAllAlarms);
        RefreshCommand = new DelegateCommand(() => _ = LoadHistoryAsync());

        // 订阅报警事件
        _alarmManager.AlarmTriggered += OnAlarmTriggered;
        _alarmManager.AlarmAcknowledged += OnAlarmAcknowledged;
        _alarmManager.AlarmCleared += OnAlarmCleared;
        _alarmManager.ActiveAlarmsChanged += OnActiveAlarmsChanged;

        // 加载历史数据
        _ = LoadHistoryAsync();
    }

    /// <summary>
    /// 从数据库加载报警历史记录。
    /// </summary>
    private async Task LoadHistoryAsync()
    {
        try
        {
            var (records, _) = await _historyRepository.GetHistoryAsync(
                pageNumber: 1,
                pageSize: 200,
                cancellationToken: CancellationToken.None);

            Application.Current?.Dispatcher.Invoke(() =>
            {
                Alarms.Clear();
                foreach (var record in records)
                {
                    Alarms.Add(AlarmRecordItem.FromDomain(record));
                }
                ApplyFilter();
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"加载报警历史失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 处理报警触发事件。
    /// </summary>
    private void OnAlarmTriggered(object? sender, AlarmEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var item = AlarmRecordItem.FromDomain(e.Record);
            Alarms.Insert(0, item);
            ApplyFilter();
        });
    }

    /// <summary>
    /// 处理报警确认事件。
    /// </summary>
    private void OnAlarmAcknowledged(object? sender, AlarmEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existingItem = Alarms.FirstOrDefault(a => a.Id == e.Record.Id);
            if (existingItem is not null)
            {
                existingItem.Status = "已确认";
                existingItem.AcknowledgedAt = e.Record.AcknowledgedAt;
                ApplyFilter();
            }
        });
    }

    /// <summary>
    /// 处理报警恢复事件。
    /// </summary>
    private void OnAlarmCleared(object? sender, AlarmEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var existingItem = Alarms.FirstOrDefault(a => a.Id == e.Record.Id);
            if (existingItem is not null)
            {
                Alarms.Remove(existingItem);
                ApplyFilter();
            }
        });
    }

    /// <summary>
    /// 处理活跃报警列表变更事件。
    /// </summary>
    private void OnActiveAlarmsChanged(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ActiveCount = _alarmManager.GetActiveAlarms().Count;
        });
    }

    private void ApplyFilter()
    {
        var filtered = Alarms.AsEnumerable();

        if (FilterSeverity != "全部")
            filtered = filtered.Where(a => a.Severity == FilterSeverity);

        if (FilterStatus != "全部")
            filtered = filtered.Where(a => a.Status == FilterStatus);

        if (!string.IsNullOrWhiteSpace(SearchText))
            filtered = filtered.Where(a =>
                a.Message.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.TagName.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        FilteredAlarms.Clear();
        foreach (var item in filtered)
            FilteredAlarms.Add(item);

        TotalCount = Alarms.Count;
        ActiveCount = Alarms.Count(a => a.Status == "活跃");
    }

    private void AcknowledgeAlarm(AlarmRecordItem item)
    {
        if (item.Status == "活跃")
        {
            _alarmManager.AcknowledgeAlarm(item.Id);
        }
    }

    private void AcknowledgeAllAlarms()
    {
        _alarmManager.AcknowledgeAllAlarms();
    }

    /// <summary>
    /// 清理资源。
    /// </summary>
    public void Cleanup()
    {
        _alarmManager.AlarmTriggered -= OnAlarmTriggered;
        _alarmManager.AlarmAcknowledged -= OnAlarmAcknowledged;
        _alarmManager.AlarmCleared -= OnAlarmCleared;
        _alarmManager.ActiveAlarmsChanged -= OnActiveAlarmsChanged;
    }
}

/// <summary>
/// 报警记录显示模型。
/// </summary>
public class AlarmRecordItem : BindableBase
{
    /// <summary>报警 ID。</summary>
    public string Id { get; }

    /// <summary>报警级别（严重 / 警告 / 信息）。</summary>
    public string Severity { get; }

    /// <summary>报警来源设备。</summary>
    public string Source { get; }

    /// <summary>报警标题。</summary>
    public string Title { get; }

    /// <summary>报警详细消息。</summary>
    public string Message { get; }

    /// <summary>发生时间。</summary>
    public DateTime OccurredAt { get; }

    private string _status;
    /// <summary>报警状态（活跃 / 已确认 / 已清除）。</summary>
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    /// <summary>关联测点名称。</summary>
    public string TagName { get; }

    private DateTime? _acknowledgedAt;
    /// <summary>确认时间。</summary>
    public DateTime? AcknowledgedAt { get => _acknowledgedAt; set => SetProperty(ref _acknowledgedAt, value); }

    /// <summary>严重级别颜色。</summary>
    public string SeverityColor => Severity switch
    {
        "严重" => "#EF4444",
        "警告" => "#F59E0B",
        "信息" => "#3B82F6",
        _ => "#9CA3AF"
    };

    /// <summary>状态颜色。</summary>
    public string StatusColor => Status switch
    {
        "活跃" => "#EF4444",
        "已确认" => "#F59E0B",
        "已清除" => "#10B981",
        _ => "#9CA3AF"
    };

    public AlarmRecordItem(string id, string severity, string source, string title,
        string message, DateTime occurredAt, string status, string tagName)
    {
        Id = id;
        Severity = severity;
        Source = source;
        Title = title;
        Message = message;
        OccurredAt = occurredAt;
        _status = status;
        TagName = tagName;
    }

    /// <summary>
    /// 从领域模型创建。
    /// </summary>
    public static AlarmRecordItem FromDomain(AlarmRecord record)
    {
        string severityText = record.Severity switch
        {
            AlarmSeverity.Critical => "严重",
            AlarmSeverity.Warning => "警告",
            AlarmSeverity.Info => "信息",
            _ => "未知"
        };

        string statusText = record.Status switch
        {
            AlarmStatus.Active => "活跃",
            AlarmStatus.Acknowledged => "已确认",
            AlarmStatus.Cleared => "已清除",
            _ => "未知"
        };

        return new AlarmRecordItem(
            record.Id,
            severityText,
            record.Source,
            record.Title,
            record.Message,
            record.OccurredAt,
            statusText,
            record.TagName)
        {
            AcknowledgedAt = record.AcknowledgedAt
        };
    }
}
