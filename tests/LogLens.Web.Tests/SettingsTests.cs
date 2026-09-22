using LogLens.Core;
using LogLens.Web.Pages;
using LogLens.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Web.Tests;

/// <summary>
/// Einstellungen (SPEC 8.9): Werte ändern, speichern, zurücksetzen – und die geöffnete
/// Datei dabei neu auswerten.
/// </summary>
public sealed class SettingsTests : DetailPageTestContext
{
    private ClassificationOptions Classification => Services.GetRequiredService<ClassificationOptions>();

    private SettingsService SettingsService => Services.GetRequiredService<SettingsService>();

    [Fact]
    public void Die_Felder_zeigen_die_Vorgaben_des_Kerns()
    {
        var cut = Render<Settings>();

        Assert.Equal("3", Value(cut, "scanner-threshold"));
        Assert.Equal("2", Value(cut, "monitoring-window"));
        Assert.Equal("100", Value(cut, "burst-requests"));
        Assert.Equal("200", Value(cut, "max-file-size"));
    }

    [Fact]
    public void Speichern_uebernimmt_die_Werte_und_legt_sie_im_Browser_ab()
    {
        var cut = Render<Settings>();

        Input(cut, "scanner-threshold").Change("7");
        Input(cut, "own-domains").Change("example.de");
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Equal(7, Classification.ScannerThreshold));
        Assert.Equal(["example.de"], Services.GetRequiredService<AggregationOptions>().OwnDomains);
        Assert.Equal(7, SettingsService.Current.ScannerThreshold);

        var stored = JSInterop.Invocations["localStorage.setItem"].Single();
        Assert.Equal(SettingsService.StorageKey, stored.Arguments[0]);
        Assert.Contains("\"scannerThreshold\":7", (string)stored.Arguments[1]!, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_ungueltiges_Netz_verhindert_das_Speichern()
    {
        var cut = Render<Settings>();

        Input(cut, "monitoring-networks").Change("198.51.100.7\nkein-netz");

        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid=save]").HasAttribute("disabled")));
        Assert.Contains("kein-netz", cut.Markup, StringComparison.Ordinal);

        Input(cut, "monitoring-networks").Change("198.51.100.7\n203.0.113.0/24");
        cut.WaitForAssertion(() => Assert.False(cut.Find("[data-testid=save]").HasAttribute("disabled")));
    }

    [Fact]
    public void Zuruecksetzen_stellt_die_Vorgaben_wieder_her()
    {
        var cut = Render<Settings>();

        Input(cut, "scanner-threshold").Change("9");
        cut.Find("[data-testid=save]").Click();
        cut.WaitForAssertion(() => Assert.Equal(9, Classification.ScannerThreshold));

        cut.Find("[data-testid=reset]").Click();

        cut.WaitForAssertion(() => Assert.Equal(3, Classification.ScannerThreshold));
        JSInterop.VerifyInvoke("localStorage.removeItem");
    }

    [Fact]
    public async Task Eine_geaenderte_Schwelle_wertet_die_geoeffnete_Datei_neu_aus()
    {
        await LoadFixtureAsync();
        Assert.Equal(3, State.Result!.Scanners.Count);

        var cut = Render<Settings>();
        Input(cut, "scanner-threshold").Change("1000");
        cut.Find("[data-testid=save]").Click();

        // Ohne Scanner-Regel bleiben nur die Einzelangriffe übrig (SPEC 5.1, 5.2).
        cut.WaitForAssertion(() => Assert.Empty(State.Result!.Scanners));
        Assert.Equal(10, State.Result!.Count(LogLens.Core.Models.TrafficClass.Attack));
        Assert.Equal(29, State.Result!.Requests);
    }

    [Fact]
    public void Der_Maskierungsschalter_wirkt_sofort_auf_die_Anzeige()
    {
        var cut = Render<Settings>();

        Input(cut, "show-full-ips").Change(true);
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.True(Preferences.ShowFullVisitorIps));
        Assert.Equal("198.51.100.23", Services.GetRequiredService<IpDisplay>().Format("198.51.100.23", false));
    }

    private static string Value(IRenderedComponent<Settings> cut, string testId) =>
        Input(cut, testId).GetAttribute("value") ?? string.Empty;

    private static AngleSharp.Dom.IElement Input(IRenderedComponent<Settings> cut, string testId)
    {
        var element = cut.Find($"[data-testid={testId}]");
        return element.TagName is "INPUT" or "TEXTAREA" ? element : element.QuerySelector("input, textarea")!;

    }
}
