// File: AlarmRecordView.xaml.cs  Module: UI (Views)  Author: IndustrialDAQ Team
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace IndustrialDAQ.UI.Views;

/// <summary>
/// 报警记录视图。
/// </summary>
public partial class AlarmRecordView : UserControl
{
    /// <summary>报警级别筛选列表。</summary>
    public static ObservableCollection<string> SeverityFilterList { get; } = new()
        { "全部", "严重", "警告", "信息" };

    /// <summary>报警状态筛选列表。</summary>
    public static ObservableCollection<string> StatusFilterList { get; } = new()
        { "全部", "活跃", "已确认", "已清除" };

    public AlarmRecordView()
    {
        InitializeComponent();
    }
}
