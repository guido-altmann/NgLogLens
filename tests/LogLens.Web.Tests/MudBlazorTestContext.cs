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
}
