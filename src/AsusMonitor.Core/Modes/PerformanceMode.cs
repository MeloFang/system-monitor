namespace AsusMonitor.Core.Modes;

/// <summary>面板上的五个档位。Windows 档不是 ACPI 模式，语义见 ModeService。</summary>
public enum PerformanceMode
{
    Windows,
    Silent,
    Performance,
    Turbo,
    Manual,
}

public static class PerformanceModeMap
{
    // ACPI 原始值 ↔ 档位（常量与 G-Helper 一致：0=Balanced, 1=Turbo, 2=Silent, 3=FullSpeed, 4=Manual）
    public static PerformanceMode? FromRaw(int raw) => raw switch
    {
        0 => PerformanceMode.Performance,
        1 => PerformanceMode.Turbo,
        2 => PerformanceMode.Silent,
        3 => PerformanceMode.Turbo,   // FullSpeed 归入增强档显示
        4 => PerformanceMode.Manual,
        _ => null,
    };

    public static uint ToRaw(PerformanceMode mode) => mode switch
    {
        PerformanceMode.Windows => 0,     // Windows 档 = 写 Balanced，交还 Windows 电源计划
        PerformanceMode.Performance => 0,
        PerformanceMode.Turbo => 1,
        PerformanceMode.Silent => 2,
        PerformanceMode.Manual => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    public static string DisplayName(PerformanceMode mode) => mode switch
    {
        PerformanceMode.Windows => "Windows",
        PerformanceMode.Silent => "安静",
        PerformanceMode.Performance => "性能",
        PerformanceMode.Turbo => "增强",
        PerformanceMode.Manual => "手动",
        _ => "?",
    };

    public static readonly PerformanceMode[] All =
    [
        PerformanceMode.Windows,
        PerformanceMode.Silent,
        PerformanceMode.Performance,
        PerformanceMode.Turbo,
        PerformanceMode.Manual,
    ];
}
