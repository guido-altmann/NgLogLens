using System.Globalization;
using ApexCharts;
using LogLens.Core;
using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Web.Charts;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Pages;

/// <summary>
/// KI-Agenten (SPEC 8.5): je Agent ein Balken Verifiziert/Unverifiziert/Getarnt und
/// die Seiten, die echte Agenten abgerufen haben.
/// </summary>
public partial class AiAgents : ComponentBase, IDisposable
{
    private const int AgentRowHeight = 40;
    private const int ChartPadding = 70;

    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private AiIpRangeSet IpRanges { get; set; } = null!;

    [CascadingParameter(Name = "IsDarkMode")] private bool IsDarkMode { get; set; }

    private ApexChartOptions<AiAgentStatistics> VerdictOptions { get; set; } = new();

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    protected override void OnParametersSet()
    {
        VerdictOptions = new ApexChartOptions<AiAgentStatistics>
        {
            Chart = new Chart { Stacked = true, Toolbar = new Toolbar { Show = false }, Background = "transparent" },
            Theme = new Theme { Mode = IsDarkMode ? Mode.Dark : Mode.Light },
            Colors = [.. AiVerdictStyles.All.Select(s => s.ColorFor(IsDarkMode))],
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar { Horizontal = true, BorderRadius = 4, BarHeight = "70%" },
            },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Grid = new Grid { BorderColor = IsDarkMode ? "#38383550" : "#0b0b0b18" },
            Xaxis = ChartAxes.CountAxis(State.Result?.AiAgents.FirstOrDefault()?.Requests ?? 0),

            // 2-px-Lücke in Hintergrundfarbe zwischen den Segmenten (Farbsehschwäche).
            Stroke = new Stroke { Width = 2, Colors = ["transparent"] },
        };
    }

    private string IpRangeNote =>
        $"Geprüft gegen die mitgelieferten IP-Bereiche, Stand {IpRanges.Updated}. "
        + "Anbieter ohne veröffentlichte IP-Liste lassen sich im Browser nicht prüfen; ihre Agenten bleiben unverifiziert.";

    private static string Summary(AnalysisResult result)
    {
        var requests = result.AiAgents.Sum(a => a.Requests);
        var verified = VerdictTotal(result, AiVerdict.Verified);
        var spoofed = VerdictTotal(result, AiVerdict.Spoofed);
        var agents = result.AiAgents.Count == 1 ? "1 KI-Agent" : $"{Number(result.AiAgents.Count)} KI-Agenten";
        var spoofedText = spoofed switch
        {
            0 => "keine war getarnt",
            1 => "1 war ein Angriff mit falscher Kennung",
            _ => $"{Number(spoofed)} waren Angriffe mit falscher Kennung",
        };

        return $"{Number(requests)} Anfragen von {agents}: {Number(verified)} verifiziert, {spoofedText}.";
    }

    private static int VerdictTotal(AnalysisResult result, AiVerdict verdict) =>
        result.AiAgents.Sum(a => a.Of(verdict));

    private static AiAgentStatistics? FirstVerified(AnalysisResult result) =>
        result.AiAgents.FirstOrDefault(a => a.Verified > 0);

    private static string PanelTitle(AiAgentStatistics agent) =>
        $"{agent.Name} – {Number(agent.Verified)} verifizierte Abrufe";

    private static string ChartHeight(AnalysisResult result) =>
        $"{(result.AiAgents.Count * AgentRowHeight) + ChartPadding}px";

    private static string Decode(string path) => UrlText.Decode(path);

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => State.Changed -= OnStateChanged;
}
