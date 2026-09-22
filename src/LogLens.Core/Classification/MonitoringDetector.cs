using System.Net;
using System.Net.Sockets;
using LogLens.Core.Models;

namespace LogLens.Core.Classification;

/// <summary>
/// Monitoring-Verdacht (SPEC 5.8): zwei verschiedene Client-IPs im selben Netz, die
/// denselben Pfad fast gleichzeitig abrufen, und das an mehreren Zeitpunkten. So
/// verhalten sich Uptime-Prüfer mit mehreren Sonden – und keine zwei echten Besucher.
/// </summary>
public sealed class MonitoringDetector(ClassificationOptions options)
{
    /// <summary>
    /// Liefert die Client-IPs mit Verdacht. Zusätzlich gelten die vom Nutzer als
    /// eigenes Monitoring markierten Netze aus <see cref="ClassificationOptions.MonitoringNetworks"/>.
    /// </summary>
    public IReadOnlySet<string> Detect(IReadOnlyList<AccessEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var suspected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var manualNetworks = ParseManualNetworks();

        // Der Vergleich auf „≤ 2 Sekunden" ist nur sinnvoll, wenn die Uhrzeit auch
        // sekundengenau im Log steht; gekürzte Zeilen (SPEC 2.3) bleiben außen vor.
        var candidates = new List<(AccessEntry Entry, IPAddress Address)>(entries.Count);

        foreach (var entry in entries)
        {
            if (IPAddress.TryParse(entry.ClientIp, out var address))
            {
                if (IsInManualNetwork(manualNetworks, address))
                {
                    suspected.Add(entry.ClientIp);
                }

                if (entry.TimePrecision == TimePrecision.Second && entry.Path is not null)
                {
                    candidates.Add((entry, address));
                }
            }
        }

        // Je IP-Paar die Zeitpunkte sammeln, an denen beide denselben Pfad fast
        // gleichzeitig geholt haben. Erst ab mehreren Zeitpunkten ist es ein Muster.
        var occurrences = new Dictionary<(string Low, string High), HashSet<DateTimeOffset>>();

        foreach (var group in candidates.GroupBy(c => c.Entry.Path!, StringComparer.Ordinal))
        {
            var ordered = group.OrderBy(c => c.Entry.Timestamp).ToList();

            for (var i = 0; i < ordered.Count; i++)
            {
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    if (ordered[j].Entry.Timestamp - ordered[i].Entry.Timestamp > options.MonitoringWindow)
                    {
                        break;
                    }

                    if (!IsPair(ordered[i], ordered[j]))
                    {
                        continue;
                    }

                    var key = PairKey(ordered[i].Entry.ClientIp, ordered[j].Entry.ClientIp);
                    if (!occurrences.TryGetValue(key, out var timestamps))
                    {
                        occurrences[key] = timestamps = [];
                    }

                    timestamps.Add(ordered[i].Entry.Timestamp);
                }
            }
        }

        foreach (var ((low, high), timestamps) in occurrences)
        {
            if (timestamps.Count >= options.MonitoringMinOccurrences)
            {
                suspected.Add(low);
                suspected.Add(high);
            }
        }

        return suspected;
    }

    private bool IsPair(
        (AccessEntry Entry, IPAddress Address) left,
        (AccessEntry Entry, IPAddress Address) right)
    {
        if (string.Equals(left.Entry.ClientIp, right.Entry.ClientIp, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var prefixLength = left.Address.AddressFamily == AddressFamily.InterNetworkV6
            ? options.MonitoringIpv6PrefixLength
            : options.MonitoringIpv4PrefixLength;

        return IpNetwork.SameNetwork(left.Address, right.Address, prefixLength);
    }

    private static (string Low, string High) PairKey(string left, string right) =>
        StringComparer.Ordinal.Compare(left, right) <= 0 ? (left, right) : (right, left);

    private List<IpNetwork> ParseManualNetworks()
    {
        var networks = new List<IpNetwork>(options.MonitoringNetworks.Count);
        foreach (var text in options.MonitoringNetworks)
        {
            if (IpNetwork.TryParse(text, out var network))
            {
                networks.Add(network);
            }
        }

        return networks;
    }

    private static bool IsInManualNetwork(List<IpNetwork> networks, IPAddress address)
    {
        foreach (var network in networks)
        {
            if (network.Contains(address))
            {
                return true;
            }
        }

        return false;
    }
}
