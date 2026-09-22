using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „.NET-Konfigdateien gesucht": jemand probiert gezielt <c>appsettings*.json</c>,
/// <c>local.settings.json</c> oder <c>web.config</c>. Hohe Priorität: liegt so eine Datei
/// im Publish-Ordner, stehen Verbindungszeichenfolgen und Schlüssel offen im Netz.
/// </summary>
public sealed class DotNetConfigRule(FindingPatternSet patterns, FindingOptions options) : IFindingRule
{
    public string Id => "dotnet-config";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var requests = result.Entries
            .Where(e => patterns.MatchesDotNetConfig(RequestTarget.From(e.Entry)))
            .ToList();

        if (requests.Count == 0)
        {
            return null;
        }

        var found = requests
            .Where(e => e.Entry.Status is int status && status is >= 200 and < 300)
            .ToList();

        var (details, more) = FindingText.Take(
            requests
                .GroupBy(e => e.Entry.Path ?? "-", StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}: {FindingText.Requests(g.Count())}, Status {StatusList(g)}"),
            options.MaxDetails);

        var reason = $"{FindingText.Requests(requests.Count)} zielten auf .NET-Konfigurationsdateien.";
        if (found.Count > 0)
        {
            reason += $" {FindingText.Number(found.Count)} davon wurden mit Erfolg beantwortet – "
                + "die Datei liegt offenbar im ausgelieferten Verzeichnis.";
        }

        return new Finding(
            Id,
            FindingPriority.High,
            ".NET-Konfigdateien gesucht",
            reason,
            "Im Publish-Ordner darf keine Konfigurationsdatei liegen; Geheimnisse gehören in "
            + "Umgebungsvariablen. Zusätzlich lässt sich der Zugriff in nginx abweisen.")
        {
            DetailsCaption = "Angefragte Pfade",
            Details = details,
            MoreDetails = more,
            Snippet = new FindingSnippet("nginx-Konfiguration", "nginx",
                """
                location ~* ^/(appsettings.*\.json|local\.settings\.json|web\.config)$ {
                    deny all;
                    return 404;
                }
                """),
        };
    }

    private static string StatusList(IEnumerable<ClassifiedEntry> entries) =>
        string.Join("/", entries
            .Select(e => e.Entry.Status)
            .Distinct()
            .Order()
            .Select(s => s is null ? "–" : FindingText.Number(s.Value)));
}
