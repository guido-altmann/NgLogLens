using LogLens.Core.Models;

namespace LogLens.Core.Tests.Aggregation;

/// <summary>Klassifizierte Einträge für Randfälle, die in der Fixture nicht vorkommen.</summary>
internal static class Entries
{
    public static ClassifiedEntry Human(
        string clientIp,
        string timestamp,
        string path = "/",
        int? status = 200,
        string? referer = null,
        TimePrecision precision = TimePrecision.Second,
        bool isPageView = true,
        bool isMonitoring = false)
        => new(
            Access(clientIp, timestamp, path, status, referer, precision),
            TrafficClass.Human, null, null, null, isPageView, isMonitoring, "Browser");

    public static ClassifiedEntry Attack(
        string clientIp,
        string timestamp,
        string category,
        string path = "/.env",
        int? status = 404,
        TimePrecision precision = TimePrecision.Second)
        => new(
            Access(clientIp, timestamp, path, status, null, precision),
            TrafficClass.Attack, category, null, null, false, false, "Angriff");

    public static ClassifiedEntry Classified(
        TrafficClass trafficClass,
        string clientIp,
        string timestamp,
        string? path,
        int? status,
        string proxyIp = "10.0.1.9")
        => new(
            Access(clientIp, timestamp, path, status, null, TimePrecision.Second, proxyIp),
            trafficClass, null, null, null, false, false, "Test");

    private static int _lineNumber;

    private static AccessEntry Access(
        string clientIp,
        string timestamp,
        string? path,
        int? status,
        string? referer,
        TimePrecision precision,
        string proxyIp = "10.0.1.9")
        => new(
            LineNumber: Interlocked.Increment(ref _lineNumber),
            Timestamp: DateTimeOffset.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture),
            TimePrecision: precision,
            ProxyIp: proxyIp,
            ClientIp: clientIp,
            HasClientIpHeader: true,
            Method: path is null ? null : "GET",
            Path: path,
            Query: null,
            Protocol: path is null ? null : "HTTP/1.1",
            Status: status,
            Bytes: status is null ? null : 100,
            Referer: referer,
            UserAgent: "Mozilla/5.0 (X11; Linux x86_64) Firefox/130.0",
            IsTruncated: path is null);
}
