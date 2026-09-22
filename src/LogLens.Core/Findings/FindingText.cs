using System.Globalization;

namespace LogLens.Core.Findings;

/// <summary>
/// Kleine Helfer für die Texte der Regeln: Zahlen in Anzeigeschreibweise und
/// Aufzählungen, die auf <c>FindingOptions.MaxDetails</c> gekürzt werden.
/// </summary>
internal static class FindingText
{
    public static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>Anfrage oder Anfragen – die Regeln melden oft genau eine.</summary>
    public static string Requests(int count) => count == 1 ? "1 Anfrage" : $"{Number(count)} Anfragen";

    public static (IReadOnlyList<string> Details, int More) Take(IEnumerable<string> details, int max)
    {
        var all = details.ToList();
        return all.Count <= max ? (all, 0) : (all[..max], all.Count - max);
    }
}
