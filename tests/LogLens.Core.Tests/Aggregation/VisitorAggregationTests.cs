using LogLens.Core.Aggregation;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>
/// Besucher (SPEC 6, 8.3). Sollwerte aus <c>tests/fixtures/sample-expected.md</c>,
/// Abschnitt „Aggregate": Top-Seiten, Netze, Referrer ohne eigene Domain.
/// </summary>
public sealed class VisitorAggregationTests
{
    [Fact]
    public async Task Top_Seiten_entsprechen_der_Erwartung()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                new RankedItem("/", 3),
                new RankedItem("/download/ki-reifegrad-analyse.pdf", 2),
                new RankedItem("/impressum", 1),
                new RankedItem("/ki-integration", 1),
            ],
            result.Visitors.TopPages);
        Assert.Equal(4, result.Visitors.DistinctPages);
        Assert.Equal(7, result.Visitors.PageViews);
    }

    [Fact]
    public async Task Ohne_Monitoring_bleiben_drei_Seitenaufrufe()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var visitors = result.VisitorsFor(excludeMonitoring: true);

        Assert.Same(result.VisitorsWithoutMonitoring, visitors);
        Assert.Equal(3, visitors.PageViews);
        Assert.Equal(
            [new RankedItem("/", 1), new RankedItem("/impressum", 1), new RankedItem("/ki-integration", 1)],
            visitors.TopPages);
    }

    [Fact]
    public async Task Referrer_ohne_eigene_Domain_ist_nur_google_com()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        // example.de kommt aus dem host-Feld der Error-Zeilen, www.example.de gehört dazu.
        Assert.Contains("example.de", result.OwnDomains);
        Assert.Equal([new RankedItem("google.com", 1)], result.Visitors.TopReferrers);
        Assert.Equal([new RankedItem("google.com", 1)], result.VisitorsWithoutMonitoring.TopReferrers);
    }

    [Fact]
    public async Task Netze_mit_Besuchern_sind_maskiert_und_nach_Seitenaufrufen_sortiert()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Visitors.Networks);
        Assert.Equal(
            [
                new NetworkActivity("198.51.100.0/24", PageViews: 5, Requests: 7, DistinctIps: 3, HasMonitoring: true),
                new NetworkActivity("198.51.101.0/24", PageViews: 2, Requests: 2, DistinctIps: 1, HasMonitoring: true),
            ],
            result.Visitors.TopNetworks);
    }

    [Fact]
    public async Task Ohne_Monitoring_bleibt_ein_Netz()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, result.VisitorsWithoutMonitoring.Networks);
        Assert.Equal(
            [new NetworkActivity("198.51.100.0/24", PageViews: 3, Requests: 5, DistinctIps: 2, HasMonitoring: false)],
            result.VisitorsWithoutMonitoring.TopNetworks);
    }

    [Fact]
    public async Task Anfragen_von_Menschen_je_Stunde_in_UTC()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        var perHour = result.Visitors.RequestsPerHour;

        Assert.Equal(24, perHour.Count);
        Assert.Equal(1, perHour[7]);
        Assert.Equal(4, perHour[8]);
        Assert.Equal(4, perHour[13]);
        Assert.Equal(9, perHour.Sum());
        Assert.Equal(5, result.VisitorsWithoutMonitoring.RequestsPerHour.Sum());
        Assert.Equal(0, result.VisitorsWithoutMonitoring.RequestsPerHour[13]);
    }

    [Fact]
    public void Tagesgenaue_Zeitstempel_fehlen_in_der_Stundenreihe_aber_nicht_in_den_Anfragen()
    {
        var aggregator = new VisitorAggregator(new AggregationOptions());

        var visitors = aggregator.Aggregate(
            [
                Entries.Human("198.51.100.1", "2026-08-28T00:00:00Z", precision: TimePrecision.Day),
                Entries.Human("198.51.100.1", "2026-08-28T09:00:00Z", precision: TimePrecision.Hour),
            ],
            excludeMonitoring: false,
            ownDomains: []);

        Assert.Equal(2, visitors.Requests);
        Assert.Equal(1, visitors.RequestsPerHour.Sum());
        Assert.Equal(1, visitors.RequestsPerHour[9]);
    }

    [Fact]
    public void Referrer_wird_auf_den_Host_ohne_www_verdichtet_und_eigene_Subdomains_fallen_weg()
    {
        var aggregator = new VisitorAggregator(new AggregationOptions());

        var visitors = aggregator.Aggregate(
            [
                Entries.Human("198.51.100.1", "2026-08-28T09:00:00Z", referer: "https://www.google.com/search?q=x"),
                Entries.Human("198.51.100.2", "2026-08-28T09:01:00Z", referer: "https://google.com/"),
                Entries.Human("198.51.100.3", "2026-08-28T09:02:00Z", referer: "https://duckduckgo.com/"),
                Entries.Human("198.51.100.4", "2026-08-28T09:03:00Z", referer: "https://blog.example.de/artikel"),
                Entries.Human("198.51.100.5", "2026-08-28T09:04:00Z", referer: "-"),
                Entries.Human("198.51.100.6", "2026-08-28T09:05:00Z", referer: "kein-uri"),
                Entries.Human("198.51.100.7", "2026-08-28T09:06:00Z", referer: "https://notexample.de/"),
            ],
            excludeMonitoring: false,
            ownDomains: ["example.de"]);

        Assert.Equal(
            [new RankedItem("google.com", 2), new RankedItem("duckduckgo.com", 1), new RankedItem("notexample.de", 1)],
            visitors.TopReferrers);
    }

    [Fact]
    public void Top_Listen_werden_auf_die_konfigurierte_Laenge_gekuerzt()
    {
        var aggregator = new VisitorAggregator(new AggregationOptions { TopListSize = 2 });

        var visitors = aggregator.Aggregate(
            [
                Entries.Human("198.51.100.1", "2026-08-28T09:00:00Z", path: "/a"),
                Entries.Human("198.51.100.1", "2026-08-28T09:01:00Z", path: "/a"),
                Entries.Human("198.51.100.1", "2026-08-28T09:02:00Z", path: "/b"),
                Entries.Human("198.51.100.1", "2026-08-28T09:03:00Z", path: "/c"),
            ],
            excludeMonitoring: false,
            ownDomains: []);

        Assert.Equal([new RankedItem("/a", 2), new RankedItem("/b", 1)], visitors.TopPages);
        Assert.Equal(3, visitors.DistinctPages);
    }
}
