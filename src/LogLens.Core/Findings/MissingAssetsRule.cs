using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „Fehlende Assets": ein Browser – also ein Mensch (SPEC 5.7) – bekommt für
/// Favicon, Touch-Icon oder eine Sourcemap einen 404. Niedrige Priorität: kosmetisch,
/// aber leicht zu beheben und ein häufiger Grund für Rauschen im Log.
/// </summary>
public sealed class MissingAssetsRule(FindingPatternSet patterns, FindingOptions options) : IFindingRule
{
    public string Id => "missing-assets";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var requests = result.Entries
            .Where(e => e.Class == TrafficClass.Human
                && e.Entry.Status == options.NotFoundStatusCode
                && patterns.MatchesMissingAsset(RequestTarget.From(e.Entry)))
            .ToList();

        if (requests.Count == 0)
        {
            return null;
        }

        var (details, more) = FindingText.Take(
            requests
                .GroupBy(e => e.Entry.Path ?? "-", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}: {FindingText.Requests(g.Count())}"),
            options.MaxDetails);

        return new Finding(
            Id,
            FindingPriority.Low,
            "Fehlende Assets",
            $"Browser holten sich {FindingText.Requests(requests.Count)} auf Dateien, die es nicht gibt.",
            "Favicon und Touch-Icon mit ausliefern; Sourcemaps gehören nicht in ein "
            + "Produktions-Build und sollten beim Veröffentlichen entfallen.")
        {
            DetailsCaption = "Fehlende Dateien",
            Details = details,
            MoreDetails = more,
        };
    }
}
