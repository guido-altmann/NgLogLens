using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „llms.txt fehlt": KI-Agenten fragen die Datei an und bekommen einen 404.
/// Mittlere Priorität – hier geht keine Sicherheit verloren, aber Sichtbarkeit.
/// </summary>
public sealed class LlmsTxtRule(FindingPatternSet patterns, FindingOptions options) : IFindingRule
{
    public string Id => "llms-txt";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var requests = result.Entries
            .Where(e => e.Entry.Status == options.NotFoundStatusCode && patterns.IsLlmsTxt(e.Entry.Path))
            .ToList();

        if (requests.Count == 0)
        {
            return null;
        }

        var agents = requests
            .Select(e => e.AgentName)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byPath = requests
            .GroupBy(e => e.Entry.Path!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => $"{g.Key}: {FindingText.Requests(g.Count())}");

        var (details, more) = FindingText.Take(byPath, options.MaxDetails);
        var domain = result.OwnDomains.Count > 0 ? result.OwnDomains[0] : "example.de";

        return new Finding(
            Id,
            FindingPriority.Medium,
            "llms.txt fehlt",
            $"{FindingText.Requests(requests.Count)} auf eine llms.txt liefen ins Leere"
            + (agents.Count == 0 ? "." : $", darunter von {string.Join(", ", agents)}."),
            "Eine llms.txt im Wurzelverzeichnis beschreibt KI-Agenten in wenigen Zeilen, worum es auf der "
            + "Seite geht und welche Seiten die wichtigen sind.")
        {
            DetailsCaption = "Angefragte Pfade",
            Details = details,
            MoreDetails = more,
            Snippet = new FindingSnippet("/llms.txt", "markdown",
                $"""
                 # {domain}

                 > Ein Satz, worum es auf dieser Seite geht.

                 ## Seiten

                 - [Startseite](https://{domain}/): Worum es geht.
                 - [Leistungen](https://{domain}/leistungen): Was angeboten wird.

                 ## Kontakt

                 - [Impressum](https://{domain}/impressum)
                 """),
        };
    }
}
