using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IndustrialDAQ.UI.Converters;

/// <summary>
/// 将整数转换为 Visibility：当值等于 ConverterParameter 时返回 Visible，否则 Collapsed。
/// 用于向导步骤切换。
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int current && parameter is string paramStr && int.TryParse(paramStr, out var target))
        {
            return current == target ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}