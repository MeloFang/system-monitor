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
    public static string Rpm(int? v) => v is null ? Missing : $"{v} RPM";

    /// <summary>华硕 WMI 风扇原始值转 RPM：本机（G533QS）实测原始值为 RPM/100。</summary>
    public static int? FanRawToRpm(int? raw) => raw is null ? null : raw * 100;
}
