using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

public sealed record DedupeResult(IReadOnlyList<ErrorEntry> Kept, int NotFoundPageErrorLines);

/// <summary>
/// Stufe „Dedupe" (SPEC 3): <c>open() "…/404.html" failed</c> ist der Folgefehler eines
/// 404 und kein eigenes Ereignis. Die Anzahl bleibt erhalten, weil daraus das Finding
/// „Fehlende 404-Seite" entsteht.
/// </summary>
public sealed class ErrorLineDeduplicator(ParserOptions options)
{
    private readonly string _marker = $"/{options.NotFoundPageFileName}\" failed";

    public DedupeResult Dedupe(IReadOnlyList<ErrorEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var kept = new List<ErrorEntry>(entries.Count);
        var removed = 0;

        foreach (var entry in entries)
        {
            if (IsNotFoundPageFollowUp(entry))
            {
                removed++;
                continue;
            }

            kept.Add(entry);
        }

        return new DedupeResult(kept, removed);
    }

    public bool IsNotFoundPageFollowUp(ErrorEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Message.Contains("open()", StringComparison.Ordinal)
            && entry.Message.Contains(_marker, StringComparison.OrdinalIgnoreCase);
    }
}
