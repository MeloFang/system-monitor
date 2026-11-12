using SystemMonitor.Core.Engines;
using Xunit;

namespace SystemMonitor.Tests;

public class EngineLogicTests
{
    private const string RealServeLine =
        "strata serve: session 0: prompt 35 tokens = 0 reused + 35 read in 885 ms (39.5 tok/s), 700 generated in 25256 ms (27.7 tok/s), drafts accepted 0 of 0 (length)";

    [Fact]
    public void StrataLogParser_Parses_Real_Serve_Line()
    {
        var m = StrataLogParser.TryParseServeLine(RealServeLine);
        Assert.NotNull(m);
        Assert.Equal(35, m!.PromptTokens);
        Assert.Equal(0, m.Reused);
        Assert.Equal(35, m.Read);
        Assert.Equal(885, m.PromptMs);
        Assert.Equal(39.5, m.PromptTokPerSec);
        Assert.Equal(700, m.Generated);
        Assert.Equal(25256, m.GenMs);
        Assert.Equal(27.7, m.GenTokPerSec);
    }

    [Fact]
    public void StrataLogParser_Ignores_NonServe_Lines()
    {
        Assert.Null(StrataLogParser.TryParseServeLine("strata-q36: hybrid experts: 108122 of 1608848 batch entries on the GPU"));
        Assert.Null(StrataLogParser.TryParseServeLine(""));
    }

    [Fact]
    public void CacheHit_Is_Reused_Over_Total()
    {
        var m = new StrataServeMetrics(40, 30, 10, 100, 40, 100, 1000, 25);
        Assert.Equal(75, m.CacheHit);
    }

    [Fact]
    public void OllamaJson_Parses_Tags_Ps_Done()
    {
        var tags = OllamaJson.ParseTags("""{"models":[{"name":"gpt-oss:20b","size":14173388800}]}""");
        Assert.Single(tags);
        Assert.Equal("gpt-oss:20b", tags[0].Name);
        Assert.Equal("13.2 GB", tags[0].SizeText);

        var (model, vram) = OllamaJson.ParsePs("""{"models":[{"name":"gpt-oss:20b","size_vram":12025908428}]}""");
        Assert.Equal("gpt-oss:20b", model);
        Assert.Equal(11.2, vram!.Value, 1);

        var (tok, ptok) = OllamaJson.ParseDone("""{"eval_count":700,"eval_duration":25256000000,"prompt_eval_count":35,"prompt_eval_duration":885000000}""");
        Assert.Equal(27.7, tok!.Value, 1);
        Assert.Equal(39.5, ptok!.Value, 1);
    }

    [Fact]
    public void VramGate_Baseline_Plus_500MB()
    {
        Assert.True(VramGate.Released(1.8, 1.3));
        Assert.False(VramGate.Released(1.81, 1.3));
        Assert.False(VramGate.Released(null, 1.3));
    }

    [Fact]
    public void CallHelp_Endpoints_Start_With_http()
    {
        var ol = CallHelpProvider.For(EngineKind.Ollama, "gpt-oss:20b");
        Assert.Contains(ol, s => s.Body == "http://127.0.0.1:11434/v1");
        Assert.All(ol.Where(s => s.Title is "curl" or "Python（openai SDK）"), s => Assert.Contains("http://127.0.0.1:11434", s.Body));

        var st = CallHelpProvider.For(EngineKind.Strata, "qwen36");
        Assert.Contains(st, s => s.Body == "http://127.0.0.1:8080/v1");
        Assert.Contains(st, s => s.Body.Contains("ANTHROPIC_BASE_URL=http://127.0.0.1:8080"));
        Assert.Contains(st, s => s.Body == "strata connect opencode");
    }

    [Fact]
    public void Small_Samples_Are_Not_Stable()
    {
        var spike = new StrataServeMetrics(11, 0, 11, 194, 56.7, 1, 1, 890.6);
        Assert.False(spike.GenStable);
        Assert.False(spike.PromptStable);
        var real = new StrataServeMetrics(35, 0, 35, 885, 39.5, 700, 25256, 27.7);
        Assert.True(real.GenStable);
        Assert.True(real.PromptStable);
    }

    [Fact]
    public void EngineState_RoundTrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"engine-state-test-{Guid.NewGuid():N}.json");
        try
        {
            var s = new EngineState { Active = "ollama", OllamaModel = "gpt-oss:20b", VramBaseline = 1.3 };
            s.Save(path);
            var loaded = EngineState.Load(path);
            Assert.Equal("ollama", loaded.Active);
            Assert.Equal("gpt-oss:20b", loaded.OllamaModel);
            Assert.Equal(1.3, loaded.VramBaseline);
        }
        finally { File.Delete(path); }
    }
}
