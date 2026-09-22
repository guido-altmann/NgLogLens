using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using LogLens.Core.Models;

namespace LogLens.Core.Parsing;

/// <summary>
/// Gekürzte Error-Zeile (SPEC 2.3). Sie beginnt wie eine Error-Zeile; dass sie auf
/// <c>"&lt;IP&gt;"</c> endet, macht sie nicht zu einer Anfrage.
/// </summary>
public sealed class TruncatedErrorLineParser : ILogLineParser
{
    private const string DateFormat = "yyyy/MM/dd";

    public bool TryParse(string line, int lineNumber, [NotNullWhen(true)] out ParsedLine? parsed)
    {
        parsed = null;

        var match = LogPatterns.TruncatedErrorLine.Match(line);
        if (!match.Success)
        {
            return false;
        }

        if (!DateOnly.TryParseExact(
                match.Groups["date"].ValueSpan, DateFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        var hour = match.Groups["hour"];
        var minute = match.Groups["minute"];
        var second = match.Groups["second"];

        var precision = (hour.Success, minute.Success, second.Success) switch
        {
            (false, _, _) => TimePrecision.Day,
            (true, false, _) => TimePrecision.Hour,
            (true, true, false) => TimePrecision.Minute,
            _ => TimePrecision.Second,
        };

        var timestamp = new DateTimeOffset(
            date.Year, date.Month, date.Day,
            Value(hour), Value(minute), Value(second), TimeSpan.Zero);

        parsed = new ParsedErrorLine(new ErrorEntry(
            LineNumber: lineNumber,
            Timestamp: timestamp,
            TimePrecision: precision,
            Level: null,
            Message: match.Groups["rest"].Value.Trim(),
            Request: null,
            Host: null,
            ClientIp: null,
            IsTruncated: true));

        return true;
    }

    private static int Value(System.Text.RegularExpressions.Group group) =>
        group.Success ? int.Parse(group.ValueSpan, CultureInfo.InvariantCulture) : 0;
}
