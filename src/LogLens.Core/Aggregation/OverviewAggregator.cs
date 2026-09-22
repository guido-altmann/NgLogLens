using System.Net;
using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// Stufe „Aggregate" der Pipeline (SPEC 6). Arbeitet auf der klassifizierten Liste im
/// Speicher und liefert die Kennzahlen, die die Übersicht braucht. Top-Listen und
/// Detail-Kennzahlen der Unterseiten kommen in M5 dazu.
/// </summary>
public sealed class OverviewAggregator(AggregationOptions options)
{
    public AnalysisResult Aggregate(
        IReadOnlyList<string> fileNames,
        ParseResult parseResult,
        ClassificationResult classification)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(classification);

        var entries = classification.Entries;
        var period = Period(entries);
        var perDay = new Dictionary<DateOnly, int[]>();
        var networks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var networksWithoutMonitoring = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var classCounts = new int[ClassOrder.Length];
        var pageViews = 0;
        var pageViewsWithoutMonitoring = 0;

        foreach (var entry in entries)
        {
            classCounts[(int)entry.Class]++;

            var day = DateOnly.FromDateTime(entry.Timestamp.UtcDateTime);
            if (!perDay.TryGetValue(day, out var counts))
            {
                counts = new int[ClassOrder.Length];
                perDay[day] = counts;
            }

            counts[(int)entry.Class]++;

            if (!entry.IsPageView)
            {
                continue;
            }

            pageViews++;
            var network = NetworkKey(entry.ClientIp);
            if (network is not null)
            {
                networks.Add(network);
            }

            if (entry.IsMonitoringSuspected)
            {
                continue;
            }

            pageViewsWithoutMonitoring++;
            if (network is not null)
            {
                networksWithoutMonitoring.Add(network);
            }
        }

        var (daily, daysWithoutEntries) = BuildDailySeries(period, perDay);

        return new AnalysisResult(
            FileNames: fileNames,
            Diagnostics: parseResult.Diagnostics,
            ErrorEntries: parseResult.ErrorEntries,
            Classification: classification,
            Period: period,
            Classes: BuildClassCounts(classCounts, entries.Count),
            Daily: daily,
            DaysWithoutEntries: daysWithoutEntries,
            PageViews: pageViews,
            PageViewsWithoutMonitoring: pageViewsWithoutMonitoring,
            VisitorNetworks: networks.Count,
            VisitorNetworksWithoutMonitoring: networksWithoutMonitoring.Count);
    }

    private static readonly TrafficClass[] ClassOrder =
        [TrafficClass.Human, TrafficClass.AiAgent, TrafficClass.Bot, TrafficClass.Attack];

    private static TimeRange? Period(IReadOnlyList<ClassifiedEntry> entries)
    {
        if (entries.Count == 0)
        {
            return null;
        }

        // Der Export ist nur grob sortiert (SPEC 2), deshalb echtes Minimum und Maximum.
        var first = entries[0].Timestamp;
        var last = first;

        foreach (var entry in entries)
        {
            if (entry.Timestamp < first)
            {
                first = entry.Timestamp;
            }
            else if (entry.Timestamp > last)
            {
                last = entry.Timestamp;
            }
        }

        return new TimeRange(first, last);
    }

    private static IReadOnlyList<ClassCount> BuildClassCounts(int[] counts, int total)
    {
        var result = new ClassCount[ClassOrder.Length];

        for (var i = 0; i < ClassOrder.Length; i++)
        {
            var trafficClass = ClassOrder[i];
            var requests = counts[(int)trafficClass];
            result[i] = new ClassCount(trafficClass, requests, total == 0 ? 0 : (double)requests / total);
        }

        return result;
    }

    /// <summary>Lückenlose Tagesreihe vom ersten bis zum letzten Tag, auch Tage mit 0 (SPEC 6).</summary>
    private static (IReadOnlyList<DailyTraffic> Daily, IReadOnlyList<DateOnly> Empty) BuildDailySeries(
        TimeRange? period,
        Dictionary<DateOnly, int[]> perDay)
    {
        if (period is null)
        {
            return ([], []);
        }

        var daily = new List<DailyTraffic>(period.Days);
        var empty = new List<DateOnly>();

        for (var day = period.FirstDay; day <= period.LastDay; day = day.AddDays(1))
        {
            if (perDay.TryGetValue(day, out var counts))
            {
                daily.Add(new DailyTraffic(
                    day,
                    counts[(int)TrafficClass.Human],
                    counts[(int)TrafficClass.AiAgent],
                    counts[(int)TrafficClass.Bot],
                    counts[(int)TrafficClass.Attack]));
            }
            else
            {
                daily.Add(new DailyTraffic(day, 0, 0, 0, 0));
                empty.Add(day);
            }
        }

        return (daily, empty);
    }

    /// <summary>Netzschlüssel einer Client-IP, null bei unbrauchbarer Adresse.</summary>
    private string? NetworkKey(string clientIp)
    {
        if (!IPAddress.TryParse(clientIp, out var address))
        {
            return null;
        }

        var prefixLength = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? options.VisitorNetworkIpv4PrefixLength
            : options.VisitorNetworkIpv6PrefixLength;

        return IpNetwork.NetworkKey(address, prefixLength);
    }
}
