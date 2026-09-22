using System.Globalization;
using ApexCharts;
using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Pages;

/// <summary>
/// Server-Zustand (SPEC 8.6): Statuscodes, 5xx, übrige Error-Zeilen, Proxy-Instanzen
/// als Zeitleiste und die Parser-Diagnose.
/// </summary>
public partial class ServerHealth : ComponentBase, IDisposable
{
    private const int ProxyRowHeight = 44;
    private const int ProxyChartPadding = 80;

    /// <summary>Neutrales Blau-Grau: die Zeitleiste zeigt Zustand, keine Verkehrsklasse.</summary>
    private const string ProxyLightColor = "#5b6b8c";
    private const string ProxyDarkColor = "#8d9bbd";

    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private ParserOptions ParserOptions { get; set; } = null!;

    [CascadingParameter(Name = "IsDarkMode")] private bool IsDarkMode { get; set; }

    private ApexChartOptions<ProxyInstance> ProxyOptions { get; set; } = new();

    private int MaxUnknownSamples => ParserOptions.MaxUnknownSamples;

    /// <summary>
    /// Ab so vielen Error-Zeilen virtualisiert das Grid mit fester Höhe; darunter
    /// wächst es mit dem Inhalt, statt eine leere Fläche zu zeigen.
    /// </summary>
    private const int VirtualizeErrorLinesFrom = 10;

    private static bool VirtualizeErrors(AnalysisResult result) =>
        result.ErrorEntries.Count >= VirtualizeErrorLinesFrom;

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override void OnParametersSet()
    {
        ProxyOptions = new ApexChartOptions<ProxyInstance>
        {
            Chart = new Chart { Toolbar = new Toolbar { Show = false }, Background = "transparent" },
            Theme = new Theme { Mode = IsDarkMode ? Mode.Dark : Mode.Light },
            Colors = [IsDarkMode ? ProxyDarkColor : ProxyLightColor],
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { Horizontal = true, BorderRadius = 4, BarHeight = "50%" },
            },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Xaxis = new XAxis { Type = XAxisType.Datetime, Labels = new XAxisLabels { Format = "dd.MM." } },
            Grid = new Grid { BorderColor = IsDarkMode ? "#38383550" : "#0b0b0b18" },
        };
    }

    private static string Summary(AnalysisResult result)
    {
        var health = result.ServerHealth;
        var errors = health.ServerErrorRequests switch
        {
            0 => "keine Serverfehler",
            1 => "1 Serverfehler",
            _ => $"{Number(health.ServerErrorRequests)} Serverfehler",
        };
        var proxies = health.ProxyInstances.Count == 1
            ? "1 Proxy-Instanz"
            : $"{Number(health.ProxyInstances.Count)} Proxy-Instanzen";

        var errorLines = result.ErrorEntries.Count == 1
            ? "1 eigenständige Error-Zeile"
            : $"{Number(result.ErrorEntries.Count)} eigenständige Error-Zeilen";

        return $"{Number(result.Requests)} Anfragen, {errors}, {proxies}, {errorLines}.";
    }

    private static string ErrorSummary(AnalysisResult result)
    {
        var followUps = result.Diagnostics.NotFoundPageErrorLines;
        var removed = followUps == 0
            ? string.Empty
            : followUps == 1
                ? " 1 Zeile „404.html failed\" ist Folgefehler eines 404 und hier nicht aufgeführt."
                : $" {Number(followUps)} Zeilen „404.html failed\" sind Folgefehler eines 404 und hier nicht aufgeführt.";

        return result.ErrorEntries.Count switch
        {
            0 => $"Keine eigenständigen Error-Zeilen.{removed}",
            1 => $"1 Error-Zeile.{removed}",
            _ => $"{Number(result.ErrorEntries.Count)} Error-Zeilen.{removed}",
        };
    }

    private static IEnumerable<StatusClass> StatusClasses(ServerHealthStatistics health) =>
        health.StatusCodes
            .GroupBy(s => s.Status / 100)
            .OrderBy(g => g.Key)
            .Select(g => new StatusClass($"{g.Key}xx", g.Sum(s => s.Requests)));

    private static IEnumerable<DiagnosticRow> DiagnosticRows(ParseDiagnostics diagnostics) =>
    [
        new("Zeilen gesamt", diagnostics.TotalLines),
        new("Access-Zeilen, vollständig", diagnostics.AccessLines),
        new("Access-Zeilen, gekürzt", diagnostics.TruncatedAccessLines),
        new("Error-Zeilen, vollständig", diagnostics.ErrorLines),
        new("Error-Zeilen, gekürzt", diagnostics.TruncatedErrorLines),
        new("davon Folgefehler „404.html failed\"", diagnostics.NotFoundPageErrorLines),
        new("Identische Zeilen entfernt", diagnostics.IdenticalLinesRemoved),
        new("Leerzeilen", diagnostics.BlankLines),
        new("Nicht erkannte Zeilen", diagnostics.UnknownLines),
        new("Zeilen ohne X-Forwarded-For", diagnostics.LinesWithoutClientIpHeader),
    ];

    /// <summary>Zusammenhängende Tage als Bereich: „29.08.–08.09.2026, 10.09.2026".</summary>
    public static string DayRanges(IReadOnlyList<DateOnly> days)
    {
        var parts = new List<string>();
        var index = 0;

        while (index < days.Count)
        {
            var start = days[index];
            var end = start;
            while (index + 1 < days.Count && days[index + 1] == end.AddDays(1))
            {
                end = days[++index];
            }

            parts.Add(start == end
                ? start.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture)
                : $"{start.ToString("dd.MM.", CultureInfo.CurrentCulture)}–{end.ToString("dd.MM.yyyy", CultureInfo.CurrentCulture)}");
            index++;
        }

        return string.Join(", ", parts);
    }

    private static decimal DayStart(DateOnly day) =>
        new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();

    private static string ProxyChartHeight(ServerHealthStatistics health) =>
        $"{(health.ProxyInstances.Count * ProxyRowHeight) + ProxyChartPadding}px";

    private static string Share(int count, int total) =>
        total == 0 ? "–" : ((double)count / total).ToString("P1", CultureInfo.CurrentCulture);

    private static string Time(DateTimeOffset value) =>
        value.UtcDateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture);

    private static string Decode(string path) => UrlText.Decode(path);

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => State.Changed -= OnStateChanged;

    private sealed record StatusClass(string Label, int Requests);

    private sealed record DiagnosticRow(string Label, int Value);
}
