using System.IO;
using SystemMonitor.Core.Engines;

namespace SystemMonitor.Services;

/// <summary>
/// 实时指标（尽力实现）：Strata 尾读 *.engine.log 的 serve 行；Ollama 模型级显存靠 /api/ps（由 EngineControlService 探测），
/// tok/s 靠最近一次加载请求的 done 对象。读不到一律 null → 界面 "--"。
/// </summary>
public sealed class MetricsService
{
    private string? _logPath;
    private long _offset;

    public StrataServeMetrics? StrataLatest { get; private set; }
    public double? StableGenTok { get; private set; }
    public double? StablePromptTok { get; private set; }
    public double? OllamaTok { get; set; }
    public double? OllamaPromptTok { get; set; }

    /// <summary>UI 每秒调用：尾读 Strata engine.log（增量，FileShare.ReadWrite）。</summary>
    public void Tick()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Strata\launch");
            var latest = Directory.Exists(dir)
                ? new DirectoryInfo(dir).GetFiles("*.engine.log").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
                : null;
            if (latest is null) return;
            if (latest.FullName != _logPath) { _logPath = latest.FullName; _offset = 0; }
            if (latest.Length < _offset) _offset = 0;

            using var fs = new FileStream(latest.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fs.Seek(_offset, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (StrataLogParser.TryParseServeLine(line) is { } m)
                {
                    StrataLatest = m;
                    if (m.GenStable) StableGenTok = m.GenTokPerSec;
                    if (m.PromptStable) StablePromptTok = m.PromptTokPerSec;
                }
            }
            _offset = fs.Position;
        }
        catch { }
    }
}
