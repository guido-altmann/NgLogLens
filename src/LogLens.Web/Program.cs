using System.Globalization;
using ApexCharts;
using LogLens.Core;
using LogLens.Web;
using LogLens.Web.Services;
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
builder.Services.AddApexCharts();
builder.Services.AddLogLensCore();

// Das Ergebnis lebt nur im Arbeitsspeicher dieser Sitzung (CLAUDE.md, Datenschutz).
builder.Services.AddSingleton<AnalysisState>();

await builder.Build().RunAsync();
