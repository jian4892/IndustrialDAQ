// File: TreeArrowConverter.cs  Module: UI (Converters)  Author: IndustrialDAQ Team
using System.Globalization;
using System.Windows.Data;

namespace IndustrialDAQ.UI.Converters;

/// <summary>
/// TreeView 展开/折叠箭头转换器 — 展开时显示 ▼，折叠时显示 ▶。
/// </summary>
[ValueConversion(typeof(bool), typeof(string))]
public sealed class TreeArrowConverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "▼" : "▶";
    }

    /// <inheritdoc />
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string s && s == "▼";
    }
}
