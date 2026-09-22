using LogLens.Core.Models;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Shared;

/// <summary>
/// Der globale Zeitraumfilter aus SPEC 8. Er schränkt das Ergebnis in
/// <see cref="AnalysisState"/> ein, damit alle Ansichten denselben Ausschnitt zeigen,
/// ohne dass jede Seite den Filter selbst kennen muss.
/// </summary>
public partial class PeriodFilter : ComponentBase, IDisposable
{
    [Inject] private AnalysisState State { get; set; } = null!;

    private DateRange? _range;

    protected override void OnInitialized() => State.Changed += OnStateChanged;

    private Task SetRangeAsync(DateRange? range)
    {
        _range = range;
        State.SetRange(range is null ? null : new DayRange(Day(range.Start), Day(range.End)));
        return Task.CompletedTask;
    }

    private Task ResetAsync() => SetRangeAsync(null);

    private static DateOnly? Day(DateTime? value) =>
        value is { } date ? DateOnly.FromDateTime(date) : null;

    /// <summary>
    /// Eine neue Datei setzt den Zeitraum zurück; die Auswahl im Feld muss dann mit.
    /// </summary>
    private void OnStateChanged() => _ = InvokeAsync(() =>
    {
        if (State.Range is null)
        {
            _range = null;
        }

        StateHasChanged();
    });

    public void Dispose() => State.Changed -= OnStateChanged;
}
