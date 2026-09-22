using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Findings;

/// <summary>
/// Abschnitt „Findings" aus sample-expected.md (SPEC 7). Erwartet sind genau sechs
/// Empfehlungen; Scan-Bursts und Serverfehler dürfen nicht auftauchen.
/// </summary>
public sealed class FixtureFindingTests
{
    [Fact]
    public async Task Genau_die_erwarteten_Empfehlungen_entstehen()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "dotnet-config",
                "missing-404-page",
                "client-ip-header",
                "llms-txt",
                "agent-discovery",
                "missing-assets",
            ],
            result.Findings.Select(f => f.Id));
    }

    [Fact]
    public async Task Dringendste_zuerst()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                FindingPriority.High,
                FindingPriority.Medium,
                FindingPriority.Medium,
                FindingPriority.Medium,
                FindingPriority.Low,
                FindingPriority.Low,
            ],
            result.Findings.Select(f => f.Priority));
    }

    [Fact]
    public async Task Keine_Scan_Bursts_und_keine_Serverfehler()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(result.Findings, f => f.Id is "scan-burst" or "server-errors");
    }

    [Fact]
    public async Task Fehlende_404_Seite_zaehlt_beide_Folgefehler()
    {
        var finding = await FindingAsync("missing-404-page");

        Assert.Equal("Fehlende 404-Seite", finding.Title);
        Assert.Contains("2 Error-Zeilen", finding.Reason, StringComparison.Ordinal);
        Assert.Contains("404.html", finding.Reason, StringComparison.Ordinal);
        Assert.Contains("log_not_found off;", finding.Snippet!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Client_IP_nur_im_Header_nennt_alle_Proxy_Adressen()
    {
        var finding = await FindingAsync("client-ip-header");

        Assert.Equal(FindingPriority.Medium, finding.Priority);
        Assert.Contains("10.0.1.9", finding.Reason, StringComparison.Ordinal);
        Assert.Contains("real_ip_header X-Forwarded-For;", finding.Snippet!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Llms_txt_zaehlt_beide_Anfragen_und_nennt_die_Agenten()
    {
        var finding = await FindingAsync("llms-txt");

        Assert.Contains("2 Anfragen", finding.Reason, StringComparison.Ordinal);
        Assert.Contains("ClaudeBot", finding.Reason, StringComparison.Ordinal);
        Assert.Contains("PerplexityBot", finding.Reason, StringComparison.Ordinal);
        Assert.Equal(["/llms.txt: 2 Anfragen"], finding.Details);

        // Die eigene Domain stammt aus den Error-Zeilen der Fixture (SPEC 6).
        Assert.Contains("# example.de", finding.Snippet!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Agent_Discovery_zaehlt_nur_die_well_known_Dateien()
    {
        var finding = await FindingAsync("agent-discovery");

        Assert.Equal(FindingPriority.Low, finding.Priority);
        Assert.Contains("1 Anfrage", finding.Reason, StringComparison.Ordinal);
        Assert.Equal(["/.well-known/agents.json: 1 Anfrage"], finding.Details);
    }

    [Fact]
    public async Task Konfigdatei_Fund_nennt_den_Pfad_und_bleibt_ohne_Treffer_mit_200()
    {
        var finding = await FindingAsync("dotnet-config");

        Assert.Equal(FindingPriority.High, finding.Priority);
        Assert.Equal(["/appsettings.Production.json: 1 Anfrage, Status 404"], finding.Details);
        Assert.DoesNotContain("mit Erfolg beantwortet", finding.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fehlende_Assets_meldet_nur_das_Favicon_des_Browsers()
    {
        var finding = await FindingAsync("missing-assets");

        Assert.Equal(["/favicon.ico: 1 Anfrage"], finding.Details);
    }

    private static async Task<Finding> FindingAsync(string id)
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);
        return result.Findings.Single(f => f.Id == id);
    }
}
