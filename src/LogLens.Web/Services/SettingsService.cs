using System.Text.Json;
using LogLens.Core;
using Microsoft.JSInterop;

namespace LogLens.Web.Services;

/// <summary>
/// Einstellungen lesen, anwenden und in <c>localStorage</c> ablegen (SPEC 8.9).
/// Persistiert werden ausschließlich Einstellungen – keine Logzeilen, keine
/// Auswertung (CLAUDE.md, Datenschutz).
/// </summary>
public sealed class SettingsService(
    IJSRuntime js,
    ParserOptions parserOptions,
    ClassificationOptions classificationOptions,
    AggregationOptions aggregationOptions,
    FindingOptions findingOptions,
    ViewPreferences preferences)
{
    public const string StorageKey = "loglens.settings";

    private const long BytesPerMegabyte = 1024 * 1024;

    /// <summary>Die Vorgaben des Kerns, festgehalten vor der ersten Änderung.</summary>
    public AppSettings Defaults { get; } = Read(
        parserOptions, classificationOptions, aggregationOptions, findingOptions, new ViewPreferences());

    public AppSettings Current { get; private set; } = Read(
        parserOptions, classificationOptions, aggregationOptions, findingOptions, preferences);

    public event Action? Changed;

    private bool _applying;

    /// <summary>
    /// Gespeicherte Einstellungen übernehmen. Fehlt oder bricht der Speicher (privater
    /// Modus, abgeschaltete Website-Daten), bleibt es bei den Vorgaben.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Die beiden Ansichtsschalter lassen sich auch außerhalb der Einstellungsseite
        // umlegen (SPEC 8.3); auch dann sollen sie die Sitzung überdauern.
        preferences.Changed -= OnPreferencesChanged;
        preferences.Changed += OnPreferencesChanged;

        string? json;
        try
        {
            json = await js.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        }
        catch (JSException)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        AppSettings? stored;
        try
        {
            stored = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings);
        }
        catch (JsonException)
        {
            // Ein kaputter Eintrag darf die App nicht am Start hindern.
            return;
        }

        if (stored is not null)
        {
            Apply(Sanitize(stored));
        }
    }

    /// <summary>Einstellungen übernehmen und sichern.</summary>
    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Apply(Sanitize(settings));

        try
        {
            await js.InvokeVoidAsync(
                "localStorage.setItem",
                cancellationToken,
                StorageKey,
                JsonSerializer.Serialize(Current, SettingsJsonContext.Default.AppSettings));
        }
        catch (JSException)
        {
            // Ohne Speicher gelten die Einstellungen trotzdem – nur eben diese Sitzung.
        }
    }

    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        Apply(Defaults);

        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
        }
        catch (JSException)
        {
        }
    }

    private void OnPreferencesChanged()
    {
        if (_applying)
        {
            return;
        }

        var updated = Current with
        {
            ShowFullVisitorIps = preferences.ShowFullVisitorIps,
            ExcludeMonitoring = preferences.ExcludeMonitoring,
        };

        if (updated != Current)
        {
            _ = SaveAsync(updated);
        }
    }

    private void Apply(AppSettings settings)
    {
        parserOptions.MaxFileSizeBytes = settings.MaxFileSizeMegabytes * BytesPerMegabyte;

        classificationOptions.ScannerThreshold = settings.ScannerThreshold;
        classificationOptions.MonitoringWindow = TimeSpan.FromSeconds(settings.MonitoringWindowSeconds);
        classificationOptions.MonitoringMinOccurrences = settings.MonitoringMinOccurrences;
        classificationOptions.MonitoringNetworks.Clear();
        foreach (var network in settings.MonitoringNetworks)
        {
            classificationOptions.MonitoringNetworks.Add(network);
        }

        aggregationOptions.OwnDomains.Clear();
        foreach (var domain in settings.OwnDomains)
        {
            aggregationOptions.OwnDomains.Add(domain);
        }

        findingOptions.ScanBurstRequests = settings.ScanBurstRequests;
        findingOptions.ScanBurstWindow = TimeSpan.FromMinutes(settings.ScanBurstWindowMinutes);

        _applying = true;
        try
        {
            preferences.ShowFullVisitorIps = settings.ShowFullVisitorIps;
            preferences.ExcludeMonitoring = settings.ExcludeMonitoring;
        }
        finally
        {
            _applying = false;
        }

        Current = settings;
        Changed?.Invoke();
    }

    /// <summary>
    /// Grenzen der Eingabe. Sie gelten auch für gespeicherte Werte: ein von Hand
    /// veränderter localStorage-Eintrag darf die Auswertung nicht unbrauchbar machen.
    /// </summary>
    private AppSettings Sanitize(AppSettings settings) => settings with
    {
        ScannerThreshold = Math.Clamp(settings.ScannerThreshold, 1, 1_000),
        MonitoringWindowSeconds = Math.Clamp(settings.MonitoringWindowSeconds, 1, 60),
        MonitoringMinOccurrences = Math.Clamp(settings.MonitoringMinOccurrences, 1, 100),
        ScanBurstRequests = Math.Clamp(settings.ScanBurstRequests, 1, 1_000_000),
        ScanBurstWindowMinutes = Math.Clamp(settings.ScanBurstWindowMinutes, 1, 1_440),
        MaxFileSizeMegabytes = Math.Clamp(settings.MaxFileSizeMegabytes, 1, 2_048),
        MonitoringNetworks = Clean(settings.MonitoringNetworks),
        OwnDomains = Clean(settings.OwnDomains),
    };

    private static IReadOnlyList<string> Clean(IReadOnlyList<string>? values) =>
    [
        .. (values ?? [])
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    private static AppSettings Read(
        ParserOptions parser,
        ClassificationOptions classification,
        AggregationOptions aggregation,
        FindingOptions findings,
        ViewPreferences preferences) => new()
        {
            ScannerThreshold = classification.ScannerThreshold,
            MonitoringWindowSeconds = (int)classification.MonitoringWindow.TotalSeconds,
            MonitoringMinOccurrences = classification.MonitoringMinOccurrences,
            ScanBurstRequests = findings.ScanBurstRequests,
            ScanBurstWindowMinutes = (int)findings.ScanBurstWindow.TotalMinutes,
            MonitoringNetworks = [.. classification.MonitoringNetworks],
            OwnDomains = [.. aggregation.OwnDomains],
            MaxFileSizeMegabytes = (int)(parser.MaxFileSizeBytes / BytesPerMegabyte),
            ShowFullVisitorIps = preferences.ShowFullVisitorIps,
            ExcludeMonitoring = preferences.ExcludeMonitoring,
        };
}
