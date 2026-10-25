namespace SystemMonitor.Core.Sensors;

/// <summary>
/// 一次采集周期的完整快照。单位固定：MHz / % / °C / V / W / GB。
/// 任何一项读不到即为 null（降级约定见 AGENTS.md），界面显示 "--"。
/// </summary>
public record SensorSnapshot(
    // CPU
    double? CpuClock,
    double? CpuLoad,
    double? CpuTemp,
    double? CpuVoltage,
    string CpuName,
    // GPU（独显优先）
    double? GpuClock,
    double? GpuMemClock,
    double? GpuLoad,
    double? GpuTemp,
    double? GpuVoltage,
    double? GpuPower,   // TGP，NVML
    string GpuName,
    // 内存
    double? MemLoad,
    double? MemUsed,
    double? StorageLoad,
    // 内存总量与频率为静态值，启动时读一次（总量取物理内存条容量之和，如 "32 GB"）
    string MemTotal,
    string MemClock
);
