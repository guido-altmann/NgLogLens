using System.Globalization;
using ApexCharts;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Shared;

/// <summary>
/// Eine Stundenreihe mit 24 Werten, Index = Stunde in UTC. Eine einzige Serie braucht
/// keine Legende; der Titel darüber nennt sie.
/// </summary>
public partial class HourlyChart : ComponentBase
{
    [Parameter, EditorRequired] public IReadOnlyList<int> Values { get; set; } = [];

    [Parameter, EditorRequired] public string SeriesName { get; set; } = string.Empty;

    [Parameter, EditorRequired] public HourlyChartColor Color { get; set; } = null!;

    [Parameter] public string TestId { get; set; } = "hourly";

    [CascadingParameter(Name = "IsDarkMode")] private bool IsDarkMode { get; set; }

    private IReadOnlyList<HourPoint> Points { get; set; } = [];

    private ApexChartOptions<HourPoint> Options { get; set; } = new();

    /// <summary>Kurzer Satz zur Spitzenstunde; trägt die Aussage auch ohne Diagramm.</summary>
    private string Summary
    {
        get
        {
            var peak = Points.Where(p => p.Requests > 0).MaxBy(p => p.Requests);
            return peak is null
                ? "Keine Einträge mit stundengenauer Uhrzeit."
                : $"Meiste Anfragen zwischen {peak.Range} Uhr (UTC): {peak.Requests.ToString("N0", CultureInfo.CurrentCulture)}.";
        }
    }

    protected override void OnParametersSet()
    {
        Points = [.. Values.Select((requests, hour) => new HourPoint(hour, requests))];

        Options = new ApexChartOptions<HourPoint>
        {
            Chart = new Chart { Toolbar = new Toolbar { Show = false }, Background = "transparent" },
            Theme = new Theme { Mode = IsDarkMode ? Mode.Dark : Mode.Light },
            Colors = [Color.For(IsDarkMode)],
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { BorderRadius = 4, ColumnWidth = "70%" },
            },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Grid = new Grid { BorderColor = IsDarkMode ? "#38383550" : "#0b0b0b18" },
            Xaxis = new XAxis { Title = new AxisTitle { Text = "Stunde (UTC)" } },
            Yaxis = [new YAxis { Title = new AxisTitle { Text = "Anfragen" } }],
        };
    }

    public sealed record HourPoint(int Hour, int Requests)
    {
        public string Label => Hour.ToString("00", CultureInfo.InvariantCulture);

        public string Range => $"{Hour:00}–{(Hour + 1) % 24:00}";
    }
}

/// <summary>Farbe der Säulen je Modus; kommt aus den Stilen der jeweiligen Klasse.</summary>
public sealed record HourlyChartColor(string LightColor, string DarkColor)
{
    public string For(bool isDarkMode) => isDarkMode ? DarkColor : LightColor;
}
