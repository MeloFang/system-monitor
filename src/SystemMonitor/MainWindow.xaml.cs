using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using SystemMonitor.Core.Diagnostics;
using SystemMonitor.Core.Sensors;
using Microsoft.Win32;

namespace SystemMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly SensorService _sensors = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private NotifyIcon? _tray;
    private bool _namesBound;
    private bool _closing;
    private bool _initErrorShown;

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "system-monitor";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            await RefreshAsync();
            _timer.Start();
            if (!_initErrorShown && !_sensors.HardwareReady)
            {
                _initErrorShown = true;
                ShowInitError();
            }
        };
        Closed += (_, _) =>
        {
            _timer.Stop();
            _sensors.Dispose();
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

        InitTray();
    }

    // ---------- 托盘与开机自启 ----------

    private void InitTray()
    {
        _tray = new NotifyIcon { Visible = true, Text = AppName, Icon = LoadAppIcon() };

        var menu = new ContextMenuStrip();
        menu.Items.Add("显示面板", null, (_, _) => RestoreFromTray());
        var autoItem = new ToolStripMenuItem("开机自启") { Checked = IsAutoStartEnabled() };
        autoItem.Click += (_, _) =>
        {
            autoItem.Checked = !autoItem.Checked;
            SetAutoStart(autoItem.Checked);
        };
        menu.Items.Add(autoItem);

        // 读数追踪开关：跑 3DMark / 压力测试前点一下开始，跑完再点一下停止
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
                "传感器驱动初始化失败，温度/频率等可能显示为 --。\n\n" + (_sensors.InitError ?? "") +
                "\n\n常见解决：关闭 Windows「内核隔离/内存完整性」后重启，或将本程序加入杀毒软件(360/火鸦/Defender)白名单。\n\n日志目录：\n" + AppLog.LogDirectory +
                "\n\n可用托盘菜单「导出诊断报告」把报告发回给开发者。",
                "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch { }
    }

    private void ExportDiagnostics()
    {
        var path = AppLog.WriteDiagnosticReport(_sensors.DescribeStatus());
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

    /// <summary>开关读数追踪：开始/停止写 CSV（每次采集一行）。</summary>
    private static void ToggleTrace(ToolStripMenuItem item)
    {
        if (AppLog.IsTracing)
        {
            string? file = AppLog.StopTrace();
            item.Checked = false;
            System.Windows.MessageBox.Show(
                "已停止记录读数。\n\nCSV 文件：\n" + file +
                "\n\n可用 Excel 打开查看整条曲线。",
                "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Information);
            if (file is not null) OpenFolder(file);
        }
        else
        {
            string? file = AppLog.StartTrace();
            item.Checked = file is not null;
            if (file is null)
            {
                System.Windows.MessageBox.Show("无法创建 CSV 文件，详见日志。",
                    "SystemMonitor", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            System.Windows.MessageBox.Show(
                "开始记录读数（每秒一行）。\n\nCSV 文件：\n" + file +
                "\n\n现在去跑 3DMark，跑完再点一次本菜单停止记录。",
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

    // ---------- 采集 ----------

    private async Task RefreshAsync()
    {
        try
        {
            SensorSnapshot snap = await Task.Run(() => _sensors.Collect());

            if (!_namesBound)
            {
                CpuName.Text = snap.CpuName;
                GpuName.Text = snap.GpuName;
                _namesBound = true;
            }

            _vm.Apply(snap, DateTime.Now);
            StatusText.Text = _sensors.HardwareReady
                ? "采集中 · 1s/次"
                : "⚠ 传感器初始化失败 — 托盘「导出诊断报告」查看原因";
        }
        catch (Exception ex)
        {
            AppLog.Error("RefreshAsync 失败", ex);
            StatusText.Text = "⚠ 刷新出错，详见日志";
        }
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
