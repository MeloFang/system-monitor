using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using AsusMonitor.Core.Modes;
using AsusMonitor.Core.Sensors;

namespace AsusMonitor;

public partial class MainWindow : Window
{
    private static readonly Brush BrushIdle = new SolidColorBrush(Color.FromRgb(0x2A, 0x2D, 0x31));
    private static readonly Brush BrushActive = new SolidColorBrush(Color.FromRgb(0xD3, 0x31, 0x3D)); // AC 红

    private readonly SensorService _sensors = new();
    private readonly ModeService _modes = new();
    private readonly DispatcherTimer _timer;

    // Windows 档不是 ACPI 模式：点击后记为会话状态，重启后按 ACPI 实际值高亮
    private PerformanceMode? _currentMode;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closed += (_, _) =>
        {
            _timer.Stop();
            _sensors.Dispose();
            _modes.Dispose();
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ModeService.IsArmouryCrateRunning())
            AcWarning.Visibility = Visibility.Visible;

        if (!_modes.IsAcpiAvailable)
            StatusBar.Text = "未检测到华硕系统控制接口（ATKACPI），模式切换不可用";

        await RefreshAsync();
        _currentMode = _modes.GetCurrent();
        HighlightMode(_currentMode);
        _timer.Start();
    }

    private async Task RefreshAsync()
    {
        SensorSnapshot snap = await Task.Run(() => _sensors.Collect());

        CpuClock.Text = snap.CpuClock;
        CpuLoad.Text = snap.CpuLoad;
        CpuTemp.Text = snap.CpuTemp;
        CpuVolt.Text = snap.CpuVoltage;

        GpuClock.Text = snap.GpuClock;
        GpuMemClock.Text = snap.GpuMemClock;
        GpuLoad.Text = snap.GpuLoad;
        GpuTemp.Text = snap.GpuTemp;
        GpuVolt.Text = snap.GpuVoltage;
        GpuPower.Text = snap.GpuPower;

        CpuFan.Text = snap.CpuFan;
        GpuFan.Text = snap.GpuFan;

        MemLoad.Text = snap.MemLoad;
        StorageLoad.Text = snap.StorageLoad;
        MemClock.Text = snap.MemClock;

        StatusBar.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
    }

    private async void ModeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        if (!Enum.TryParse(tag, out PerformanceMode mode)) return;

        if (ModeService.IsArmouryCrateRunning())
        {
            var result = MessageBox.Show(
                this,
                "Armoury Crate 正在运行，它的后台服务可能会把模式重置回去。\n建议停用 Armoury Crate 服务后再切换。\n\n仍要继续吗？",
                "asus-monitor",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;
        }

        SetButtonsEnabled(false);
        PerformanceMode? readBack = await Task.Run(() => _modes.Set(mode));
        SetButtonsEnabled(true);

        if (readBack is null)
        {
            StatusBar.Text = "模式切换失败（ACPI 接口不可用？）";
            return;
        }

        // Windows 档：ACPI 读回的是 Balanced，但 UI 高亮 Windows（会话状态）
        _currentMode = mode == PerformanceMode.Windows ? PerformanceMode.Windows : readBack;
        HighlightMode(_currentMode);
        StatusBar.Text = $"已切换到 {PerformanceModeMap.DisplayName(mode)}";
    }

    private void HighlightMode(PerformanceMode? active)
    {
        foreach (var (btn, mode) in ModeButtons())
            btn.Background = mode == active ? BrushActive : BrushIdle;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        foreach (var (btn, _) in ModeButtons())
            btn.IsEnabled = enabled;
    }

    private IEnumerable<(Button Btn, PerformanceMode Mode)> ModeButtons()
    {
        yield return (BtnWindows, PerformanceMode.Windows);
        yield return (BtnSilent, PerformanceMode.Silent);
        yield return (BtnPerformance, PerformanceMode.Performance);
        yield return (BtnTurbo, PerformanceMode.Turbo);
        yield return (BtnManual, PerformanceMode.Manual);
    }
}
