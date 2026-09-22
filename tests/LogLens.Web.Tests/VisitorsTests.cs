using LogLens.Web.Pages;

namespace LogLens.Web.Tests;

/// <summary>Besucher (SPEC 8.3) mit den Sollwerten aus sample-expected.md.</summary>
public sealed class VisitorsTests : DetailPageTestContext
{
    [Fact]
    public void Ohne_Auswertung_steht_ein_Hinweis()
    {
        var cut = Render<Visitors>();

        Assert.Contains("keine Datei geöffnet", cut.Find("[data-testid=no-data]").TextContent);
    }

    [Fact]
    public async Task Zeigt_Top_Seiten_mit_Anteil()
    {
        await LoadFixtureAsync();

        var cut = Render<Visitors>();

        Assert.Equal(
            [
                ["/", "3", "43 %"],
                ["/download/ki-reifegrad-analyse.pdf", "2", "29 %"],
                ["/impressum", "1", "14 %"],
                ["/ki-integration", "1", "14 %"],
            ],
            Rows(cut, "top-pages"));
    }

    [Fact]
    public async Task Referrer_ohne_eigene_Domain_und_Hinweis_auf_die_erkannte_Domain()
    {
        await LoadFixtureAsync();

        var cut = Render<Visitors>();

        Assert.Equal([["google.com", "1"]], Rows(cut, "top-referrers"));
        Assert.Contains("example.de", cut.Find("[data-testid=own-domains]").TextContent);
    }

    [Fact]
    public async Task Netze_sind_maskiert_und_Monitoring_ist_markiert()
    {
        await LoadFixtureAsync();

        var cut = Render<Visitors>();

        var rows = Rows(cut, "top-networks");
        Assert.Equal(["198.51.100.0/24", "5", "7", "3", "Monitoring-Verdacht"], rows[0]);
        Assert.Equal(["198.51.101.0/24", "2", "2", "1", "Monitoring-Verdacht"], rows[1]);
        Assert.DoesNotContain("198.51.100.23", cut.Markup);
    }

    [Fact]
    public async Task Monitoring_Schalter_rechnet_Seitenaufrufe_heraus()
    {
        await LoadFixtureAsync();

        var cut = Render<Visitors>();
        Assert.StartsWith("7 Seitenaufrufe von Menschen aus 2 Netzen", cut.Find("[data-testid=visitor-summary]").TextContent);

        cut.Find("input[data-testid=monitoring-switch]").Change(true);

        // MudSwitch meldet die Änderung asynchron.
        cut.WaitForAssertion(() => Assert.True(Preferences.ExcludeMonitoring));
        cut.WaitForAssertion(() => Assert.StartsWith(
            "3 Seitenaufrufe von Menschen aus 1 Netz", cut.Find("[data-testid=visitor-summary]").TextContent));
        Assert.Equal(3, Rows(cut, "top-pages").Length);
        Assert.Contains("herausgerechnet", cut.Find("[data-testid=monitoring-hint]").TextContent);
    }

    [Fact]
    public async Task Tageszeit_nennt_die_Spitzenstunde_und_hat_eine_Tabelle_mit_24_Stunden()
    {
        await LoadFixtureAsync();

        var cut = Render<Visitors>();

        Assert.Equal(
            "Meiste Anfragen zwischen 08–09 Uhr (UTC): 4.",
            cut.Find("[data-testid=visitor-hours-summary]").TextContent.Trim());
        Assert.Equal(24, cut.FindAll("[data-testid=visitor-hours-table] tbody tr").Count);
    }
}
