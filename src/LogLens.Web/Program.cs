using System.Globalization;
using LogLens.Core;
using LogLens.Web;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

// Anzeige in de-DE, Parsen bleibt invariant (CLAUDE.md).
var displayCulture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentCulture = displayCulture;
CultureInfo.DefaultThreadCurrentUICulture = displayCulture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddLogLensCore();

await builder.Build().RunAsync();
