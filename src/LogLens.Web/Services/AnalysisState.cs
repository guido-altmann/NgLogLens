using LogLens.Core.Aggregation;
using LogLens.Core.Models;

namespace LogLens.Web.Services;

/// <summary>
/// Hält das aktuelle Auswertungsergebnis für alle Ansichten – ausschließlich im
/// Arbeitsspeicher. Rohdaten werden nicht in localStorage oder IndexedDB abgelegt
/// (CLAUDE.md, Datenschutz); beim Neuladen der Seite ist die Auswertung weg.
///
/// Zusätzlich verwaltet die Klasse den globalen Zeitraumfilter (SPEC 8): die
/// Auswertung der ganzen Datei bleibt erhalten, die Ansichten lesen den Ausschnitt.
/// </summary>
public sealed class AnalysisState(AnalysisRangeFilter rangeFilter)
{
    /// <summary>Die Auswertung der ganzen Datei, unabhängig vom Zeitraumfilter.</summary>
    public AnalysisResult? Full { get; private set; }

    /// <summary>Was die Ansichten zeigen: die Auswertung im gewählten Zeitraum.</summary>
    public AnalysisResult? Result { get; private set; }

    public DayRange? Range { get; private set; }

    public bool HasResult => Result is not null;

    /// <summary>True, sobald der Zeitraumfilter die Auswertung tatsächlich einschränkt.</summary>
    public bool IsRestricted => Result?.Range is not null;

    /// <summary>Wird ausgelöst, wenn eine Auswertung ankommt, sich ändert oder verworfen wird.</summary>
    public event Action? Changed;

    /// <param name="keepRange">
    /// Bei einer neuen Datei wird der Zeitraum zurückgesetzt; nach einer Neuberechnung
    /// mit geänderten Einstellungen (SPEC 8.9) bleibt die Auswahl stehen.
    /// </param>
    public void Set(AnalysisResult result, bool keepRange = false)
    {
        ArgumentNullException.ThrowIfNull(result);

        Full = result;
        if (!keepRange)
        {
            Range = null;
        }

        Apply();
    }

    public void SetRange(DayRange? range)
    {
        Range = range is null || range.IsOpen ? null : range;
        Apply();
    }

    public void ClearRange() => SetRange(null);

    public void Clear()
    {
        if (Full is null)
        {
            return;
        }

        Full = null;
        Result = null;
        Range = null;
        Changed?.Invoke();
    }

    private void Apply()
    {
        Result = Full is null ? null : rangeFilter.Apply(Full, Range);
        Changed?.Invoke();
    }
}
