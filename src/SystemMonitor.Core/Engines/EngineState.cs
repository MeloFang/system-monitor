using System.Text.Json;

namespace SystemMonitor.Core.Engines;

/// <summary>state.json：当前激活引擎 / 各引擎上次模型 / 显存基线。读写失败一律降级，不抛异常。</summary>
public sealed class EngineState
{
    public string? Active { get; set; }
    public string? OllamaModel { get; set; }
    public string? StrataFamily { get; set; }
    public string? StrataQuant { get; set; }
    public double? VramBaseline { get; set; }

    public static EngineState Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<EngineState>(File.ReadAllText(path)) ?? new EngineState();
        }
        catch { }
        return new EngineState();
    }

    public void Save(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
