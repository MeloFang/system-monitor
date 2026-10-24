namespace AsusMonitor.Core.Sensors;

/// <summary>
/// 一次采集周期的完整快照。所有字段为已格式化的显示字符串，
/// 读不到的数据一律为 "--"（降级约定，见 AGENTS.md）。
/// </summary>
public record SensorSnapshot(
    // CPU
    string CpuClock,    // 频率，如 "4143 MHz"
    string CpuLoad,     // 使用率，如 "55.7 %"
    string CpuTemp,     // 温度，如 "88.1 °C"
    string CpuVoltage,  // 电压，如 "1.55 V"
    // GPU（独显优先）
    string GpuClock,
    string GpuMemClock,
    string GpuLoad,
    string GpuTemp,
    string GpuVoltage,
    string GpuPower,    // TGP，如 "12.2 W"（NVML）
    // 风扇（华硕 WMI，单位 RPM）
    string CpuFan,
    string GpuFan,
    // 内存
    string MemLoad,     // RAM 使用率
    string StorageLoad, // C 盘占用率
    string MemClock     // 内存频率（静态，启动时读取一次）
);
