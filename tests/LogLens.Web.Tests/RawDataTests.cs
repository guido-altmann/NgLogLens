using LogLens.Web.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace LogLens.Web.Tests;

/// <summary>Rohdaten (SPEC 8.8): Filter über die klassifizierten Anfragen.</summary>
public sealed class RawDataTests : DetailPageTestContext
{
    public RawDataTests() => Render<MudPopoverProvider>();

    [Fact]
    public async Task Ohne_Filter_stehen_alle_Anfragen_da()
    {
        await LoadFixtureAsync();

        var cut = Render<RawData>();

        Assert.Equal("29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim());
        Assert.True(cut.Find("[data-testid=reset-filters]").HasAttribute("disabled"));
    }

    [Fact]
    public async Task IP_aus_dem_Link_ist_vorbelegt()
    {
        await LoadFixtureAsync();

        Navigate("/raw?ip=203.0.113.55");
        var cut = Render<RawData>();

        Assert.Equal("4 von 29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim());
    }

    [Fact]
    public async Task Klasse_aus_dem_Link_ist_vorbelegt()
    {
        await LoadFixtureAsync();

        Navigate("/raw?class=attack");
        var cut = Render<RawData>();

        Assert.Equal("13 von 29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim());
    }

    [Fact]
    public async Task Statusfilter_und_Zuruecksetzen()
    {
        await LoadFixtureAsync();

        var cut = Render<RawData>();
        Input(cut, "status-filter").Input("4xx");

        cut.WaitForAssertion(() =>
            Assert.Equal("13 von 29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim()));

        var button = cut.Find("[data-testid=reset-filters]");
        Assert.False(button.HasAttribute("disabled"));
        button.Click();

        cut.WaitForAssertion(() =>
            Assert.Equal("29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim()));
        Assert.Equal(string.Empty, Input(cut, "status-filter").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public async Task Ungueltiger_Status_filtert_nicht_und_zeigt_einen_Fehler()
    {
        await LoadFixtureAsync();

        var cut = Render<RawData>();
        Input(cut, "status-filter").Input("abc");

        cut.WaitForAssertion(() => Assert.Contains("z. B. 404, 4xx", cut.Markup));
        Assert.Equal("29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim());
    }

    [Fact]
    public async Task Pfadfilter_findet_dekodierte_Pfade()
    {
        await LoadFixtureAsync();

        var cut = Render<RawData>();
        Input(cut, "path-filter").Input("../");

        cut.WaitForAssertion(() =>
            Assert.Equal("1 von 29 Anfragen", cut.Find("[data-testid=row-count]").TextContent.Trim()));
    }

    /// <summary>SupplyParameterFromQuery liest die Adresse, nicht die Parameterliste.</summary>
    private void Navigate(string uri) => Services.GetRequiredService<NavigationManager>().NavigateTo(uri);

    private static AngleSharp.Dom.IElement Input(IRenderedComponent<RawData> cut, string testId)
    {
        var element = cut.Find($"[data-testid={testId}]");
        return element.TagName == "INPUT" ? element : element.QuerySelector("input")!;
    }
}
