using LogLens.Core.Models;

namespace LogLens.Core.Classification;

/// <summary>
/// Baut die Scanner-Liste aus klassifizierten Anfragen (SPEC 5.2, 6). Getrennt vom
/// Klassifizierer, weil der Zeitraumfilter die Zahlen für einen Ausschnitt neu
/// berechnen muss, ohne noch einmal zu klassifizieren.
/// </summary>
public static class ScannerList
{
    /// <param name="scannerIps">Im ganzen Log ermittelte Scanner-IPs; sie bleiben Scanner.</param>
    /// <param name="reconnaissanceCategory">
    /// Kategorie der Anfragen, die nur über die Scanner-Regel zum Angriff wurden. Alle
    /// übrigen Angriffe einer Scanner-IP sind Einzelangriffe im Sinne von SPEC 5.1.
    /// </param>
    public static IReadOnlyList<Scanner> Build(
        IEnumerable<ClassifiedEntry> entries,
        IReadOnlySet<string> scannerIps,
        string reconnaissanceCategory)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(scannerIps);

        var scanners = new List<Scanner>();

        foreach (var group in entries
            .Where(e => scannerIps.Contains(e.ClientIp))
            .GroupBy(e => e.ClientIp, StringComparer.OrdinalIgnoreCase))
        {
            scanners.Add(new Scanner(
                ClientIp: group.Key,
                IndividualAttacks: group.Count(e =>
                    e.Class == TrafficClass.Attack
                    && !string.Equals(e.AttackCategory, reconnaissanceCategory, StringComparison.Ordinal)),
                Requests: group.Count(),
                FirstSeen: group.Min(e => e.Timestamp),
                LastSeen: group.Max(e => e.Timestamp),
                MainCategory: MainCategory(group, reconnaissanceCategory)));
        }

        return [.. scanners.OrderByDescending(s => s.Requests).ThenBy(s => s.ClientIp, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Häufigste Kategorie der IP. „Aufklärung" gewinnt nie, solange es eine andere
    /// gibt: sie sagt über den Angreifer am wenigsten aus.
    /// </summary>
    private static string MainCategory(IEnumerable<ClassifiedEntry> entries, string reconnaissanceCategory)
    {
        var categories = entries
            .Select(e => e.AttackCategory)
            .Where(c => c is not null)
            .GroupBy(c => c!, StringComparer.Ordinal)
            .OrderBy(g => g.Key == reconnaissanceCategory)
            .ThenByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        return categories.Count > 0 ? categories[0].Key : reconnaissanceCategory;
    }
}
