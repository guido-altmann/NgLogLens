using LogLens.Core.Aggregation;
using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>
/// Globaler Zeitraumfilter (SPEC 8). Die Sollwerte ergeben sich aus den Zeilen der
/// Fixture, die in den Zeitraum fallen; sample-expected.md bleibt unangetastet.
/// </summary>
public sealed class AnalysisRangeFilterTests
{
    private static readonly DayRange TwoDays = new(new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 28));

    private static AnalysisRangeFilter CreateFilter() => new(
        FixtureLog.CreateAggregator(),
        FixtureLog.CreateFindingEvaluator(),
        PatternResources.LoadAttackPatterns());

    [Fact]
    public async Task Ein_offener_Zeitraum_liefert_die_Auswertung_unveraendert()
    {
        var full = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Same(full, CreateFilter().Apply(full, null));
        Assert.Same(full, CreateFilter().Apply(full, new DayRange(null, null)));
        Assert.Same(full, CreateFilter().Apply(full, new DayRange(new DateOnly(2026, 1, 1), null)));
    }

    [Fact]
    public async Task Nur_Anfragen_der_gewaehlten_Tage_bleiben_uebrig()
    {
        var full = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var restricted = CreateFilter().Apply(full, TwoDays);

        Assert.Equal(TwoDays, restricted.Range);
        Assert.Equal(11, restricted.Requests);
        Assert.Equal(11, restricted.Entries.Count);
        Assert.Equal(4, restricted.Count(TrafficClass.Attack));
        Assert.Equal(3, restricted.Count(TrafficClass.AiAgent));
        Assert.Equal(4, restricted.Count(TrafficClass.Human));
        Assert.Equal(0, restricted.Count(TrafficClass.Bot));
        Assert.Equal(2, restricted.PageViews);
        Assert.Equal(
            [new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 28)],
            restricted.Daily.Select(d => d.Day));
    }

    [Fact]
    public async Task Scanner_behalten_ihre_Einstufung_und_werden_neu_gezaehlt()
    {
        var full = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var restricted = CreateFilter().Apply(full, TwoDays);

        // Die Scanner-Regel gilt für das ganze Log (SPEC 5.2); im Ausschnitt tauchen
        // nur die auf, die dort auch Anfragen haben.
        Assert.Equal(3, full.Scanners.Count);
        var scanner = Assert.Single(restricted.Scanners);
        Assert.Equal("203.0.113.10", scanner.ClientIp);
        Assert.Equal(4, scanner.Requests);
        Assert.Equal(3, scanner.IndividualAttacks);
        // Je ein Treffer in drei Kategorien; bei Gleichstand entscheidet der Name.
        Assert.Equal("PHP-Webshells", scanner.MainCategory);
    }

    [Fact]
    public async Task Zaehlwerte_des_Einlesens_gelten_fuer_den_Ausschnitt()
    {
        var full = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var restricted = CreateFilter().Apply(full, TwoDays);

        Assert.Equal(10, restricted.Diagnostics.AccessLines);
        Assert.Equal(1, restricted.Diagnostics.TruncatedAccessLines);
        Assert.Equal(0, restricted.Diagnostics.TruncatedErrorLines);
        Assert.Equal(2, restricted.Diagnostics.NotFoundPageErrorLines);

        // Ohne Zeitstempel nicht zuzuordnen und deshalb unverändert (SPEC 2.4).
        Assert.Equal(full.Diagnostics.TotalLines, restricted.Diagnostics.TotalLines);
        Assert.Equal(full.Diagnostics.UnknownLines, restricted.Diagnostics.UnknownLines);
    }

    [Fact]
    public async Task Empfehlungen_gelten_fuer_den_Ausschnitt()
    {
        var full = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var restricted = CreateFilter().Apply(full, TwoDays);

        Assert.Equal(
            ["missing-404-page", "client-ip-header", "missing-assets"],
            restricted.Findings.Select(f => f.Id));
    }

    [Fact]
    public async Task Ein_Zeitraum_ohne_Anfragen_ergibt_eine_leere_Auswertung()
    {
        var full = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var restricted = CreateFilter().Apply(
            full, new DayRange(new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 3)));

        Assert.True(restricted.IsEmpty);
        Assert.Null(restricted.Period);
        Assert.Empty(restricted.Entries);
    }
}
