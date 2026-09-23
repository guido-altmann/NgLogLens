using System.Text.Json.Serialization;
using LogLens.Core;

namespace LogLens.Web.Services;

/// <summary>Erscheinungsbild der Oberfläche (SPEC 8: folgt dem System, umschaltbar).</summary>
public enum ThemePreference
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Die Einstellungen aus SPEC 8.9 in genau der Form, in der sie in localStorage
/// liegen. Nur Einstellungen werden gespeichert, niemals Loginhalte (CLAUDE.md,
/// Datenschutz). Die Vorgaben stammen aus den Options-Klassen des Kerns, damit es
/// keine zweite Stelle mit denselben Zahlen gibt.
/// </summary>
public sealed record AppSettings
{
    public int ScannerThreshold { get; init; }

    public int MonitoringWindowSeconds { get; init; }

    public int MonitoringMinOccurrences { get; init; }

    public int ScanBurstRequests { get; init; }

    public int ScanBurstWindowMinutes { get; init; }

    /// <summary>IPs oder Netze in CIDR-Schreibweise, die als eigenes Monitoring gelten (SPEC 5.8).</summary>
    public IReadOnlyList<string> MonitoringNetworks { get; init; } = [];

    /// <summary>Eigene Domains; ihre Referrer zählen nicht als Herkunft (SPEC 6).</summary>
    public IReadOnlyList<string> OwnDomains { get; init; } = [];

    public int MaxFileSizeMegabytes { get; init; }

    /// <summary>Besucher-IPs ungekürzt zeigen statt auf /24 bzw. /48 zu maskieren.</summary>
    public bool ShowFullVisitorIps { get; init; }

    /// <summary>Schalter „Monitoring aus Seitenaufrufen herausrechnen" (SPEC 5.8).</summary>
    public bool ExcludeMonitoring { get; init; }

    /// <summary>Hell, dunkel oder wie das Betriebssystem. Ändert nur die Anzeige.</summary>
    public ThemePreference Theme { get; init; }

    /// <summary>
    /// Einstellungen, die eine Anfrage anders einordnen oder zählen lassen. Ändert sich
    /// eine davon, muss die geöffnete Datei neu ausgewertet werden.
    /// </summary>
    public bool RequiresReanalysis(AppSettings other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return ScannerThreshold != other.ScannerThreshold
            || MonitoringWindowSeconds != other.MonitoringWindowSeconds
            || MonitoringMinOccurrences != other.MonitoringMinOccurrences
            || ScanBurstRequests != other.ScanBurstRequests
            || ScanBurstWindowMinutes != other.ScanBurstWindowMinutes
            || !MonitoringNetworks.SequenceEqual(other.MonitoringNetworks, StringComparer.OrdinalIgnoreCase)
            || !OwnDomains.SequenceEqual(other.OwnDomains, StringComparer.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Quellgenerierte Deserialisierung: unter WebAssembly wird getrimmt, Reflexion über
/// Modelltypen wäre dort weder sicher noch schnell (wie in <c>LogLens.Core</c>).
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
