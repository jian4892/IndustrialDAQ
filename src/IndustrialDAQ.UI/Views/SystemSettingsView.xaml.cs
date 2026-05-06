// File: SystemSettingsView.xaml.cs  Module: UI (Views)  Author: IndustrialDAQ Team
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IndustrialDAQ.UI.ViewModels;

namespace IndustrialDAQ.UI.Views;

/// <summary>
/// 系统设置视图。
/// </summary>
public partial class SystemSettingsView : UserControl
{
    public SystemSettingsView()
    {
        InitializeComponent();
    }

    private void CategoryClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not SettingsCategory category) return;

        // 更新选中状态
        if (DataContext is SystemSettingsViewModel vm)
        {
            foreach (var cat in vm.Categories)
                cat.IsSelected = false;

            category.IsSelected = true;
            vm.SelectedCategory = category;

            // 更新高亮样式
            var parent = VisualTreeHelper.GetParent(border) as Panel;
            if (parent is null) return;

            foreach (var child in parent.Children)
            {
                if (child is Border b)
                    b.Background = Brushes.Transparent;
            }

            border.Background = (Brush)FindResource("BrushBgTertiary")!;
        }
    }
}
