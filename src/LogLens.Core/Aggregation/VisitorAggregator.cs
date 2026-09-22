using System.Net;
using System.Net.Sockets;
using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// Kennzahlen der Besucher-Ansicht (SPEC 6, 8.3): Top-Seiten, Referrer, Tageszeit und
/// aktivste Netze. Nur Anfragen von Menschen zählen.
/// </summary>
public sealed class VisitorAggregator(AggregationOptions options)
{
    /// <param name="excludeMonitoring">Anfragen mit Monitoring-Verdacht herausrechnen (SPEC 5.8).</param>
    /// <param name="ownDomains">Referrer von diesen Domains und ihren Subdomains zählen nicht.</param>
    public VisitorStatistics Aggregate(
        IReadOnlyList<ClassifiedEntry> entries,
        bool excludeMonitoring,
        IReadOnlyCollection<string> ownDomains)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(ownDomains);

        var pages = new Dictionary<string, int>(StringComparer.Ordinal);
        var referrers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var networks = new Dictionary<string, NetworkCounter>(StringComparer.OrdinalIgnoreCase);
        var hours = new int[Ranking.HoursPerDay];
        var requests = 0;
        var pageViews = 0;

        foreach (var entry in entries)
        {
            if (entry.Class != TrafficClass.Human || (excludeMonitoring && entry.IsMonitoringSuspected))
            {
                continue;
            }

            requests++;
            Ranking.CountHour(hours, entry.Entry);

            var network = NetworkKey(entry.ClientIp);
            NetworkCounter? counter = null;
            if (network is not null)
            {
                if (!networks.TryGetValue(network, out counter))
                {
                    networks[network] = counter = new NetworkCounter();
                }

                counter.Requests++;
                counter.Ips.Add(entry.ClientIp);
                counter.HasMonitoring |= entry.IsMonitoringSuspected;
            }

            if (!entry.IsPageView)
            {
                continue;
            }

            pageViews++;
            Ranking.Increment(pages, PagePath.Normalize(entry.Entry.Path));

            if (counter is not null)
            {
                counter.PageViews++;
            }

            if (ReferrerHost(entry.Entry.Referer) is { } host && !IsOwnDomain(host, ownDomains))
            {
                Ranking.Increment(referrers, host);
            }
        }

        // „Netze mit Besuchern" meint Netze mit Seitenaufrufen (SPEC 6); ein Netz, aus
        // dem nur Assets kamen, taucht in der Liste auf, zählt aber nicht mit.
        var activeNetworks = networks
            .Select(p => new NetworkActivity(
                p.Key, p.Value.PageViews, p.Value.Requests, p.Value.Ips.Count, p.Value.HasMonitoring))
            .OrderByDescending(n => n.PageViews)
            .ThenByDescending(n => n.Requests)
            .ThenBy(n => n.Network, StringComparer.Ordinal)
            .ToList();

        return new VisitorStatistics(
            Requests: requests,
            PageViews: pageViews,
            TopPages: Ranking.Top(pages, options.TopListSize),
            DistinctPages: pages.Count,
            TopReferrers: Ranking.Top(referrers, options.TopListSize),
            RequestsPerHour: hours,
            TopNetworks: [.. activeNetworks.Take(options.TopListSize)],
            Networks: activeNetworks.Count(n => n.PageViews > 0));
    }

    /// <summary>
    /// Host eines Referrers ohne <c>www.</c>, klein geschrieben. Null bei <c>-</c>,
    /// leerem Feld oder allem, was keine absolute http(s)-Adresse ist.
    /// </summary>
    internal static string? ReferrerHost(string? referer)
    {
        if (string.IsNullOrWhiteSpace(referer) || referer == "-"
            || !Uri.TryCreate(referer, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
    }

    /// <summary>Gleich der Domain oder eine Subdomain davon – <c>notexample.de</c> ist es nicht.</summary>
    internal static bool IsOwnDomain(string host, IReadOnlyCollection<string> ownDomains)
    {
        foreach (var domain in ownDomains)
        {
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || (host.Length > domain.Length
                    && host.EndsWith(domain, StringComparison.OrdinalIgnoreCase)
                    && host[host.Length - domain.Length - 1] == '.'))
            {
                return true;
            }
        }

        return false;
    }

    private string? NetworkKey(string clientIp)
    {
        if (!IPAddress.TryParse(clientIp, out var address))
        {
            return null;
        }

        var prefixLength = address.AddressFamily == AddressFamily.InterNetwork
            ? options.VisitorNetworkIpv4PrefixLength
            : options.VisitorNetworkIpv6PrefixLength;

        return IpNetwork.NetworkKey(address, prefixLength);
    }

    private sealed class NetworkCounter
    {
        public int PageViews { get; set; }

        public int Requests { get; set; }

        public bool HasMonitoring { get; set; }

        public HashSet<string> Ips { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
