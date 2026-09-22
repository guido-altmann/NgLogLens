namespace LogLens.Core.Models;

/// <summary>
/// Eine Anfrage aus dem Access-Log (SPEC 2.1). Genau eine Access-Zeile, vollständig
/// oder gekürzt, ist genau eine Anfrage (SPEC 3).
/// </summary>
/// <param name="LineNumber">1-basierte Zeilennummer, über mehrere Dateien fortlaufend.</param>
/// <param name="ProxyIp">Absender laut Logzeile, bei Coolify die Traefik-Instanz.</param>
/// <param name="ClientIp">Erste IP aus X-Forwarded-For, sonst <paramref name="ProxyIp"/>.</param>
/// <param name="HasClientIpHeader">False, wenn die Zeile kein X-Forwarded-For-Feld hatte.</param>
/// <param name="Path">Pfad ohne Query, im Rohzustand (nicht URL-dekodiert).</param>
/// <param name="Query">Query ohne führendes Fragezeichen, im Rohzustand.</param>
/// <param name="IsTruncated">Zeile war gekürzt; Pfad, Status und Größe fehlen.</param>
public sealed record AccessEntry(
    int LineNumber,
    DateTimeOffset Timestamp,
    TimePrecision TimePrecision,
    string ProxyIp,
    string ClientIp,
    bool HasClientIpHeader,
    string? Method,
    string? Path,
    string? Query,
    string? Protocol,
    int? Status,
    long? Bytes,
    string? Referer,
    string? UserAgent,
    bool IsTruncated);
