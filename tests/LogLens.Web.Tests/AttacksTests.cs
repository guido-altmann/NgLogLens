using LogLens.Web.Pages;
using LogLens.Web.Services;

namespace LogLens.Web.Tests;

/// <summary>Angriffe (SPEC 8.4) mit den Sollwerten aus sample-expected.md.</summary>
public sealed class AttacksTests : DetailPageTestContext
{
    [Fact]
    public async Task Zusammenfassung_und_Kategorien()
    {
        await LoadFixtureAsync();

        var cut = Render<Attacks>();

        Assert.Equal(
            "13 Anfragen waren Angriffe, von 4 IPs. 3 Scanner-IPs stehen für 12 davon.",
            cut.Find("[data-testid=attack-summary]").TextContent.Trim());

        var categories = Rows(cut, "categories").ToDictionary(r => r[0], r => r[1]);
        Assert.Equal(7, categories.Count);
        Assert.Equal("3", categories["Secrets & Credentials"]);
        Assert.Equal("3", categories["Aufklärung"]);
        Assert.Equal("3", categories["POST-Proben & Login-Versuche"]);
        Assert.Equal("1", categories["Vite-Dev-Server"]);
    }

    [Fact]
    public async Task Scanner_stehen_vollstaendig_da_und_verlinken_die_Rohdaten()
    {
        await LoadFixtureAsync();

        var cut = Render<Attacks>();

        var rows = Rows(cut, "scanners");
        Assert.Equal(
            ["192.0.2.40", "203.0.113.10", "203.0.113.55"],
            rows.Select(r => r[0]).Order(StringComparer.Ordinal));
        Assert.All(rows, r => Assert.Equal(["4", "3"], r[1..3]));

        var link = cut.FindAll("[data-testid=scanners] a").Single(a => a.TextContent == "203.0.113.10");
        Assert.Equal("/raw?ip=203.0.113.10", link.GetAttribute("href"));
    }

    [Fact]
    public async Task Export_der_deny_Liste_laedt_eine_Datei_im_Browser()
    {
        await LoadFixtureAsync();
        var module = JSInterop.SetupModule(FileDownloadService.ModulePath);
        module.SetupVoid("downloadText", _ => true);

        var cut = Render<Attacks>();
        cut.Find("[data-testid=export-nginx]").Click();

        cut.WaitForAssertion(() => module.VerifyInvoke("downloadText"));
        var invocation = module.VerifyInvoke("downloadText");
        Assert.Equal("loglens-scanner-deny.conf", invocation.Arguments[0]);
        Assert.Contains("deny 203.0.113.55;", (string)invocation.Arguments[1]!);
        Assert.Equal("text/plain;charset=utf-8", invocation.Arguments[2]);
    }

    [Fact]
    public async Task Export_als_CSV()
    {
        await LoadFixtureAsync();
        var module = JSInterop.SetupModule(FileDownloadService.ModulePath);
        module.SetupVoid("downloadText", _ => true);

        var cut = Render<Attacks>();
        cut.Find("[data-testid=export-csv]").Click();

        cut.WaitForAssertion(() => module.VerifyInvoke("downloadText"));
        var invocation = module.VerifyInvoke("downloadText");
        Assert.Equal("loglens-scanner.csv", invocation.Arguments[0]);
        Assert.StartsWith("ip,requests,", (string)invocation.Arguments[1]!);
    }

    [Fact]
    public async Task Ohne_Angriffe_steht_eine_Entwarnung()
    {
        await LoadAsync("""
            10.0.1.9 - - [28/Aug/2026:08:15:44 +0000] "GET / HTTP/1.1" 200 8123 "-" "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) Safari/605.1.15" "198.51.100.23"
            """);

        var cut = Render<Attacks>();

        Assert.Contains("Keine Anfrage wurde als Angriff gewertet", cut.Find("[data-testid=no-attacks]").TextContent);
    }

    [Theory]
    [InlineData(3, "3 s")]
    [InlineData(250, "4 min 10 s")]
    [InlineData(7_500, "2 h 5 min")]
    [InlineData(273_600, "3 d 4 h")]
    public void Burst_Dauer_ist_kompakt(int seconds, string expected)
    {
        Assert.Equal(expected, Attacks.Duration(TimeSpan.FromSeconds(seconds)));
    }
}
