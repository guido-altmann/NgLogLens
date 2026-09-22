using System.Diagnostics.CodeAnalysis;

namespace LogLens.Core;

/// <summary>Aufbereitung von Rohwerten aus dem Log.</summary>
public static class UrlText
{
    /// <summary>
    /// Prozentkodierung auflösen. Gespeichert wird immer der Rohwert; dekodiert wird
    /// für Anzeige, Filter und Erkennung (SPEC 2.1). Ungültige Sequenzen bleiben
    /// stehen, statt eine einzelne Zeile scheitern zu lassen.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf('%') < 0)
        {
            return value;
        }

        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }
}
