using System.Globalization;
using LogLens.Core;
using LogLens.Core.Models;
using LogLens.Web.Charts;
using LogLens.Web.Services;
using LogLens.Web.Shared;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Pages;

/// <summary>
/// Besucher (SPEC 8.3): Top-Seiten, Referrer, Tageszeit und aktivste Netze, mit dem
/// Schalter „Monitoring herausrechnen". Rechnet nichts selbst.
/// </summary>
public partial class Visitors : ComponentBase, IDisposable
{
    private static readonly TrafficClassStyle Human = TrafficClassStyles.For(TrafficClass.Human);

    private static readonly HourlyChartColor HumanColor = new(Human.LightColor, Human.DarkColor);

    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private ViewPreferences Preferences { get; set; } = null!;

    protected override void OnInitialized()
    {
        State.Changed += OnChanged;
        Preferences.Changed += OnChanged;
    }

    private void SetExcludeMonitoring(bool value) => Preferences.ExcludeMonitoring = value;

    private static string Summary(VisitorStatistics visitors)
    {
        var pageViews = visitors.PageViews == 1 ? "1 Seitenaufruf" : $"{Number(visitors.PageViews)} Seitenaufrufe";
        var networks = visitors.Networks == 1 ? "1 Netz" : $"{Number(visitors.Networks)} Netzen";
        var requests = visitors.Requests == 1 ? "1 Anfrage" : $"{Number(visitors.Requests)} Anfragen";

        return $"{pageViews} von Menschen aus {networks}, insgesamt {requests} einschließlich Bildern, CSS und Skripten.";
    }

    private static int MonitoringPageViews(AnalysisResult result) =>
        result.PageViews - result.PageViewsWithoutMonitoring;

    private string MonitoringHint(AnalysisResult result)
    {
        var count = MonitoringPageViews(result);
        var views = count == 1 ? "1 Seitenaufruf stammt" : $"{Number(count)} Seitenaufrufe stammen";

        return Preferences.ExcludeMonitoring
            ? $"{views} vermutlich von einem Monitoring-Dienst und sind hier herausgerechnet."
            : $"{views} vermutlich von einem Monitoring-Dienst: zwei Adressen im selben Netz rufen dieselbe Seite fast gleichzeitig ab.";
    }

    private static string OwnDomainsHint(AnalysisResult result) =>
        result.OwnDomains.Count == 0
            ? "Eigene Domain nicht erkannt; Links innerhalb der Seite können hier mitgezählt sein."
            : $"Ohne Links von der eigenen Domain ({string.Join(", ", result.OwnDomains)}).";

    private static string Share(int count, int total) =>
        total == 0 ? "–" : ((double)count / total).ToString("P0", CultureInfo.CurrentCulture);

    private static string Decode(string path) => UrlText.Decode(path);

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose()
    {
        State.Changed -= OnChanged;
        Preferences.Changed -= OnChanged;
    }
}
