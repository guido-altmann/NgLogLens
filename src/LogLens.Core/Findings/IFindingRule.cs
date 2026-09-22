using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// Eine Regel aus SPEC 7. Jede Regel ist eine Klasse und liefert entweder genau eine
/// Empfehlung oder <c>null</c>, wenn ihr Auslöser nicht zutrifft.
/// </summary>
public interface IFindingRule
{
    /// <summary>Stabiler Schlüssel, auch der Schlüssel der erzeugten Empfehlung.</summary>
    string Id { get; }

    Finding? Evaluate(AnalysisResult result);
}
