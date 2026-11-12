namespace SystemMonitor.Core.Engines;

public enum EngineKind { Ollama, Strata }

public static class EngineInfo
{
    public const string OllamaEndpoint = "http://127.0.0.1:11434";
    public const string StrataEndpoint = "http://127.0.0.1:8080";

    public static string Endpoint(EngineKind kind) => kind == EngineKind.Ollama ? OllamaEndpoint : StrataEndpoint;
    public static string DisplayName(EngineKind kind) => kind == EngineKind.Ollama ? "Ollama" : "Strata";
}

/// <summary>某引擎某一时刻的运行状态（探测所得，外部启动的进程 Pid 可能为 null）。</summary>
public sealed record EngineStatus(
    EngineKind Kind,
    bool Running,
    int? Pid,
    DateTime? Since,
    string? Model);

/// <summary>Strata 落地模型：目录名约定 family-quant，如 qwen36-UD-Q4_K_XL。</summary>
public sealed record StrataModelInfo(string Family, string Quant, string GgufPath)
{
    public string Display => $"{Family} {Quant}";
}

public sealed record OllamaModelInfo(string Name, long SizeBytes)
{
    public string SizeText => $"{SizeBytes / 1073741824.0:0.0} GB";
}

/// <summary>Strata engine.log 一行 serve 记录的解析结果。</summary>
public sealed record StrataServeMetrics(
    int PromptTokens, int Reused, int Read,
    double PromptMs, double PromptTokPerSec,
    int Generated, double GenMs, double GenTokPerSec)
{
    public double CacheHit => Reused + Read > 0 ? (double)Reused / (Reused + Read) * 100 : 0;

    // 小样本（如 1 token/1ms）会算出失真的瞬时尖峰，展示只采纳稳定样本
    public bool GenStable => Generated >= 32;
    public bool PromptStable => PromptTokens >= 32;
}
