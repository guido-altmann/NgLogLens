namespace LogLens.Core.Models;

/// <summary>
/// Zeitraum der Auswertung (SPEC 6). Grenzen sind die Zeitstempel der ersten und
/// letzten Anfrage, die Tage werden in UTC gezählt.
/// </summary>
public sealed record TimeRange(DateTimeOffset First, DateTimeOffset Last)
{
    public DateOnly FirstDay => DateOnly.FromDateTime(First.UtcDateTime);

    public DateOnly LastDay => DateOnly.FromDateTime(Last.UtcDateTime);

    /// <summary>Kalendertage einschließlich Rand, also mindestens 1.</summary>
    public int Days => LastDay.DayNumber - FirstDay.DayNumber + 1;
}

/// <summary>
/// Gewählter Zeitraum der Ansicht (SPEC 8, „Globaler Zeitraumfilter"). Beide Grenzen
/// sind Kalendertage in UTC und gehören zum Zeitraum dazu; null heißt „offen".
/// </summary>
public sealed record DayRange(DateOnly? From, DateOnly? To)
{
    public bool IsOpen => From is null && To is null;

    /// <summary>Deckt der Zeitraum die gesamte Auswertung ab, ist er keine Einschränkung.</summary>
    public bool Covers(TimeRange period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return (From is null || From <= period.FirstDay) && (To is null || To >= period.LastDay);
    }

    public bool Contains(DateOnly day) => day >= (From ?? DateOnly.MinValue) && day <= (To ?? DateOnly.MaxValue);
}

/// <summary>
/// Anfragen eines Kalendertages je Klasse. Die Reihe ist lückenlos: Tage ohne
/// Einträge stehen mit 0 darin (SPEC 6).
/// </summary>
public sealed record DailyTraffic(DateOnly Day, int Human, int AiAgent, int Bot, int Attack)
{
    public int Total => Human + AiAgent + Bot + Attack;

    /// <summary>Für den Umschalter „Ohne Angriffe" der Übersicht (SPEC 8.2).</summary>
    public int WithoutAttacks => Human + AiAgent + Bot;

    public bool IsEmpty => Total == 0;

    public int Of(TrafficClass trafficClass) => trafficClass switch
    {
        TrafficClass.Human => Human,
        TrafficClass.AiAgent => AiAgent,
        TrafficClass.Bot => Bot,
        TrafficClass.Attack => Attack,
        _ => 0,
    };
}

/// <summary>Anfragen einer Verkehrsklasse mit ihrem Anteil (0–1) an allen Anfragen.</summary>
public sealed record ClassCount(TrafficClass Class, int Requests, double Share);

/// <summary>
/// Ergebnis der Stufe „Aggregate" (SPEC 6). Enthält die Zählwerte des Einlesens und
/// die klassifizierten Anfragen, damit die Detailseiten darauf aufbauen können.
/// </summary>
/// <param name="Period">Null, wenn die Datei keine einzige Anfrage enthielt.</param>
/// <param name="VisitorNetworks">Unterschiedliche /24- bzw. /48-Netze mit Seitenaufrufen.</param>
public sealed record AnalysisResult(
    IReadOnlyList<string> FileNames,
    ParseDiagnostics Diagnostics,
    IReadOnlyList<ErrorEntry> ErrorEntries,
    ClassificationResult Classification,
    TimeRange? Period,
    IReadOnlyList<ClassCount> Classes,
    IReadOnlyList<DailyTraffic> Daily,
    IReadOnlyList<DateOnly> DaysWithoutEntries,
    int PageViews,
    int PageViewsWithoutMonitoring,
    int VisitorNetworks,
    int VisitorNetworksWithoutMonitoring)
{
    public IReadOnlyList<ClassifiedEntry> Entries => Classification.Entries;

    /// <summary>Empfehlungen aus den Regeln von SPEC 7, dringendste zuerst.</summary>
    public IReadOnlyList<Finding> Findings { get; init; } = [];

    /// <summary>
    /// Gesetzt, wenn dieses Ergebnis auf einen Zeitraum eingeschränkt wurde. Die
    /// Zählwerte der Parser-Diagnose beziehen sich weiterhin auf die ganze Datei:
    /// Leerzeilen und unbekannte Zeilen haben keinen Zeitstempel.
    /// </summary>
    public DayRange? Range { get; init; }

    /// <summary>Eigene Domains: konfiguriert oder aus den Error-Zeilen erkannt.</summary>
    public IReadOnlyList<string> OwnDomains { get; init; } = [];

    /// <summary>Besucher-Kennzahlen mit allen Anfragen von Menschen (SPEC 8.3).</summary>
    public VisitorStatistics Visitors { get; init; } = VisitorStatistics.Empty;

    /// <summary>Besucher-Kennzahlen ohne Anfragen mit Monitoring-Verdacht (SPEC 5.8).</summary>
    public VisitorStatistics VisitorsWithoutMonitoring { get; init; } = VisitorStatistics.Empty;

    public AttackStatistics Attacks { get; init; } = AttackStatistics.Empty;

    /// <summary>KI-Agenten, meiste Anfragen zuerst (SPEC 8.5).</summary>
    public IReadOnlyList<AiAgentStatistics> AiAgents { get; init; } = [];

    public ServerHealthStatistics ServerHealth { get; init; } = ServerHealthStatistics.Empty;

    public IReadOnlyList<Scanner> Scanners => Classification.Scanners;

    /// <summary>Anfragen insgesamt: vollständige und gekürzte Access-Zeilen (SPEC 3).</summary>
    public int Requests => Diagnostics.Requests;

    public bool IsEmpty => Requests == 0;

    public int Count(TrafficClass trafficClass)
    {
        foreach (var entry in Classes)
        {
            if (entry.Class == trafficClass)
            {
                return entry.Requests;
            }
        }

        return 0;
    }

    /// <summary>Seitenaufrufe je nach Schalter „Monitoring herausrechnen" (SPEC 5.8).</summary>
    public int PageViewsFor(bool excludeMonitoring) =>
        excludeMonitoring ? PageViewsWithoutMonitoring : PageViews;

    public int VisitorNetworksFor(bool excludeMonitoring) =>
        excludeMonitoring ? VisitorNetworksWithoutMonitoring : VisitorNetworks;

    public VisitorStatistics VisitorsFor(bool excludeMonitoring) =>
        excludeMonitoring ? VisitorsWithoutMonitoring : Visitors;
}
