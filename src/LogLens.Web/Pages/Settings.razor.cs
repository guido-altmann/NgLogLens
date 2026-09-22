using LogLens.Core;
using LogLens.Core.Classification;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Pages;

/// <summary>
/// Einstellungen (SPEC 8.9): Schwellwerte, IP-Maskierung, eigene Monitoring-Netze,
/// eigene Domains und die maximale Dateigröße. Gespeichert wird in localStorage –
/// nur Einstellungen, nie Loginhalte (CLAUDE.md, Datenschutz).
/// </summary>
public partial class Settings : ComponentBase, IDisposable
{
    [Inject] private SettingsService SettingsService { get; set; } = null!;

    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private LogAnalysisPipeline Pipeline { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    private AppSettings _edited = new();
    private string _monitoringNetworks = string.Empty;
    private string _ownDomains = string.Empty;
    private bool _busy;

    /// <summary>Netze, die weder IP noch CIDR sind; sie blockieren das Speichern.</summary>
    private IReadOnlyList<string> _invalidNetworks = [];

    private bool CanSave => _invalidNetworks.Count == 0 && !_busy;

    private bool HasChanges =>
        _edited != SettingsService.Current
        || !Lines(_monitoringNetworks).SequenceEqual(SettingsService.Current.MonitoringNetworks, StringComparer.Ordinal)
        || !Lines(_ownDomains).SequenceEqual(SettingsService.Current.OwnDomains, StringComparer.Ordinal);

    protected override void OnInitialized()
    {
        SettingsService.Changed += OnSettingsChanged;
        Load();
    }

    private void Load()
    {
        _edited = SettingsService.Current;
        _monitoringNetworks = string.Join(Environment.NewLine, _edited.MonitoringNetworks);
        _ownDomains = string.Join(Environment.NewLine, _edited.OwnDomains);
        _invalidNetworks = [];
    }

    private void SetNetworks(string? text)
    {
        _monitoringNetworks = text ?? string.Empty;
        _invalidNetworks = [.. Lines(_monitoringNetworks).Where(n => !IpNetwork.TryParse(n, out _))];
    }

    private void SetDomains(string? text) => _ownDomains = text ?? string.Empty;

    private async Task SaveAsync()
    {
        if (!CanSave)
        {
            return;
        }

        var previous = SettingsService.Current;
        var settings = _edited with
        {
            MonitoringNetworks = Lines(_monitoringNetworks),
            OwnDomains = Lines(_ownDomains),
        };

        _busy = true;
        try
        {
            await SettingsService.SaveAsync(settings);

            if (settings.RequiresReanalysis(previous) && State.Full is { } full)
            {
                // Die Datei selbst ist längst zu; neu gerechnet wird auf den Zeilen,
                // die noch im Arbeitsspeicher stehen (CLAUDE.md, Datenschutz).
                State.Set(await Pipeline.ReanalyzeAsync(full), keepRange: true);
                Snackbar.Add("Einstellungen gespeichert, die Auswertung wurde neu gerechnet.", Severity.Success);
            }
            else
            {
                Snackbar.Add("Einstellungen gespeichert.", Severity.Success);
            }
        }
        finally
        {
            _busy = false;
            Load();
        }
    }

    private async Task ResetAsync()
    {
        var previous = SettingsService.Current;

        _busy = true;
        try
        {
            await SettingsService.ResetAsync();

            if (SettingsService.Current.RequiresReanalysis(previous) && State.Full is { } full)
            {
                State.Set(await Pipeline.ReanalyzeAsync(full), keepRange: true);
            }

            Snackbar.Add("Einstellungen auf die Vorgaben zurückgesetzt.", Severity.Success);
        }
        finally
        {
            _busy = false;
            Load();
        }
    }

    /// <summary>Eine Eingabe je Zeile; Leerzeilen und Wiederholungen fallen weg.</summary>
    private static IReadOnlyList<string> Lines(string? text) =>
    [
        .. (text ?? string.Empty)
            .Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase),
    ];

    private void OnSettingsChanged() => _ = InvokeAsync(() =>
    {
        Load();
        StateHasChanged();
    });

    public void Dispose() => SettingsService.Changed -= OnSettingsChanged;
}
