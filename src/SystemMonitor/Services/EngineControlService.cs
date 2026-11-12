using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using SystemMonitor.Core.Diagnostics;
using SystemMonitor.Core.Engines;

namespace SystemMonitor.Services;

/// <summary>
/// 引擎起停/切换/互斥/PID 跟踪。单活跃引擎；切换 = 停当前 → 显存回落校验 → 起目标。
/// 全部耗时操作在后台线程；Changed 事件在后台线程触发，UI 侧自行 Dispatcher  marshal。
/// </summary>
public sealed class EngineControlService : IDisposable
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static readonly string OllamaExePath = Path.Combine(LocalAppData, @"Programs\Ollama\ollama.exe");
    public static readonly string StrataCmdPath = Path.Combine(LocalAppData, @"Strata\terminal\current\bin\strata.cmd");
    public const string StrataModelsDir = @"E:\Strata-Models\models";

    private readonly HttpClient _http = new(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(3) };
    private readonly Func<double?> _vramUsed;
    private readonly string _statePath;
    private readonly CancellationTokenSource _cts = new();
    private readonly Dictionary<EngineKind, Runtime> _rt = new()
    {
        [EngineKind.Ollama] = new Runtime(),
        [EngineKind.Strata] = new Runtime(),
    };

    private sealed class Runtime
    {
        public Process? Child;
        public bool Running;
        public int? Pid;
        public DateTime? Since;
        public string? Model;
    }

    public EngineState State { get; private set; }
    public bool IsBusy { get; private set; }
    public string? BusyText { get; private set; }
    public string? LastError { get; private set; }
    public string? LastWarning { get; private set; }
    public event Action? Changed;

    public EngineControlService(string statePath, Func<double?> vramUsed)
    {
        _statePath = statePath;
        _vramUsed = vramUsed;
        State = EngineState.Load(statePath);
        _ = Task.Run(ProbeLoopAsync);
    }

    public EngineStatus Status(EngineKind kind)
    {
        var r = _rt[kind];
        return new EngineStatus(kind, r.Running, r.Pid, r.Since, r.Model);
    }

    public EngineKind? Active
    {
        get
        {
            if (_rt[EngineKind.Ollama].Running && State.Active == "ollama") return EngineKind.Ollama;
            if (_rt[EngineKind.Strata].Running && State.Active == "strata") return EngineKind.Strata;
            if (_rt[EngineKind.Ollama].Running) return EngineKind.Ollama;
            if (_rt[EngineKind.Strata].Running) return EngineKind.Strata;
            return null;
        }
    }

    // ---------- 探测 ----------

    private async Task ProbeLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await ProbeAsync(EngineKind.Ollama);
                await ProbeAsync(EngineKind.Strata);
                if (!_rt[EngineKind.Ollama].Running && !_rt[EngineKind.Strata].Running && _vramUsed() is { } baseUsed)
                {
                    if (State.VramBaseline is null || Math.Abs(State.VramBaseline.Value - baseUsed) > 0.05)
                    {
                        State.VramBaseline = baseUsed;
                        State.Save(_statePath);
                    }
                }
                Changed?.Invoke();
            }
            catch (Exception ex) { AppLog.Error("引擎探测循环异常", ex); }
            await Task.Delay(2000, _cts.Token).ContinueWith(_ => { });
        }
    }

    private async Task ProbeAsync(EngineKind kind)
    {
        var r = _rt[kind];
        bool up;
        string? model = null;
        try
        {
            if (kind == EngineKind.Ollama)
            {
                using var v = await _http.GetAsync(EngineInfo.OllamaEndpoint + "/api/version");
                up = v.IsSuccessStatusCode;
                if (up)
                {
                    using var ps = await _http.GetAsync(EngineInfo.OllamaEndpoint + "/api/ps");
                    if (ps.IsSuccessStatusCode)
                        (model, _) = OllamaJson.ParsePs(await ps.Content.ReadAsStringAsync());
                }
            }
            else
            {
                using var m = await _http.GetAsync(EngineInfo.StrataEndpoint + "/v1/models");
                up = m.IsSuccessStatusCode;
                if (up) model = State.StrataFamily is null ? null : $"{State.StrataFamily} {State.StrataQuant}";
            }
        }
        catch { up = false; }

        if (up != r.Running)
        {
            r.Running = up;
            if (!up) { r.Child = null; r.Pid = null; r.Since = null; }
            else if (r.Child is { } c && !c.HasExited) { r.Pid = c.Id; r.Since = c.StartTime; }
            AppLog.Info($"引擎探测: {EngineInfo.DisplayName(kind)} {(up ? "运行中" : "已停止")}");
        }
        if (up && model is not null) r.Model = model;
        if (up && r.Pid is null && r.Child is { } c2 && !c2.HasExited) r.Pid = c2.Id;
    }

    private async Task<bool> WaitReadyAsync(EngineKind kind, int timeoutSec)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < timeoutSec)
        {
            await ProbeAsync(kind);
            if (_rt[kind].Running) return true;
            await Task.Delay(2000);
        }
        return false;
    }

    private async Task WaitVramReleasedAsync()
    {
        var baseline = State.VramBaseline ?? _vramUsed() ?? 1.5;
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 10)
        {
            if (VramGate.Released(_vramUsed(), baseline)) return;
            await Task.Delay(500);
        }
        LastWarning = $"显存 10s 内未回落到基线+500MB（{baseline:0.0}+0.5 GB），仍继续启动";
        AppLog.Warn(LastWarning);
    }

    // ---------- 起停 ----------

    public async Task<bool> StartAsync(EngineKind kind, string? model = null, bool silent = false)
    {
        if (IsBusy) return false;
        IsBusy = true; LastError = null; LastWarning = null;
        BusyText = $"正在启动 {EngineInfo.DisplayName(kind)}…"; Changed?.Invoke();
        try
        {
            var other = kind == EngineKind.Ollama ? EngineKind.Strata : EngineKind.Ollama;
            if (_rt[other].Running)
            {
                BusyText = $"互斥：先停 {EngineInfo.DisplayName(other)}…"; Changed?.Invoke();
                await StopCoreAsync(other);
                BusyText = "等待显存回落…"; Changed?.Invoke();
                await WaitVramReleasedAsync();
            }

            Process child;
            if (kind == EngineKind.Ollama)
            {
                child = Launch(OllamaExePath, "serve", Path.GetDirectoryName(OllamaExePath)!, "ollama-serve.log",
                    ("OLLAMA_HOST", "127.0.0.1:11434"));
            }
            else
            {
                // Strata 只认 family+quant+data-dir，裸 gguf 的 --model-path 会被引擎拒绝
                var (fam, quant) = ResolveStrataModel(model);
                if (fam is null) { LastError = "E:\\Strata-Models 下没有可用模型"; return false; }
                child = Launch(StrataCmdPath,
                    $"launch --family {fam} --model {quant} --data-dir \"E:\\Strata-Models\" --no-tui --yes --port 8080",
                    @"E:\Strata-Models", "strata-launch.log");
            }
            var r = _rt[kind];
            r.Child = child; r.Since = DateTime.Now;
            r.Model = model ?? (kind == EngineKind.Ollama ? State.OllamaModel : $"{State.StrataFamily} {State.StrataQuant}");

            BusyText = $"等待 {EngineInfo.DisplayName(kind)} 就绪（Strata 首次加载可能数分钟）…"; Changed?.Invoke();
            if (!await WaitReadyAsync(kind, kind == EngineKind.Ollama ? 30 : 240))
            {
                LastError = $"{EngineInfo.DisplayName(kind)} 启动超时，详见日志目录";
                AppLog.Error(LastError, null);
                return false;
            }
            State.Active = kind == EngineKind.Ollama ? "ollama" : "strata";
            State.Save(_statePath);
            if (!silent) AppLog.Info($"引擎已启动: {EngineInfo.DisplayName(kind)} · {r.Model}");
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            AppLog.Error($"启动 {kind} 失败", ex);
            return false;
        }
        finally
        {
            IsBusy = false; BusyText = null; Changed?.Invoke();
        }
    }

    public async Task StopAsync(EngineKind kind)
    {
        if (IsBusy) return;
        IsBusy = true; BusyText = $"正在停止 {EngineInfo.DisplayName(kind)}…"; Changed?.Invoke();
        try
        {
            await StopCoreAsync(kind);
            if (State.Active == (kind == EngineKind.Ollama ? "ollama" : "strata")) State.Active = null;
            State.Save(_statePath);
        }
        catch (Exception ex) { LastError = ex.Message; AppLog.Error($"停止 {kind} 失败", ex); }
        finally { IsBusy = false; BusyText = null; Changed?.Invoke(); }
    }

    private async Task StopCoreAsync(EngineKind kind)
    {
        var r = _rt[kind];
        try
        {
            if (r.Child is { HasExited: false } c)
            {
                c.Kill(entireProcessTree: true);
            }
            else if (kind == EngineKind.Ollama)
            {
                RunHidden("taskkill.exe", "/F /IM ollama.exe");
            }
            else
            {
                RunHidden(StrataCmdPath, "launch --stop", @"E:\Strata-Models");
            }
        }
        catch (Exception ex) { AppLog.Warn($"停止 {kind} 首选手段失败，回退等待端口关闭", ex); }

        var sw = Stopwatch.StartNew();
        while (sw.Elapsed.TotalSeconds < 10)
        {
            await ProbeAsync(kind);
            if (!r.Running) break;
            await Task.Delay(500);
        }
        r.Child = null; r.Running = false; r.Pid = null; r.Since = null;
    }

    /// <summary>切换 = 启动目标（内部先停另一个 + 显存校验）。</summary>
    public Task<bool> SwitchAsync(EngineKind target) => StartAsync(target);

    // ---------- 模型 ----------

    public async Task<List<OllamaModelInfo>> ListOllamaModelsAsync()
    {
        try
        {
            using var r = await _http.GetAsync(EngineInfo.OllamaEndpoint + "/api/tags");
            return r.IsSuccessStatusCode ? OllamaJson.ParseTags(await r.Content.ReadAsStringAsync()) : new();
        }
        catch { return new(); }
    }

    public static List<StrataModelInfo> ListStrataModels()
    {
        var list = new List<StrataModelInfo>();
        try
        {
            foreach (var dir in Directory.GetDirectories(StrataModelsDir))
            {
                var name = Path.GetFileName(dir);
                var i = name.IndexOf('-');
                if (i <= 0) continue;
                var gguf = Directory.GetFiles(dir, "*.gguf").FirstOrDefault();
                if (gguf is not null)
                    list.Add(new StrataModelInfo(name[..i], name[(i + 1)..], gguf));
            }
        }
        catch { }
        return list;
    }

    public static string? FindGguf(string display)
    {
        foreach (var m in ListStrataModels())
            if (string.Equals(m.Display, display, StringComparison.OrdinalIgnoreCase)
                || string.Equals($"{m.Family}-{m.Quant}", display, StringComparison.OrdinalIgnoreCase))
                return m.GgufPath;
        return null;
    }

    public static string? DefaultStrataGguf()
    {
        var saved = EngineState.Load(Path.Combine(AppLog.LogDirectory, "engine-state.json"));
        if (saved.StrataFamily is not null)
            return FindGguf($"{saved.StrataFamily} {saved.StrataQuant}");
        return ListStrataModels().FirstOrDefault()?.GgufPath;
    }

    private static (string? Fam, string? Quant) ResolveStrataModel(string? display)
    {
        if (display is not null)
        {
            var i = display.IndexOf(' ');
            if (i > 0) return (display[..i], display[(i + 1)..]);
        }
        var saved = EngineState.Load(Path.Combine(AppLog.LogDirectory, "engine-state.json"));
        if (saved.StrataFamily is not null) return (saved.StrataFamily, saved.StrataQuant);
        var first = ListStrataModels().FirstOrDefault();
        return (first?.Family, first?.Quant);
    }

    /// <summary>Ollama 点选加载：未运行先启动；空 prompt 的 generate 只加载不产出。</summary>
    public async Task<bool> LoadOllamaModelAsync(string name)
    {
        if (!_rt[EngineKind.Ollama].Running && !await StartAsync(EngineKind.Ollama, name)) return false;
        IsBusy = true; BusyText = $"加载模型 {name}…"; Changed?.Invoke();
        try
        {
            using var longHttp = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromMinutes(3) };
            var body = JsonSerializer.Serialize(new { model = name, prompt = "", stream = false, keep_alive = "30m" });
            using var r = await longHttp.PostAsync(EngineInfo.OllamaEndpoint + "/api/generate",
                new StringContent(body, Encoding.UTF8, "application/json"));
            if (!r.IsSuccessStatusCode) { LastError = $"加载 {name} 失败: HTTP {(int)r.StatusCode}"; return false; }
            var (tok, ptok) = OllamaJson.ParseDone(await r.Content.ReadAsStringAsync());
            LastOllamaDone = (tok, ptok);
            _rt[EngineKind.Ollama].Model = name;
            State.OllamaModel = name; State.Active = "ollama"; State.Save(_statePath);
            return true;
        }
        catch (Exception ex) { LastError = ex.Message; AppLog.Error($"加载 {name} 失败", ex); return false; }
        finally { IsBusy = false; BusyText = null; Changed?.Invoke(); }
    }

    public (double? Tok, double? PromptTok)? LastOllamaDone { get; private set; }

    public async Task<bool> LoadStrataAsync(string family, string quant)
    {
        State.StrataFamily = family; State.StrataQuant = quant; State.Save(_statePath);
        if (_rt[EngineKind.Strata].Running)
        {
            IsBusy = true; BusyText = "重启 Strata 以加载所选模型…"; Changed?.Invoke();
            try { await StopCoreAsync(EngineKind.Strata); await WaitVramReleasedAsync(); }
            finally { IsBusy = false; Changed?.Invoke(); }
        }
        return await StartAsync(EngineKind.Strata, $"{family} {quant}");
    }

    // ---------- 进程 ----------

    private static Process Launch(string exe, string args, string workDir, string logName, params (string, string)[] env)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        // 清代理：本机沙盒代理会把引擎下载/连接 502 挡死
        foreach (var k in new[] { "HTTPS_PROXY", "HTTP_PROXY", "ALL_PROXY", "https_proxy", "http_proxy", "all_proxy" })
            psi.EnvironmentVariables.Remove(k);
        foreach (var (k, v) in env) psi.EnvironmentVariables[k] = v;

        var logPath = Path.Combine(AppLog.LogDirectory, logName);
        var child = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var writer = new StreamWriter(logPath, append: true, Encoding.UTF8) { AutoFlush = true };
        child.OutputDataReceived += (_, e) => { if (e.Data is not null) writer.WriteLine(e.Data); };
        child.ErrorDataReceived += (_, e) => { if (e.Data is not null) writer.WriteLine(e.Data); };
        child.Exited += (_, _) => { try { writer.Dispose(); } catch { } };
        child.Start();
        child.BeginOutputReadLine();
        child.BeginErrorReadLine();
        AppLog.Info($"引擎子进程已起: {exe} {args} → PID {child.Id}，日志 {logPath}");
        return child;
    }

    private static void RunHidden(string exe, string args, string? workDir = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = args,
            WorkingDirectory = workDir ?? Path.GetTempPath(),
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi);
        p?.WaitForExit(15000);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _http.Dispose();
    }
}
