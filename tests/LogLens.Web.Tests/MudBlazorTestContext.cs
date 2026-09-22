using LogLens.Core;
using LogLens.Web.Services;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace LogLens.Web.Tests;

/// <summary>
/// Basis für Komponententests: MudBlazor-Dienste registriert, JS-Interop tolerant.
/// </summary>
public abstract class MudBlazorTestContext : BunitContext
{
    protected MudBlazorTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    /// <summary>
    /// Kern und die Dienste der App, wie <c>Program.cs</c> sie registriert. Für Tests,
    /// die Layout oder Einstellungen rendern.
    /// </summary>
    protected void AddAppServices()
    {
        Services.AddLogLensCore();
        Services.AddSingleton<AnalysisState>();
        Services.AddSingleton<ViewPreferences>();
        Services.AddSingleton<IpDisplay>();
        Services.AddSingleton<SettingsService>();
        Services.AddScoped<FileDownloadService>();
    }
}
