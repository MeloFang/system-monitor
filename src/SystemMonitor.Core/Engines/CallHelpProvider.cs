namespace SystemMonitor.Core.Engines;

public sealed record CallHelpSnippet(string Title, string Body);

/// <summary>各引擎的调用助手：endpoint + 客户端配置 + curl/Python 示例，每段可复制。</summary>
public static class CallHelpProvider
{
    public static List<CallHelpSnippet> For(EngineKind kind, string? model)
    {
        return kind == EngineKind.Ollama ? Ollama(model ?? "gpt-oss:20b") : Strata(model ?? "qwen36");
    }

    private static List<CallHelpSnippet> Ollama(string model) => new()
    {
        new("Base URL（OpenAI 兼容）", $"{EngineInfo.OllamaEndpoint}/v1"),
        new("curl", $@"curl {EngineInfo.OllamaEndpoint}/v1/chat/completions \
  -H ""Content-Type: application/json"" \
  -d '{{""model"": ""{model}"", ""messages"": [{{""role"": ""user"", ""content"": ""你好""}}]}}'"),
        new("Python（openai SDK）", $@"from openai import OpenAI
client = OpenAI(base_url=""{EngineInfo.OllamaEndpoint}/v1"", api_key=""ollama"")
r = client.chat.completions.create(model=""{model}"",
    messages=[{{""role"": ""user"", ""content"": ""你好""}}])
print(r.choices[0].message.content)"),
        new("opencode 配置", $@"{{
  ""provider"": {{
    ""ollama"": {{
      ""npm"": ""@ai-sdk/openai-compatible"",
      ""options"": {{ ""baseURL"": ""{EngineInfo.OllamaEndpoint}/v1"" }},
      ""models"": {{ ""{model}"": {{ ""name"": ""{model}"" }} }}
    }}
  }}
}}"),
        new("Claude Code", $@"ANTHROPIC_BASE_URL={EngineInfo.OllamaEndpoint}
ANTHROPIC_MODEL={model}
claude"),
    };

    private static List<CallHelpSnippet> Strata(string model) => new()
    {
        new("OpenAI 兼容 /v1", $"{EngineInfo.StrataEndpoint}/v1"),
        new("Anthropic 根地址", EngineInfo.StrataEndpoint),
        new("curl", $@"curl {EngineInfo.StrataEndpoint}/v1/chat/completions \
  -H ""Content-Type: application/json"" \
  -d '{{""model"": ""{model}"", ""messages"": [{{""role"": ""user"", ""content"": ""你好""}}]}}'"),
        new("Python（openai SDK）", $@"from openai import OpenAI
client = OpenAI(base_url=""{EngineInfo.StrataEndpoint}/v1"", api_key=""strata"")
r = client.chat.completions.create(model=""{model}"",
    messages=[{{""role"": ""user"", ""content"": ""你好""}}])
print(r.choices[0].message.content)"),
        new("opencode 一键接入", "strata connect opencode"),
        new("Claude Code", $@"ANTHROPIC_BASE_URL={EngineInfo.StrataEndpoint}
claude"),
    };
}
