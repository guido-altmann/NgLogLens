using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using LogLens.Web.Pages;
using LogLens.Web.Services;
using ApexCharts;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Web.Tests;

/// <summary>
/// Übersicht (SPEC 8.2). Die Seite rechnet nichts selbst; geprüft wird, dass sie die
/// Zahlen der Aggregation unverändert zeigt.
/// </summary>
public sealed class OverviewTests : MudBlazorTestContext
{
    private AnalysisState State => Services.GetRequiredService<AnalysisState>();

    public OverviewTests()
    {
        Services.AddLogLensCore();
        Services.AddApexCharts();
        Services.AddSingleton<AnalysisState>();
    }

    [Fact]
    public void Ohne_Auswertung_steht_ein_Hinweis_mit_Link_auf_die_Upload_Seite()
    {
        var cut = Render<Overview>();

        Assert.Contains("keine Datei geöffnet", cut.Find("[data-testid=no-data]").TextContent);
        Assert.Contains(
            cut.FindAll("[data-testid=no-data] a").Select(a => a.GetAttribute("href")), h => h == "/");
    }

    [Fact]
    public async Task Zeigt_Kernaussage_Zeitraum_und_Anteile_der_vier_Klassen()
    {
        State.Set(await AnalyzeAsync(Xunit.TestContext.Current.CancellationToken));

        var cut = Render<Overview>();

        Assert.Equal(
            "Von 5 Anfragen waren 2 Seitenaufrufe von Menschen.",
            cut.Find("[data-testid=headline]").TextContent.Trim());

        var subline = cut.Find("[data-testid=subline]").TextContent;
        Assert.Contains("27.08.2026 bis 29.08.2026 (3 Tage)", subline);
        Assert.Contains("2 Anfragen (40,0 %) waren Angriffe", subline);

        var legend = cut.Find("[data-testid=class-legend]").TextContent;
        Assert.Contains("Menschen", legend);
        Assert.Contains("KI-Agenten", legend);
        Assert.Contains("Bots", legend);
        Assert.Contains("Angriffe", legend);
    }

    [Fact]
    public async Task Tabellen_Alternative_zeigt_die_lueckenlose_Tagesreihe()
    {
        State.Set(await AnalyzeAsync(Xunit.TestContext.Current.CancellationToken));

        var cut = Render<Overview>();

        var days = cut.FindAll("[data-testid=daily-table-panel] tbody tr th")
            .Select(cell => cell.TextContent.Trim())
            .ToArray();

        // Der 28.08. hat keinen Eintrag und steht trotzdem in der Reihe (SPEC 6).
        Assert.Equal(["27.08.2026", "28.08.2026", "29.08.2026"], days);
        Assert.Contains("1 Tage im Zeitraum", cut.Find("[data-testid=empty-days]").TextContent);
    }

    [Fact]
    public async Task Umschalter_Nur_Menschen_blendet_die_uebrigen_Klassen_aus()
    {
        State.Set(await AnalyzeAsync(Xunit.TestContext.Current.CancellationToken));

        var cut = Render<Overview>();

        cut.FindAll(".mud-toggle-item").Single(i => i.TextContent.Contains("Nur Menschen")).Click();

        // MudToggleGroup meldet die Auswahl asynchron; ohne Warten war der Test sporadisch rot.
        cut.WaitForAssertion(() => Assert.Equal(
            ["Tag", "Menschen", "Summe"],
            cut.FindAll("[data-testid=daily-table-panel] thead th").Select(cell => cell.TextContent.Trim())));
    }

    [Fact]
    public async Task Datei_ohne_erkennbare_Anfrage_bekommt_eine_eigene_Meldung()
    {
        State.Set(await AnalyzeAsync(
            "kein Logformat, nur Text", Xunit.TestContext.Current.CancellationToken));

        var cut = Render<Overview>();

        var message = cut.Find("[data-testid=empty-data]").TextContent;

        Assert.Contains("keine einzige Anfrage", message);
        Assert.Contains("1 Zeilen wurden gelesen, davon 1 nicht erkannt", Compact(message));
    }

    private static string Compact(string text) => string.Join(' ', text.Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// Fünf Anfragen an drei aufeinanderfolgenden Tagen, eine Klasse je Zeile,
    /// der 28.08. bleibt leer. IPs aus RFC 5737.
    /// </summary>
    private const string Log = """
        10.0.1.9 - - [27/Aug/2026:10:16:59 +0000] "GET /.env HTTP/1.1" 404 555 "-" "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0" "203.0.113.10"
        10.0.1.9 - - [27/Aug/2026:10:17:00 +0000] "POST /signin HTTP/1.1" 405 157 "-" "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0.0.0" "203.0.113.11"
        10.0.1.9 - - [27/Aug/2026:11:00:00 +0000] "GET /sitemap.xml HTTP/1.1" 200 1200 "-" "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)" "203.0.113.20"
        10.0.1.9 - - [29/Aug/2026:08:15:44 +0000] "GET / HTTP/1.1" 200 8123 "-" "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15" "198.51.100.23"
        10.0.1.9 - - [29/Aug/2026:08:16:30 +0000] "GET /impressum.html HTTP/1.1" 200 6400 "-" "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15" "198.51.100.23"
        """;

    private Task<AnalysisResult> AnalyzeAsync(CancellationToken cancellationToken) =>
        AnalyzeAsync(Log, cancellationToken);

    private Task<AnalysisResult> AnalyzeAsync(string content, CancellationToken cancellationToken) =>
        Services.GetRequiredService<LogAnalysisPipeline>()
            .AnalyzeAsync(LogFileSource.FromText("test.log", content), cancellationToken: cancellationToken);
}
