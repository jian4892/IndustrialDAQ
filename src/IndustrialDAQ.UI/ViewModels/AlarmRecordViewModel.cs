// File: AlarmRecordViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using Prism.Commands;
using Prism.Mvvm;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// 报警记录 ViewModel — 管理报警列表的展示、筛选和确认。
/// </summary>
public class AlarmRecordViewModel : BindableBase
{
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

    public AlarmRecordViewModel()
    {
        AcknowledgeCommand = new DelegateCommand<AlarmRecordItem>(
            item => AcknowledgeAlarm(item!));

        AcknowledgeAllCommand = new DelegateCommand(AcknowledgeAllAlarms);

        GenerateMockData();
        ApplyFilter();
    }

    private void GenerateMockData()
    {
        var now = DateTime.Now;
        Alarms.Add(new AlarmRecordItem("A-001", "严重", "灌装机 1", "灌装体积超限",
            "灌装体积超出上限 800 mL，当前值: 856 mL", now.AddMinutes(-5), "活跃", "Temp_Reactor_01"));
        Alarms.Add(new AlarmRecordItem("A-002", "警告", "旋盖机 1", "力矩偏低",
            "旋盖力矩低于下限 2 Nm，当前值: 1.3 Nm", now.AddMinutes(-12), "活跃", "Temp_Boiler_03"));
        Alarms.Add(new AlarmRecordItem("A-003", "严重", "传送带 A", "电机过热",
            "传送带电机温度超过 85°C，当前值: 92°C", now.AddMinutes(-18), "已确认", "Flow_Boiler_03"));
        Alarms.Add(new AlarmRecordItem("A-004", "信息", "灌装机 2", "维护提醒",
            "距离下次保养还剩 48 小时", now.AddHours(-1), "已确认", "-"));
        Alarms.Add(new AlarmRecordItem("A-005", "警告", "锅炉 #3", "压力波动",
            "压力波动超过死区范围，当前波动: ±3.2 bar", now.AddMinutes(-25), "活跃", "Pressure_Reactor_01"));
        Alarms.Add(new AlarmRecordItem("A-006", "严重", "旋盖机 2", "通讯中断",
            "与旋盖机 2 的 Modbus 通讯中断，重试 3 次失败", now.AddMinutes(-30), "活跃", "-"));
        Alarms.Add(new AlarmRecordItem("A-007", "信息", "CIP 清洗", "清洗完成",
            "CIP 清洗程序已正常完成", now.AddHours(-2), "已清除", "-"));
        Alarms.Add(new AlarmRecordItem("A-008", "警告", "传送带 B", "速度异常",
            "传送速度偏差超过 ±10%，当前: 18 m/min (设定: 15 m/min)", now.AddMinutes(-45), "活跃", "Flow_Boiler_03"));
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
            item.Status = "已确认";
            item.AcknowledgedAt = DateTime.Now;
            ApplyFilter();
        }
    }

    private void AcknowledgeAllAlarms()
    {
        foreach (var item in Alarms.Where(a => a.Status == "活跃"))
        {
            item.Status = "已确认";
            item.AcknowledgedAt = DateTime.Now;
        }
        ApplyFilter();
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

    /// <summary>确认时间。</summary>
    public DateTime? AcknowledgedAt { get; set; }

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
}
