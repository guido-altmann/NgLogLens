using LogLens.Core.Findings;
using LogLens.Core.Models;
using LogLens.Core.Tests.Aggregation;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Findings;

/// <summary>
/// Regeln, die in der Fixture bewusst nicht auslösen (SPEC 7). Gearbeitet wird auf
/// gebauten Einträgen, damit die Fixture unangetastet bleibt.
/// </summary>
public sealed class FindingRuleTests
{
    [Fact]
    public void Scan_Burst_greift_ab_der_Schwelle_und_nur_im_Zeitfenster()
    {
        var options = new FindingOptions { ScanBurstRequests = 5, ScanBurstWindow = TimeSpan.FromMinutes(5) };
        var rule = new ScanBurstRule(options);

        var burst = Result(scanners:
        [
            Scanner("203.0.113.10", requests: 6, minutes: 2),
            Scanner("203.0.113.11", requests: 6, minutes: 30),
            Scanner("203.0.113.12", requests: 4, minutes: 1),
        ]);

        var finding = rule.Evaluate(burst);

        Assert.NotNull(finding);
        Assert.Equal(FindingPriority.High, finding.Priority);
        Assert.Equal(["203.0.113.10: 6 Anfragen in 2 Minuten (WordPress)"], finding.Details);
        Assert.Contains("ratelimit", finding.Snippet!.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Burst_kein_Finding()
    {
        var rule = new ScanBurstRule(new FindingOptions());

        Assert.Null(rule.Evaluate(Result(scanners: [Scanner("203.0.113.10", requests: 99, minutes: 1)])));
    }

    [Fact]
    public void Serverfehler_listen_Pfad_und_Status()
    {
        var rule = new ServerErrorRule(new FindingOptions());

        var result = Result(serverErrors:
        [
            new ServerErrorGroup("/api/kontakt", 502, 3, Time(0), Time(5)),
            new ServerErrorGroup("/suche", 500, 1, Time(1), Time(1)),
        ]);

        var finding = rule.Evaluate(result);

        Assert.NotNull(finding);
        Assert.Contains("4 Anfragen", finding.Reason, StringComparison.Ordinal);
        Assert.Equal(
            ["/api/kontakt: Status 502, 3 Anfragen", "/suche: Status 500, 1 Anfrage"],
            finding.Details);
    }

    [Fact]
    public void Oeffentliche_Proxy_Adresse_loest_die_Client_IP_Regel_nicht_aus()
    {
        var rule = new ClientIpHeaderRule();

        var result = Result(proxies:
        [
            new ProxyInstance("10.0.1.9", Time(0), Time(5), 10),
            new ProxyInstance("203.0.113.7", Time(0), Time(5), 10),
        ]);

        Assert.Null(rule.Evaluate(result));
    }

    [Fact]
    public void Fehlendes_X_Forwarded_For_hebt_die_Client_IP_Regel_auf_hoch()
    {
        var rule = new ClientIpHeaderRule();

        var result = Result(proxies: [new ProxyInstance("10.0.1.9", Time(0), Time(5), 10)]) with
        {
            Diagnostics = Diagnostics() with { LinesWithoutClientIpHeader = 4 },
        };

        var finding = rule.Evaluate(result);

        Assert.NotNull(finding);
        Assert.Equal(FindingPriority.High, finding.Priority);
        Assert.Contains("4 Anfragen", finding.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Ausgelieferte_Konfigdatei_wird_als_Fund_benannt()
    {
        var rule = new DotNetConfigRule(
            LogLens.Core.Classification.PatternResources.LoadFindingPatterns(), new FindingOptions());

        var result = Result(entries:
        [
            Entries.Classified(TrafficClass.Bot, "203.0.113.9", "2026-09-01T10:00:00Z", "/appsettings.json", 200),
        ]);

        var finding = rule.Evaluate(result);

        Assert.NotNull(finding);
        Assert.Contains("mit Erfolg beantwortet", finding.Reason, StringComparison.Ordinal);
        Assert.Equal(["/appsettings.json: 1 Anfrage, Status 200"], finding.Details);
    }

    [Fact]
    public void Ein_404_auf_eine_Sourcemap_gilt_nur_beim_Menschen_als_fehlendes_Asset()
    {
        var rule = new MissingAssetsRule(
            LogLens.Core.Classification.PatternResources.LoadFindingPatterns(), new FindingOptions());

        var bot = Result(entries:
        [
            Entries.Classified(TrafficClass.Bot, "203.0.113.9", "2026-09-01T10:00:00Z", "/js/app.js.map", 404),
        ]);
        Assert.Null(rule.Evaluate(bot));

        var human = Result(entries:
        [
            Entries.Classified(TrafficClass.Human, "203.0.113.9", "2026-09-01T10:00:00Z", "/js/app.js.map", 404),
        ]);
        Assert.Equal(["/js/app.js.map: 1 Anfrage"], rule.Evaluate(human)!.Details);
    }

    private static DateTimeOffset Time(int minutes) =>
        new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero).AddMinutes(minutes);

    private static Scanner Scanner(string ip, int requests, int minutes) =>
        new(ip, requests - 1, requests, Time(0), Time(minutes), "WordPress");

    private static ParseDiagnostics Diagnostics() =>
        new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], []);

    /// <summary>Ein Ergebnis mit genau den Feldern, die die jeweilige Regel liest.</summary>
    private static AnalysisResult Result(
        IReadOnlyList<ClassifiedEntry>? entries = null,
        IReadOnlyList<Scanner>? scanners = null,
        IReadOnlyList<ProxyInstance>? proxies = null,
        IReadOnlyList<ServerErrorGroup>? serverErrors = null)
    {
        var classification = new ClassificationResult(
            entries ?? [],
            scanners ?? [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        return new AnalysisResult(
            FileNames: [FixtureLog.Name],
            Diagnostics: Diagnostics(),
            ErrorEntries: [],
            Classification: classification,
            Period: null,
            Classes: [],
            Daily: [],
            DaysWithoutEntries: [],
            PageViews: 0,
            PageViewsWithoutMonitoring: 0,
            VisitorNetworks: 0,
            VisitorNetworksWithoutMonitoring: 0)
        {
            ServerHealth = new ServerHealthStatistics([], 0, serverErrors ?? [], proxies ?? [], []),
        };
    }
}
