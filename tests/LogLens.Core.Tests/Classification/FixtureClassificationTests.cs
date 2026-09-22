using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Classification;

/// <summary>
/// Abschnitt „Klassifizierung je Zeile" aus <c>tests/fixtures/sample-expected.md</c>.
/// Eine Zeile der Tabelle ist ein Testfall. Weicht das Ergebnis ab, wird die Erwartung
/// nicht angepasst, sondern nachgefragt (CLAUDE.md).
/// </summary>
public sealed class FixtureClassificationTests
{
    public static TheoryData<int, TrafficClass, string?, string?, AiVerdict?, bool, bool> ErwartungJeZeile() => new()
    {
        // Zeile, Klasse, Kategorie, Agent, Verdikt, PageView, Monitoring
        { 1, TrafficClass.Attack, "PHP-Webshells", null, null, false, false },
        { 3, TrafficClass.Attack, "WordPress", null, null, false, false },
        { 5, TrafficClass.Attack, "Secrets & Credentials", null, null, false, false },
        { 6, TrafficClass.Attack, "Aufklärung", null, null, false, false },
        { 7, TrafficClass.AiAgent, null, "ClaudeBot", AiVerdict.Verified, false, false },
        { 8, TrafficClass.AiAgent, null, "ClaudeBot", AiVerdict.Verified, false, false },
        { 9, TrafficClass.Human, null, null, null, true, false },
        { 10, TrafficClass.Human, null, null, null, false, false },
        { 11, TrafficClass.Human, null, null, null, true, false },
        { 12, TrafficClass.Human, null, null, null, false, false },
        { 13, TrafficClass.Human, null, null, null, true, true },
        { 14, TrafficClass.Human, null, null, null, true, true },
        { 15, TrafficClass.Human, null, null, null, true, true },
        { 16, TrafficClass.Human, null, null, null, true, true },
        { 17, TrafficClass.Bot, null, null, null, false, false },
        { 18, TrafficClass.Bot, null, null, null, false, false },
        { 19, TrafficClass.Attack, "Vite-Dev-Server", "ChatGPT-User", AiVerdict.Spoofed, false, false },
        { 20, TrafficClass.Attack, "Secrets & Credentials", "Perplexity-User", AiVerdict.Spoofed, false, false },
        { 21, TrafficClass.Attack, "Secrets & Credentials", null, null, false, false },
        { 22, TrafficClass.Attack, "Aufklärung", "ClaudeBot", AiVerdict.Spoofed, false, false },
        { 23, TrafficClass.Attack, "Path Traversal & Injection", null, null, false, false },
        { 24, TrafficClass.AiAgent, null, "PerplexityBot", AiVerdict.Unverified, false, false },
        { 25, TrafficClass.AiAgent, null, "AgentTrustBot", AiVerdict.Unverified, false, false },
        { 26, TrafficClass.Attack, "POST-Proben & Login-Versuche", null, null, false, false },
        { 27, TrafficClass.Attack, "POST-Proben & Login-Versuche", null, null, false, false },
        { 28, TrafficClass.Attack, "POST-Proben & Login-Versuche", null, null, false, false },
        { 29, TrafficClass.Attack, "Aufklärung", null, null, false, false },
        { 30, TrafficClass.Human, null, null, null, true, false },
        { 31, TrafficClass.AiAgent, null, "ClaudeBot", AiVerdict.Verified, false, false },
    };

    [Theory]
    [MemberData(nameof(ErwartungJeZeile))]
    public async Task Jede_Fixture_Zeile_wird_wie_erwartet_eingeordnet(
        int lineNumber,
        TrafficClass expectedClass,
        string? expectedCategory,
        string? expectedAgent,
        AiVerdict? expectedVerdict,
        bool expectedPageView,
        bool expectedMonitoring)
    {
        var entry = await FixtureLog.ClassifiedLineAsync(lineNumber, TestContext.Current.CancellationToken);

        Assert.Equal(expectedClass, entry.Class);
        Assert.Equal(expectedCategory, entry.AttackCategory);
        Assert.Equal(expectedAgent, entry.AgentName);
        Assert.Equal(expectedVerdict, entry.AiVerdict);
        Assert.Equal(expectedPageView, entry.IsPageView);
        Assert.Equal(expectedMonitoring, entry.IsMonitoringSuspected);
    }

    [Theory]
    [MemberData(nameof(ErwartungJeZeile))]
    public async Task Jede_Fixture_Zeile_traegt_einen_lesbaren_Grund(
        int lineNumber,
        TrafficClass expectedClass,
        string? expectedCategory,
        string? expectedAgent,
        AiVerdict? expectedVerdict,
        bool expectedPageView,
        bool expectedMonitoring)
    {
        _ = (expectedClass, expectedCategory, expectedAgent, expectedVerdict, expectedPageView, expectedMonitoring);

        var entry = await FixtureLog.ClassifiedLineAsync(lineNumber, TestContext.Current.CancellationToken);

        Assert.False(string.IsNullOrWhiteSpace(entry.Reason));
    }

    [Fact]
    public async Task Die_Kategorie_haengt_an_der_Klasse_Attack()
    {
        var result = await FixtureLog.ClassifyAsync(TestContext.Current.CancellationToken);

        foreach (var entry in result.Entries)
        {
            Assert.Equal(entry.Class == TrafficClass.Attack, entry.AttackCategory is not null);
            Assert.Equal(entry.AgentName is not null, entry.AiVerdict is not null);
        }
    }

    [Fact]
    public async Task Ein_Grund_nennt_die_Scanner_IP()
    {
        // Zeile 6 ist für sich unauffällig und nur über die Scanner-Regel ein Angriff (SPEC 5.2).
        var entry = await FixtureLog.ClassifiedLineAsync(6, TestContext.Current.CancellationToken);

        Assert.Contains("203.0.113.10", entry.Reason, StringComparison.Ordinal);
    }
}
