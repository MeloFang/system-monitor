using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AsusMonitor.Core.Modes;
using AsusMonitor.Core.Sensors;

namespace AsusMonitor;

public partial class MainWindow : Window
{
    private static readonly Brush BrushIdleBg = new SolidColorBrush(Color.FromRgb(0x1A, 0x1D, 0x21));
    private static readonly Brush BrushIdleBorder = new SolidColorBrush(Color.FromRgb(0x26, 0x2B, 0x31));
    private static readonly Brush BrushActiveBg = new SolidColorBrush(Color.FromRgb(0xE5, 0x35, 0x3F)); // ROG 红
    private static readonly Brush BrushActiveBorder = new SolidColorBrush(Color.FromRgb(0xE5, 0x35, 0x3F));

    private readonly MainViewModel _vm = new();
    private readonly SensorService _sensors = new();
    private readonly ModeService _modes = new();
    private readonly DispatcherTimer _timer;

    // Windows 档不是 ACPI 模式：点击后记为会话状态，重启后按 ACPI 实际值高亮
    private PerformanceMode? _currentMode;
    private bool _namesBound;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

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
        // 采集前先与 Armoury Crate 的关系说清楚
        string acNote = ModeService.IsArmouryCrateRunning()
            ? "Armoury Crate 运行中，切换模式可能被它重置"
            : "未检测到 Armoury Crate 服务";
        Subtitle.Text = acNote;

        if (!_modes.IsAcpiAvailable)
            StatusText.Text = "未检测到华硕系统控制接口（ATKACPI），模式切换不可用";

        await RefreshAsync();
        _currentMode = _modes.GetCurrent();
        HighlightMode(_currentMode);
        _timer.Start();
    }

    private async Task RefreshAsync()
    {
        SensorSnapshot snap = await Task.Run(() => _sensors.Collect());

        if (!_namesBound)
        {
            CpuName.Text = snap.CpuName;
            GpuName.Text = snap.GpuName;
            _namesBound = true;
        }

        _vm.Apply(snap, DateTime.Now);
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
            StatusText.Text = "模式切换失败（ACPI 接口不可用？）";
            return;
        }

        // Windows 档：ACPI 读回的是 Balanced，但 UI 高亮 Windows（会话状态）
        _currentMode = mode == PerformanceMode.Windows ? PerformanceMode.Windows : readBack;
        HighlightMode(_currentMode);
        StatusText.Text = $"已切换到 {PerformanceModeMap.DisplayName(mode)}";
    }

    private void HighlightMode(PerformanceMode? active)
    {
        foreach (var (btn, mode) in ModeButtons())
        {
            bool on = mode == active;
            btn.Background = on ? BrushActiveBg : BrushIdleBg;
            btn.BorderBrush = on ? BrushActiveBorder : BrushIdleBorder;
            btn.FontWeight = on ? FontWeights.SemiBold : FontWeights.Normal;
        }
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

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
