using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SystemMonitor.Core.Engines;
using SystemMonitor.Services;

namespace SystemMonitor.Views;

public partial class EngineView : UserControl
{
    private EngineKind _pane = EngineKind.Ollama;
    private List<StrataModelInfo> _strataModels = new();
    private bool _suppressSel;
    private string _helpKey = "";
    private bool _esPulse;

    public EngineView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            _pane = Hub.Engines.Active ?? (Hub.Engines.State.Active == "strata" ? EngineKind.Strata : EngineKind.Ollama);
            Hub.Tick += Refresh;
            await RefreshListsAsync();
            Refresh();
            UiAnim.Enter(this);
        };
        Unloaded += (_, _) => Hub.Tick -= Refresh;
    }

    // ---------- 顶栏 ----------

    private void SegOllama_Click(object sender, RoutedEventArgs e) => SelectPane(EngineKind.Ollama);
    private void SegStrata_Click(object sender, RoutedEventArgs e) => SelectPane(EngineKind.Strata);

    private async void SelectPane(EngineKind kind)
    {
        _pane = kind;
        if (!Hub.Engines.Status(kind).Running && !Hub.Engines.IsBusy)
        {
            var ok = await Hub.Engines.StartAsync(kind);
            Hub.Toast(ok ? $"{EngineInfo.DisplayName(kind)} 已启动 · 显存加载中" : Hub.Engines.LastError ?? "启动失败",
                ok ? "ok" : "warn", null);
            await RefreshListsAsync();
        }
        Refresh();
    }

    private async void SwitchBtn_Click(object sender, RoutedEventArgs e)
    {
        var target = _pane == EngineKind.Ollama ? EngineKind.Strata : EngineKind.Ollama;
        var ok = await Hub.Engines.SwitchAsync(target);
        if (ok)
        {
            _pane = target;
            Hub.Toast($"已切换到 {EngineInfo.DisplayName(target)}", "ok", null);
            await RefreshListsAsync();
        }
        Refresh();
    }

    private async void StopBtn_Click(object sender, RoutedEventArgs e)
    {
        var act = Hub.Engines.Active;
        if (act is null) return;
        var kind = act.Value;
        var model = Hub.Engines.Status(kind).Model;
        await Hub.Engines.StopAsync(kind);
        Hub.Toast($"{EngineInfo.DisplayName(kind)} 已停止 · 显存释放中", "warn", async () =>
        {
            var ok = await Hub.Engines.StartAsync(kind, model, silent: true);
            Hub.Toast(ok ? $"已恢复 {EngineInfo.DisplayName(kind)}" : Hub.Engines.LastError ?? "恢复失败",
                ok ? "ok" : "warn", null);
        });
        Refresh();
    }

    // ---------- 模型 ----------

    private async Task RefreshListsAsync()
    {
        _strataModels = EngineControlService.ListStrataModels();

        _suppressSel = true;
        // 本地 Strata 列表先上屏，Ollama 列表走 HTTP 后补（避免空窗）
        var families = _strataModels.Select(m => m.Family).Distinct().ToList();
        FamList.ItemsSource = families;
        var fam = Hub.Engines.State.StrataFamily ?? families.FirstOrDefault();
        FamList.SelectedItem = fam;
        FillQuants(fam);

        var ollamaModels = await Hub.Engines.ListOllamaModelsAsync();
        ModelList.ItemsSource = ollamaModels;
        OllamaListHint.Text = ollamaModels.Count == 0 ? "引擎未运行或无本地模型 · 启动后可列模型" : "";
        var loaded = Hub.Engines.Status(EngineKind.Ollama).Model;
        if (loaded is not null)
            ModelList.SelectedItem = ollamaModels.FirstOrDefault(m => m.Name == loaded);
        _suppressSel = false;
    }

    private void FillQuants(string? family)
    {
        var quants = _strataModels.Where(m => m.Family == family).Select(m => m.Quant).ToList();
        QuantList.ItemsSource = quants;
        QuantList.SelectedItem = quants.Contains(Hub.Engines.State.StrataQuant) ? Hub.Engines.State.StrataQuant : quants.FirstOrDefault();
    }

    private void FamList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSel) return;
        FillQuants(FamList.SelectedItem as string);
    }

    private async void ModelList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSel || ModelList.SelectedItem is not OllamaModelInfo m || Hub.Engines.IsBusy) return;
        if (m.Name == Hub.Engines.Status(EngineKind.Ollama).Model) return;
        var ok = await Hub.Engines.LoadOllamaModelAsync(m.Name);
        Hub.Toast(ok ? $"已加载 {m.Name}" : Hub.Engines.LastError ?? $"加载 {m.Name} 失败", ok ? "ok" : "warn", null);
        Refresh();
    }

    private async void StrataLoadBtn_Click(object sender, RoutedEventArgs e)
    {
        if (FamList.SelectedItem is not string fam || QuantList.SelectedItem is not string quant || Hub.Engines.IsBusy) return;
        var ok = await Hub.Engines.LoadStrataAsync(fam, quant);
        Hub.Toast(ok ? $"Strata 已加载 {fam} {quant}" : Hub.Engines.LastError ?? "加载失败", ok ? "ok" : "warn", null);
        Refresh();
    }

    // ---------- 复制 ----------

    private void EsCopy_Click(object sender, RoutedEventArgs e) => CopyText(EsUrl.Text, (Button)sender);

    private void CopyHelp_Click(object sender, RoutedEventArgs e)
    {
        if (((Button)sender).Tag is string body) CopyText(body, (Button)sender);
    }

    private static async void CopyText(string text, Button btn)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
            var oldFg = btn.Foreground;
            var oldBd = btn.BorderBrush;
            btn.Content = "✓ 已复制";
            btn.Foreground = (Brush)btn.FindResource("Ok");
            btn.BorderBrush = (Brush)btn.FindResource("Ok");
            await Task.Delay(1400);
            btn.Content = "复制";
            btn.Foreground = oldFg;
            btn.BorderBrush = oldBd;
        }
        catch { }
    }

    // ---------- 刷新 ----------

    private void Refresh()
    {
        var e = Hub.Engines;
        var act = e.Active;
        var st = e.Status(_pane);
        var busy = e.IsBusy;

        HeadSub.Text = act is null
            ? "无活跃引擎 · 点引擎名启动"
            : $"当前在跑 · {EngineInfo.DisplayName(act.Value)} · {EngineInfo.Endpoint(act.Value)}";

        SegOllamaDot.Fill = (Brush)FindResource(e.Status(EngineKind.Ollama).Running ? "Ok" : "Faint");
        SegStrataDot.Fill = (Brush)FindResource(e.Status(EngineKind.Strata).Running ? "Ok" : "Faint");
        SegOllamaDot.Effect = e.Status(EngineKind.Ollama).Running ? (DropShadowEffect)FindResource("GlowOk") : null;
        SegStrataDot.Effect = e.Status(EngineKind.Strata).Running ? (DropShadowEffect)FindResource("GlowOk") : null;
        StyleSeg(SegOllama, _pane == EngineKind.Ollama, "Ollama");
        StyleSeg(SegStrata, _pane == EngineKind.Strata, "Strata");
        SwitchBtn.IsEnabled = !busy;
        StopBtn.IsEnabled = !busy && act is not null;

        EsDot.Fill = (Brush)FindResource(st.Running ? "Ok" : "Faint");
        if (st.Running && !_esPulse) { UiAnim.Pulse(EsDot, (DropShadowEffect)FindResource("GlowOk")); _esPulse = true; }
        if (!st.Running && _esPulse) { UiAnim.StopPulse(EsDot); _esPulse = false; }
        EsName.Text = EngineInfo.DisplayName(_pane);
        EsName.Foreground = (Brush)FindResource(_pane == EngineKind.Ollama ? "Ollama" : "Strata");
        EsState.Text = busy ? e.BusyText ?? "进行中…"
            : st.Running ? $"运行中 · PID {(st.Pid is null ? "外部" : st.Pid)} · {(st.Since is null ? "--" : FmtUp(DateTime.Now - st.Since.Value))}"
            : "已停止 · 点上方引擎名启动";
        EsDesc.Text = _pane == EngineKind.Ollama ? "本地模型推理 · OpenAI 兼容 · 全显存驻留" : "本地模型推理 · OpenAI / Anthropic 兼容 · GPU+RAM";
        EsModel.Text = st.Model ?? "--";
        var tok = _pane == EngineKind.Ollama ? Hub.Metrics.OllamaTok : Hub.Metrics.StableGenTok;
        EsTok.Text = st.Running && tok is not null ? $"{tok:0} tok/s" : "--";
        EsUrl.Text = EngineInfo.Endpoint(_pane);

        OllamaPanel.Visibility = _pane == EngineKind.Ollama ? Visibility.Visible : Visibility.Collapsed;
        StrataPanel.Visibility = _pane == EngineKind.Strata ? Visibility.Visible : Visibility.Collapsed;

        if (_pane == EngineKind.Strata)
        {
            var m = Hub.Metrics.StrataLatest;
            MTok.Text = Hub.Metrics.StableGenTok is null ? "--" : $"{Hub.Metrics.StableGenTok:0.0} tok/s";
            MPrompt.Text = Hub.Metrics.StablePromptTok is null ? "--" : $"{Hub.Metrics.StablePromptTok:0.0} tok/s";
            MCache.Text = m is null ? "--" : $"{m.CacheHit:0}%";
            MCtx.Text = "--";
        }
        else
        {
            MTok.Text = Hub.Metrics.OllamaTok is null ? "--" : $"{Hub.Metrics.OllamaTok:0.0} tok/s";
            MPrompt.Text = Hub.Metrics.OllamaPromptTok is null ? "--" : $"{Hub.Metrics.OllamaPromptTok:0.0} tok/s";
            MCache.Text = "--";
            MCtx.Text = "--";
        }

        var brush = (Brush)FindResource(_pane == EngineKind.Ollama ? "Ollama" : "Strata");
        MTok.Foreground = brush;
        MPrompt.Foreground = brush;
        MCache.Foreground = brush;
        MCtx.Foreground = brush;
        TokSpark.Stroke = brush;
        BigTokVal.Foreground = brush;
        BigTokVal.Text = tok is null ? "--" : $"{tok:0} tok/s";
        TokSpark.Max = _pane == EngineKind.Ollama ? 80 : 40;
        TokSpark.Values = (_pane == EngineKind.Ollama ? Hub.Hist.OllamaTok : Hub.Hist.StrataTok).ToArray();

        var helpKey = _pane + "|" + (st.Model ?? "");
        if (helpKey != _helpKey)
        {
            _helpKey = helpKey;
            HelpList.Tag = brush;
            HelpList.ItemsSource = CallHelpProvider.For(_pane, st.Model);
        }
    }

    private void StyleSeg(Button btn, bool active, string colorKey)
    {
        if (active)
        {
            btn.BorderBrush = (Brush)FindResource(colorKey);
            btn.Foreground = (Brush)FindResource(colorKey);
            btn.Background = (Brush)FindResource(colorKey == "Ollama" ? "OllamaSoft" : "StrataSoft");
        }
        else
        {
            btn.BorderBrush = (Brush)FindResource("Line2");
            btn.Foreground = (Brush)FindResource("Dim");
            btn.Background = (Brush)FindResource("Surface");
        }
    }

    private static string FmtUp(TimeSpan t)
        => $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
}
