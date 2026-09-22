using LogLens.Core.Aggregation;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>
/// Sollwerte aus <c>tests/fixtures/sample-expected.md</c>, Abschnitt „Aggregate".
/// </summary>
public sealed class OverviewAggregationTests
{
    [Fact]
    public async Task Zeitraum_reicht_vom_27_August_bis_22_September()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(result.Period);
        Assert.Equal(new DateOnly(2026, 8, 27), result.Period.FirstDay);
        Assert.Equal(new DateOnly(2026, 9, 22), result.Period.LastDay);
        Assert.Equal(27, result.Period.Days);
    }

    [Fact]
    public async Task Anfragen_gesamt_sind_29()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(29, result.Requests);
        Assert.Equal(29, result.Entries.Count);
    }

    [Theory]
    [InlineData(TrafficClass.Attack, 13)]
    [InlineData(TrafficClass.AiAgent, 5)]
    [InlineData(TrafficClass.Bot, 2)]
    [InlineData(TrafficClass.Human, 9)]
    public async Task Klassen_haben_die_erwarteten_Anzahlen(TrafficClass trafficClass, int expected)
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Count(trafficClass));
    }

    [Fact]
    public async Task Anteile_der_Klassen_summieren_sich_auf_eins()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(4, result.Classes.Count);
        Assert.Equal(1.0, result.Classes.Sum(c => c.Share), 10);
        Assert.Equal(13d / 29d, result.Classes.Single(c => c.Class == TrafficClass.Attack).Share, 10);
    }

    [Fact]
    public async Task Seitenaufrufe_sind_sieben_und_ohne_Monitoring_drei()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, result.PageViews);
        Assert.Equal(3, result.PageViewsWithoutMonitoring);
    }

    [Fact]
    public async Task Netze_mit_Seitenaufrufen_sind_zwei_und_ohne_Monitoring_eins()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.VisitorNetworks);
        Assert.Equal(1, result.VisitorNetworksWithoutMonitoring);
    }

    [Fact]
    public async Task Tagesreihe_ist_lueckenlos_und_zaehlt_leere_Tage()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(27, result.Daily.Count);
        Assert.Equal(
            Enumerable.Range(0, 27).Select(i => new DateOnly(2026, 8, 27).AddDays(i)),
            result.Daily.Select(d => d.Day));

        // Einträge gibt es an 9 Tagen (27./28./30./31.08., 09./13./16./21./22.09.).
        Assert.Equal(18, result.DaysWithoutEntries.Count);
        Assert.All(result.DaysWithoutEntries, day =>
            Assert.Equal(0, result.Daily.Single(d => d.Day == day).Total));
    }

    [Theory]
    [InlineData(2026, 8, 27, 0, 0, 0, 4)]
    [InlineData(2026, 8, 28, 4, 3, 0, 0)]
    [InlineData(2026, 8, 29, 0, 0, 0, 0)]
    [InlineData(2026, 8, 30, 4, 0, 0, 0)]
    [InlineData(2026, 8, 31, 0, 0, 2, 0)]
    [InlineData(2026, 9, 9, 0, 0, 0, 4)]
    [InlineData(2026, 9, 13, 0, 0, 0, 1)]
    [InlineData(2026, 9, 16, 0, 2, 0, 0)]
    [InlineData(2026, 9, 21, 0, 0, 0, 4)]
    [InlineData(2026, 9, 22, 1, 0, 0, 0)]
    public async Task Tagesreihe_zaehlt_je_Klasse(
        int year, int month, int day, int human, int aiAgent, int bot, int attack)
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var entry = result.Daily.Single(d => d.Day == new DateOnly(year, month, day));

        Assert.Equal(new DailyTraffic(entry.Day, human, aiAgent, bot, attack), entry);
        Assert.Equal(human + aiAgent + bot, entry.WithoutAttacks);
    }

    [Fact]
    public async Task Tagessummen_entsprechen_den_Klassensummen()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(result.Requests, result.Daily.Sum(d => d.Total));
        Assert.Equal(result.Count(TrafficClass.Human), result.Daily.Sum(d => d.Human));
        Assert.Equal(result.Count(TrafficClass.Attack), result.Daily.Sum(d => d.Attack));
    }

    [Fact]
    public async Task Diagnose_und_Scanner_bleiben_im_Ergebnis_erhalten()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Diagnostics.NotFoundPageErrorLines);
        Assert.Equal(1, result.Diagnostics.TruncatedAccessLines);
        Assert.Equal(0, result.Diagnostics.UnknownLines);
        Assert.Equal(
            new[] { "192.0.2.40", "203.0.113.10", "203.0.113.55" },
            result.Scanners.Select(s => s.ClientIp).Order(StringComparer.Ordinal));
        Assert.Equal([FixtureLog.Name], result.FileNames);
    }

    [Fact]
    public void Leeres_Log_liefert_ein_leeres_Ergebnis_ohne_Zeitraum()
    {
        var aggregator = new OverviewAggregator(new AggregationOptions());

        var result = aggregator.Aggregate(
            ["leer.log"],
            new ParseResult([], [], new ParseDiagnostics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], [])),
            new ClassificationResult([], [], new HashSet<string>(), new HashSet<string>()));

        Assert.True(result.IsEmpty);
        Assert.Null(result.Period);
        Assert.Empty(result.Daily);
        Assert.Empty(result.DaysWithoutEntries);
        Assert.All(result.Classes, c => Assert.Equal(0, c.Requests));
        Assert.All(result.Classes, c => Assert.Equal(0, c.Share));
    }
}
