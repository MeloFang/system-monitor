namespace AsusMonitor.Core.Sensors;

/// <summary>显示格式化。所有方法对 null 返回 "--"，绝不抛异常。</summary>
public static class Formatting
{
    public const string Missing = "--";

    public static string Mhz(double? v) => v is null ? Missing : $"{v:0} MHz";
    public static string Percent(double? v) => v is null ? Missing : $"{v:0.0} %";
    public static string Celsius(double? v) => v is null ? Missing : $"{v:0.0} °C";
    public static string Volt(double? v) => v is null ? Missing : $"{v:0.000} V";
    public static string Watt(double? v) => v is null ? Missing : $"{v:0.0} W";
    public static string Gigabyte(double? v) => v is null ? Missing : $"{v:0.0} GB";
}
