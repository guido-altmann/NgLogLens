using LogLens.Core.Aggregation;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>Angriffe (SPEC 6, 8.4). Sollwerte aus <c>tests/fixtures/sample-expected.md</c>.</summary>
public sealed class AttackAggregationTests
{
    [Fact]
    public async Task Kategorien_entsprechen_der_Erwartung()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["Secrets & Credentials"] = 3,
                ["Aufklärung"] = 3,
                ["POST-Proben & Login-Versuche"] = 3,
                ["PHP-Webshells"] = 1,
                ["WordPress"] = 1,
                ["Vite-Dev-Server"] = 1,
                ["Path Traversal & Injection"] = 1,
            },
            result.Attacks.Categories.ToDictionary(c => c.Category, c => c.Requests));
    }

    [Fact]
    public async Task Kategorien_sind_absteigend_sortiert_und_Anteile_summieren_sich_auf_eins()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var counts = result.Attacks.Categories.Select(c => c.Requests).ToList();

        Assert.Equal(counts.OrderByDescending(c => c), counts);
        Assert.Equal(1.0, result.Attacks.Categories.Sum(c => c.Share), 10);
        Assert.Equal(3d / 13d, result.Attacks.Categories[0].Share, 10);
    }

    [Fact]
    public async Task Anfragen_IPs_und_Scanner_Anteil()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(13, result.Attacks.Requests);
        Assert.Equal(4, result.Attacks.DistinctIps);
        Assert.Equal(12, result.Attacks.ScannerRequests);
    }

    [Fact]
    public async Task Scanner_haben_je_drei_Einzelangriffe_und_vier_Anfragen()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Scanners.Count);
        Assert.All(result.Scanners, s =>
        {
            Assert.Equal(3, s.IndividualAttacks);
            Assert.Equal(4, s.Requests);
        });
    }

    [Fact]
    public async Task Angriffe_je_Stunde_in_UTC()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var perHour = result.Attacks.RequestsPerHour;

        Assert.Equal(24, perHour.Count);
        Assert.Equal(4, perHour[3]);
        Assert.Equal(4, perHour[7]);
        Assert.Equal(4, perHour[10]);
        Assert.Equal(1, perHour[13]);
        Assert.Equal(13, perHour.Sum());
    }

    [Fact]
    public void Ohne_Angriffe_ist_alles_leer()
    {
        var statistics = new AttackAggregator().Aggregate(
            [Entries.Human("198.51.100.1", "2026-08-28T09:00:00Z")],
            new HashSet<string>());

        Assert.Equal(0, statistics.Requests);
        Assert.Empty(statistics.Categories);
        Assert.Equal(24, statistics.RequestsPerHour.Count);
        Assert.All(statistics.RequestsPerHour, h => Assert.Equal(0, h));
    }
}
