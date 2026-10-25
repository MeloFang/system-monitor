using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace AsusMonitor.Core.Sensors;

/// <summary>
/// 采集层唯一入口。只读（AGENTS.md 纪律 #3）。
/// 数据来源：LibreHardwareMonitor（CPU/GPU/内存）+ NVML（GPU 功耗）。
/// 任何单点失败降级为 null，不影响整体。
/// </summary>
public sealed class SensorService : IDisposable
{
    private readonly Computer _computer;
    private readonly string _memClock;
    private readonly string _memTotal;
    private IntPtr _nvmlDevice;
    private readonly bool _nvmlReady;

    public SensorService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = false,
            IsStorageEnabled = false,
            IsControllerEnabled = false,
            IsBatteryEnabled = false,
            IsNetworkEnabled = false,
        };
        _computer.Open();
        (_memTotal, _memClock) = ReadMemoryInfo();
        _nvmlReady = InitNvml();
    }

    /// <summary>采集一次快照。非线程安全，调用方保证串行。</summary>
    public SensorSnapshot Collect()
    {
        IHardware? cpu = null, gpuNvidia = null, gpuAmd = null, memory = null;
        foreach (IHardware hw in _computer.Hardware)
        {
            SafeUpdate(hw);
            switch (hw.HardwareType)
            {
                case HardwareType.Cpu: cpu = hw; break;
                case HardwareType.GpuNvidia: gpuNvidia = hw; break;
                case HardwareType.GpuAmd: gpuAmd = hw; break;
                case HardwareType.Memory: memory = hw; break;
            }
        }

        // 独显优先；独显休眠时回退到核显
        IHardware? gpu = gpuNvidia ?? gpuAmd;

        return new SensorSnapshot(
            CpuClock: CpuAverageClock(cpu),
            CpuLoad: Find(cpu, SensorType.Load, "CPU Total"),
            CpuTemp: Find(cpu, SensorType.Temperature, "Core (Tctl/Tdie)")
                     ?? FirstOfType(cpu, SensorType.Temperature),
            CpuVoltage: Find(cpu, SensorType.Voltage, "Core (SVI2 TFN)"),
            CpuName: cpu?.Name ?? Formatting.Missing,
            GpuClock: Find(gpu, SensorType.Clock, "GPU Core"),
            GpuMemClock: Find(gpu, SensorType.Clock, "GPU Memory"),
            GpuLoad: Find(gpu, SensorType.Load, "GPU Core"),
            GpuTemp: Find(gpu, SensorType.Temperature, "GPU Core"),
            GpuVoltage: FirstOfType(gpu, SensorType.Voltage),
            GpuPower: NvmlPowerWatts(),
            GpuName: gpu?.Name ?? Formatting.Missing,
            MemLoad: Find(memory, SensorType.Load, "Memory"),
            MemUsed: Find(memory, SensorType.Data, "Memory Used"),
            StorageLoad: StorageUsedPercent(),
            MemTotal: _memTotal,
            MemClock: _memClock
        );
    }

    private static void SafeUpdate(IHardware hw)
    {
        try { hw.Update(); } catch { /* 单硬件失败不阻塞 */ }
    }

    private static double? Find(IHardware? hw, SensorType type, string name)
    {
        if (hw is null) return null;
        foreach (ISensor s in hw.Sensors)
            if (s.SensorType == type && s.Name == name && s.Value is not null)
                return s.Value.Value;
        return null;
    }

    private static double? FirstOfType(IHardware? hw, SensorType type)
    {
        if (hw is null) return null;
        foreach (ISensor s in hw.Sensors)
            if (s.SensorType == type && s.Value is not null)
                return s.Value.Value;
        return null;
    }

    /// <summary>CPU 频率 = 各核 "Core #n" 时钟的均值（排除 Bus Speed）。</summary>
    private static double? CpuAverageClock(IHardware? cpu)
    {
        if (cpu is null) return null;
        double sum = 0;
        int count = 0;
        foreach (ISensor s in cpu.Sensors)
        {
            if (s.SensorType == SensorType.Clock && s.Name.StartsWith("Core #") && s.Value is not null)
            {
                sum += s.Value.Value;
                count++;
            }
        }
        return count > 0 ? sum / count : null;
    }

    private static double? StorageUsedPercent()
    {
        try
        {
            var drive = new DriveInfo("C");
            return (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100.0;
        }
        catch { return null; }
    }

    /// <summary>内存总量（物理条容量之和）与频率，一次 WMI 查询。</summary>
    private static (string Total, string Clock) ReadMemoryInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Capacity, ConfiguredClockSpeed FROM Win32_PhysicalMemory");
            long totalBytes = 0;
            int maxClock = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["Capacity"] is ulong cap) totalBytes += (long)cap;
                if (obj["ConfiguredClockSpeed"] is uint speed && speed > maxClock)
                    maxClock = (int)speed;
            }
            string total = totalBytes > 0 ? $"{totalBytes / 1024 / 1024 / 1024} GB" : Formatting.Missing;
            string clock = maxClock > 0 ? $"{maxClock} MHz" : Formatting.Missing;
            return (total, clock);
        }
        catch { return (Formatting.Missing, Formatting.Missing); }
    }

    // ---------- NVML ----------

    private bool InitNvml()
    {
        try
        {
            if (NvmlInit() != 0) return false;
            return NvmlGetHandle(0, out _nvmlDevice) == 0;
        }
        catch { return false; }
    }

    private double? NvmlPowerWatts()
    {
        if (!_nvmlReady) return null;
        try
        {
            return NvmlGetPower(_nvmlDevice, out uint mw) == 0 ? mw / 1000.0 : null;
        }
        catch { return null; }
    }

    public void Dispose()
    {
        try { _computer.Close(); } catch { }
        if (_nvmlReady) { try { NvmlShutdown(); } catch { } }
    }

    [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
    private static extern int NvmlInit();
    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
    private static extern int NvmlGetHandle(uint index, out IntPtr device);
    [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
    private static extern int NvmlGetPower(IntPtr device, out uint powerMilliwatts);
    [DllImport("nvml.dll", EntryPoint = "nvmlShutdown")]
    private static extern int NvmlShutdown();
}
