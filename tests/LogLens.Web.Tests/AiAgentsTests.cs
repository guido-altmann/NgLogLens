using LogLens.Web.Pages;

namespace LogLens.Web.Tests;

/// <summary>KI-Agenten (SPEC 8.5) mit den Sollwerten aus sample-expected.md.</summary>
public sealed class AiAgentsTests : DetailPageTestContext
{
    [Fact]
    public async Task Tabelle_zeigt_Verdikte_je_Agent()
    {
        await LoadFixtureAsync();

        var cut = Render<AiAgents>();

        Assert.Equal(
            [
                ["ClaudeBot", "Anthropic", "3", "0", "1", "4"],
                ["AgentTrustBot", "AgentTrust", "0", "1", "0", "1"],
                ["ChatGPT-User", "OpenAI", "0", "0", "1", "1"],
                ["Perplexity-User", "Perplexity", "0", "0", "1", "1"],
                ["PerplexityBot", "Perplexity", "0", "1", "0", "1"],
            ],
            Rows(cut, "agents"));
    }

    [Fact]
    public async Task Legende_traegt_die_Summen_je_Verdikt()
    {
        await LoadFixtureAsync();

        var cut = Render<AiAgents>();

        var legend = Compact(cut.Find("[data-testid=verdict-legend]").TextContent);
        Assert.Contains("Verifiziert 3", legend);
        Assert.Contains("Unverifiziert 2", legend);
        Assert.Contains("Getarnt 3", legend);
        Assert.Equal(
            "8 Anfragen von 5 KI-Agenten: 3 verifiziert, 3 waren Angriffe mit falscher Kennung.",
            cut.Find("[data-testid=agent-summary]").TextContent.Trim());
    }

    [Fact]
    public async Task Seiten_echter_Agenten_samt_gekuerzter_Zeile()
    {
        await LoadFixtureAsync();

        var cut = Render<AiAgents>();

        var pages = Compact(cut.Find("[data-testid=verified-pages]").TextContent);
        Assert.Contains("ClaudeBot – 3 verifizierte Abrufe", pages);
        Assert.Contains("/artikel 1", pages);
        Assert.Contains("/download/ki-reifegrad-analyse.pdf 1", pages);
        Assert.Contains("ohne Pfad (gekürzte Zeile) 1", pages);
        Assert.DoesNotContain("/llms.txt", pages);
    }

    [Fact]
    public async Task Hinweis_auf_den_Stand_der_IP_Bereiche()
    {
        await LoadFixtureAsync();

        var cut = Render<AiAgents>();

        Assert.Contains("Stand 2026-09-22", cut.Find("[data-testid=ip-range-note]").TextContent);
    }
}
