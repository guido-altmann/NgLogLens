using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// Kennzahlen der Ansicht „Server-Zustand" (SPEC 6, 8.6): Statuscodes, Serverfehler,
/// Proxy-Instanzen und die übrigen Error-Zeilen.
/// </summary>
public sealed class ServerHealthAggregator
{
    private const int ServerErrorMin = 500;
    private const int ServerErrorMax = 599;

    public ServerHealthStatistics Aggregate(
        IReadOnlyList<ClassifiedEntry> entries,
        IReadOnlyList<ErrorEntry> errorEntries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(errorEntries);

        var statuses = new SortedDictionary<int, int>();
        var serverErrors = new Dictionary<(string Path, int Status), List<DateTimeOffset>>();
        var proxies = new Dictionary<string, ProxyCounter>(StringComparer.OrdinalIgnoreCase);
        var withoutStatus = 0;

        foreach (var classified in entries)
        {
            var entry = classified.Entry;

            if (!proxies.TryGetValue(entry.ProxyIp, out var proxy))
            {
                proxies[entry.ProxyIp] = proxy = new ProxyCounter(entry.Timestamp);
            }

            proxy.Add(entry.Timestamp);

            if (entry.Status is not int status)
            {
                withoutStatus++;
                continue;
            }

            statuses[status] = statuses.TryGetValue(status, out var count) ? count + 1 : 1;

            if (status is >= ServerErrorMin and <= ServerErrorMax)
            {
                var key = (entry.Path ?? "-", status);
                if (!serverErrors.TryGetValue(key, out var timestamps))
                {
                    serverErrors[key] = timestamps = [];
                }

                timestamps.Add(entry.Timestamp);
            }
        }

        return new ServerHealthStatistics(
            StatusCodes: [.. statuses.Select(p => new StatusCount(p.Key, p.Value))],
            RequestsWithoutStatus: withoutStatus,
            ServerErrors:
            [
                .. serverErrors
                    .Select(p => new ServerErrorGroup(p.Key.Path, p.Key.Status, p.Value.Count, p.Value.Min(), p.Value.Max()))
                    .OrderByDescending(e => e.Requests)
                    .ThenBy(e => e.Path, StringComparer.Ordinal)
                    .ThenBy(e => e.Status),
            ],
            ProxyInstances:
            [
                .. proxies
                    .Select(p => new ProxyInstance(p.Key, p.Value.First, p.Value.Last, p.Value.Requests))
                    .OrderBy(p => p.FirstSeen)
                    .ThenBy(p => p.ProxyIp, StringComparer.Ordinal),
            ],
            ErrorLevels: CountLevels(errorEntries));
    }

    /// <summary>Häufigste Level zuerst; Zeilen ohne Level (gekürzt) stehen am Ende.</summary>
    private static IReadOnlyList<ErrorLevelCount> CountLevels(IReadOnlyList<ErrorEntry> errorEntries) =>
    [
        .. errorEntries
            .GroupBy(e => e.Level, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ErrorLevelCount(g.Key, g.Count()))
            .OrderBy(l => l.Level is null)
            .ThenByDescending(l => l.Lines)
            .ThenBy(l => l.Level, StringComparer.Ordinal),
    ];

    private sealed class ProxyCounter(DateTimeOffset firstSeen)
    {
        public DateTimeOffset First { get; private set; } = firstSeen;

        public DateTimeOffset Last { get; private set; } = firstSeen;

        public int Requests { get; private set; }

        public void Add(DateTimeOffset timestamp)
        {
            Requests++;
            if (timestamp < First)
            {
                First = timestamp;
            }

            if (timestamp > Last)
            {
                Last = timestamp;
            }
        }
    }
}
