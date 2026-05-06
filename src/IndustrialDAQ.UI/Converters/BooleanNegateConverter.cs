// File: BooleanNegateConverter.cs  Module: UI (Converters)  Author: IndustrialDAQ Team
using System.Globalization;
using System.Windows.Data;

namespace IndustrialDAQ.UI.Converters;

/// <summary>
/// 布尔值取反转换器 — 用于将 IsRunning 取反后绑定到启动按钮的 IsEnabled。
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public sealed class BooleanNegateConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}
