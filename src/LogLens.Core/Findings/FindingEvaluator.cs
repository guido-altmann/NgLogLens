using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// Stufe „Findings" der Pipeline (CLAUDE.md): führt alle Regeln aus und sortiert das
/// Ergebnis nach Priorität. Bei gleicher Priorität bleibt die Reihenfolge der
/// Registrierung erhalten – sie steht in <c>ServiceCollectionExtensions</c> und
/// entspricht der Tabelle in SPEC 7.
/// </summary>
public sealed class FindingEvaluator(IEnumerable<IFindingRule> rules)
{
    public IReadOnlyList<Finding> Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var findings = new List<(int Order, Finding Finding)>();
        var order = 0;

        foreach (var rule in rules)
        {
            if (rule.Evaluate(result) is { } finding)
            {
                findings.Add((order, finding));
            }

            order++;
        }

        return [.. findings.OrderBy(f => f.Finding.Priority).ThenBy(f => f.Order).Select(f => f.Finding)];
    }
}
