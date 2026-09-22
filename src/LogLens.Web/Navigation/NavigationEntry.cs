using MudBlazor;

namespace LogLens.Web.Navigation;

/// <summary>Ein Eintrag der Hauptnavigation. Routen englisch, Beschriftung deutsch.</summary>
public sealed record NavigationEntry(string Route, string Label, string Icon);

public static class NavigationEntries
{
    public static IReadOnlyList<NavigationEntry> All { get; } =
    [
        new("/", "Datei öffnen", Icons.Material.Outlined.UploadFile),
        new("/overview", "Übersicht", Icons.Material.Outlined.Dashboard),
        new("/visitors", "Besucher", Icons.Material.Outlined.People),
        new("/attacks", "Angriffe", Icons.Material.Outlined.GppMaybe),
        new("/ai-agents", "KI-Agenten", Icons.Material.Outlined.SmartToy),
        new("/server-health", "Server-Zustand", Icons.Material.Outlined.MonitorHeart),
        new("/recommendations", "Empfehlungen", Icons.Material.Outlined.Lightbulb),
        new("/raw", "Rohdaten", Icons.Material.Outlined.TableRows),
        new("/settings", "Einstellungen", Icons.Material.Outlined.Settings),
    ];
}
