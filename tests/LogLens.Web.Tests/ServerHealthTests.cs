using LogLens.Web.Pages;

namespace LogLens.Web.Tests;

/// <summary>Server-Zustand (SPEC 8.6) mit den Sollwerten aus sample-expected.md.</summary>
public sealed class ServerHealthTests : DetailPageTestContext
{
    [Fact]
    public async Task Statuscodes_und_ihre_Klassen()
    {
        await LoadFixtureAsync();

        var cut = Render<ServerHealth>();

        var rows = Rows(cut, "status-codes");
        Assert.Equal(
            ["200 14", "206 1", "400 1", "404 10", "405 2", "ohne Status (gekürzt) 1"],
            rows.Select(r => $"{r[0]} {r[1]}"));

        Assert.Equal(
            ["2xx: 15", "4xx: 13"],
            cut.FindAll("[data-testid=status-classes] .mud-chip").Select(c => Compact(c.TextContent)));
    }

    [Fact]
    public async Task Keine_Serverfehler()
    {
        await LoadFixtureAsync();

        var cut = Render<ServerHealth>();

        Assert.NotNull(cut.Find("[data-testid=no-server-errors]"));
        Assert.Contains("keine Serverfehler", cut.Find("[data-testid=health-summary]").TextContent);
    }

    [Fact]
    public async Task Proxy_Instanzen_und_Tage_ohne_Eintraege()
    {
        await LoadFixtureAsync();

        var cut = Render<ServerHealth>();

        Assert.Equal(
            [
                ["10.0.1.9", "27.08.2026", "31.08.2026", "17"],
                ["10.0.1.4", "09.09.2026", "13.09.2026", "5"],
                ["10.0.1.8", "16.09.2026", "16.09.2026", "2"],
                ["10.0.1.2", "21.09.2026", "22.09.2026", "5"],
            ],
            Rows(cut, "proxies"));

        var days = Compact(cut.Find("[data-testid=days-without-entries]").TextContent);
        Assert.Contains("Tage ohne Einträge (18):", days);
        Assert.Contains("29.08.2026, 01.09.–08.09.2026", days);
    }

    [Fact]
    public async Task Error_Zeilen_und_Parser_Diagnose()
    {
        await LoadFixtureAsync();

        var cut = Render<ServerHealth>();

        var errors = cut.Find("[data-testid=error-summary]").TextContent;
        Assert.Contains("1 Error-Zeile.", errors);
        Assert.Contains("2 Zeilen „404.html failed\" sind Folgefehler", errors);

        var diagnostics = Rows(cut, "diagnostics").ToDictionary(r => r[0], r => r[1]);
        Assert.Equal("32", diagnostics["Zeilen gesamt"]);
        Assert.Equal("28", diagnostics["Access-Zeilen, vollständig"]);
        Assert.Equal("0", diagnostics["Nicht erkannte Zeilen"]);
    }

    [Theory]
    [InlineData(new[] { "2026-08-29" }, "29.08.2026")]
    [InlineData(new[] { "2026-08-29", "2026-08-30", "2026-09-02" }, "29.08.–30.08.2026, 02.09.2026")]
    public void Tage_werden_zu_Bereichen_zusammengefasst(string[] days, string expected)
    {
        Assert.Equal(expected, ServerHealth.DayRanges([.. days.Select(DateOnly.Parse)]));
    }
}
