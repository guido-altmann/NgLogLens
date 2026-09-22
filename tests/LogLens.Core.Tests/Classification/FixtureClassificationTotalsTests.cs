using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Classification;

/// <summary>
/// Abschnitt „Aggregate" aus sample-expected.md, soweit er aus der Klassifizierung
/// folgt. Die restlichen Kennzahlen kommen mit der Aggregation (M4).
/// </summary>
public sealed class FixtureClassificationTotalsTests
{
    [Fact]
    public async Task Die_vier_Klassen_sind_wie_erwartet_verteilt()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        var byClass = result.Entries
            .GroupBy(e => e.Class)
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(
            new Dictionary<TrafficClass, int>
            {
                [TrafficClass.Attack] = 13,
                [TrafficClass.AiAgent] = 5,
                [TrafficClass.Bot] = 2,
                [TrafficClass.Human] = 9,
            },
            byClass);
    }

    [Fact]
    public async Task Drei_IPs_sind_Scanner_mit_je_drei_Einzelangriffen_und_vier_Anfragen()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["192.0.2.40", "203.0.113.10", "203.0.113.55"],
            result.ScannerIps.Order(StringComparer.Ordinal));

        foreach (var scanner in result.Scanners)
        {
            Assert.Equal(3, scanner.IndividualAttacks);
            Assert.Equal(4, scanner.Requests);
        }
    }

    [Fact]
    public async Task Die_einzelne_Injection_IP_wird_kein_Scanner()
    {
        // 192.0.2.10 (Zeile 23) hat nur einen Einzelangriff (sample-expected.md, Hinweis).
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain("192.0.2.10", result.ScannerIps);
    }

    [Fact]
    public async Task Angriffe_verteilen_sich_wie_erwartet_auf_die_Kategorien()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        var byCategory = result.Entries
            .Where(e => e.AttackCategory is not null)
            .GroupBy(e => e.AttackCategory!)
            .ToDictionary(g => g.Key, g => g.Count());

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
            byCategory);
    }

    [Fact]
    public async Task Sieben_Seitenaufrufe_davon_drei_ohne_Monitoring()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(7, result.Entries.Count(e => e.IsPageView));
        Assert.Equal(3, result.Entries.Count(e => e is { IsPageView: true, IsMonitoringSuspected: false }));
    }

    [Fact]
    public async Task Monitoring_betrifft_genau_das_Sondenpaar()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            ["198.51.100.40", "198.51.101.41"],
            result.MonitoringIps.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Top_Seiten_ergeben_sich_aus_den_normalisierten_Pfaden()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        var topPages = result.Entries
            .Where(e => e.IsPageView)
            .GroupBy(e => PagePath.Normalize(e.Entry.Path))
            .ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(
            new Dictionary<string, int>
            {
                ["/"] = 3,
                ["/download/ki-reifegrad-analyse.pdf"] = 2,
                ["/ki-integration"] = 1,
                ["/impressum"] = 1,
            },
            topPages);
    }

    [Fact]
    public async Task KI_Agenten_verteilen_sich_wie_erwartet_auf_die_Verdikte()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        var byAgent = result.Entries
            .Where(e => e.AgentName is not null)
            .GroupBy(e => e.AgentName!)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(e => e.AiVerdict!.Value).ToDictionary(v => v.Key, v => v.Count()));

        Assert.Equal(
            new Dictionary<AiVerdict, int> { [AiVerdict.Verified] = 3, [AiVerdict.Spoofed] = 1 },
            byAgent["ClaudeBot"]);
        Assert.Equal(new Dictionary<AiVerdict, int> { [AiVerdict.Spoofed] = 1 }, byAgent["ChatGPT-User"]);
        Assert.Equal(new Dictionary<AiVerdict, int> { [AiVerdict.Spoofed] = 1 }, byAgent["Perplexity-User"]);
        Assert.Equal(new Dictionary<AiVerdict, int> { [AiVerdict.Unverified] = 1 }, byAgent["PerplexityBot"]);
        Assert.Equal(new Dictionary<AiVerdict, int> { [AiVerdict.Unverified] = 1 }, byAgent["AgentTrustBot"]);
        Assert.Equal(5, byAgent.Count);
    }
}
