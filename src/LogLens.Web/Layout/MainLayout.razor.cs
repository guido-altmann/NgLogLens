using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace LogLens.Web.Layout;

public partial class MainLayout : LayoutComponentBase, IDisposable
{
    [Inject] private SettingsService Settings { get; set; } = null!;

    private MudThemeProvider _themeProvider = null!;
    private bool _drawerOpen = true;
    private bool _isDarkMode;

    /// <summary>Erst nach dem ersten Rendern gibt es JS-Interop und damit das System-Schema.</summary>
    private bool _rendered;

    private bool FollowsSystem => Settings.Current.Theme == ThemePreference.System;

    private string DarkModeIcon => _isDarkMode
        ? Icons.Material.Outlined.LightMode
        : Icons.Material.Outlined.DarkMode;

    private string DarkModeLabel => _isDarkMode
        ? "Zu hellem Erscheinungsbild wechseln"
        : "Zu dunklem Erscheinungsbild wechseln";

    protected override void OnInitialized() => Settings.Changed += OnSettingsChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        // Erst jetzt gibt es JS-Interop: die gespeicherten Einstellungen (SPEC 8.9)
        // werden geladen, bevor irgendeine Datei geöffnet werden kann.
        await Settings.InitializeAsync();

        _rendered = true;
        await ApplyThemeAsync();
        StateHasChanged();
    }

    private void ToggleDrawer() => _drawerOpen = !_drawerOpen;

    /// <summary>
    /// Ab der ersten Umschaltung gilt die Wahl des Nutzers statt des Systems – auch
    /// beim nächsten Besuch, denn sie ist eine Einstellung wie jede andere.
    /// </summary>
    private Task ToggleDarkModeAsync() => Settings.SaveAsync(Settings.Current with
    {
        Theme = _isDarkMode ? ThemePreference.Light : ThemePreference.Dark,
    });

    private async Task ApplyThemeAsync()
    {
        _isDarkMode = Settings.Current.Theme switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => _rendered && await _themeProvider.GetSystemDarkModeAsync(),
        };
    }

    private void OnSettingsChanged() => _ = InvokeAsync(async () =>
    {
        await ApplyThemeAsync();
        StateHasChanged();
    });

    public void Dispose() => Settings.Changed -= OnSettingsChanged;
}
