using System.Net;
using System.Net.Sockets;
using LogLens.Core;
using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Web.Services;

/// <summary>
/// IP-Adressen für die Anzeige. Besucher-IPs werden auf /24 (IPv4) bzw. /48 (IPv6)
/// maskiert, Scanner-IPs bleiben vollständig, damit sie in eine Blockliste passen
/// (CLAUDE.md, Datenschutz).
/// </summary>
public sealed class IpDisplay(AggregationOptions options, ViewPreferences preferences)
{
    /// <param name="isScanner">Scanner-IPs werden nie maskiert.</param>
    public string Format(string clientIp, bool isScanner)
    {
        if (isScanner || preferences.ShowFullVisitorIps || !IPAddress.TryParse(clientIp, out var address))
        {
            return clientIp;
        }

        var prefixLength = address.AddressFamily == AddressFamily.InterNetwork
            ? options.VisitorNetworkIpv4PrefixLength
            : options.VisitorNetworkIpv6PrefixLength;

        return IpNetwork.NetworkKey(address, prefixLength);
    }

    public string Format(ClassifiedEntry entry, AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(result);

        return Format(entry.ClientIp, result.Classification.ScannerIps.Contains(entry.ClientIp));
    }
}
