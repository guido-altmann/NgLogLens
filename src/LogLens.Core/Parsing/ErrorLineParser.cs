using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

/// <summary>Vollständige Error-Zeile (SPEC 2.2).</summary>
public sealed class ErrorLineParser : ILogLineParser
{
    private const string TimestampFormat = "yyyy/MM/dd HH:mm:ss";

    public bool TryParse(string line, int lineNumber, [NotNullWhen(true)] out ParsedLine? parsed)
    {
        parsed = null;

        var match = LogPatterns.ErrorLine.Match(line);
        if (!match.Success)
        {
            return false;
        }

        // nginx schreibt im Error-Log keinen Zeitzonenversatz; der Coolify-Export läuft in UTC.
        if (!DateTimeOffset.TryParseExact(
                $"{match.Groups["date"].Value} {match.Groups["time"].Value}", TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        var rest = match.Groups["rest"].Value;

        parsed = new ParsedErrorLine(new ErrorEntry(
            LineNumber: lineNumber,
            Timestamp: timestamp,
            TimePrecision: TimePrecision.Second,
            Level: match.Groups["level"].Value,
            Message: ExtractMessage(rest),
            Request: Group(LogPatterns.ErrorRequest, rest, "request"),
            Host: Group(LogPatterns.ErrorHost, rest, "host"),
            ClientIp: Group(LogPatterns.ErrorClient, rest, "client")?.Trim(),
            IsTruncated: false));

        return true;
    }

    private static string ExtractMessage(string rest)
    {
        var message = LogPatterns.ErrorMessagePrefix.Replace(rest, string.Empty, 1);

        var clientMarker = message.IndexOf(", client:", StringComparison.Ordinal);
        if (clientMarker >= 0)
        {
            message = message[..clientMarker];
        }

        return message.Trim();
    }

    private static string? Group(System.Text.RegularExpressions.Regex regex, string input, string name)
    {
        var match = regex.Match(input);
        return match.Success && match.Groups[name].Value.Length > 0 ? match.Groups[name].Value : null;
    }
}
