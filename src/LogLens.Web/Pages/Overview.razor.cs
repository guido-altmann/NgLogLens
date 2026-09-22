using System.Globalization;
using ApexCharts;
using LogLens.Core.Models;
using LogLens.Web.Charts;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Pages;

/// <summary>
/// Übersicht (SPEC 8.2): Kernaussage, Anteile der vier Klassen und Tagesverlauf.
/// Rechnet nichts selbst – alle Zahlen kommen aus der Aggregation in Core.
/// </summary>
public partial class Overview : ComponentBase, IDisposable
{
    [Inject] private AnalysisState State { get; set; } = null!;

    /// <summary>Kommt aus dem MainLayout; die Diagramme brauchen eigene Farben je Modus.</summary>
    [CascadingParameter(Name = "IsDarkMode")] private bool IsDarkMode { get; set; }

    private AnalysisResult? Result => State.Result;

    private DailyView _dailyView = DailyView.All;
    private ApexChartOptions<SharePoint> _shareOptions = new();
    private ApexChartOptions<DailyTraffic> _dailyOptions = new();
    private IReadOnlyList<ShareSeries> _shareSeries = [];
    private bool _isDarkModeOfOptions;

    /// <summary>Umschalter über dem Tagesverlauf (SPEC 8.2).</summary>
    public enum DailyView
    {
        All,
        WithoutAttacks,
        HumansOnly,
    }

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override void OnParametersSet()
    {
        if (_shareSeries.Count == 0 || _isDarkModeOfOptions != IsDarkMode)
        {
            BuildChartData();
        }
    }

    private IReadOnlyList<TrafficClass> VisibleClasses => _dailyView switch
    {
        DailyView.HumansOnly => [TrafficClass.Human],
        DailyView.WithoutAttacks => [TrafficClass.Human, TrafficClass.AiAgent, TrafficClass.Bot],
        _ => [TrafficClass.Human, TrafficClass.AiAgent, TrafficClass.Bot, TrafficClass.Attack],
    };

    private int VisibleTotal(DailyTraffic day) => _dailyView switch
    {
        DailyView.HumansOnly => day.Human,
        DailyView.WithoutAttacks => day.WithoutAttacks,
        _ => day.Total,
    };

    /// <summary>Kernaussage als Satz (SPEC 8.2).</summary>
    private string Headline => Result is null ? string.Empty : SummaryText.Headline(Result);

    private string Subline
    {
        get
        {
            if (Result?.Period is not { } period)
            {
                return string.Empty;
            }

            var days = period.Days == 1 ? "1 Tag" : $"{Number(period.Days)} Tage";

            return $"{period.FirstDay:dd.MM.yyyy} bis {period.LastDay:dd.MM.yyyy} ({days}). "
                + (SummaryText.Attacks(Result) ?? "Keine Anfrage war ein Angriff.");
        }
    }

    private string FileSummary =>
        Result is null || Result.FileNames.Count == 0
            ? "dieser Datei"
            : string.Join(", ", Result.FileNames);

    private IReadOnlyList<Tile> Tiles
    {
        get
        {
            if (Result is null)
            {
                return [];
            }

            var monitoring = Result.PageViews - Result.PageViewsWithoutMonitoring;

            return
            [
                new Tile("Anfragen", Number(Result.Requests),
                    $"aus {Number(Result.Diagnostics.TotalLines)} Zeilen"),
                new Tile("Seitenaufrufe", Number(Result.PageViews),
                    monitoring > 0
                        ? $"davon {Number(monitoring)} mit Monitoring-Verdacht"
                        : "ohne Bilder, CSS und Skripte"),
                new Tile("Netze mit Besuchern", Number(Result.VisitorNetworks),
                    "unterschiedliche /24- bzw. /48-Netze"),
                new Tile("Scanner-IPs", Number(Result.Scanners.Count),
                    $"{Number(Result.Count(TrafficClass.Attack))} Anfragen als Angriff gewertet"),
            ];
        }
    }

    private void SetDailyView(DailyView view)
    {
        _dailyView = view;

        // Die Farben des Tagesverlaufs folgen den sichtbaren Klassen, nicht ihrer
        // Position: „Nur Menschen" bleibt blau und wird nicht umgefärbt.
        BuildChartData();
    }

    private void OnStateChanged()
    {
        BuildChartData();
        _ = InvokeAsync(StateHasChanged);
    }

    private void BuildChartData()
    {
        _isDarkModeOfOptions = IsDarkMode;
        _shareSeries =
        [
            .. TrafficClassStyles.All.Select(style => new ShareSeries(
                style, [new SharePoint(Result?.Count(style.Class) ?? 0)])),
        ];

        var colors = TrafficClassStyles.All.Select(s => s.ColorFor(IsDarkMode)).ToList();

        // Ein 100-%-Balken: eine Kategorie, vier gestapelte Serien.
        _shareOptions = new ApexChartOptions<SharePoint>
        {
            Chart = new Chart
            {
                Stacked = true,
                StackType = StackType.Percent100,
                Toolbar = new Toolbar { Show = false },
                Background = "transparent",
            },
            Theme = new Theme { Mode = IsDarkMode ? Mode.Dark : Mode.Light },
            Colors = colors,
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { Horizontal = true, BorderRadius = 4 },
            },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Xaxis = new XAxis { Labels = new XAxisLabels { Show = false } },
            Yaxis = [new YAxis { Show = false }],
            Grid = new Grid { Show = false },
            Stroke = new Stroke { Width = 2, Colors = ["transparent"] },
        };

        _dailyOptions = new ApexChartOptions<DailyTraffic>
        {
            Chart = new Chart
            {
                Stacked = true,
                Toolbar = new Toolbar { Show = false },
                Background = "transparent",
            },
            Theme = new Theme { Mode = IsDarkMode ? Mode.Dark : Mode.Light },
            Colors = ColorsFor(VisibleClasses),
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { BorderRadius = 4, ColumnWidth = "70%" },
            },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Position = LegendPosition.Bottom, HorizontalAlign = Align.Left },
            Grid = new Grid { BorderColor = IsDarkMode ? "#38383550" : "#0b0b0b18" },
            Stroke = new Stroke { Width = 2, Colors = ["transparent"] },
            Yaxis = [new YAxis { Title = new AxisTitle { Text = "Anfragen" } }],
        };
    }

    private List<string> ColorsFor(IReadOnlyList<TrafficClass> classes) =>
        [.. classes.Select(c => TrafficClassStyles.For(c).ColorFor(IsDarkMode))];

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    public void Dispose() => State.Changed -= OnStateChanged;

    private sealed record Tile(string Label, string Value, string Hint);

    /// <summary>Ein Punkt des Anteilsbalkens. Alle vier Serien liegen auf derselben Kategorie.</summary>
    public sealed record SharePoint(int Requests);

    /// <summary>Eine Serie des Anteilsbalkens: genau ein Wert, damit alle vier stapeln.</summary>
    private sealed record ShareSeries(TrafficClassStyle Style, IEnumerable<SharePoint> Items);
}
