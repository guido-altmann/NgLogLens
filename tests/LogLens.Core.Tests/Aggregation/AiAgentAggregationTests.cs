using LogLens.Core.Aggregation;
using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>KI-Agenten (SPEC 6, 8.5). Sollwerte aus <c>tests/fixtures/sample-expected.md</c>.</summary>
public sealed class AiAgentAggregationTests
{
    [Fact]
    public async Task Verdikte_je_Agent_entsprechen_der_Erwartung()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("ClaudeBot", 3, 0, 1),
                ("AgentTrustBot", 0, 1, 0),
                ("ChatGPT-User", 0, 0, 1),
                ("Perplexity-User", 0, 0, 1),
                ("PerplexityBot", 0, 1, 0),
            ],
            result.AiAgents.Select(a => (a.Name, a.Verified, a.Unverified, a.Spoofed)));
    }

    [Fact]
    public async Task Anbieter_kommt_aus_der_Agentenliste()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Anthropic", result.AiAgents.Single(a => a.Name == "ClaudeBot").Provider);
        Assert.Equal("Perplexity", result.AiAgents.Single(a => a.Name == "PerplexityBot").Provider);
    }

    [Fact]
    public async Task Seiten_echter_Agenten_ohne_Getarnte_und_mit_Zaehlung_gekuerzter_Zeilen()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var claudeBot = result.AiAgents.Single(a => a.Name == "ClaudeBot");

        // Zeile 22 (llms.txt von der Scanner-IP) ist getarnt und fehlt hier bewusst.
        Assert.Equal(
            [new RankedItem("/artikel", 1), new RankedItem("/download/ki-reifegrad-analyse.pdf", 1)],
            claudeBot.VerifiedPages);
        Assert.Equal(1, claudeBot.VerifiedWithoutPath);
    }

    [Fact]
    public async Task Unverifizierte_Agenten_haben_keine_Seitenliste()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.AiAgents.Single(a => a.Name == "PerplexityBot").VerifiedPages);
        Assert.Empty(result.AiAgents.Single(a => a.Name == "ChatGPT-User").VerifiedPages);
    }

    [Fact]
    public void Ohne_Agenten_ist_die_Liste_leer()
    {
        var aggregator = new AiAgentAggregator(PatternResources.LoadAiAgents(), new AggregationOptions());

        Assert.Empty(aggregator.Aggregate([Entries.Human("198.51.100.1", "2026-08-28T09:00:00Z")]));
    }
}
