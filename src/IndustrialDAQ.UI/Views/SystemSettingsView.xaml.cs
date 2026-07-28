// File: SystemSettingsView.xaml.cs  Module: UI (Views)  Author: IndustrialDAQ Team
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using IndustrialDAQ.UI.ViewModels;

namespace IndustrialDAQ.UI.Views;

/// <summary>
/// 按分类名称切换 Visibility 的值转换器。
/// ConverterParameter 为目标分类名，当 SelectedCategory.Name 匹配时返回 Visible，否则 Collapsed。
/// </summary>
public class CategoryNameToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string categoryName && parameter is string targetName)
            return string.Equals(categoryName, targetName, StringComparison.OrdinalIgnoreCase)
                ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

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
