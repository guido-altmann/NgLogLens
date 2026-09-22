using LogLens.Core.Models;

namespace LogLens.Web.Services;

/// <summary>
/// Hält das aktuelle Auswertungsergebnis für alle Ansichten – ausschließlich im
/// Arbeitsspeicher. Rohdaten werden nicht in localStorage oder IndexedDB abgelegt
/// (CLAUDE.md, Datenschutz); beim Neuladen der Seite ist die Auswertung weg.
/// </summary>
public sealed class AnalysisState
{
    public AnalysisResult? Result { get; private set; }

    public bool HasResult => Result is not null;

    /// <summary>Wird ausgelöst, wenn eine Auswertung ankommt oder verworfen wird.</summary>
    public event Action? Changed;

    public void Set(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        Result = result;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Result is null)
        {
            return;
        }

        Result = null;
        Changed?.Invoke();
    }
}
