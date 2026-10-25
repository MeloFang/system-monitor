using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
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

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "asus-monitor";

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        _timer.Tick += async (_, _) => await RefreshAsync();

        Loaded += async (_, _) =>
        {
            await RefreshAsync();
            _timer.Start();
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
        SensorSnapshot snap = await Task.Run(() => _sensors.Collect());

        if (!_namesBound)
        {
            CpuName.Text = snap.CpuName;
            GpuName.Text = snap.GpuName;
            _namesBound = true;
        }

        _vm.Apply(snap, DateTime.Now);
        StatusText.Text = "采集中 · 1s/次";
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
