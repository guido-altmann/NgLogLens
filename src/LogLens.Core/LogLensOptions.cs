namespace LogLens.Core;

/// <summary>
/// Einstellungen des Parsers. Alle Grenzwerte stehen hier, nicht im Code.
/// </summary>
public sealed class ParserOptions
{
    /// <summary>Nach so vielen Zeilen wird der UI-Thread freigegeben und Fortschritt gemeldet.</summary>
    public int YieldInterval { get; set; } = 2_000;

    /// <summary>Höchstzahl gesammelter Beispiele für nicht erkannte Zeilen (SPEC 2.4).</summary>
    public int MaxUnknownSamples { get; set; } = 50;

    /// <summary>Höchstlänge eines gespeicherten Beispiels einer unbekannten Zeile.</summary>
    public int UnknownSampleLength { get; set; } = 300;

    /// <summary>Maximale Dateigröße beim Einlesen im Browser (SPEC/CLAUDE.md: Default 200 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 200L * 1024 * 1024;

    /// <summary>
    /// Dateiname der Fehlerseite. Error-Zeilen „open() …/&lt;Name&gt; failed" sind
    /// Folgefehler eines 404 und werden aussortiert (SPEC 3).
    /// </summary>
    public string NotFoundPageFileName { get; set; } = "404.html";
}
