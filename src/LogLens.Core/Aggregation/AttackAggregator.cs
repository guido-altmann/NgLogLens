using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// Kennzahlen der Angriffs-Ansicht (SPEC 6, 8.4): Kategorien und Tageszeit. Die
/// Scanner selbst entstehen schon bei der Klassifizierung (SPEC 5.2).
/// </summary>
public sealed class AttackAggregator
{
    public AttackStatistics Aggregate(IReadOnlyList<ClassifiedEntry> entries, IReadOnlySet<string> scannerIps)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(scannerIps);

        var categories = new Dictionary<string, int>(StringComparer.Ordinal);
        var ips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hours = new int[Ranking.HoursPerDay];
        var requests = 0;
        var scannerRequests = 0;

        foreach (var entry in entries)
        {
            if (entry.Class != TrafficClass.Attack)
            {
                continue;
            }

            requests++;
            ips.Add(entry.ClientIp);
            Ranking.CountHour(hours, entry.Entry);

            if (entry.AttackCategory is { } category)
            {
                Ranking.Increment(categories, category);
            }

            if (scannerIps.Contains(entry.ClientIp))
            {
                scannerRequests++;
            }
        }

        return new AttackStatistics(
            Requests: requests,
            DistinctIps: ips.Count,
            ScannerRequests: scannerRequests,
            Categories:
            [
                .. Ranking.Top(categories, int.MaxValue)
                    .Select(c => new CategoryCount(c.Key, c.Count, (double)c.Count / requests)),
            ],
            RequestsPerHour: hours);
    }
}
