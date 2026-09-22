namespace LogLens.Core.Models;

/// <summary>Die vier Verkehrsklassen der Auswertung (SPEC 4, 5.1–5.7).</summary>
public enum TrafficClass
{
    Human,
    AiAgent,
    Bot,
    Attack,
}

/// <summary>
/// Ergebnis der IP-Prüfung eines KI-Agenten (SPEC 5.5). <see cref="Spoofed"/> gewinnt:
/// wer angreift, benutzt die Agent-Kennung als Tarnung.
/// </summary>
public enum AiVerdict
{
    Verified,
    Unverified,
    Spoofed,
}
