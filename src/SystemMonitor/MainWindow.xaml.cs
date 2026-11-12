using System.Drawing;
using System.Globalization;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using SystemMonitor.Core.Diagnostics;
using SystemMonitor.Core.Engines;
using SystemMonitor.Core.Sensors;
using SystemMonitor.Services;
using Microsoft.Win32;

namespace SystemMonitor;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer _toastTimer = new();
    private NotifyIcon? _tray;
    private bool _closing;
    private bool _initErrorShown;
    private int _uiLogCount;
    private Action? _undo;
    private string? _lastEngineError, _lastEngineWarning;
    private int _collecting;
    private bool _hwPulse, _engPulse;

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "system-monitor";

    public MainWindow()
    {
        InitializeComponent();

        Hub.Sensors = new SensorService();
        Hub.Metrics = new MetricsService();
        Hub.Engines = new EngineControlService(
            System.IO.Path.Combine(AppLog.LogDirectory, "engine-state.json"),
            () => Hub.Latest.GpuMemUsed);
        Hub.Engines.Changed += () => Dispatcher.BeginInvoke(OnEnginesChanged);
        Hub.Toast = ShowToast;
        Hub.Navigate = Show;

        _timer.Tick += async (_, _) => await RefreshAsync();
        _toastTimer.Tick += (_, _) => ToastBox.Visibility = Visibility.Collapsed;

        Loaded += async (_, _) =>
        {
            Show("home");
            await RefreshAsync();
            _timer.Start();
            if (!_initErrorShown && !Hub.Sensors.HardwareReady)
            {
                _initErrorShown = true;
                ShowInitError();
            }
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            Hub.Sensors.Dispose();
            Hub.Engines.Dispose();
            if (_tray is not null) { _tray.Visible = false; _tray.Dispose(); }
        };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized && _tray is not null)
            {
                Hide();
                _tray.ShowBalloonTip(1500, AppName, "已最小化到托盘，双击图标恢复", ToolTipIcon.None);
            }
        };
        SizeChanged += (_, _) =>
        {
            // 窄窗口隐藏时钟，保证顶栏不溢出（原型 ≤640 同口径）
            ClockText.Visibility = ActualWidth < 760 ? Visibility.Collapsed : Visibility.Visible;
            // 悬浮窗宽度下收起状态徽标（状态在视图内仍有展示）
            var chips = ActualWidth < 560 ? Visibility.Collapsed : Visibility.Visible;
            SensorChip.Visibility = chips;
            EngineBadge.Visibility = chips;
        };

        InitTray();
    }

    // ---------- 导航 ----------

    public void Show(string view)
    {
        NavHost.Content = view switch
        {
            "monitor" => new Views.MonitorView(),
            "engine" => new Views.EngineView(),
            _ => new Views.HomePage(),
        };
        BackBtn.Visibility = view == "home" ? Visibility.Collapsed : Visibility.Visible;
        Crumb.Text = view switch { "monitor" => "/ 硬件监控", "engine" => "/ 模型引擎", _ => "" };
    }

    private void Back_Click(object sender, RoutedEventArgs e) => Show("home");

    // ---------- 采集 ----------

    private async Task RefreshAsync()
    {
        // LHM/NVML 非线程安全：Collect 超 1s 时禁止 Timer 重入并发采集（曾致 native AV 崩溃）
        if (Interlocked.CompareExchange(ref _collecting, 1, 0) != 0) return;
        try
        {
            SensorSnapshot snap = await Task.Run(() => Hub.Sensors.Collect());
            Hub.Latest = snap;
            Hub.Metrics.Tick();
            if (Hub.Engines.LastOllamaDone is { } d)
            {
                Hub.Metrics.OllamaTok = d.Tok;
                Hub.Metrics.OllamaPromptTok = d.PromptTok;
            }
            Hub.Hist.Push(snap, Hub.Engines, Hub.Metrics);
            Hub.RaiseTick();

            ClockText.Text = DateTime.Now.ToString("HH:mm:ss");
            bool hw = Hub.Sensors.HardwareReady;
            SensorChipText.Text = hw ? "采集中" : "数据不可用";
            var chipColor = (Brush)FindResource(hw ? "Ok" : "Warn");
            SensorChipText.Foreground = chipColor;
            SensorPd.Fill = chipColor;
            if (hw && !_hwPulse) { UiAnim.Pulse(SensorPd, (DropShadowEffect)FindResource("GlowOk")); _hwPulse = true; }
            if (!hw && _hwPulse) { UiAnim.StopPulse(SensorPd); _hwPulse = false; }

            var act = Hub.Engines.Active;
            EngineBadgeText.Text = act is null ? "无" : EngineInfo.DisplayName(act.Value);
            EngineBadgeText.Foreground = (Brush)FindResource(act switch
            {
                EngineKind.Ollama => "Ollama",
                EngineKind.Strata => "Strata",
                _ => "Text",
            });
            if (act is not null && !_engPulse) { UiAnim.Pulse(EngineDot, (DropShadowEffect)FindResource("GlowOk")); _engPulse = true; }
            if (act is null)
            {
                if (_engPulse) { UiAnim.StopPulse(EngineDot); _engPulse = false; }
                EngineDot.Fill = (Brush)FindResource("Faint");
            }
            else EngineDot.Fill = (Brush)FindResource("Ok");

            if (++_uiLogCount % 30 == 1)
                AppLog.Info(string.Format(CultureInfo.InvariantCulture,
                    "UI输出: GpuTemp={0} GpuMemUsed={1} active={2}",
                    snap.GpuTemp, snap.GpuMemUsed, act));
        }
        catch (Exception ex)
        {
            AppLog.Error("RefreshAsync 失败", ex);
        }
        finally
        {
            Interlocked.Exchange(ref _collecting, 0);
        }
    }

    private void OnEnginesChanged()
    {
        Hub.RaiseTick();
        if (Hub.Engines.LastError is { } err && err != _lastEngineError)
        {
            _lastEngineError = err;
            ShowToast(err, "warn", null);
        }
        if (Hub.Engines.LastWarning is { } warn && warn != _lastEngineWarning)
        {
            _lastEngineWarning = warn;
            ShowToast(warn, "warn", null);
        }
    }

    // ---------- toast ----------

    private void ShowToast(string msg, string kind, Action? undo)
    {
        ToastText.Text = msg;
        ToastDot.Fill = (Brush)FindResource(kind switch { "ok" => "Ok", "warn" => "Warn", _ => "Info" });
        _undo = undo;
        ToastUndo.Visibility = undo is null ? Visibility.Collapsed : Visibility.Visible;
        ToastBox.Visibility = Visibility.Visible;
        _toastTimer.Interval = TimeSpan.FromSeconds(undo is null ? 3.2 : 6);
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ToastUndo_Click(object sender, RoutedEventArgs e)
    {
        ToastBox.Visibility = Visibility.Collapsed;
        var u = _undo;
        _undo = null;
        u?.Invoke();
    }

    // ---------- 托盘与开机自启 ----------

    private void InitTray()
    {
        _tray = new NotifyIcon { Visible = true, Text = AppName, Icon = LoadAppIcon() };

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示面板", null, (_, _) => RestoreFromTray());
        var topItem = new ToolStripMenuItem("悬浮置顶") { Checked = Topmost };
        topItem.Click += (_, _) =>
        {
            Topmost = !Topmost;
            topItem.Checked = Topmost;
        };
        menu.Items.Add(topItem);
        var autoItem = new ToolStripMenuItem("开机自启") { Checked = IsAutoStartEnabled() };
        autoItem.Click += (_, _) =>
        {
            autoItem.Checked = !autoItem.Checked;
            SetAutoStart(autoItem.Checked);
        };
        menu.Items.Add(autoItem);

        var traceItem = new ToolStripMenuItem("记录读数到 CSV（压力测试用）") { Checked = AppLog.IsTracing };
        traceItem.Click += (_, _) => ToggleTrace(traceItem);
        menu.Items.Add(traceItem);

        menu.Items.Add("导出诊断报告", null, (_, _) => ExportDiagnostics());
        menu.Items.Add("打开日志目录", null, (_, _) => OpenLogDir());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => { _closing = true; Close(); });
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    // ---------- 诊断与异常收集 ----------

    private void ShowInitError()
    {
        try
        {
            System.Windows.MessageBox.Show(
                "传感器驱动初始化失败，温度/频率等可能显示为 --。\n\n" + (Hub.Sensors.InitError ?? "") +
                "\n\n常见解决：关闭 Windows「内核隔离/内存完整性」后重启，或将本程序加入杀毒软件(360/火鸦/Defender)白名单。\n\n日志目录：\n" + AppLog.LogDirectory +
                "\n\n可用托盘菜单「导出诊断报告」把报告发回给开发者。",
                "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { }
    }

    private void ExportDiagnostics()
    {
        var path = AppLog.WriteDiagnosticReport(Hub.Sensors.DescribeStatus());
        if (path is null)
        {
            System.Windows.MessageBox.Show("导出诊断报告失败。", "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        var r = System.Windows.MessageBox.Show(
            $"诊断报告已生成：\n{path}\n\n是否打开所在文件夹？",
            "SystemMonitor", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (r == MessageBoxResult.Yes) OpenFolder(path);
    }

    private static void ToggleTrace(ToolStripMenuItem item)
    {
        if (AppLog.IsTracing)
        {
            string? file = AppLog.StopTrace();
            item.Checked = false;
            System.Windows.MessageBox.Show("已停止记录读数。\n\nCSV 文件：\n" + file + "\n\n可用 Excel 打开查看整条曲线。",
                "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Information);
            if (file is not null) OpenFolder(file);
        }
        else
        {
            string? file = AppLog.StartTrace();
            item.Checked = file is not null;
            if (file is null)
            {
                System.Windows.MessageBox.Show("无法创建 CSV 文件，详见日志。", "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            System.Windows.MessageBox.Show("开始记录读数（每秒一行）。\n\nCSV 文件：\n" + file + "\n\n现在去跑 3DMark，跑完再点一次本菜单停止。",
                "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OpenLogDir()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppLog.LogDirectory) { UseShellExecute = true });
        }
        catch { }
    }

    private static void OpenFolder(string filePath)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"") { UseShellExecute = true });
        }
        catch { }
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            using var stream = typeof(MainWindow).Assembly.GetManifestResourceStream("SystemMonitor.app.ico");
            if (stream is not null) return new Icon(stream);
        }
        catch { }
        return SystemIcons.Application;
    }

    private static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) is not null;
        }
        catch { return false; }
    }

    private static void SetAutoStart(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (enabled)
                key.SetValue(AppName, Environment.ProcessPath ?? "");
            else
                key.DeleteValue(AppName, false);
        }
        catch { }
    }

    // ---------- 窗口控制 ----------

    private void TitleBar_Drag(object sender, System.Windows.Input.MouseButtonEventArgs e) => DragMove();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        if (_tray is not null && !_closing)
        {
            Hide();
            _tray.ShowBalloonTip(1500, AppName, "仍在后台运行，双击托盘图标恢复", ToolTipIcon.None);
            return;
        }
        _closing = true;
        Close();
    }
}
