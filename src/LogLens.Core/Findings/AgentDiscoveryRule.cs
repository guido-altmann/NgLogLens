using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „Agent-Discovery-Nachfrage": Anfragen auf die Agent-Dateien unter
/// <c>.well-known</c>. Sie sind kein Angriff (SPEC 5.4), sondern ein Hinweis darauf,
/// dass Agenten die Seite maschinenlesbar erschließen wollen. Niedrige Priorität.
/// </summary>
public sealed class AgentDiscoveryRule(
    AttackPatternSet attackPatterns,
    FindingPatternSet patterns,
    FindingOptions options) : IFindingRule
{
    public string Id => "agent-discovery";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var wellKnown = attackPatterns.AgentDiscoveryPaths
            .Where(patterns.IsWellKnown)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var requests = result.Entries
            .Where(e => e.Entry.Path is { } path && wellKnown.Contains(path))
            .ToList();

        if (requests.Count == 0)
        {
            return null;
        }

        var (details, more) = FindingText.Take(
            requests
                .GroupBy(e => e.Entry.Path!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}: {FindingText.Requests(g.Count())}"),
            options.MaxDetails);

        return new Finding(
            Id,
            FindingPriority.Low,
            "Agent-Discovery-Nachfrage",
            $"{FindingText.Requests(requests.Count)} suchten nach Agent-Dateien unter "
            + $"{patterns.WellKnownPrefix}.",
            "Wer von KI-Agenten gefunden werden möchte, legt dort eine Beschreibung ab; "
            + "wer nicht, kann die Anfragen ignorieren – gefährlich sind sie nicht.")
        {
            DetailsCaption = "Angefragte Pfade",
            Details = details,
            MoreDetails = more,
        };
    }
}
