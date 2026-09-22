using LogLens.Core.Models;
using LogLens.Web.Pages;
using LogLens.Web.Shared;

namespace LogLens.Web.Tests;

/// <summary>
/// Globaler Zeitraumfilter (SPEC 8): er lebt im <c>AnalysisState</c> und wirkt dadurch
/// auf jede Ansicht, nicht nur auf die, in der er bedient wird.
/// </summary>
public sealed class PeriodFilterTests : DetailPageTestContext
{
    private static readonly DayRange TwoDays = new(new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 28));

    [Fact]
    public void Ohne_Auswertung_ist_der_Filter_nicht_sichtbar()
    {
        var cut = Render<PeriodFilter>();

        Assert.Empty(cut.FindAll("[data-testid=period-filter]"));
    }

    [Fact]
    public async Task Mit_Auswertung_erscheint_er_ohne_Zuruecksetzen_Knopf()
    {
        await LoadFixtureAsync();

        var cut = Render<PeriodFilter>();

        Assert.NotNull(cut.Find("[data-testid=period-filter]"));
        Assert.Empty(cut.FindAll("[data-testid=period-reset]"));
    }

    [Fact]
    public async Task Der_Knopf_hebt_die_Einschraenkung_wieder_auf()
    {
        await LoadFixtureAsync();
        var cut = Render<PeriodFilter>();

        State.SetRange(TwoDays);

        cut.WaitForAssertion(() => cut.Find("[data-testid=period-reset]").Click());
        Assert.Null(State.Range);
        Assert.Equal(29, State.Result!.Requests);
    }

    [Fact]
    public async Task Der_Zeitraum_wirkt_auf_die_Uebersicht()
    {
        await LoadFixtureAsync();
        var cut = Render<Overview>();

        State.SetRange(TwoDays);

        cut.WaitForAssertion(() => Assert.Equal(
            "Von 11 Anfragen waren 2 Seitenaufrufe von Menschen.",
            cut.Find("[data-testid=headline]").TextContent.Trim()));
    }

    [Fact]
    public async Task Jede_Ansicht_weist_auf_die_Einschraenkung_hin()
    {
        await LoadFixtureAsync();
        var cut = Render<Overview>();

        State.SetRange(TwoDays);

        cut.WaitForAssertion(() => Assert.Contains(
            "Eingeschränkt auf 27.08.2026 bis 28.08.2026",
            Compact(cut.Find("[data-testid=range-note]").TextContent)));
    }

    [Fact]
    public async Task Der_Zeitraum_wirkt_auf_die_Empfehlungen()
    {
        await LoadFixtureAsync();
        var cut = Render<Recommendations>();

        State.SetRange(TwoDays);

        // Außerhalb des Zeitraums liegen die Anfragen auf llms.txt, .well-known und
        // appsettings – ihre Empfehlungen verschwinden mit ihnen.
        cut.WaitForAssertion(() => Assert.Equal(
            ["finding-missing-404-page", "finding-client-ip-header", "finding-missing-assets"],
            cut.FindAll("[data-testid^=finding-]").Select(e => e.GetAttribute("data-testid"))));
    }

    [Fact]
    public async Task Eine_neue_Datei_setzt_den_Zeitraum_zurueck()
    {
        await LoadFixtureAsync();
        State.SetRange(TwoDays);
        Assert.True(State.IsRestricted);

        await LoadFixtureAsync();

        Assert.Null(State.Range);
        Assert.False(State.IsRestricted);
    }
}
