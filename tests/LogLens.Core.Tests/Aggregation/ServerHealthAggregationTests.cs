using LogLens.Core.Aggregation;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>Server-Zustand (SPEC 6, 8.6). Sollwerte aus <c>tests/fixtures/sample-expected.md</c>.</summary>
public sealed class ServerHealthAggregationTests
{
    [Fact]
    public async Task Statuscodes_der_vollstaendigen_Zeilen()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new StatusCount(200, 14),
                new StatusCount(206, 1),
                new StatusCount(400, 1),
                new StatusCount(404, 10),
                new StatusCount(405, 2),
            ],
            result.ServerHealth.StatusCodes);

        // Die gekürzte Zeile 31 hat keinen Status.
        Assert.Equal(1, result.ServerHealth.RequestsWithoutStatus);
    }

    [Fact]
    public async Task Die_Fixture_hat_keine_Serverfehler()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Empty(result.ServerHealth.ServerErrors);
        Assert.Equal(0, result.ServerHealth.ServerErrorRequests);
    }

    [Fact]
    public async Task Proxy_Instanzen_mit_erstem_und_letztem_Tag()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                ("10.0.1.9", new DateOnly(2026, 8, 27), new DateOnly(2026, 8, 31), 17),
                ("10.0.1.4", new DateOnly(2026, 9, 9), new DateOnly(2026, 9, 13), 5),
                ("10.0.1.8", new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 16), 2),
                ("10.0.1.2", new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), 5),
            ],
            result.ServerHealth.ProxyInstances.Select(p => (p.ProxyIp, p.FirstDay, p.LastDay, p.Requests)));
    }

    [Fact]
    public async Task Uebrige_Error_Zeilen_ohne_404_Folgefehler()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        // Zeilen 2 und 4 sind Folgefehler (SPEC 3), übrig bleibt die gekürzte Zeile 32.
        var entry = Assert.Single(result.ErrorEntries);
        Assert.Equal(32, entry.LineNumber);
        Assert.Equal(1, result.ServerHealth.ErrorLevels.Sum(l => l.Lines));
    }

    [Fact]
    public void Serverfehler_werden_je_Pfad_und_Status_gruppiert()
    {
        var aggregator = new ServerHealthAggregator();

        var health = aggregator.Aggregate(
            [
                Entries.Classified(TrafficClass.Human, "198.51.100.1", "2026-08-28T09:00:00Z", "/kontakt", 502),
                Entries.Classified(TrafficClass.Bot, "192.0.2.77", "2026-08-28T10:00:00Z", "/kontakt", 502),
                Entries.Classified(TrafficClass.Human, "198.51.100.1", "2026-08-29T09:00:00Z", "/api", 500),
                Entries.Classified(TrafficClass.Human, "198.51.100.1", "2026-08-29T09:00:01Z", "/", 200),
            ],
            []);

        Assert.Equal(
            [
                new ServerErrorGroup("/kontakt", 502, 2,
                    DateTimeOffset.Parse("2026-08-28T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse("2026-08-28T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
                new ServerErrorGroup("/api", 500, 1,
                    DateTimeOffset.Parse("2026-08-29T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
                    DateTimeOffset.Parse("2026-08-29T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            ],
            health.ServerErrors);
        Assert.Equal(3, health.ServerErrorRequests);
    }

    [Fact]
    public void Error_Zeilen_werden_je_Level_gezaehlt()
    {
        var aggregator = new ServerHealthAggregator();
        var at = DateTimeOffset.Parse("2026-08-28T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

        var health = aggregator.Aggregate(
            [],
            [
                new ErrorEntry(1, at, TimePrecision.Second, "error", "a", null, null, null, false),
                new ErrorEntry(2, at, TimePrecision.Second, "warn", "b", null, null, null, false),
                new ErrorEntry(3, at, TimePrecision.Second, "error", "c", null, null, null, false),
                new ErrorEntry(4, at, TimePrecision.Day, null, "d", null, null, null, true),
            ]);

        Assert.Equal(
            [new ErrorLevelCount("error", 2), new ErrorLevelCount("warn", 1), new ErrorLevelCount(null, 1)],
            health.ErrorLevels);
    }
}
