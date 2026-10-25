using System.Management;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;
using SystemMonitor.Core.Diagnostics;

namespace SystemMonitor.Core.Sensors;

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
    private int _collectFailCount;
    private int _updateFailLogged;

    /// <summary>硬件监控(LHM)是否初始化成功。</summary>
    public bool HardwareReady { get; private set; }

    /// <summary>初始化失败的原因（成功为 null），供界面提示与诊断。</summary>
    public string? InitError { get; private set; }

    /// <summary>NVML(GPU 功耗)是否就绪。</summary>
    public bool NvmlReady => _nvmlReady;

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
        try
        {
            _computer.Open();
            HardwareReady = true;
            AppLog.Info("硬件监控(LibreHardwareMonitor)初始化成功");
        }
        catch (Exception ex)
        {
            HardwareReady = false;
            InitError = "硬件监控驱动初始化失败，温度/频率等可能读不到。常见原因：Windows「内核隔离/内存完整性」拦截了 WinRing0 驱动，或杀毒软件(360/火鸦/Defender)拦截。";
            AppLog.Error("Computer.Open() 失败：WinRing0 驱动无法加载（多为内核隔离/内存完整性或杀毒软件拦截）", ex);
        }
        (_memTotal, _memClock) = ReadMemoryInfo();
        _nvmlReady = InitNvml();
        AppLog.Info($"初始化完成：内存={_memTotal} {_memClock}；NVML(GPU功耗)={(_nvmlReady ? "就绪" : "不可用")}");
    }

    /// <summary>采集一次快照。任何失败都降级为空快照并记录日志，绝不抛异常。</summary>
    public SensorSnapshot Collect()
    {
        try
        {
            return CollectCore();
        }
        catch (Exception ex)
        {
            if (_collectFailCount++ == 0)
                AppLog.Error("Collect() 采集失败（已降级为空快照；持续失败只记第一次）", ex);
            return SensorSnapshot.Empty;
        }
    }

    /// <summary>非线程安全，调用方保证串行。</summary>
    private SensorSnapshot CollectCore()
    {
        // 如果硬件监控没初始化成功，直接返回空快照（避免访问 _computer.Hardware 崩溃）
        if (!HardwareReady)
        {
            return SensorSnapshot.Empty;
        }

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

    private void SafeUpdate(IHardware hw)
    {
        try { hw.Update(); }
        catch (Exception ex)
        {
            if (_updateFailLogged++ == 0)
                AppLog.Warn($"硬件 Update 失败（{hw.Name}，只记第一次）", ex);
        }

        // 诊断日志：打印 GPU 的传感器名称（仅第一次采集）
        if (hw.HardwareType == HardwareType.GpuNvidia && _updateFailLogged == 0)
        {
            var sb = new System.Text.StringBuilder();
            int sensorCount = hw.Sensors.Count();
            sb.Append("GPU 传感器列表：");
            sb.Append(sensorCount);
            sb.AppendLine(" 个");
            foreach (var s in hw.Sensors)
            {
                if (s.SensorType == SensorType.Clock)
                {
                    sb.Append("  Clock: ");
                    sb.Append(s.Name);
                    sb.Append(" = ");
                    sb.Append(s.Value);
                    sb.AppendLine(" MHz");
                }
            }
            AppLog.Info(sb.ToString().TrimEnd());
            _updateFailLogged = 1; // 只打一次
        }
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

    /// <summary>传感器/驱动状态摘要，供诊断报告与界面提示。</summary>
    public string DescribeStatus()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("硬件监控(LHM)初始化: ").AppendLine(HardwareReady ? "成功" : "失败");
        if (!string.IsNullOrEmpty(InitError)) sb.Append("  原因: ").AppendLine(InitError);
        sb.Append("NVML(GPU功耗): ").AppendLine(_nvmlReady ? "就绪" : "不可用(可能无NVIDIA独显)");
        sb.Append("内存: ").Append(_memTotal).Append(" · ").AppendLine(_memClock);
        return sb.ToString();
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
