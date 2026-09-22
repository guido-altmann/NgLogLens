using LogLens.Core;
using LogLens.Core.Classification;

namespace LogLens.Core.Tests.Classification;

/// <summary>Seitenbegriff und Pfad-Normalisierung nach SPEC 5.7.</summary>
public sealed class PagePathTests
{
    private static readonly ClassificationOptions Options = new();

    [Theory]
    [InlineData("/", true)]
    [InlineData("/impressum", true)]
    [InlineData("/ki-integration.html", true)]
    [InlineData("/download/ki-reifegrad-analyse.pdf", true)]
    [InlineData("/css/styles.css", false)]
    [InlineData("/js/app.js", false)]
    [InlineData("/bilder/logo.svg", false)]
    [InlineData("/fonts/inter.woff2", false)]
    [InlineData("/sitemap.xml", false)]
    [InlineData("/favicon.ico", false)]
    public void Seiten_werden_von_Assets_unterschieden(string path, bool expected)
    {
        Assert.Equal(expected, PagePath.IsPage(path, Options));
    }

    [Theory]
    [InlineData("/impressum.html", "/impressum")]
    [InlineData("/impressum", "/impressum")]
    [InlineData("/index.html", "/")]
    [InlineData("/", "/")]
    [InlineData("/blog/", "/blog")]
    [InlineData("/blog/beitrag.html?ref=x", "/blog/beitrag")]
    [InlineData(null, "/")]
    public void Normalisierung_fasst_dieselbe_Seite_zusammen(string? path, string expected)
    {
        Assert.Equal(expected, PagePath.Normalize(path));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(206, true)]
    [InlineData(304, true)]
    [InlineData(404, false)]
    [InlineData(500, false)]
    public void Nur_ausgelieferte_Seiten_zaehlen(int status, bool expected)
    {
        Assert.Equal(expected, PagePath.IsSuccess(status));
    }
}
