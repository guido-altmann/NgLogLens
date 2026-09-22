using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Layout;

public partial class MainLayout : LayoutComponentBase
{
    [Inject] private SettingsService Settings { get; set; } = null!;

    private MudThemeProvider _themeProvider = null!;
    private bool _drawerOpen = true;
    private bool _isDarkMode;
    private bool _followsSystemPreference = true;

    private string DarkModeIcon => _isDarkMode
        ? Icons.Material.Outlined.LightMode
        : Icons.Material.Outlined.DarkMode;

    private string DarkModeLabel => _isDarkMode
        ? "Zu hellem Erscheinungsbild wechseln"
        : "Zu dunklem Erscheinungsbild wechseln";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Erst jetzt gibt es JS-Interop: die gespeicherten Einstellungen (SPEC 8.9)
        // werden geladen, bevor irgendeine Datei geöffnet werden kann.
        await Settings.InitializeAsync();

        if (_followsSystemPreference)
        {
            _isDarkMode = await _themeProvider.GetSystemDarkModeAsync();
        }

        StateHasChanged();
    }

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    private void ToggleDarkMode()
    {
        // Ab der ersten manuellen Umschaltung gewinnt die Auswahl des Nutzers.
        _followsSystemPreference = false;
        _isDarkMode = !_isDarkMode;
    }
}
