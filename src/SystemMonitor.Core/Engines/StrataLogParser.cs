using System.Globalization;
using System.Text.RegularExpressions;

namespace SystemMonitor.Core.Engines;

/// <summary>
/// 解析 Strata engine.log 的 serve 行，例：
/// strata serve: session 0: prompt 35 tokens = 0 reused + 35 read in 885 ms (39.5 tok/s), 700 generated in 25256 ms (27.7 tok/s), drafts accepted 0 of 0 (length)
/// </summary>
public static class StrataLogParser
{
    private static readonly Regex ServeLine = new(
        @"prompt (\d+) tokens = (\d+) reused \+ (\d+) read in ([\d.]+) ms \(([\d.]+) tok/s\), (\d+) generated in ([\d.]+) ms \(([\d.]+) tok/s\)",
        RegexOptions.Compiled);

    public static StrataServeMetrics? TryParseServeLine(string line)
    {
        var m = ServeLine.Match(line);
        if (!m.Success) return null;
        double D(int i) => double.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
        int I(int i) => int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
        return new StrataServeMetrics(I(1), I(2), I(3), D(4), D(5), I(6), D(7), D(8));
    }
}
