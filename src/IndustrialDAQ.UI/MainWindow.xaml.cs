// File: MainWindow.xaml.cs  Module: UI (Shell)  Author: IndustrialDAQ Team
using System.Windows;
using System.Windows.Input;
using IndustrialDAQ.UI.Events;
using IndustrialDAQ.UI.ViewModels;
using Prism.Ioc;
using Prism.Navigation.Regions;

namespace IndustrialDAQ.UI;

/// <summary>
/// Prism Shell 窗口 — 主应用程序窗口，左侧导航栏 + 右侧 Prism 内容区域 + 底部全局状态栏。
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 在 Shell 级别，AutoWireViewModel 对 Window 可能不生效，
        // 因此通过 Code-behind 手动解析并设置 DataContext。
        var app = (App)Application.Current;
        var vm = app.Container.Resolve<MainWindowViewModel>();
        DataContext = vm;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            BtnMaximize_Click(sender, e);
        }
        else
        {
            DragMove();
        }
    }

    /// <summary>
    /// 通知点击事件 — 跳转到对应页面。
    /// </summary>
    private void NotificationBorder_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is NotificationMessage message)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.NotificationClickCommand.Execute(message);
            }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
            WindowState = WindowState.Normal;
        else
            WindowState = WindowState.Maximized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
