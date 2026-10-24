using AsusMonitor.Core.Modes;
using AsusMonitor.Core.Sensors;
using Xunit;

namespace AsusMonitor.Tests;

public class FormattingTests
{
    [Fact]
    public void Null_Always_Returns_Missing_Placeholder()
    {
        Assert.Equal("--", Formatting.Mhz(null));
        Assert.Equal("--", Formatting.Percent(null));
        Assert.Equal("--", Formatting.Celsius(null));
        Assert.Equal("--", Formatting.Volt(null));
        Assert.Equal("--", Formatting.Watt(null));
        Assert.Equal("--", Formatting.Rpm(null));
    }

    [Fact]
    public void Formats_With_Units()
    {
        Assert.Equal("4143 MHz", Formatting.Mhz(4142.7656));
        Assert.Equal("55.7 %", Formatting.Percent(55.699));
        Assert.Equal("88.1 °C", Formatting.Celsius(88.125));
        Assert.Equal("1.550 V", Formatting.Volt(1.55));
        Assert.Equal("12.2 W", Formatting.Watt(12.18));
        Assert.Equal("3000 RPM", Formatting.Rpm(3000));
    }

    [Fact]
    public void Fan_Raw_Is_Rpm_Divided_By_100()
    {
        // G533QS 实测：原始值 30 对应 AC 显示的 ~3000 RPM
        Assert.Equal(3000, Formatting.FanRawToRpm(30));
        Assert.Null(Formatting.FanRawToRpm(null));
    }
}

public class PerformanceModeMapTests
{
    [Fact]
    public void Raw_Values_Map_To_Five_Modes()
    {
        Assert.Equal(PerformanceMode.Performance, PerformanceModeMap.FromRaw(0));
        Assert.Equal(PerformanceMode.Turbo, PerformanceModeMap.FromRaw(1));
        Assert.Equal(PerformanceMode.Silent, PerformanceModeMap.FromRaw(2));
        Assert.Equal(PerformanceMode.Turbo, PerformanceModeMap.FromRaw(3)); // FullSpeed 归入增强
        Assert.Equal(PerformanceMode.Manual, PerformanceModeMap.FromRaw(4));
        Assert.Null(PerformanceModeMap.FromRaw(99));
    }

    [Fact]
    public void Every_Mode_Has_A_Writable_Raw_Value()
    {
        foreach (var mode in PerformanceModeMap.All)
            Assert.InRange(PerformanceModeMap.ToRaw(mode), 0u, 4u);
    }

    [Fact]
    public void Windows_Mode_Writes_Balanced()
    {
        // Windows 档语义：写 Balanced(0)，交还 Windows 电源计划
        Assert.Equal(0u, PerformanceModeMap.ToRaw(PerformanceMode.Windows));
    }

    [Fact]
    public void Display_Names_Are_Chinese_Labels()
    {
        Assert.Equal("安静", PerformanceModeMap.DisplayName(PerformanceMode.Silent));
        Assert.Equal("性能", PerformanceModeMap.DisplayName(PerformanceMode.Performance));
        Assert.Equal("增强", PerformanceModeMap.DisplayName(PerformanceMode.Turbo));
        Assert.Equal("手动", PerformanceModeMap.DisplayName(PerformanceMode.Manual));
        Assert.Equal("Windows", PerformanceModeMap.DisplayName(PerformanceMode.Windows));
    }
}
