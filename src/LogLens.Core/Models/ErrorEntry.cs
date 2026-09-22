namespace LogLens.Core.Models;

/// <summary>
/// Eine Zeile aus dem Error-Log (SPEC 2.2). Folgefehler einer fehlenden 404-Seite
/// werden vor der Auswertung aussortiert (SPEC 3).
/// </summary>
public sealed record ErrorEntry(
    int LineNumber,
    DateTimeOffset Timestamp,
    TimePrecision TimePrecision,
    string? Level,
    string Message,
    string? Request,
    string? Host,
    string? ClientIp,
    bool IsTruncated);
