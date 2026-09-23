using LogLens.Web.Layout;
using LogLens.Web.Pages;
using LogLens.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace LogLens.Web.Tests;

/// <summary>
/// Hell/Dunkel folgt dem System und ist umschaltbar (SPEC 8). Die Wahl ist eine
/// Einstellung und bleibt deshalb im Browser gespeichert (SPEC 8.9).
/// </summary>
public sealed class ThemePreferenceTests : MudBlazorTestContext
{
    public ThemePreferenceTests() => AddAppServices();

    private SettingsService SettingsService => Services.GetRequiredService<SettingsService>();

    [Fact]
    public void Ohne_Einstellung_folgt_das_Erscheinungsbild_dem_System()
    {
        Assert.Equal(ThemePreference.System, SettingsService.Current.Theme);
    }

    [Fact]
    public void Der_Schalter_in_der_Kopfzeile_wechselt_und_speichert_das_Erscheinungsbild()
    {
        var cut = RenderLayout();
        Assert.Equal("Zu dunklem Erscheinungsbild wechseln", ThemeToggle(cut).GetAttribute("aria-label"));

        ThemeToggle(cut).Click();

        cut.WaitForAssertion(() => Assert.Equal(ThemePreference.Dark, SettingsService.Current.Theme));
        Assert.Equal("Zu hellem Erscheinungsbild wechseln", ThemeToggle(cut).GetAttribute("aria-label"));

        var stored = JSInterop.Invocations["localStorage.setItem"].Last();
        Assert.Contains("\"theme\":\"Dark\"", (string)stored.Arguments[1]!, StringComparison.Ordinal);
    }

    [Fact]
    public void Ein_gespeichertes_Erscheinungsbild_gilt_ab_dem_Start()
    {
        JSInterop.Setup<string?>("localStorage.getItem", SettingsService.StorageKey)
            .SetResult("{\"scannerThreshold\":3,\"theme\":\"dark\"}");

        var cut = RenderLayout();

        cut.WaitForAssertion(() =>
            Assert.Equal("Zu hellem Erscheinungsbild wechseln", ThemeToggle(cut).GetAttribute("aria-label")));
    }

    [Fact]
    public void Ein_unbekannter_gespeicherter_Wert_faellt_auf_das_System_zurueck()
    {
        JSInterop.Setup<string?>("localStorage.getItem", SettingsService.StorageKey)
            .SetResult("{\"scannerThreshold\":3,\"theme\":7}");

        RenderLayout();

        Assert.Equal(ThemePreference.System, SettingsService.Current.Theme);
    }

    [Fact]
    public void Das_Erscheinungsbild_laesst_sich_in_den_Einstellungen_waehlen()
    {
        var cut = Render<Settings>();

        cut.Find("input[data-testid=theme-light]").Click();
        cut.Find("[data-testid=save]").Click();

        cut.WaitForAssertion(() => Assert.Equal(ThemePreference.Light, SettingsService.Current.Theme));
    }

    [Fact]
    public void Das_Erscheinungsbild_erzwingt_keine_neue_Auswertung()
    {
        var current = SettingsService.Current;

        Assert.False((current with { Theme = ThemePreference.Dark }).RequiresReanalysis(current));
    }

    private IRenderedComponent<MainLayout> RenderLayout() =>
        Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Inhalt</h1>"))));

    private static AngleSharp.Dom.IElement ThemeToggle(IRenderedComponent<MainLayout> cut) =>
        cut.Find("[data-testid=theme-toggle]");
}
