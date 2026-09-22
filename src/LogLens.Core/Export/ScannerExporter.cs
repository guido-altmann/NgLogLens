using System.Globalization;
using System.Net;
using System.Text;
using LogLens.Core.Models;

namespace LogLens.Core.Export;

/// <summary>
/// Export der Scanner-IPs (SPEC 8.4) als nginx-<c>deny</c>-Liste, Klartext und CSV.
/// Alle Formate sind kulturunabhängig, nach Adresse sortiert und enden mit <c>\n</c>,
/// damit sie sich unverändert auf einem Linux-Server einbinden lassen.
/// </summary>
public static class ScannerExporter
{
    private const string LineBreak = "\n";

    public static string ToNginxDenyList(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ToNginxDenyList(result.Scanners, result.FileNames, result.Period);
    }

    public static string ToNginxDenyList(
        IReadOnlyList<Scanner> scanners,
        IReadOnlyList<string> fileNames,
        TimeRange? period)
    {
        ArgumentNullException.ThrowIfNull(scanners);
        ArgumentNullException.ThrowIfNull(fileNames);

        var text = new StringBuilder();
        text.Append("# Scanner-IPs aus ").Append(string.Join(", ", fileNames)).Append(LineBreak);

        if (scanners.Count == 0)
        {
            text.Append("# Keine Scanner gefunden, erzeugt mit LogLens").Append(LineBreak);
            return text.ToString();
        }

        if (period is not null)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"# Zeitraum: {period.FirstDay:yyyy-MM-dd} bis {period.LastDay:yyyy-MM-dd} (UTC), ");
        }
        else
        {
            text.Append("# ");
        }

        text.Append(CultureInfo.InvariantCulture, $"{scanners.Count} IPs, erzeugt mit LogLens").Append(LineBreak);
        text.Append("# Einbinden im server- oder http-Block: include /etc/nginx/conf.d/loglens-deny.conf;")
            .Append(LineBreak);

        foreach (var scanner in Sorted(scanners))
        {
            // Kommentar ohne Zeilenumbruch: die Kategorie stammt aus der eigenen
            // Musterliste, aber eine Zeile darf die Konfiguration nie zerbrechen.
            text.Append(CultureInfo.InvariantCulture,
                    $"deny {scanner.ClientIp};  # {scanner.Requests} Anfragen, {SingleLine(scanner.MainCategory)}")
                .Append(LineBreak);
        }

        return text.ToString();
    }

    public static string ToPlainText(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return ToPlainText(result.Scanners);
    }

    public static string ToPlainText(IReadOnlyList<Scanner> scanners)
    {
        ArgumentNullException.ThrowIfNull(scanners);

        var text = new StringBuilder();
        foreach (var scanner in Sorted(scanners))
        {
            text.Append(scanner.ClientIp).Append(LineBreak);
        }

        return text.ToString();
    }

    /// <summary>
    /// CSV nach RFC 4180 mit Komma als Trenner und Zeitangaben in ISO 8601 (UTC).
    /// Spaltennamen englisch, damit Skripte und Tabellen sie ohne Übersetzung lesen.
    /// </summary>
    public static string ToCsv(IReadOnlyList<Scanner> scanners)
    {
        ArgumentNullException.ThrowIfNull(scanners);

        var text = new StringBuilder();
        text.Append("ip,requests,individual_attacks,first_seen_utc,last_seen_utc,burst_seconds,main_category")
            .Append(LineBreak);

        foreach (var scanner in Sorted(scanners))
        {
            text.AppendJoin(',',
                    Csv(scanner.ClientIp),
                    scanner.Requests.ToString(CultureInfo.InvariantCulture),
                    scanner.IndividualAttacks.ToString(CultureInfo.InvariantCulture),
                    Iso(scanner.FirstSeen),
                    Iso(scanner.LastSeen),
                    ((long)scanner.Burst.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                    Csv(scanner.MainCategory))
                .Append(LineBreak);
        }

        return text.ToString();
    }

    /// <summary>IPv4 vor IPv6, innerhalb der Familie numerisch – 192.0.2.9 vor 192.0.2.100.</summary>
    private static IEnumerable<Scanner> Sorted(IReadOnlyList<Scanner> scanners) =>
        scanners.OrderBy(s => s.ClientIp, AddressComparer.Instance);

    private static string Iso(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Csv(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string SingleLine(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ');

    private sealed class AddressComparer : IComparer<string>
    {
        public static AddressComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            var left = Parse(x);
            var right = Parse(y);

            if (left is null || right is null)
            {
                // Unbrauchbare Adressen ans Ende, untereinander ordinal.
                return left is null && right is null
                    ? StringComparer.Ordinal.Compare(x, y)
                    : left is null ? 1 : -1;
            }

            var leftBytes = left.GetAddressBytes();
            var rightBytes = right.GetAddressBytes();

            var byLength = leftBytes.Length.CompareTo(rightBytes.Length);
            return byLength != 0 ? byLength : leftBytes.AsSpan().SequenceCompareTo(rightBytes);
        }

        private static IPAddress? Parse(string? value) =>
            IPAddress.TryParse(value, out var address) ? address : null;
    }
}
