using System.Globalization;
using ApexCharts;
using LogLens.Core;
using LogLens.Core.Export;
using LogLens.Core.Models;
using LogLens.Web.Charts;
using LogLens.Web.Services;
using LogLens.Web.Shared;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Pages;

/// <summary>
/// Angriffe (SPEC 8.4): Kategorien, Scanner-Tabelle und Export der Scanner-IPs als
/// nginx-deny-Liste, Klartext und CSV. Die Dateien entstehen im Browser.
/// </summary>
public partial class Attacks : ComponentBase, IDisposable
{
    private const string FilePrefix = "loglens-scanner";
    private const int CategoryRowHeight = 36;
    private const int CategoryChartPadding = 60;
    private const int CategoryLabelWidth = 260;

    private static readonly TrafficClassStyle Attack = TrafficClassStyles.For(TrafficClass.Attack);

    private static readonly HourlyChartColor AttackColor = new(Attack.LightColor, Attack.DarkColor);

    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private ClassificationOptions ClassificationOptions { get; set; } = null!;

    [Inject] private FileDownloadService Downloads { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    [CascadingParameter(Name = "IsDarkMode")] private bool IsDarkMode { get; set; }

    private ApexChartOptions<CategoryCount> CategoryOptions { get; set; } = new();

    private int ScannerThreshold => ClassificationOptions.ScannerThreshold;

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override void OnParametersSet()
    {
        CategoryOptions = new ApexChartOptions<CategoryCount>
        {
            Chart = new Chart { Toolbar = new Toolbar { Show = false }, Background = "transparent" },
            Theme = new Theme { Mode = IsDarkMode ? Mode.Dark : Mode.Light },
            Colors = [Attack.ColorFor(IsDarkMode)],
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { Horizontal = true, BorderRadius = 4, BarHeight = "70%" },
            },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Grid = new Grid { BorderColor = IsDarkMode ? "#38383550" : "#0b0b0b18" },
            Xaxis = ChartAxes.CountAxis(State.Result?.Attacks.Categories.FirstOrDefault()?.Requests ?? 0),
            Yaxis = ChartAxes.CategoryLabels(CategoryLabelWidth),
        };
    }

    private static string Summary(AnalysisResult result)
    {
        var attacks = result.Attacks;
        var requests = attacks.Requests == 1 ? "1 Anfrage war ein Angriff" : $"{Number(attacks.Requests)} Anfragen waren Angriffe";
        var ips = attacks.DistinctIps == 1 ? "1 IP" : $"{Number(attacks.DistinctIps)} IPs";
        var scanners = result.Scanners.Count switch
        {
            0 => "Keine davon ist ein Scanner.",
            1 => $"1 Scanner-IP steht für {Number(attacks.ScannerRequests)} davon.",
            _ => $"{Number(result.Scanners.Count)} Scanner-IPs stehen für {Number(attacks.ScannerRequests)} davon.",
        };

        return $"{requests}, von {ips}. {scanners}";
    }

    private static string CategoryChartHeight(AnalysisResult result) =>
        $"{(result.Attacks.Categories.Count * CategoryRowHeight) + CategoryChartPadding}px";

    private static string RawDataLink(string ip) => $"/raw?ip={Uri.EscapeDataString(ip)}";

    private static string Time(DateTimeOffset value) =>
        value.UtcDateTime.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.CurrentCulture);

    /// <summary>Burst-Dauer kompakt: „3 s", „4 min 10 s", „2 h 5 min", „3 d 4 h".</summary>
    public static string Duration(TimeSpan span) => span switch
    {
        { TotalMinutes: < 1 } => $"{(int)span.TotalSeconds} s",
        { TotalHours: < 1 } => $"{span.Minutes} min {span.Seconds} s",
        { TotalDays: < 1 } => $"{span.Hours} h {span.Minutes} min",
        _ => $"{(int)span.TotalDays} d {span.Hours} h",
    };

    private Task DownloadNginxAsync() =>
        DownloadAsync($"{FilePrefix}-deny.conf", ScannerExporter.ToNginxDenyList, "text/plain");

    private Task DownloadPlainTextAsync() =>
        DownloadAsync($"{FilePrefix}.txt", ScannerExporter.ToPlainText, "text/plain");

    private Task DownloadCsvAsync() =>
        DownloadAsync($"{FilePrefix}.csv", r => ScannerExporter.ToCsv(r.Scanners), "text/csv");

    private async Task DownloadAsync(string fileName, Func<AnalysisResult, string> export, string contentType)
    {
        if (State.Result is not { } result)
        {
            return;
        }

        await Downloads.DownloadTextAsync(fileName, export(result), $"{contentType};charset=utf-8");
    }

    private async Task CopyNginxAsync()
    {
        if (State.Result is not { } result)
        {
            return;
        }

        var copied = await Downloads.CopyTextAsync(ScannerExporter.ToNginxDenyList(result));

        if (copied)
        {
            Snackbar.Add("deny-Liste in die Zwischenablage kopiert.", Severity.Success);
        }
        else
        {
            Snackbar.Add("Der Browser hat den Zugriff auf die Zwischenablage verweigert. Bitte die Datei herunterladen.",
                Severity.Warning);
        }
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => State.Changed -= OnStateChanged;
}
