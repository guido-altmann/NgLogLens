using System.Text.RegularExpressions;

namespace LogLens.Core.Parsing;

/// <summary>
/// Alle Zeilenmuster als Source-Generator-Regex. <c>RegexOptions.Compiled</c> bringt
/// unter WebAssembly nichts und wird deshalb nicht verwendet (CLAUDE.md).
/// </summary>
internal static partial class LogPatterns
{
    /// <summary>nginx <c>combined</c> mit optionalem X-Forwarded-For als letztem Feld.</summary>
    [GeneratedRegex(
        """^(?<proxy>\S+) (?<ident>\S+) (?<user>\S+) \[(?<timestamp>[^\]]+)\] "(?<request>[^"]*)" (?<status>\d{3}|-) (?<bytes>\d+|-) "(?<referer>[^"]*)" "(?<agent>[^"]*)"(?:\s+"(?<forwarded>[^"]*)")?\s*$""")]
    internal static partial Regex AccessLine { get; }

    /// <summary>
    /// Gekürzte Access-Zeile: beginnt wie eine Access-Zeile, Uhrzeit möglicherweise
    /// abgeschnitten, endet auf <c>"&lt;IP&gt;"</c> (SPEC 2.3).
    /// </summary>
    [GeneratedRegex(
        """^(?<proxy>\S+) (?<ident>\S+) (?<user>\S+) \[(?<date>\d{2}/[A-Za-z]{3}/\d{4})(?<time>(?::\d{2}){0,3})(?<rest>.*)"(?<forwarded>[^"]+)"\s*$""")]
    internal static partial Regex TruncatedAccessLine { get; }

    /// <summary>nginx-Error-Log.</summary>
    [GeneratedRegex(
        """^(?<date>\d{4}/\d{2}/\d{2}) (?<time>\d{2}:\d{2}:\d{2}) \[(?<level>[a-z]+)\] (?<rest>.*)$""")]
    internal static partial Regex ErrorLine { get; }

    /// <summary>Gekürzte Error-Zeile: Datum sicher, Uhrzeit nur teilweise vorhanden.</summary>
    [GeneratedRegex(
        """^(?<date>\d{4}/\d{2}/\d{2})(?: (?<hour>\d{2})(?::(?<minute>\d{2})(?::(?<second>\d{2}))?)?)?(?<rest>.*)$""")]
    internal static partial Regex TruncatedErrorLine { get; }

    /// <summary>Worker- und Verbindungskennung am Anfang einer Error-Meldung.</summary>
    [GeneratedRegex(@"^\d+#\d+:\s+(?:\*\d+\s+)?")]
    internal static partial Regex ErrorMessagePrefix { get; }

    [GeneratedRegex(@",\s*client:\s*(?<client>[^,]+)")]
    internal static partial Regex ErrorClient { get; }

    [GeneratedRegex("""
        ,\s*request:\s*"(?<request>[^"]*)"
        """, RegexOptions.IgnorePatternWhitespace)]
    internal static partial Regex ErrorRequest { get; }

    [GeneratedRegex("""
        ,\s*host:\s*"(?<host>[^"]*)"
        """, RegexOptions.IgnorePatternWhitespace)]
    internal static partial Regex ErrorHost { get; }

    /// <summary>Plausible HTTP-Methode; alles andere gilt als ungültige Request-Zeile.</summary>
    [GeneratedRegex("^[A-Za-z]{3,20}$")]
    internal static partial Regex HttpMethod { get; }
}
