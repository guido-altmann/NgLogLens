using Bunit.Rendering;
using LogLens.Web.Layout;
using LogLens.Web.Pages;
using Microsoft.AspNetCore.Components;

namespace LogLens.Web.Tests;

/// <summary>
/// Barrierearm nach SPEC 9: Tastaturbedienung, Überschriften als Gerüst für Screenreader,
/// Diagramme mit Textalternative. Die Kontraste prüft <see cref="ThemeContrastTests"/>.
/// </summary>
public sealed class AccessibilityTests : DetailPageTestContext
{
    public static TheoryData<string> Pages =>
        ["Overview", "Visitors", "Attacks", "AiAgents", "ServerHealth", "Recommendations", "RawData", "Settings"];

    [Fact]
    public void Die_Sprungmarke_ist_das_erste_Bedienelement_und_fuehrt_zum_Inhalt()
    {
        var cut = RenderLayout();

        var first = cut.FindAll("a[href], button, input, [tabindex='0']").First();
        Assert.Equal("skip-link", first.GetAttribute("data-testid"));

        first.Click();

        var main = cut.Find("main#main-content");
        Assert.Equal("-1", main.GetAttribute("tabindex"));
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public void Der_Navigationsschalter_meldet_ob_die_Navigation_offen_ist()
    {
        var cut = RenderLayout();
        var toggle = () => cut.Find("[data-testid=drawer-toggle]");

        // Der Ausgangszustand hängt an der Fensterbreite (MudDrawer); geprüft wird der Wechsel.
        var before = toggle().GetAttribute("aria-expanded");
        Assert.Contains(before, new[] { "true", "false" });

        toggle().Click();

        Assert.Equal(before == "true" ? "false" : "true", toggle().GetAttribute("aria-expanded"));
        Assert.Equal("main-navigation", toggle().GetAttribute("aria-controls"));
        Assert.NotNull(cut.Find("#main-navigation"));
    }

    [Theory]
    [MemberData(nameof(Pages))]
    public async Task Jede_Seite_hat_genau_eine_Hauptueberschrift_und_keine_Luecken_im_Gliederungsbaum(string page)
    {
        await LoadFixtureAsync();
        var cut = RenderPage(page);

        var levels = cut.FindAll("h1, h2, h3, h4, h5, h6").Select(h => h.TagName[1] - '0').ToList();

        Assert.Equal(1, levels.Count(l => l == 1));
        Assert.Equal(1, levels[0]);
        for (var i = 1; i < levels.Count; i++)
        {
            Assert.True(
                levels[i] <= levels[i - 1] + 1,
                $"{page}: Überschrift h{levels[i]} folgt auf h{levels[i - 1]} (Reihenfolge: {string.Join(", ", levels)}).");
        }
    }

    [Theory]
    [InlineData("Overview", 2)]
    [InlineData("Visitors", 1)]
    [InlineData("Attacks", 2)]
    [InlineData("AiAgents", 1)]
    [InlineData("ServerHealth", 1)]
    public async Task Jedes_Diagramm_hat_eine_Textalternative(string page, int charts)
    {
        await LoadFixtureAsync();
        var cut = RenderPage(page);

        var figures = cut.FindAll(".chart-figure");

        Assert.Equal(charts, figures.Count);
        Assert.All(figures, figure =>
        {
            // Kein role="img": die Tastatur-Navigation von ApexCharts bliebe sonst stumm.
            Assert.Equal("FIGURE", figure.TagName);
            Assert.Null(figure.GetAttribute("role"));
            Assert.False(string.IsNullOrWhiteSpace(figure.GetAttribute("aria-label")));
        });
    }

    [Fact]
    public void Die_Startseite_erlaubt_Zoomen_und_nennt_ihre_Sprache()
    {
        var html = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "LogLens.Web", "wwwroot", "index.html"));

        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain("user-scalable=no", html, StringComparison.Ordinal);
        Assert.DoesNotContain("maximum-scale", html, StringComparison.Ordinal);
    }

    private IRenderedComponent<MainLayout> RenderLayout() =>
        Render<MainLayout>(parameters => parameters
            .Add(p => p.Body, (RenderFragment)(builder => builder.AddMarkupContent(0, "<h1>Inhalt</h1>"))));

    /// <summary>Eine Seite aus <c>LogLens.Web.Pages</c> über ihren Klassennamen.</summary>
    private IRenderedComponent<ContainerFragment> RenderPage(string page)
    {
        var type = typeof(Overview).Assembly.GetType($"LogLens.Web.Pages.{page}", throwOnError: true)!;
        return Render(builder =>
        {
            builder.OpenComponent(0, type);
            builder.CloseComponent();
        });
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LogLens.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("LogLens.sln nicht gefunden.");
    }
}
