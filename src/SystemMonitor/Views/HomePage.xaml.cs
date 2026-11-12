using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemMonitor.Core.Engines;
using SystemMonitor.Core.Sensors;

namespace SystemMonitor.Views;

public partial class HomePage : UserControl
{
    private bool _monPulse, _engPulse;

    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Hub.Tick += Refresh;
            Refresh();
            UiAnim.Enter(CardMon, 0.05);
            UiAnim.Enter(CardEng, 0.14);
        };
        Unloaded += (_, _) => Hub.Tick -= Refresh;
    }

    private void MonitorCard_Click(object sender, RoutedEventArgs e) => Hub.Navigate("monitor");
    private void EngineCard_Click(object sender, RoutedEventArgs e) => Hub.Navigate("engine");

    private void Refresh()
    {
        var s = Hub.Latest;
        bool hw = Hub.Sensors.HardwareReady;
        MonStatus.Text = hw ? "采集中" : "数据不可用";
        var mc = (Brush)FindResource(hw ? "Ok" : "Warn");
        MonStatus.Foreground = mc;
        MonPd.Fill = mc;
        MonStatusBd.Background = (Brush)FindResource(hw ? "OkSoft" : "Surface");
        MonStatusBd.BorderBrush = hw ? new SolidColorBrush(Color.FromRgb(0x2b, 0x5c, 0x40)) : (Brush)FindResource("Line2");
        if (hw && !_monPulse) { UiAnim.Pulse(MonPd, (DropShadowEffect)FindResource("GlowOk")); _monPulse = true; }
        if (!hw && _monPulse) { UiAnim.StopPulse(MonPd); _monPulse = false; }

        HwGpuTemp.Text = s.GpuTemp is null ? "--" : $"{s.GpuTemp:0}";
        HwVram.Text = s.GpuMemUsed is null ? "--" : $"{s.GpuMemUsed:0.0}";
        HwGpuLoad.Text = s.GpuLoad is null ? "--" : $"{s.GpuLoad:0}";
        HwCpuTemp.Text = s.CpuTemp is null ? "--" : $"{s.CpuTemp:0}";

        var act = Hub.Engines.Active;
        var disp = act ?? EngineKind.Ollama;
        var st = Hub.Engines.Status(disp);
        var ec = (Brush)FindResource(disp == EngineKind.Ollama ? "Ollama" : "Strata");
        EngSub.Text = "ENGINE · " + EngineInfo.DisplayName(disp).ToUpperInvariant();
        EngStatus.Text = st.Running ? "运行中" : "已停止";
        EngStatus.Foreground = st.Running ? (Brush)FindResource("Ok") : (Brush)FindResource("Faint");
        EngPd.Fill = st.Running ? (Brush)FindResource("Ok") : (Brush)FindResource("Faint");
        EngStatusBd.Background = st.Running ? (Brush)FindResource("OkSoft") : (Brush)FindResource("Surface");
        EngStatusBd.BorderBrush = st.Running ? new SolidColorBrush(Color.FromRgb(0x2b, 0x5c, 0x40)) : (Brush)FindResource("Line2");
        if (st.Running && !_engPulse) { UiAnim.Pulse(EngPd, (DropShadowEffect)FindResource("GlowOk")); _engPulse = true; }
        if (!st.Running && _engPulse) { UiAnim.StopPulse(EngPd); _engPulse = false; }

        EngModel.Text = st.Running ? EngineInfo.DisplayName(disp) + " · " + (st.Model ?? "--") : "无活跃引擎";
        var tok = disp == EngineKind.Ollama ? Hub.Metrics.OllamaTok : Hub.Metrics.StableGenTok;
        EngTok.Text = st.Running && tok is not null ? $"{tok:0}" : "--";
        EngUrl.Text = EngineInfo.Endpoint(disp);
        EngIconBd.BorderBrush = ec;
        EngIconBd.Background = (Brush)FindResource(disp == EngineKind.Ollama ? "OllamaSoft" : "StrataSoft");
        EngIconBd.Effect = (DropShadowEffect)FindResource(disp == EngineKind.Ollama ? "GlowOllama" : "GlowStrata");
        EngGo.Foreground = ec;
        EngGoArr.Foreground = ec;
    }
}
