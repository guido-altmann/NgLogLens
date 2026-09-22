using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

/// <summary>
/// Gekürzte Access-Zeile (SPEC 2.3): Datum, Proxy-IP und Client-IP sind noch lesbar,
/// Pfad und Status fehlen. Der Resttext bleibt erhalten, damit die Agent-Erkennung
/// eine enthaltene Kontaktdomain auswerten kann.
/// </summary>
public sealed class TruncatedAccessLineParser : ILogLineParser
{
    private const string DateFormat = "dd/MMM/yyyy";

    public bool TryParse(string line, int lineNumber, [NotNullWhen(true)] out ParsedLine? parsed)
    {
        parsed = null;

        var match = LogPatterns.TruncatedAccessLine.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var forwarded = match.Groups["forwarded"].Value.Split(',')[0].Trim();
        if (!IPAddress.TryParse(forwarded, out _))
        {
            return false;
        }

        if (!DateOnly.TryParseExact(
                match.Groups["date"].ValueSpan, DateFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        var (timestamp, precision) = CombineTime(date, match.Groups["time"].Value);

        parsed = new ParsedAccessLine(new AccessEntry(
            LineNumber: lineNumber,
            Timestamp: timestamp,
            TimePrecision: precision,
            ProxyIp: match.Groups["proxy"].Value,
            ClientIp: forwarded,
            HasClientIpHeader: true,
            Method: null,
            Path: null,
            Query: null,
            Protocol: null,
            Status: null,
            Bytes: null,
            Referer: null,
            UserAgent: Remainder(match.Groups["rest"].Value),
            IsTruncated: true));

        return true;
    }

    /// <summary>
    /// Die Zeitangabe kann nach dem Datum abgeschnitten sein. Fehlende Teile werden auf
    /// den Beginn der bekannten Einheit gesetzt und über <see cref="TimePrecision"/> markiert.
    /// Ein Zeitzonenversatz ist bei gekürzten Zeilen nicht mehr lesbar; nginx protokolliert
    /// im Coolify-Export in UTC.
    /// </summary>
    private static (DateTimeOffset Timestamp, TimePrecision Precision) CombineTime(DateOnly date, string time)
    {
        var parts = time.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var hour = parts.Length > 0 ? int.Parse(parts[0], CultureInfo.InvariantCulture) : 0;
        var minute = parts.Length > 1 ? int.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        var second = parts.Length > 2 ? int.Parse(parts[2], CultureInfo.InvariantCulture) : 0;

        var precision = parts.Length switch
        {
            0 => TimePrecision.Day,
            1 => TimePrecision.Hour,
            2 => TimePrecision.Minute,
            _ => TimePrecision.Second,
        };

        var timestamp = new DateTimeOffset(
            date.Year, date.Month, date.Day, hour, minute, second, TimeSpan.Zero);

        return (timestamp, precision);
    }

    private static string? Remainder(string rest)
    {
        var trimmed = rest.TrimStart(':', ' ').Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
