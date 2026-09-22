namespace LogLens.Core.Classification;

/// <summary>
/// Muster der Findings-Regeln (SPEC 7) aus <c>finding-patterns.json</c>. Die Regeln
/// selbst stehen in <c>Findings/</c>; die Listen gehören wie alle anderen in eine
/// eingebettete Datei und nicht in den Code (CLAUDE.md).
/// </summary>
public sealed class FindingPatternSet
{
    private readonly HashSet<string> _llmsTxtPaths;

    internal FindingPatternSet(FindingPatternDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Updated = document.Updated;
        LlmsTxtPaths = document.LlmsTxtPaths;
        WellKnownPrefix = document.WellKnownPrefix;
        DotNetConfigPatterns = document.DotNetConfigPatterns;
        MissingAssetPatterns = document.MissingAssetPatterns;
        _llmsTxtPaths = new HashSet<string>(document.LlmsTxtPaths, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Stand der Liste; jede Musterliste trägt ihn (CLAUDE.md).</summary>
    public string Updated { get; }

    /// <summary>Pfade, deren 404 das Finding „llms.txt fehlt" auslöst (SPEC 7).</summary>
    public IReadOnlyList<string> LlmsTxtPaths { get; }

    /// <summary>Präfix der Agent-Discovery-Dateien, die unter <c>.well-known</c> liegen.</summary>
    public string WellKnownPrefix { get; }

    public IReadOnlyList<PathPattern> DotNetConfigPatterns { get; }

    public IReadOnlyList<PathPattern> MissingAssetPatterns { get; }

    public bool IsLlmsTxt(string? path) => path is not null && _llmsTxtPaths.Contains(path);

    public bool IsWellKnown(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.StartsWith(WellKnownPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesDotNetConfig(in RequestTarget target) =>
        target.MatchesPath(DotNetConfigPatterns, out _);

    public bool MatchesMissingAsset(in RequestTarget target) =>
        target.MatchesPath(MissingAssetPatterns, out _);
}
