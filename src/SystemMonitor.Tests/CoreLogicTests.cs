using SystemMonitor.Core.Sensors;
using Xunit;

namespace SystemMonitor.Tests;

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
        Assert.Equal("--", Formatting.Gigabyte(null));
    }

    [Fact]
    public void Formats_With_Units()
    {
        Assert.Equal("4143 MHz", Formatting.Mhz(4142.7656));
        Assert.Equal("55.7 %", Formatting.Percent(55.699));
        Assert.Equal("88.1 °C", Formatting.Celsius(88.125));
        Assert.Equal("1.550 V", Formatting.Volt(1.55));
        Assert.Equal("12.2 W", Formatting.Watt(12.18));
        Assert.Equal("18.4 GB", Formatting.Gigabyte(18.44));
    }
}
