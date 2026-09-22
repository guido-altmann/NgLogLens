using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „Serverfehler": jede 5xx-Antwort ist ein Fehler der eigenen Anwendung und
/// keine Frage der Konfiguration. Hohe Priorität.
/// </summary>
public sealed class ServerErrorRule(FindingOptions options) : IFindingRule
{
    public string Id => "server-errors";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var groups = result.ServerHealth.ServerErrors;
        if (groups.Count == 0)
        {
            return null;
        }

        var requests = result.ServerHealth.ServerErrorRequests;

        var (details, more) = FindingText.Take(
            groups.Select(g => $"{g.Path}: Status {FindingText.Number(g.Status)}, "
                + $"{FindingText.Requests(g.Requests)}"),
            options.MaxDetails);

        return new Finding(
            Id,
            FindingPriority.High,
            "Serverfehler",
            $"{FindingText.Requests(requests)} wurden mit einem Serverfehler beantwortet, "
            + $"verteilt auf {FindingText.Number(groups.Count)} Pfad-/Status-Kombinationen.",
            "Diese Pfade in den Anwendungslogs nachverfolgen; ein 5xx ist nie das Verschulden "
            + "des Besuchers.")
        {
            DetailsCaption = "Betroffene Pfade",
            Details = details,
            MoreDetails = more,
        };
    }
}
