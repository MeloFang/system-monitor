using SystemMonitor.Core.Engines;
using SystemMonitor.Core.Sensors;
using SystemMonitor.Services;

namespace SystemMonitor;

/// <summary>进程内服务与共享状态枢纽。MainWindow 每秒采集后 RaiseTick，各视图订阅刷新。</summary>
public static class Hub
{
    public static SensorService Sensors = null!;
    public static EngineControlService Engines = null!;
    public static MetricsService Metrics = null!;
    public static SensorSnapshot Latest = SensorSnapshot.Empty;
    public static readonly History Hist = new();

    public static event Action? Tick;
    public static void RaiseTick() => Tick?.Invoke();

    /// <summary>MainWindow 注册的 toast 入口（kind: ok/warn/info；undo 非空时带撤销按钮）。</summary>
    public static Action<string, string, Action?> Toast = (_, _, _) => { };

    /// <summary>MainWindow 注册的导航入口（home/monitor/engine）。</summary>
    public static Action<string> Navigate = _ => { };
}

/// <summary>近 60s 滚动历史，供 sparkline。</summary>
public sealed class History
{
    public readonly List<double> CpuTemp = new(), CpuLoad = new(), GpuTemp = new(), GpuLoad = new(),
        OllamaTok = new(), StrataTok = new();

    public void Push(SensorSnapshot s, EngineControlService e, MetricsService m)
    {
        Add(CpuTemp, s.CpuTemp); Add(CpuLoad, s.CpuLoad);
        Add(GpuTemp, s.GpuTemp); Add(GpuLoad, s.GpuLoad);
        Add(OllamaTok, e.Status(EngineKind.Ollama).Running ? (m.OllamaTok ?? 0) : 0);
        Add(StrataTok, e.Status(EngineKind.Strata).Running ? (m.StableGenTok ?? 0) : 0);
    }

    private static void Add(List<double> buf, double? v)
    {
        buf.Add(v ?? 0);
        if (buf.Count > 60) buf.RemoveAt(0);
    }
}
