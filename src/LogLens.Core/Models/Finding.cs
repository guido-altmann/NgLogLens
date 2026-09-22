namespace LogLens.Core.Models;

/// <summary>Dringlichkeit einer Empfehlung (SPEC 7). Die Reihenfolge ist die Sortierung der Ansicht.</summary>
public enum FindingPriority
{
    High,
    Medium,
    Low,
}

/// <summary>
/// Konfigurations-Schnipsel einer Empfehlung. <paramref name="Language"/> ist die
/// Sprache für die Anzeige (<c>nginx</c>, <c>yaml</c>, <c>markdown</c>), nicht mehr.
/// </summary>
public sealed record FindingSnippet(string Caption, string Language, string Text);

/// <summary>
/// Eine regelbasierte Empfehlung (SPEC 7). <paramref name="Reason"/> enthält die Zahlen
/// aus der Auswertung, <paramref name="Advice"/> sagt, was zu tun ist.
/// </summary>
/// <param name="Id">Stabiler Schlüssel der Regel, unabhängig vom Anzeigetext.</param>
public sealed record Finding(
    string Id,
    FindingPriority Priority,
    string Title,
    string Reason,
    string Advice)
{
    /// <summary>Betroffene Pfade, IPs oder Zeilen; gekürzt auf <c>FindingOptions.MaxDetails</c>.</summary>
    public IReadOnlyList<string> Details { get; init; } = [];

    public string? DetailsCaption { get; init; }

    /// <summary>Weitere Betroffene, die nicht mehr in <see cref="Details"/> passten.</summary>
    public int MoreDetails { get; init; }

    public FindingSnippet? Snippet { get; init; }
}
