using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

/// <summary>Vollständige Access-Zeile im Format <c>combined</c> plus X-Forwarded-For (SPEC 2.1).</summary>
public sealed class AccessLineParser : ILogLineParser
{
    private const string TimestampFormat = "dd/MMM/yyyy:HH:mm:ss zzz";

    public bool TryParse(string line, int lineNumber, [NotNullWhen(true)] out ParsedLine? parsed)
    {
        parsed = null;

        var match = LogPatterns.AccessLine.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!DateTimeOffset.TryParseExact(
                match.Groups["timestamp"].ValueSpan, TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        var forwarded = match.Groups["forwarded"];
        var proxyIp = match.Groups["proxy"].Value;
        var clientIp = forwarded.Success ? FirstForwardedIp(forwarded.Value) : null;

        var (method, path, query, protocol) = SplitRequest(match.Groups["request"].Value);

        parsed = new ParsedAccessLine(new AccessEntry(
            LineNumber: lineNumber,
            Timestamp: timestamp,
            TimePrecision: TimePrecision.Second,
            ProxyIp: proxyIp,
            ClientIp: string.IsNullOrEmpty(clientIp) ? proxyIp : clientIp,
            HasClientIpHeader: !string.IsNullOrEmpty(clientIp),
            Method: method,
            Path: path,
            Query: query,
            Protocol: protocol,
            Status: ParseInt(match.Groups["status"].Value),
            Bytes: ParseLong(match.Groups["bytes"].Value),
            Referer: Nullable(match.Groups["referer"].Value),
            UserAgent: Nullable(match.Groups["agent"].Value),
            IsTruncated: false));

        return true;
    }

    /// <summary>Bei einer Kette „a, b" gilt die erste Adresse als Client (SPEC 2.1).</summary>
    private static string? FirstForwardedIp(string value)
    {
        var separator = value.IndexOf(',');
        var first = (separator < 0 ? value : value[..separator]).Trim();
        return first is "" or "-" ? null : first;
    }

    /// <summary>
    /// Zerlegt <c>"GET /pfad?query HTTP/1.1"</c>. Ungültige Request-Zeilen bleiben als
    /// Anfrage ohne Methode erhalten (SPEC 2.1).
    /// </summary>
    private static (string? Method, string? Path, string? Query, string? Protocol) SplitRequest(string request)
    {
        if (request.Length == 0 || request == "-")
        {
            return (null, null, null, null);
        }

        var parts = request.Split(' ');
        if (parts.Length is not (2 or 3) || !LogPatterns.HttpMethod.IsMatch(parts[0]))
        {
            return (null, null, null, null);
        }

        var target = parts[1];
        var questionMark = target.IndexOf('?');
        var path = questionMark < 0 ? target : target[..questionMark];
        var query = questionMark < 0 ? null : target[(questionMark + 1)..];

        return (parts[0], path, Nullable(query), parts.Length == 3 ? parts[2] : null);
    }

    private static string? Nullable(string? value) =>
        string.IsNullOrEmpty(value) || value == "-" ? null : value;

    private static int? ParseInt(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static long? ParseLong(string value) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
