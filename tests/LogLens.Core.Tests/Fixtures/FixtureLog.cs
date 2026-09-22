using LogLens.Core;
using LogLens.Core.Aggregation;
using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Core.Tests.Fixtures;

/// <summary>
/// Zugriff auf <c>tests/fixtures/sample-nginx.log</c>. Sollwerte stehen in
/// <c>tests/fixtures/sample-expected.md</c> und werden dort nicht angepasst.
/// </summary>
internal static class FixtureLog
{
    public const string Name = "sample-nginx.log";

    public static string FullPath { get; } =
        Path.Combine(AppContext.BaseDirectory, "fixtures", Name);

    public static string[] Lines { get; } = File.ReadAllLines(FullPath);

    /// <summary>Eine Zeile, 1-basiert wie in sample-expected.md.</summary>
    public static string Line(int lineNumber) => Lines[lineNumber - 1];

    public static LogParseService CreateService()
        => new ServiceCollection().AddLogLensCore().BuildServiceProvider()
            .GetRequiredService<LogParseService>();

    public static Task<ParseResult> ParseAsync(CancellationToken cancellationToken = default)
        => CreateService().ParseAsync(
            LogFileSource.FromText(Name, File.ReadAllText(FullPath)),
            cancellationToken: cancellationToken);

    /// <summary>
    /// IP-Bereiche für die Tests, wie in sample-expected.md festgelegt:
    /// <c>216.73.216.0/22</c> gehört Anthropic, <c>192.0.2.0/24</c> keinem Anbieter.
    /// Die ausgelieferte Liste (Resources/ai-ip-ranges.json) bleibt außen vor, damit
    /// die Erwartungswerte nicht mit jeder Aktualisierung der Quellen wandern.
    /// </summary>
    public static AiIpRangeSet TestIpRanges { get; } = new(
        "2026-09-22",
        [
            new AiIpRangeProvider(
                Provider: "Anthropic",
                Agents: ["ClaudeBot", "Claude-User", "Claude-SearchBot"],
                Verification: AiVerificationMethod.PublishedRanges,
                Source: "https://claude.com/crawling/bots.json",
                SourceUpdated: "2026-08-18T23:56:36Z",
                Retrieved: "2026-09-22",
                Note: null,
                Networks: [IpNetwork.Parse("216.73.216.0/22")]),
        ]);

    /// <summary>
    /// Parsen und Klassifizieren der Fixture. Das Ergebnis wird einmal berechnet und
    /// von allen Erwartungen je Zeile geteilt.
    /// </summary>
    public static Task<ClassificationResult> ClassifyAsync(CancellationToken cancellationToken = default)
        => Classified.Value.WaitAsync(cancellationToken);

    private static readonly Lazy<Task<ClassificationResult>> Classified = new(RunClassificationAsync);

    private static async Task<ClassificationResult> RunClassificationAsync()
    {
        var options = new ClassificationOptions();
        var classifier = new TrafficClassifier(
            PatternResources.LoadAttackPatterns(),
            PatternResources.LoadBotPatterns(),
            PatternResources.LoadAiAgents(),
            TestIpRanges,
            new MonitoringDetector(options),
            options);

        var parsed = await ParseAsync().ConfigureAwait(false);
        return await classifier.ClassifyAsync(parsed).ConfigureAwait(false);
    }

    /// <summary>
    /// Parsen, Klassifizieren und Aggregieren der Fixture – wie die Oberfläche es tut,
    /// aber mit den IP-Bereichen aus <see cref="TestIpRanges"/>.
    /// </summary>
    public static Task<AnalysisResult> AnalyzeAsync(CancellationToken cancellationToken = default)
        => Analyzed.Value.WaitAsync(cancellationToken);

    private static readonly Lazy<Task<AnalysisResult>> Analyzed = new(RunAnalysisAsync);

    private static async Task<AnalysisResult> RunAnalysisAsync()
    {
        var parsed = await ParseAsync().ConfigureAwait(false);
        var classified = await ClassifyAsync().ConfigureAwait(false);

        return new OverviewAggregator(new AggregationOptions())
            .Aggregate([Name], parsed, classified);
    }

    /// <summary>Der klassifizierte Eintrag zu einer Zeilennummer aus sample-expected.md.</summary>
    public static async Task<ClassifiedEntry> ClassifiedLineAsync(
        int lineNumber, CancellationToken cancellationToken = default)
    {
        var result = await ClassifyAsync(cancellationToken).ConfigureAwait(false);
        return result.Entries.Single(e => e.Entry.LineNumber == lineNumber);
    }
}
