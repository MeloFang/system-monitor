using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AsusMonitor.Core.Sensors;

namespace AsusMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private readonly SensorService _sensors = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _namesBound;

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
        };
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
        StatusText.Text = "采集中 · 1s/次";
    }

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e) => DragMove();

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
