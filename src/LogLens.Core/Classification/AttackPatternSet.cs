namespace LogLens.Core.Classification;

/// <summary>Eine Angriffskategorie aus <c>attack-patterns.json</c> (SPEC 5.3).</summary>
/// <param name="AppliesToNonReadMethods">Fängt alles ab, was nicht GET oder HEAD ist.</param>
/// <param name="Injection">
/// Die Query-Muster dieser Kategorie sind Injection-Muster und machen eine Anfrage
/// unabhängig vom Status zum Angriff (SPEC 5.1.4).
/// </param>
public sealed record AttackCategory(
    string Name,
    bool AppliesToNonReadMethods,
    bool Injection,
    IReadOnlyList<PathPattern> PathPatterns,
    IReadOnlyList<PathPattern> QueryPatterns);

/// <summary>
/// Angriffsmuster, Kategorien und die Ausnahmeliste der Agent-Discovery-Pfade
/// (SPEC 5.1, 5.3, 5.4). Die Reihenfolge der Kategorien ist Fachlogik: die erste
/// passende gewinnt.
/// </summary>
public sealed class AttackPatternSet
{
    private readonly HashSet<string> _agentDiscoveryPaths;

    internal AttackPatternSet(AttackPatternDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Updated = document.Updated;
        ReconnaissanceCategory = document.ReconnaissanceCategory;
        FallbackCategory = document.FallbackCategory;
        AgentDiscoveryPaths = document.AgentDiscoveryPaths;
        _agentDiscoveryPaths = new HashSet<string>(document.AgentDiscoveryPaths, StringComparer.OrdinalIgnoreCase);

        Categories =
        [
            .. document.Categories.Select(c => new AttackCategory(
                c.Name,
                c.AppliesToNonReadMethods,
                c.Injection,
                c.PathPatterns ?? [],
                c.QueryPatterns ?? []))
        ];
    }

    /// <summary>Stand der Liste; jede Musterliste trägt ihn (CLAUDE.md).</summary>
    public string Updated { get; }

    public IReadOnlyList<AttackCategory> Categories { get; }

    public IReadOnlyList<string> AgentDiscoveryPaths { get; }

    /// <summary>Name für Anfragen, die nur über die Scanner-Regel zum Angriff werden (SPEC 5.2).</summary>
    public string ReconnaissanceCategory { get; }

    /// <summary>Name für Angriffe, auf die keine Kategorie passt (SPEC 5.3, „Sonstige Proben").</summary>
    public string FallbackCategory { get; }

    /// <summary>
    /// Agent-Discovery ist kein Angriff, auch nicht bei 404 (SPEC 5.4). Die Ausnahme
    /// greift nur für den Pfad selbst; die Scanner-Regel bleibt davon unberührt.
    /// </summary>
    public bool IsAgentDiscovery(string? path) =>
        path is not null && _agentDiscoveryPaths.Contains(path);

    /// <summary>Passt der Pfad auf ein Angriffsmuster? Grundlage von SPEC 5.1.3.</summary>
    public bool MatchesAttackPath(in RequestTarget target, out string? matched)
    {
        foreach (var category in Categories)
        {
            if (target.MatchesPath(category.PathPatterns, out var pattern))
            {
                matched = pattern!.Label;
                return true;
            }
        }

        matched = null;
        return false;
    }

    /// <summary>Enthält die Query ein Injection-Muster? Grundlage von SPEC 5.1.4.</summary>
    public bool MatchesInjectionQuery(in RequestTarget target, out string? matched)
    {
        foreach (var category in Categories)
        {
            if (category.Injection && target.MatchesQuery(category.QueryPatterns, out var pattern))
            {
                matched = pattern!.Label;
                return true;
            }
        }

        matched = null;
        return false;
    }

    /// <summary>
    /// Kategorie eines Angriffs (SPEC 5.3). Geprüft wird gegen Pfad und Query,
    /// jeweils roh und dekodiert; die erste passende Kategorie gewinnt.
    /// </summary>
    public string Categorize(in RequestTarget target, string? method, bool isReadMethod)
    {
        foreach (var category in Categories)
        {
            if (category.AppliesToNonReadMethods && method is not null && !isReadMethod)
            {
                return category.Name;
            }

            if (target.MatchesPath(category.PathPatterns, out _)
                || target.MatchesQuery(category.QueryPatterns, out _))
            {
                return category.Name;
            }
        }

        return FallbackCategory;
    }
}
