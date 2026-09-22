using LogLens.Core.Models;

namespace LogLens.Core.Classification;

/// <summary>
/// Pfad und Query einer Anfrage, je im Rohzustand und URL-dekodiert. Die Erkennung
/// arbeitet auf beiden Werten (SPEC 2.1): <c>%2f</c> ist nur im Rohwert sichtbar,
/// <c>..</c> nur im dekodierten.
/// </summary>
public readonly struct RequestTarget
{
    private readonly string?[] _pathValues;
    private readonly string?[] _queryValues;

    public RequestTarget(string? path, string? query)
    {
        Path = path;
        Query = query;

        var decodedPath = Decode(path);
        var decodedQuery = Decode(query);

        _pathValues = string.Equals(decodedPath, path, StringComparison.Ordinal)
            ? [path]
            : [path, decodedPath];
        _queryValues = string.Equals(decodedQuery, query, StringComparison.Ordinal)
            ? [query]
            : [query, decodedQuery];
    }

    public static RequestTarget From(AccessEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new RequestTarget(entry.Path, entry.Query);
    }

    public string? Path { get; }

    public string? Query { get; }

    public bool MatchesPath(IReadOnlyList<PathPattern> patterns, out PathPattern? matched) =>
        Matches(patterns, _pathValues, out matched);

    public bool MatchesQuery(IReadOnlyList<PathPattern> patterns, out PathPattern? matched) =>
        Matches(patterns, _queryValues, out matched);

    private static bool Matches(
        IReadOnlyList<PathPattern> patterns,
        string?[] values,
        out PathPattern? matched)
    {
        for (var i = 0; i < patterns.Count; i++)
        {
            foreach (var value in values)
            {
                if (patterns[i].Matches(value))
                {
                    matched = patterns[i];
                    return true;
                }
            }
        }

        matched = null;
        return false;
    }

    /// <summary>
    /// Prozentkodierung auflösen. Ungültige Sequenzen bleiben stehen, statt die
    /// Klassifizierung einer einzelnen Zeile scheitern zu lassen.
    /// </summary>
    private static string? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0)
        {
            return value;
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
