using ApexCharts;
using LogLens.Core;
using LogLens.Core.Classification;
using LogLens.Core.Models;
using LogLens.Core.Parsing;
using LogLens.Web.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Web.Tests;

/// <summary>
/// Basis für die Detailseiten (SPEC 8.3–8.8). Ausgewertet wird
/// <c>tests/fixtures/sample-nginx.log</c>; die Sollwerte stehen in
/// <c>sample-expected.md</c> und werden hier nicht angepasst.
/// </summary>
public abstract class DetailPageTestContext : MudBlazorTestContext
{
    protected const string FixtureName = "sample-nginx.log";

    protected DetailPageTestContext()
    {
        // Vor AddLogLensCore: TryAdd lässt diese Registrierung dann stehen.
        Services.AddSingleton(TestIpRanges);
        Services.AddLogLensCore();
        Services.AddApexCharts();
        Services.AddSingleton<AnalysisState>();
        Services.AddSingleton<ViewPreferences>();
        Services.AddSingleton<IpDisplay>();
        Services.AddSingleton<SettingsService>();
        Services.AddScoped<FileDownloadService>();
    }

    /// <summary>
    /// Erst beim Zugriff aufgelöst: der Dienstanbieter von bUnit wird beim ersten
    /// Abruf gebaut, danach nimmt er keine Registrierung mehr an.
    /// </summary>
    protected AnalysisState State => Services.GetRequiredService<AnalysisState>();

    protected ViewPreferences Preferences => Services.GetRequiredService<ViewPreferences>();

    /// <summary>
    /// Wie in sample-expected.md festgelegt: <c>216.73.216.0/22</c> gehört Anthropic,
    /// <c>192.0.2.0/24</c> keinem Anbieter.
    /// </summary>
    protected static AiIpRangeSet TestIpRanges { get; } = new(
        "2026-09-22",
        [
            new AiIpRangeProvider(
                Provider: "Anthropic",
                Agents: ["ClaudeBot", "Claude-User", "Claude-SearchBot"],
                Verification: AiVerificationMethod.PublishedRanges,
                Source: "https://claude.com/crawling/bots.json",
                SourceUpdated: null,
                Retrieved: "2026-09-22",
                Note: null,
                Networks: [IpNetwork.Parse("216.73.216.0/22")]),
        ]);

    protected async Task LoadFixtureAsync()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", FixtureName);
        var content = await File.ReadAllTextAsync(path, Xunit.TestContext.Current.CancellationToken);
        State.Set(await AnalyzeAsync(content));
    }

    protected async Task LoadAsync(string content) => State.Set(await AnalyzeAsync(content));

    private Task<AnalysisResult> AnalyzeAsync(string content) =>
        Services.GetRequiredService<LogAnalysisPipeline>().AnalyzeAsync(
            LogFileSource.FromText(FixtureName, content),
            cancellationToken: Xunit.TestContext.Current.CancellationToken);

    /// <summary>Text einer Zelle ohne Mehrfach-Leerzeichen aus dem Markup.</summary>
    protected static string Compact(string text) => string.Join(' ', text.Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>Zeilen einer Tabelle als Zellentexte, ohne Kopfzeile.</summary>
    protected static string[][] Rows<T>(IRenderedComponent<T> cut, string testId)
        where T : Microsoft.AspNetCore.Components.IComponent =>
    [
        .. cut.FindAll($"[data-testid={testId}] tbody tr")
            .Select(row => row.Children.Select(cell => Compact(cell.TextContent)).ToArray()),
    ];
}
