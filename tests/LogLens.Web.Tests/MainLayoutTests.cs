using LogLens.Web.Layout;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Tests;

public sealed class MainLayoutTests : MudBlazorTestContext
{
    public MainLayoutTests() => AddAppServices();

    [Fact]
    public void MainLayout_rendert_Kopfzeile_und_Inhalt()
    {
        var cut = Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<p>Inhalt</p>"))));

        Assert.Contains("LogLens", cut.Find(".mud-appbar").TextContent);
        Assert.Contains("Inhalt", cut.Markup);
    }
}
