using System.Text.Json;

namespace SystemMonitor.Core.Engines;

/// <summary>Ollama HTTP API 的最小解析（/api/tags、/api/ps、/api/generate done）。</summary>
public static class OllamaJson
{
    public static List<OllamaModelInfo> ParseTags(string json)
    {
        var list = new List<OllamaModelInfo>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("models", out var models))
            foreach (var m in models.EnumerateArray())
                list.Add(new OllamaModelInfo(
                    m.GetProperty("name").GetString() ?? "--",
                    m.TryGetProperty("size", out var s) ? s.GetInt64() : 0));
        return list;
    }

    /// <summary>/api/ps：当前加载的模型与模型级显存（size_vram，字节）。</summary>
    public static (string? Model, double? VramGb) ParsePs(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("models", out var models)) return (null, null);
        foreach (var m in models.EnumerateArray())
        {
            var name = m.GetProperty("name").GetString();
            double? vram = m.TryGetProperty("size_vram", out var v) ? v.GetInt64() / 1073741824.0 : null;
            return (name, vram);
        }
        return (null, null);
    }

    /// <summary>/api/generate 的 done 对象：eval_count/eval_duration → 输出 tok/s。</summary>
    public static (double? TokPerSec, double? PromptTokPerSec) ParseDone(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        double? tok = null, ptok = null;
        if (r.TryGetProperty("eval_count", out var ec) && r.TryGetProperty("eval_duration", out var ed)
            && ed.GetInt64() > 0)
            tok = ec.GetInt64() / (ed.GetInt64() / 1e9);
        if (r.TryGetProperty("prompt_eval_count", out var pc) && r.TryGetProperty("prompt_eval_duration", out var pd)
            && pd.GetInt64() > 0)
            ptok = pc.GetInt64() / (pd.GetInt64() / 1e9);
        return (tok, ptok);
    }
}
