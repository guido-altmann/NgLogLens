using LogLens.Core.Models;

namespace LogLens.Web.Charts;

/// <summary>Beschriftung und Farbe eines KI-Verdikts (SPEC 5.5).</summary>
public sealed record AiVerdictStyle(
    AiVerdict Verdict, string Label, string Description, string LightColor, string DarkColor)
{
    public string ColorFor(bool isDarkMode) => isDarkMode ? DarkColor : LightColor;
}

/// <summary>
/// Die drei Verdikte in fester Reihenfolge Verifiziert – Unverifiziert – Getarnt,
/// damit Grün und Rot im Stapel nie nebeneinander liegen.
/// </summary>
/// <remarks>
/// Mit dem dataviz-Validator geprüft. Hell: alle Prüfungen bestanden, Grün und Gelb
/// liegen unter 3:1 gegen den Hintergrund. Dunkel: bestanden, Gelb/Rot bei
/// Farbsehschwäche nur ΔE 7,8. Deshalb tragen die Diagramme Legende mit Zahlen,
/// 2-px-Lücken zwischen den Segmenten und eine Tabelle (SPEC 9).
/// </remarks>
public static class AiVerdictStyles
{
    public static IReadOnlyList<AiVerdictStyle> All { get; } =
    [
        new(AiVerdict.Verified, "Verifiziert", "IP im veröffentlichten Bereich des Anbieters", "#1baf7a", "#199e70"),
        new(AiVerdict.Unverified, "Unverifiziert", "IP in keinem veröffentlichten Bereich", "#eda100", "#b39000"),
        new(AiVerdict.Spoofed, "Getarnt", "Angriff mit der Kennung eines KI-Agenten", "#e34948", "#e25c6c"),
    ];

    public static AiVerdictStyle For(AiVerdict verdict) => All.Single(s => s.Verdict == verdict);

    public static string Label(AiVerdict verdict) => For(verdict).Label;
}
