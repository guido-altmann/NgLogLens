using LogLens.Web.Layout;
using LogLens.Web.Navigation;

namespace LogLens.Web.Tests;

public sealed class NavMenuTests : MudBlazorTestContext
{
    [Fact]
    public void NavMenu_zeigt_alle_neun_Ansichten()
    {
        var cut = Render<NavMenu>();

        var links = cut.FindAll("a.mud-nav-link");

        Assert.Equal(9, links.Count);
        Assert.Equal(
            NavigationEntries.All.Select(e => e.Label),
            links.Select(l => l.TextContent.Trim()));
    }

    [Fact]
    public void NavMenu_verlinkt_die_Routen_aus_der_Spezifikation()
    {
        var cut = Render<NavMenu>();

        var hrefs = cut.FindAll("a.mud-nav-link").Select(l => l.GetAttribute("href"));

        Assert.Equal(
            new[] { "/", "/overview", "/visitors", "/attacks", "/ai-agents", "/server-health", "/recommendations", "/raw", "/settings" },
            hrefs);
    }
}
