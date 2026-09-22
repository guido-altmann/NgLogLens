using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>Gemeinsame Bausteine der Top-Listen und Stundenreihen.</summary>
internal static class Ranking
{
    /// <summary>Stunden eines Tages; Länge jeder Stundenreihe.</summary>
    public const int HoursPerDay = 24;

    /// <summary>
    /// Häufigste zuerst, bei Gleichstand ordinal nach Schlüssel – damit ist die
    /// Reihenfolge unabhängig von der Kultur und von der Reihenfolge im Log.
    /// </summary>
    public static IReadOnlyList<RankedItem> Top(Dictionary<string, int> counts, int size) =>
    [
        .. counts
            .OrderByDescending(p => p.Value)
            .ThenBy(p => p.Key, StringComparer.Ordinal)
            .Take(size)
            .Select(p => new RankedItem(p.Key, p.Value)),
    ];

    public static void Increment(Dictionary<string, int> counts, string key) =>
        counts[key] = counts.TryGetValue(key, out var count) ? count + 1 : 1;

    /// <summary>
    /// Zählt einen Eintrag in seiner Stunde (UTC). Einträge, deren Zeit nur tagesgenau
    /// bekannt ist, fehlen hier – sie stünden sonst alle um 0 Uhr (SPEC 4, 6).
    /// </summary>
    public static void CountHour(int[] hours, AccessEntry entry)
    {
        if (entry.TimePrecision <= TimePrecision.Hour)
        {
            hours[entry.Timestamp.UtcDateTime.Hour]++;
        }
    }
}
