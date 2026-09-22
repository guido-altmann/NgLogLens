using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Layout;

public partial class MainLayout : LayoutComponentBase
{
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
        if (!firstRender || !_followsSystemPreference)
        {
            return;
        }

        _isDarkMode = await _themeProvider.GetSystemDarkModeAsync();
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
