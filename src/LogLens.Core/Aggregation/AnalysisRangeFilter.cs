using LogLens.Core.Classification;
using LogLens.Core.Findings;
using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// Globaler Zeitraumfilter (SPEC 8): rechnet eine fertige Auswertung für einen
/// Ausschnitt neu. Es wird nicht neu klassifiziert – wer im ganzen Log ein Scanner war,
/// bleibt es auch in einem Ausschnitt, sonst hinge die Klasse einer Anfrage davon ab,
/// welchen Zeitraum man gerade ansieht (SPEC 5.2).
/// </summary>
public sealed class AnalysisRangeFilter(
    AnalysisAggregator aggregator,
    FindingEvaluator findings,
    AttackPatternSet attackPatterns)
{
    /// <param name="full">
    /// Die uneingeschränkte Auswertung. Ein bereits eingeschränktes Ergebnis führt zu
    /// falschen Zahlen; die Oberfläche hält deshalb immer das Original vor.
    /// </param>
    public AnalysisResult Apply(AnalysisResult full, DayRange? range)
    {
        ArgumentNullException.ThrowIfNull(full);

        if (range is null || range.IsOpen || full.Period is null || range.Covers(full.Period))
        {
            return full;
        }

        var entries = new EntryFilter { From = range.From, To = range.To }.Apply(full.Entries);
        var errorEntries = full.ErrorEntries
            .Where(e => range.Contains(DateOnly.FromDateTime(e.Timestamp.UtcDateTime)))
            .ToList();

        var classification = full.Classification with
        {
            Entries = entries,
            Scanners = ScannerList.Build(
                entries, full.Classification.ScannerIps, attackPatterns.ReconnaissanceCategory),
        };

        var parseResult = new ParseResult(
            [.. entries.Select(e => e.Entry)],
            errorEntries,
            Restrict(full.Diagnostics, entries, errorEntries, range));

        var result = aggregator.Aggregate(full.FileNames, parseResult, classification);
        return result with { Range = range, Findings = findings.Evaluate(result) };
    }

    /// <summary>
    /// Zählwerte des Einlesens für den Ausschnitt. Was keinen Zeitstempel hat –
    /// Leerzeilen, unbekannte Zeilen, Dateien – bleibt unverändert und gilt weiterhin
    /// für die ganze Datei (siehe <see cref="AnalysisResult.Range"/>).
    /// </summary>
    private static ParseDiagnostics Restrict(
        ParseDiagnostics diagnostics,
        IReadOnlyList<ClassifiedEntry> entries,
        IReadOnlyList<ErrorEntry> errorEntries,
        DayRange range)
        => diagnostics with
        {
            AccessLines = entries.Count(e => !e.Entry.IsTruncated),
            TruncatedAccessLines = entries.Count(e => e.Entry.IsTruncated),
            ErrorLines = errorEntries.Count(e => !e.IsTruncated),
            TruncatedErrorLines = errorEntries.Count(e => e.IsTruncated),
            NotFoundPageErrorLines = diagnostics.NotFoundPageErrorLinesPerDay
                .Where(p => range.Contains(p.Key))
                .Sum(p => p.Value),
            LinesWithoutClientIpHeader = entries.Count(e => !e.Entry.HasClientIpHeader),
        };
}
