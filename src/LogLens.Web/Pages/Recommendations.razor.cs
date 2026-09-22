using System.Globalization;
using LogLens.Core.Models;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Pages;

/// <summary>
/// Empfehlungen (SPEC 8.7): die Findings aus SPEC 7, nach Priorität sortiert, jeweils
/// mit Begründung und Konfigurations-Schnipsel zum Kopieren. Die Regeln selbst stehen
/// im Kern; diese Seite stellt nur dar.
/// </summary>
public partial class Recommendations : ComponentBase, IDisposable
{
    [Inject] private AnalysisState State { get; set; } = null!;

    [Inject] private FileDownloadService Downloads { get; set; } = null!;

    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    private static string PriorityLabel(FindingPriority priority) => priority switch
    {
        FindingPriority.High => "Hoch",
        FindingPriority.Medium => "Mittel",
        _ => "Niedrig",
    };

    private static Color PriorityColor(FindingPriority priority) => priority switch
    {
        FindingPriority.High => Color.Error,
        FindingPriority.Medium => Color.Warning,
        _ => Color.Default,
    };

    private static string Summary(AnalysisResult result)
    {
        var findings = result.Findings;
        var head = findings.Count == 1 ? "Eine Empfehlung" : $"{Number(findings.Count)} Empfehlungen";

        var byPriority = Enum.GetValues<FindingPriority>()
            .Select(p => (Priority: p, Count: findings.Count(f => f.Priority == p)))
            .Where(p => p.Count > 0)
            .Select(p => $"{Number(p.Count)} × {PriorityLabel(p.Priority).ToLowerInvariant()}");

        return $"{head}: {string.Join(", ", byPriority)}.";
    }

    private async Task CopyAsync(Finding finding)
    {
        if (finding.Snippet is not { } snippet)
        {
            return;
        }

        if (await Downloads.CopyTextAsync(snippet.Text))
        {
            Snackbar.Add($"„{finding.Title}“ in die Zwischenablage kopiert.", Severity.Success);
        }
        else
        {
            Snackbar.Add(
                "Der Browser hat den Zugriff auf die Zwischenablage verweigert. Bitte den Text von Hand markieren.",
                Severity.Warning);
        }
    }

    private static string Number(int value) => value.ToString("N0", CultureInfo.CurrentCulture);

    private void OnStateChanged() => _ = InvokeAsync(StateHasChanged);

    public void Dispose() => State.Changed -= OnStateChanged;
}
