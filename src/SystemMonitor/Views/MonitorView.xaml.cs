using System.Windows.Controls;
using System.Windows.Media;
using SystemMonitor.Core.Sensors;

namespace SystemMonitor.Views;

public partial class MonitorView : UserControl
{
    private bool _namesBound;
    private bool _pulse;

    public MonitorView()
    {
        InitializeComponent();
        Loaded += (_, _) => { Hub.Tick += Refresh; Refresh(); UiAnim.Enter(this); };
        Unloaded += (_, _) => Hub.Tick -= Refresh;
    }

    private void Refresh()
    {
        var s = Hub.Latest;
        if (!_namesBound)
        {
            CpuName.Text = s.CpuName;
            GpuName.Text = s.GpuName;
            MemName.Text = $"{s.MemTotal} · {s.MemClock}";
            _namesBound = true;
        }

        CpuRing.Value = s.CpuTemp ?? 0;
        CpuTempNum.Text = s.CpuTemp is null ? "--" : $"{s.CpuTemp:0}";
        CpuLoadText.Text = Formatting.Percent(s.CpuLoad);
        CpuLoadBar.Value = s.CpuLoad ?? 0;
        CpuClockText.Text = Formatting.Mhz(s.CpuClock);
        CpuVoltText.Text = Formatting.Volt(s.CpuVoltage);

        GpuRing.Value = s.GpuTemp ?? 0;
        GpuTempNum.Text = s.GpuTemp is null ? "--" : $"{s.GpuTemp:0}";
        GpuLoadText.Text = Formatting.Percent(s.GpuLoad);
        GpuLoadBar.Value = s.GpuLoad ?? 0;
        GpuClockText.Text = $"{FmtClock(s.GpuClock, s.GpuLoad)}/{FmtClock(s.GpuMemClock, s.GpuLoad)}";
        GpuPowerText.Text = Formatting.Watt(s.GpuPower);
        GpuVramText.Text = s.GpuMemUsed is null ? "--" : $"{s.GpuMemUsed:0.0}/{s.GpuMemTotal:0} GB";
        GpuVramBar.Value = s.GpuMemUsed is null || s.GpuMemTotal is null or 0 ? 0 : s.GpuMemUsed.Value / s.GpuMemTotal.Value * 100;

        MemRing.Value = s.MemLoad ?? 0;
        MemLoadNum.Text = s.MemLoad is null ? "--" : $"{s.MemLoad:0}";
        MemUsedText.Text = $"{Formatting.Gigabyte(s.MemUsed)} / {s.MemTotal}";
        MemLoadBar.Value = s.MemLoad ?? 0;
        MemClockText.Text = s.MemClock;
        double totalGb = 0;
        double.TryParse(s.MemTotal.Split(' ')[0], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out totalGb);
        var free = totalGb > 0 && s.MemUsed is not null ? totalGb - s.MemUsed.Value : (double?)null;
        MemFreeText.Text = free is null ? "--" : $"{free:0.0} GB";
        MemFreeBar.Value = free is null || totalGb == 0 ? 0 : free.Value / totalGb * 100;

        bool hw = Hub.Sensors.HardwareReady;
        MonChip.Text = hw ? "实时采集" : "数据不可用";
        var cc = (Brush)FindResource(hw ? "Ok" : "Warn");
        MonChip.Foreground = cc;
        MonPd.Fill = cc;
        if (hw && !_pulse) { UiAnim.Pulse(MonPd, (DropShadowEffect)FindResource("GlowOk")); _pulse = true; }
        if (!hw && _pulse) { UiAnim.StopPulse(MonPd); _pulse = false; }
        StatusText.Text = hw ? "采集中 · 1s/次 · 显存为 NVML 设备级读数" : "⚠ 传感器初始化失败 — 托盘「导出诊断报告」查看原因";
    }

    private static string FmtClock(double? v, double? load)
        => v is not null ? $"{v:0}" : (load ?? 0) < 5 ? "待机" : "--";
}
