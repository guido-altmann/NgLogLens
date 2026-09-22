namespace LogLens.Core.Parsing;

/// <summary>
/// Probiert die Zeilenparser in fester Reihenfolge durch. Die Reihenfolge ist Teil der
/// Fachlogik: eine gekürzte Zeile kann wie mehrere Formate aussehen, deshalb gewinnen
/// die vollständigen Formen (SPEC 2.3).
/// </summary>
public sealed class LogLineParserChain(IEnumerable<ILogLineParser> parsers)
{
    private readonly ILogLineParser[] _parsers = [.. parsers];

    /// <summary>Die Standardreihenfolge, wie sie auch <c>AddLogLensCore()</c> registriert.</summary>
    public static IReadOnlyList<ILogLineParser> CreateDefaultParsers() =>
    [
        new AccessLineParser(),
        new TruncatedAccessLineParser(),
        new ErrorLineParser(),
        new TruncatedErrorLineParser(),
    ];

    /// <summary>Liefert <c>null</c>, wenn keine Form passt; die Zeile gilt dann als unbekannt.</summary>
    public ParsedLine? Parse(string line, int lineNumber)
    {
        foreach (var parser in _parsers)
        {
            if (parser.TryParse(line, lineNumber, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }
}
