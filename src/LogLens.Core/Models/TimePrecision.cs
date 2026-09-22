namespace LogLens.Core.Models;

/// <summary>
/// Wie genau der Zeitstempel einer Zeile bekannt ist. Gekürzte Zeilen (SPEC 2.3)
/// enthalten oft nur Datum oder Datum und Stunde; <see cref="Second"/> ist der Normalfall.
/// Auswertungen je Stunde dürfen nur Einträge ab <see cref="Hour"/> verwenden.
/// </summary>
public enum TimePrecision
{
    Second,
    Minute,
    Hour,
    Day,
}
