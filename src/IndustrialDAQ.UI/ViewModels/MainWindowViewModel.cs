using System.Collections.ObjectModel;
using IndustrialDAQ.UI.Events;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// Shell 窗口 ViewModel — 管理侧边栏导航和全局状态栏。
/// </summary>
public class MainWindowViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private readonly IEventAggregator _eventAggregator;

    /// <summary>全局通知集合。</summary>
    public ObservableCollection<NotificationMessage> Notifications { get; } = new();

    private string _currentPage = "首页";
    /// <summary>当前页面标题。</summary>
    public string CurrentPage { get => _currentPage; set => SetProperty(ref _currentPage, value); }

    private string _statusMessage = "就绪";
    /// <summary>状态栏消息。</summary>
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    private bool _isConnected;
    /// <summary>采集引擎连接状态。</summary>
    public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

    /// <summary>统一导航命令，参数为视图名称。</summary>
    public DelegateCommand<string> NavigateCommand { get; }

    /// <summary>手动关闭通知命令。</summary>
    public DelegateCommand<NotificationMessage> CloseNotificationCommand { get; }

    public MainWindowViewModel(IRegionManager regionManager, IEventAggregator eventAggregator)
    {
        _regionManager = regionManager;
        _eventAggregator = eventAggregator;

        NavigateCommand = new DelegateCommand<string>(page =>
        {
            if (string.IsNullOrEmpty(page)) return;

            string viewName = page switch
            {
                "Dashboard" => "DashboardView",
                "ProductionMonitor" => "ProductionMonitorView",
                "DeviceDetail" => "DeviceDetailView",
                "AlarmRecord" => "AlarmRecordView",
                "SystemSettings" => "SystemSettingsView",
                _ => page
            };

            CurrentPage = page switch
            {
                "Dashboard" => "生产监控中心",
                "ProductionMonitor" => "设备详情",
                "DeviceDetail" => "数据点详情",
                "AlarmRecord" => "警报日志",
                "SystemSettings" => "系统设置",
                _ => page
            };

            _regionManager.RequestNavigate("MainRegion", viewName);
            StatusMessage = $"📍 {CurrentPage} — 已就绪";
        });

        CloseNotificationCommand = new DelegateCommand<NotificationMessage>(msg =>
        {
            if (msg != null && Notifications.Contains(msg))
                Notifications.Remove(msg);
        });

        // 订阅全局通知事件
        _eventAggregator.GetEvent<NotificationEvent>().Subscribe(OnNotificationReceived);

        IsConnected = true;
        StatusMessage = "📍 首页 — 已就绪";
    }

    private void OnNotificationReceived(NotificationMessage message)
    {
        if (message == null) return;

        // 在 UI 线程添加通知
        System.Windows.Application.Current.Dispatcher.Invoke(async () =>
        {
            Notifications.Add(message);

            // 自动消失逻辑
            if (message.DurationMs > 0)
            {
                await Task.Delay(message.DurationMs);
                if (Notifications.Contains(message))
                {
                    Notifications.Remove(message);
                }
            }
        });
    }
}
