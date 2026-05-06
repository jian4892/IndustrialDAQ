// File: MainWindowViewModel.cs  Module: UI (ViewModels)  Author: IndustrialDAQ Team
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace IndustrialDAQ.UI.ViewModels;

/// <summary>
/// Shell 窗口 ViewModel — 管理侧边栏导航和全局状态栏。
/// </summary>
public class MainWindowViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;

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

    public MainWindowViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;

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

        IsConnected = true;
        StatusMessage = "📍 首页 — 已就绪";
    }
}
