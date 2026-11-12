namespace SystemMonitor.Core.Engines;

/// <summary>切换引擎的「显存已释放」判据：设备级 used 回落到 基线 + 500MB 以内。</summary>
public static class VramGate
{
    public const double HeadroomGb = 0.5;

    public static bool Released(double? usedGb, double baselineGb)
        => usedGb is not null && usedGb <= baselineGb + HeadroomGb;
}
