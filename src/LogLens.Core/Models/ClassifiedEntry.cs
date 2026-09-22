namespace LogLens.Core.Models;

/// <summary>
/// Eine Anfrage mit ihrem Urteil (SPEC 4). <paramref name="Reason"/> ist der kurze,
/// menschenlesbare Grund für die Einordnung und wird in der Rohdaten-Ansicht gezeigt.
/// </summary>
/// <param name="AttackCategory">Gesetzt, wenn <paramref name="Class"/> = <see cref="TrafficClass.Attack"/>.</param>
/// <param name="AgentName">Erkannter KI-Agent, auch wenn die Anfrage als Angriff gilt (Tarnung).</param>
/// <param name="AiVerdict">Gesetzt, sobald <paramref name="AgentName"/> gesetzt ist.</param>
public sealed record ClassifiedEntry(
    AccessEntry Entry,
    TrafficClass Class,
    string? AttackCategory,
    string? AgentName,
    AiVerdict? AiVerdict,
    bool IsPageView,
    bool IsMonitoringSuspected,
    string Reason)
{
    /// <summary>Kürzel für Filter und Gruppierungen.</summary>
    public string ClientIp => Entry.ClientIp;

    public DateTimeOffset Timestamp => Entry.Timestamp;
}

/// <summary>
/// Ein Scanner: eine Client-IP mit mindestens <c>ScannerThreshold</c> Einzelangriffen (SPEC 5.2).
/// </summary>
/// <param name="IndividualAttacks">Anfragen, die für sich allein schon ein Angriff sind (SPEC 5.1).</param>
/// <param name="Requests">Alle Anfragen dieser IP, also inklusive der Aufklärung.</param>
public sealed record Scanner(
    string ClientIp,
    int IndividualAttacks,
    int Requests,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    string MainCategory)
{
    /// <summary>Dauer des Bursts; Grundlage für das Finding „Scan-Bursts" (SPEC 7).</summary>
    public TimeSpan Burst => LastSeen - FirstSeen;
}

/// <summary>Ergebnis der Stufe „Classify" der Pipeline.</summary>
/// <param name="ScannerIps">Client-IPs, deren Anfragen vollständig als Angriff gelten (SPEC 5.2).</param>
/// <param name="MonitoringIps">Client-IPs mit Monitoring-Verdacht (SPEC 5.8).</param>
public sealed record ClassificationResult(
    IReadOnlyList<ClassifiedEntry> Entries,
    IReadOnlyList<Scanner> Scanners,
    IReadOnlySet<string> ScannerIps,
    IReadOnlySet<string> MonitoringIps);

/// <summary>Fortschritt der Klassifizierung; im Browser alle paar tausend Anfragen gemeldet.</summary>
public sealed record ClassifyProgress(int EntriesDone, int EntriesTotal);
