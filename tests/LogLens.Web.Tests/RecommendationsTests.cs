using LogLens.Web.Pages;

namespace LogLens.Web.Tests;

/// <summary>
/// Empfehlungen (SPEC 8.7). Erwartet werden die sechs Findings aus
/// <c>sample-expected.md</c>, sortiert nach Priorität.
/// </summary>
public sealed class RecommendationsTests : DetailPageTestContext
{
    [Fact]
    public void Ohne_Auswertung_steht_ein_Hinweis()
    {
        var cut = Render<Recommendations>();

        Assert.Contains("keine Datei geöffnet", cut.Find("[data-testid=no-data]").TextContent);
    }

    [Fact]
    public async Task Zeigt_die_Empfehlungen_der_Fixture_dringendste_zuerst()
    {
        await LoadFixtureAsync();

        var cut = Render<Recommendations>();

        Assert.Equal(
            [
                "finding-dotnet-config",
                "finding-missing-404-page",
                "finding-client-ip-header",
                "finding-llms-txt",
                "finding-agent-discovery",
                "finding-missing-assets",
            ],
            cut.FindAll("[data-testid^=finding-]").Select(e => e.GetAttribute("data-testid")));

        Assert.Equal(
            "6 Empfehlungen: 1 × hoch, 3 × mittel, 2 × niedrig.",
            Compact(cut.Find("[data-testid=findings-summary]").TextContent));
    }

    [Fact]
    public async Task Begruendung_Aufzaehlung_und_Schnipsel_stehen_in_der_Empfehlung()
    {
        await LoadFixtureAsync();

        var cut = Render<Recommendations>();
        var finding = cut.Find("[data-testid=finding-missing-404-page]");

        Assert.Contains("Hoch", cut.Find("[data-testid=finding-dotnet-config] .mud-chip").TextContent);
        Assert.Contains("Mittel", finding.QuerySelector(".mud-chip")!.TextContent);
        Assert.Contains("2 Error-Zeilen", finding.QuerySelector("[data-testid=reason]")!.TextContent);
        Assert.Contains("log_not_found off;", finding.QuerySelector("[data-testid=snippet]")!.TextContent);
        Assert.NotNull(finding.QuerySelector("[data-testid=copy-missing-404-page]"));

        Assert.Equal(
            ["/favicon.ico: 1 Anfrage"],
            cut.FindAll("[data-testid=finding-missing-assets] [data-testid=details] li")
                .Select(li => Compact(li.TextContent)));
    }

    [Fact]
    public async Task Ohne_Auffaelligkeit_bleibt_die_Seite_leer()
    {
        // Eine einzelne Anfrage eines Browsers löst keine Regel aus.
        await LoadAsync(
            """
            203.0.113.7 - - [27/Aug/2026:10:16:58 +0000] "GET / HTTP/1.1" 200 8123 "-" "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36" "198.51.100.23"
            """);

        var cut = Render<Recommendations>();

        Assert.NotNull(cut.Find("[data-testid=no-findings]"));
    }
}
