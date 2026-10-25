using System.ComponentModel;
using System.Runtime.CompilerServices;
using SystemMonitor.Core.Sensors;

namespace SystemMonitor;

/// <summary>界面数据。同时提供格式化文本与原始数值（数值供进度条使用）。</summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(CpuClockText), nameof(CpuLoadText), nameof(CpuLoadValue),
                     nameof(CpuTempText), nameof(CpuVoltageText), nameof(CpuTempValue),
                     nameof(GpuClockText), nameof(GpuMemClockText), nameof(GpuLoadText),
                     nameof(GpuLoadValue), nameof(GpuTempText), nameof(GpuVoltageText),
                     nameof(GpuPowerText), nameof(GpuTempValue),
                     nameof(MemLoadText), nameof(MemLoadValue), nameof(MemUsedText),
                     nameof(MemTotalText), nameof(StorageLoadText), nameof(StorageLoadValue),
                     nameof(MemClockText), nameof(UpdateTime),
                 })
            Raise(name);
    }

    private double? _cpuClock, _cpuLoad, _cpuTemp, _cpuVoltage;
    private double? _gpuClock, _gpuMemClock, _gpuLoad, _gpuTemp, _gpuVoltage, _gpuPower;
    private double? _memLoad, _memUsed, _storageLoad;

    public string CpuClockText => Formatting.Mhz(_cpuClock);
    public string CpuLoadText => Formatting.Percent(_cpuLoad);
    public double CpuLoadValue => _cpuLoad ?? 0;
    public string CpuTempText => Formatting.Celsius(_cpuTemp);
    public double CpuTempValue => _cpuTemp ?? 0;
    public string CpuVoltageText => Formatting.Volt(_cpuVoltage);

    // 空闲休眠时驱动不报告核心频率（NVML 返 1 / LHM 偶发极小值，已在数据层过滤为 null）。
    // 负载 <5% 视为空闲（界面显示 0.0% 的实际值可能是 0.04%，不能按 ==0 判断）
    public string GpuClockText => _gpuClock is not null
        ? Formatting.Mhz(_gpuClock)
        : ((_gpuLoad ?? 0) < 5 ? "待机" : Formatting.Missing);
    public string GpuMemClockText => _gpuMemClock is not null
        ? Formatting.Mhz(_gpuMemClock)
        : ((_gpuLoad ?? 0) < 5 ? "待机" : Formatting.Missing);
    public string GpuLoadText => Formatting.Percent(_gpuLoad);
    public double GpuLoadValue => _gpuLoad ?? 0;
    public string GpuTempText => Formatting.Celsius(_gpuTemp);
    public double GpuTempValue => _gpuTemp ?? 0;
    public string GpuVoltageText => Formatting.Volt(_gpuVoltage);
    public string GpuPowerText => Formatting.Watt(_gpuPower);

    public string MemLoadText => Formatting.Percent(_memLoad);
    public double MemLoadValue => _memLoad ?? 0;
    public string MemUsedText => Formatting.Gigabyte(_memUsed);
    public string MemTotalText { get; private set; } = Formatting.Missing;
    public string StorageLoadText => Formatting.Percent(_storageLoad);
    public double StorageLoadValue => _storageLoad ?? 0;
    public string MemClockText { get; private set; } = Formatting.Missing;
    public string UpdateTime { get; private set; } = Formatting.Missing;

    public void Apply(SensorSnapshot snap, DateTime stamp)
    {
        _cpuClock = snap.CpuClock;
        _cpuLoad = snap.CpuLoad;
        _cpuTemp = snap.CpuTemp;
        _cpuVoltage = snap.CpuVoltage;

        _gpuClock = snap.GpuClock;
        _gpuMemClock = snap.GpuMemClock;
        _gpuLoad = snap.GpuLoad;
        _gpuTemp = snap.GpuTemp;
        _gpuVoltage = snap.GpuVoltage;
        _gpuPower = snap.GpuPower;

        _memLoad = snap.MemLoad;
        _memUsed = snap.MemUsed;
        _storageLoad = snap.StorageLoad;

        MemTotalText = snap.MemTotal;
        MemClockText = snap.MemClock;
        UpdateTime = stamp.ToString("HH:mm:ss");
        RaiseAll();
    }
}
