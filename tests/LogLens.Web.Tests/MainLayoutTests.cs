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

    [Theory]
    [InlineData("author-link", "https://www.guido-altmann.de")]
    [InlineData("github-link", "https://github.com/guido-altmann/NgLogLens")]
    public void Externe_Links_oeffnen_in_neuem_Tab_ohne_Referrer(string testId, string href)
    {
        var cut = Render<MainLayout>();

        var link = cut.Find($"[data-testid='{testId}']");
        Assert.Equal("a", link.TagName, ignoreCase: true);
        Assert.Equal(href, link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Contains("noopener", link.GetAttribute("rel"));
        Assert.Contains("noreferrer", link.GetAttribute("rel"));
    }
}
