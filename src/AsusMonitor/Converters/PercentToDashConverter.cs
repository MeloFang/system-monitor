using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace AsusMonitor.Converters;

/// <summary>
/// 百分比(0-100) → 圆环虚线长度，用于把 Ellipse 画成进度环。
/// ConverterParameter = 圆周长（描边中心线）。
/// </summary>
public sealed class PercentToDashConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture)
    {
        double pct = value is double d ? Math.Clamp(d, 0, 100) : 0;
        double circumference = parameter is string s
            ? double.Parse(s, CultureInfo.InvariantCulture)
            : 352;
        return new DoubleCollection { circumference * pct / 100.0, circumference };
    }

    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
