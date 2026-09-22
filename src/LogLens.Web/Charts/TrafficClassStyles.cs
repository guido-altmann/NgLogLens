using LogLens.Core.Models;

namespace LogLens.Web.Charts;

/// <summary>
/// Beschriftung und Farbe einer Verkehrsklasse. Beide Farbsätze sind eigens für
/// ihren Hintergrund gewählt, Dunkel ist keine Umkehrung von Hell.
/// </summary>
public sealed record TrafficClassStyle(
    TrafficClass Class, string Label, string Description, string LightColor, string DarkColor)
{
    public string ColorFor(bool isDarkMode) => isDarkMode ? DarkColor : LightColor;
}

/// <summary>
/// Die vier Klassen in fester Reihenfolge, mit fester Farbzuordnung: eine Klasse behält
/// ihre Farbe über alle Ansichten und alle Filter hinweg.
/// </summary>
/// <remarks>
/// Palette geprüft auf Unterscheidbarkeit bei Farbsehschwäche (benachbarte Paare im
/// Stapel, ΔE ≥ 8 in OKLab) und auf Kontrast gegen hellen wie dunklen Hintergrund.
/// Türkis liegt im Hellmodus unter 3:1; deshalb tragen alle Diagramme zusätzlich
/// Legende mit Zahlen und eine Tabellenansicht (SPEC 9, Textalternative).
/// </remarks>
public static class TrafficClassStyles
{
    public static IReadOnlyList<TrafficClassStyle> All { get; } =
    [
        new(TrafficClass.Human, "Menschen", "Browser-Anfragen echter Besucher", "#2a78d6", "#3987e5"),
        new(TrafficClass.AiAgent, "KI-Agenten", "Crawler und Assistenten von KI-Anbietern", "#1baf7a", "#199e70"),
        new(TrafficClass.Bot, "Bots", "Suchmaschinen, Monitoring, Skripte", "#4a3aa7", "#9085e9"),
        new(TrafficClass.Attack, "Angriffe", "Scans, Proben und Scanner-IPs", "#e34948", "#e66767"),
    ];

    public static TrafficClassStyle For(TrafficClass trafficClass)
    {
        foreach (var style in All)
        {
            if (style.Class == trafficClass)
            {
                return style;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(trafficClass), trafficClass, "Unbekannte Verkehrsklasse.");
    }

    public static string Label(TrafficClass trafficClass) => For(trafficClass).Label;
}
