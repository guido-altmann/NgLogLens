namespace LogLens.Core.Models;

/// <summary>Ein Eintrag einer Top-Liste: Seite, Referrer oder Pfad mit seiner Anzahl.</summary>
public sealed record RankedItem(string Key, int Count);

/// <summary>
/// Aktivität eines Besucher-Netzes (/24 bzw. /48). Der Netzschlüssel ist bereits
/// maskiert und darf so angezeigt werden (CLAUDE.md, Datenschutz).
/// </summary>
/// <param name="Requests">Anfragen von Menschen aus diesem Netz, Assets eingeschlossen.</param>
/// <param name="DistinctIps">Unterschiedliche Client-IPs im Netz.</param>
/// <param name="HasMonitoring">Mindestens eine IP im Netz steht unter Monitoring-Verdacht (SPEC 5.8).</param>
public sealed record NetworkActivity(
    string Network,
    int PageViews,
    int Requests,
    int DistinctIps,
    bool HasMonitoring);

/// <summary>
/// Kennzahlen der Besucher-Ansicht (SPEC 6, 8.3). Grundlage sind nur Anfragen der
/// Klasse <see cref="TrafficClass.Human"/>; es gibt sie einmal mit und einmal ohne
/// Monitoring-Verdacht.
/// </summary>
/// <param name="TopPages">Seitenaufrufe je normalisiertem Pfad (SPEC 5.7).</param>
/// <param name="DistinctPages">Unterschiedliche Seiten, auch jenseits der Top-Liste.</param>
/// <param name="TopReferrers">Referrer-Hosts der Seitenaufrufe, eigene Domain ausgeschlossen.</param>
/// <param name="RequestsPerHour">24 Werte, Index = Stunde in UTC. Nur Einträge mit mindestens stundengenauer Zeit.</param>
public sealed record VisitorStatistics(
    int Requests,
    int PageViews,
    IReadOnlyList<RankedItem> TopPages,
    int DistinctPages,
    IReadOnlyList<RankedItem> TopReferrers,
    IReadOnlyList<int> RequestsPerHour,
    IReadOnlyList<NetworkActivity> TopNetworks,
    int Networks)
{
    public static VisitorStatistics Empty { get; } = new(0, 0, [], 0, [], new int[24], [], 0);
}

/// <summary>Anfragen einer Angriffskategorie mit ihrem Anteil (0–1) an allen Angriffen.</summary>
public sealed record CategoryCount(string Category, int Requests, double Share);

/// <summary>Kennzahlen der Angriffs-Ansicht (SPEC 6, 8.4). Die Scanner stehen in <see cref="AnalysisResult.Scanners"/>.</summary>
/// <param name="DistinctIps">Unterschiedliche Client-IPs mit mindestens einer Angriffs-Anfrage.</param>
/// <param name="ScannerRequests">Anfragen von Scanner-IPs, Aufklärung eingeschlossen.</param>
/// <param name="RequestsPerHour">24 Werte, Index = Stunde in UTC. Nur Einträge mit mindestens stundengenauer Zeit.</param>
public sealed record AttackStatistics(
    int Requests,
    int DistinctIps,
    int ScannerRequests,
    IReadOnlyList<CategoryCount> Categories,
    IReadOnlyList<int> RequestsPerHour)
{
    public static AttackStatistics Empty { get; } = new(0, 0, 0, [], new int[24]);
}

/// <summary>
/// Ein KI-Agent mit der Verteilung seiner Verdikte (SPEC 5.5, 8.5). Auch getarnte
/// Anfragen zählen hier mit, obwohl sie in der Klasse <see cref="TrafficClass.Attack"/> stehen.
/// </summary>
/// <param name="Provider">Anbieter laut <c>ai-agents.json</c>; null, wenn der Agent dort fehlt.</param>
/// <param name="VerifiedPages">Von verifizierten Anfragen abgerufene Pfade, normalisiert.</param>
/// <param name="VerifiedWithoutPath">Verifizierte Anfragen aus gekürzten Zeilen, deren Pfad fehlt.</param>
public sealed record AiAgentStatistics(
    string Name,
    string? Provider,
    int Verified,
    int Unverified,
    int Spoofed,
    IReadOnlyList<RankedItem> VerifiedPages,
    int VerifiedWithoutPath)
{
    public int Requests => Verified + Unverified + Spoofed;

    public int Of(AiVerdict verdict) => verdict switch
    {
        AiVerdict.Verified => Verified,
        AiVerdict.Unverified => Unverified,
        AiVerdict.Spoofed => Spoofed,
        _ => 0,
    };
}

/// <summary>Anfragen mit einem bestimmten HTTP-Status.</summary>
public sealed record StatusCount(int Status, int Requests);

/// <summary>Serverfehler (5xx) je Pfad und Status; Grundlage für das Finding „Serverfehler" (SPEC 7).</summary>
public sealed record ServerErrorGroup(
    string Path,
    int Status,
    int Requests,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen);

/// <summary>
/// Eine Proxy-Instanz (bei Coolify eine Traefik-IP) mit ihrem ersten und letzten
/// Auftreten. Wechselt die IP, wurde meist neu deployt (SPEC 6).
/// </summary>
public sealed record ProxyInstance(
    string ProxyIp,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    int Requests)
{
    public DateOnly FirstDay => DateOnly.FromDateTime(FirstSeen.UtcDateTime);

    public DateOnly LastDay => DateOnly.FromDateTime(LastSeen.UtcDateTime);
}

/// <summary>Error-Zeilen je Level. <paramref name="Level"/> ist null bei gekürzten Zeilen ohne Level.</summary>
public sealed record ErrorLevelCount(string? Level, int Lines);

/// <summary>Kennzahlen der Ansicht „Server-Zustand" (SPEC 6, 8.6).</summary>
/// <param name="StatusCodes">Nur vollständige Zeilen; aufsteigend nach Status.</param>
/// <param name="RequestsWithoutStatus">Gekürzte oder ungültige Zeilen ohne Status.</param>
/// <param name="ServerErrors">5xx je Pfad, häufigste zuerst.</param>
/// <param name="ProxyInstances">In der Reihenfolge ihres ersten Auftretens.</param>
/// <param name="ErrorLevels">Error-Zeilen ohne die aussortierten 404-Folgefehler (SPEC 3).</param>
public sealed record ServerHealthStatistics(
    IReadOnlyList<StatusCount> StatusCodes,
    int RequestsWithoutStatus,
    IReadOnlyList<ServerErrorGroup> ServerErrors,
    IReadOnlyList<ProxyInstance> ProxyInstances,
    IReadOnlyList<ErrorLevelCount> ErrorLevels)
{
    public static ServerHealthStatistics Empty { get; } = new([], 0, [], [], []);

    public int ServerErrorRequests => ServerErrors.Sum(e => e.Requests);
}
