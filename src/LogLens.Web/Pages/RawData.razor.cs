using System.Globalization;
using LogLens.Core;
using LogLens.Core.Aggregation;
using LogLens.Core.Models;
using LogLens.Web.Charts;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Pages;

/// <summary>
/// Rohdaten (SPEC 8.8): alle Anfragen mit ihrem Urteil in einem virtualisierten Grid,
/// Filter nach Klasse, Status, IP, Pfad und Zeitraum. Die Filterlogik steckt in
/// <see cref="EntryFilter"/>; die Seite übersetzt nur Eingaben.
/// </summary>
public partial class RawData : ComponentBase, IDisposable
{
    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private IpDisplay Ips { get; set; } = null!;

    [Inject] private ViewPreferences Preferences { get; set; } = null!;

    [CascadingParameter(Name = "IsDarkMode")] private bool IsDarkMode { get; set; }

    /// <summary>Vorbelegung per Link, z. B. aus der Scanner-Tabelle: <c>/raw?ip=203.0.113.10</c>.</summary>
    [SupplyParameterFromQuery(Name = "ip")] public string? IpQuery { get; set; }

    [SupplyParameterFromQuery(Name = "path")] public string? PathQuery { get; set; }

    [SupplyParameterFromQuery(Name = "status")] public string? StatusQuery { get; set; }

    [SupplyParameterFromQuery(Name = "class")] public string? ClassQuery { get; set; }

    private IReadOnlyCollection<TrafficClass> _classes = [];
    private string? _statusText;
    private bool _statusValid = true;
    private StatusFilter? _status;
    private string? _ip;
    private string? _path;
    private DateTime? _from;
    private DateTime? _to;

    private (string? Ip, string? Path, string? Status, string? Class)? _appliedQuery;
    private int _resetCount;
    private EntryFilter _filter = EntryFilter.None;
    private IReadOnlyList<ClassifiedEntry> _rows = [];

    protected override void OnInitialized()
    {
        State.Changed += OnStateChanged;
        Preferences.Changed += OnPreferencesChanged;
    }

    protected override void OnParametersSet()
    {
        // Auch ein Wechsel von Hell/Dunkel setzt Parameter; die Filter des Nutzers
        // bleiben dann stehen. Nur eine neue Query überschreibt sie.
        var query = (IpQuery, PathQuery, StatusQuery, ClassQuery);
        if (_appliedQuery == query)
        {
            return;
        }

        _appliedQuery = query;
        _ip = IpQuery;
        _path = PathQuery;
        _statusText = StatusQuery;
        _statusValid = StatusFilter.TryParse(_statusText, out _status);
        _classes = Enum.TryParse<TrafficClass>(ClassQuery, ignoreCase: true, out var trafficClass)
            && Enum.IsDefined(trafficClass)
                ? [trafficClass]
                : [];

        ApplyFilter();
    }

    private Task SetClassesAsync(IReadOnlyCollection<TrafficClass>? classes)
    {
        _classes = classes ?? [];
        return RefreshAsync();
    }

    private Task SetStatusAsync(string? text)
    {
        _statusText = text;
        _statusValid = StatusFilter.TryParse(text, out _status);
        return RefreshAsync();
    }

    private Task SetIpAsync(string? text)
    {
        _ip = text;
        return RefreshAsync();
    }

    private Task SetPathAsync(string? text)
    {
        _path = text;
        return RefreshAsync();
    }

    private Task SetFromAsync(DateTime? date)
    {
        _from = date;
        return RefreshAsync();
    }

    private Task SetToAsync(DateTime? date)
    {
        _to = date;
        return RefreshAsync();
    }

    private Task ResetAsync()
    {
        _classes = [];
        _statusText = null;
        _status = null;
        _statusValid = true;
        _ip = null;
        _path = null;
        _from = null;
        _to = null;
        _resetCount++;
        return RefreshAsync();
    }

    private Task RefreshAsync()
    {
        ApplyFilter();
        return Task.CompletedTask;
    }

    private void ApplyFilter()
    {
        _filter = new EntryFilter
        {
            Classes = _classes.Count == 0 ? null : _classes.ToHashSet(),

            // Ein ungültiger Status filtert nicht; das Feld zeigt stattdessen den Fehler.
            Status = _statusValid ? _status : null,
            Ip = _ip,
            Path = _path,
            From = _from is { } from ? DateOnly.FromDateTime(from) : null,
            To = _to is { } to ? DateOnly.FromDateTime(to) : null,
        };

        _rows = State.Result is { } result ? _filter.Apply(result.Entries) : [];
    }

    private string RowCount(AnalysisResult result) =>
        _filter.IsEmpty
            ? $"{Number(result.Entries.Count)} Anfragen"
            : $"{Number(_rows.Count)} von {Number(result.Entries.Count)} Anfragen";

    private static DateTime? MinDate(AnalysisResult result) =>
        result.Period?.FirstDay.ToDateTime(TimeOnly.MinValue);

    private static DateTime? MaxDate(AnalysisResult result) =>
        result.Period?.LastDay.ToDateTime(TimeOnly.MinValue);

    private string ClassColor(TrafficClass trafficClass) =>
        TrafficClassStyles.For(trafficClass).ColorFor(IsDarkMode);

    /// <summary>
    /// Zeit so genau, wie sie im Log stand: gekürzte Zeilen kennen oft nur den Tag
    /// oder die Stunde (SPEC 2.3).
    /// </summary>
    private static string Time(AccessEntry entry)
    {
        var utc = entry.Timestamp.UtcDateTime;
        var format = entry.TimePrecision switch
        {
            TimePrecision.Day => "dd.MM.yyyy",
            TimePrecision.Hour => "dd.MM.yyyy HH 'Uhr'",
            TimePrecision.Minute => "dd.MM.yyyy HH:mm",
            _ => "dd.MM.yyyy HH:mm:ss",
        };

        return utc.ToString(format, CultureInfo.CurrentCulture);
    }

    /// <summary>Pfad und Query URL-dekodiert für die Anzeige (SPEC 2.1); gekürzte Zeilen haben keinen.</summary>
    private static string Target(AccessEntry entry)
    {
        if (entry.Path is null)
        {
            return entry.IsTruncated ? "(gekürzte Zeile)" : "–";
        }

        var target = entry.Query is null ? entry.Path : $"{entry.Path}?{entry.Query}";
        return UrlText.Decode(target);
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnStateChanged()
    {
        ApplyFilter();
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnPreferencesChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        Preferences.Changed -= OnPreferencesChanged;
    }
}
