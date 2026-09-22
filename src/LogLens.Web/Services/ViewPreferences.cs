namespace LogLens.Web.Services;

/// <summary>
/// Ansichtsschalter, die über mehrere Seiten gelten. Nur im Arbeitsspeicher;
/// die Einstellungsseite (M6) übernimmt das Speichern in localStorage.
/// </summary>
public sealed class ViewPreferences
{
    private bool _excludeMonitoring;
    private bool _showFullVisitorIps;

    /// <summary>Schalter „Monitoring aus Seitenaufrufen herausrechnen" (SPEC 5.8).</summary>
    public bool ExcludeMonitoring
    {
        get => _excludeMonitoring;
        set => Set(ref _excludeMonitoring, value);
    }

    /// <summary>
    /// Besucher-IPs ungekürzt zeigen. Standard ist die Maskierung auf /24 bzw. /48
    /// (CLAUDE.md, Datenschutz); Scanner-IPs stehen immer vollständig da.
    /// </summary>
    public bool ShowFullVisitorIps
    {
        get => _showFullVisitorIps;
        set => Set(ref _showFullVisitorIps, value);
    }

    public event Action? Changed;

    private void Set(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }
}
