using LogLens.Core.Aggregation;
using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Core.Parsing;

namespace LogLens.Core;

/// <summary>Stufe der Pipeline, in der die Auswertung gerade steckt.</summary>
public enum AnalysisStage
{
    Reading,
    Classifying,
    Aggregating,
    Done,
}

/// <summary>
/// Fortschritt der gesamten Auswertung. <paramref name="Total"/> ist 0, solange die
/// Gesamtmenge noch unbekannt ist – beim Lesen kennt niemand die Zeilenzahl vorab.
/// </summary>
public sealed record AnalysisProgress(AnalysisStage Stage, int Done, int Total, string? FileName)
{
    public bool IsIndeterminate => Total <= 0;

    public double Percent => Total <= 0 ? 0 : Math.Clamp(Done * 100d / Total, 0, 100);
}

/// <summary>
/// Fassade über die vier Stufen Parse → Dedupe → Classify → Aggregate (CLAUDE.md).
/// Die Oberfläche kennt nur diese Klasse; jede Stufe bleibt einzeln testbar.
/// </summary>
public sealed class LogAnalysisPipeline(
    LogParseService parser,
    TrafficClassifier classifier,
    AnalysisAggregator aggregator)
{
    public Task<AnalysisResult> AnalyzeAsync(
        LogFileSource file,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => AnalyzeAsync([file], progress, cancellationToken);

    public async Task<AnalysisResult> AnalyzeAsync(
        IReadOnlyList<LogFileSource> files,
        IProgress<AnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var parsed = await parser.ParseAsync(
            files,
            Relay<ParseProgress>(progress, p =>
                new AnalysisProgress(AnalysisStage.Reading, p.LinesRead, 0, p.FileName)),
            cancellationToken).ConfigureAwait(false);

        var classified = await classifier.ClassifyAsync(
            parsed,
            Relay<ClassifyProgress>(progress, p =>
                new AnalysisProgress(AnalysisStage.Classifying, p.EntriesDone, p.EntriesTotal, null)),
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new AnalysisProgress(AnalysisStage.Aggregating, 0, 0, null));

        // Vor der letzten, rein rechnenden Stufe den UI-Thread noch einmal freigeben,
        // damit der Fortschritt auch sichtbar wird (CLAUDE.md, WASM-Fallstricke).
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var result = aggregator.Aggregate(
            [.. files.Select(f => f.Name)],
            parsed,
            classified);

        progress?.Report(new AnalysisProgress(AnalysisStage.Done, result.Requests, result.Requests, null));

        return result;
    }

    /// <summary>
    /// Meldungen einer Stufe unverändert weiterreichen. Bewusst synchron: das Marshalling
    /// auf den UI-Thread macht der Empfänger, sonst käme der Fortschritt nach dem Ergebnis an.
    /// </summary>
    private static IProgress<TStage>? Relay<TStage>(
        IProgress<AnalysisProgress>? target,
        Func<TStage, AnalysisProgress> map)
        => target is null ? null : new ProgressRelay<TStage>(target, map);

    private sealed class ProgressRelay<TStage>(
        IProgress<AnalysisProgress> target,
        Func<TStage, AnalysisProgress> map) : IProgress<TStage>
    {
        public void Report(TStage value) => target.Report(map(value));
    }
}
