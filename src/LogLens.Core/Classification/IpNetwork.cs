using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace LogLens.Core.Classification;

/// <summary>
/// Ein IP-Netz in CIDR-Schreibweise. Wird für die Verifikation von KI-Agenten
/// (SPEC 5.5), für den Monitoring-Verdacht (SPEC 5.8) und später für die Maskierung
/// in der Oberfläche gebraucht. Vergleich rein bitweise, ohne DNS und ohne Netzzugriff.
/// </summary>
public sealed class IpNetwork
{
    private readonly byte[] _network;

    private IpNetwork(string text, AddressFamily family, byte[] network, int prefixLength)
    {
        Text = text;
        Family = family;
        _network = network;
        PrefixLength = prefixLength;
    }

    /// <summary>Ursprüngliche Schreibweise, z. B. <c>216.73.216.0/22</c>.</summary>
    public string Text { get; }

    public AddressFamily Family { get; }

    public int PrefixLength { get; }

    public static IpNetwork Parse(string cidr) =>
        TryParse(cidr, out var network)
            ? network
            : throw new FormatException($"'{cidr}' ist kein gültiges IP-Netz in CIDR-Schreibweise.");

    /// <summary>
    /// Nimmt <c>a.b.c.d/len</c> und <c>addr</c> ohne Länge an; ohne Länge gilt die
    /// volle Adresslänge (/32 bzw. /128).
    /// </summary>
    public static bool TryParse(string? cidr, [NotNullWhen(true)] out IpNetwork? network)
    {
        network = null;
        if (string.IsNullOrWhiteSpace(cidr))
        {
            return false;
        }

        var text = cidr.Trim();
        var slash = text.IndexOf('/');
        var addressPart = slash < 0 ? text : text[..slash];

        if (!IPAddress.TryParse(addressPart, out var address))
        {
            return false;
        }

        var bits = address.GetAddressBytes().Length * 8;
        var prefixLength = bits;

        if (slash >= 0 && (!int.TryParse(
                text.AsSpan(slash + 1), NumberStyles.None, CultureInfo.InvariantCulture, out prefixLength)
            || prefixLength < 0 || prefixLength > bits))
        {
            return false;
        }

        network = new IpNetwork(text, address.AddressFamily, Mask(address.GetAddressBytes(), prefixLength), prefixLength);
        return true;
    }

    public bool Contains(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.AddressFamily != Family)
        {
            return false;
        }

        return HasSamePrefix(address.GetAddressBytes(), _network, PrefixLength);
    }

    public bool Contains(string? address) =>
        IPAddress.TryParse(address, out var parsed) && Contains(parsed);

    /// <summary>
    /// Netzanteil zweier Adressen vergleichen, ohne Zwischenobjekte. Grundlage der
    /// /16-Prüfung beim Monitoring-Verdacht.
    /// </summary>
    public static bool SameNetwork(IPAddress left, IPAddress right, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return left.AddressFamily == right.AddressFamily
            && HasSamePrefix(left.GetAddressBytes(), right.GetAddressBytes(), prefixLength);
    }

    /// <summary>Netzschlüssel wie <c>198.51.0.0/16</c>; für Gruppierungen und Anzeige.</summary>
    public static string NetworkKey(IPAddress address, int prefixLength)
    {
        ArgumentNullException.ThrowIfNull(address);

        var masked = new IPAddress(Mask(address.GetAddressBytes(), prefixLength));
        return $"{masked}/{prefixLength}";
    }

    public override string ToString() => Text;

    private static bool HasSamePrefix(byte[] left, byte[] right, int prefixLength)
    {
        var wholeBytes = prefixLength / 8;
        for (var i = 0; i < wholeBytes; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (left[wholeBytes] & mask) == (right[wholeBytes] & mask);
    }

    private static byte[] Mask(byte[] bytes, int prefixLength)
    {
        var masked = new byte[bytes.Length];
        var wholeBytes = Math.Min(prefixLength / 8, bytes.Length);
        Array.Copy(bytes, masked, wholeBytes);

        var remainingBits = prefixLength % 8;
        if (remainingBits != 0 && wholeBytes < bytes.Length)
        {
            masked[wholeBytes] = (byte)(bytes[wholeBytes] & (0xFF << (8 - remainingBits)));
        }

        return masked;
    }
}
