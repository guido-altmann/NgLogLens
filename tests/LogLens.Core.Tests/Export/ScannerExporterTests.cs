using LogLens.Core.Export;
using LogLens.Core.Models;
using LogLens.Core.Tests.Fixtures;

namespace LogLens.Core.Tests.Export;

/// <summary>
/// Export der Scanner-IPs (SPEC 8.4): nginx-deny-Liste, Klartext und CSV. Geprüft
/// wird die vollständige Ausgabe, damit sie sich ohne Nacharbeit einbinden lässt.
/// </summary>
public sealed class ScannerExporterTests
{
    [Fact]
    public async Task Nginx_deny_Liste_ist_nach_Adresse_sortiert_und_kommentiert()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            """
            # Scanner-IPs aus sample-nginx.log
            # Zeitraum: 2026-08-27 bis 2026-09-22 (UTC), 3 IPs, erzeugt mit LogLens
            # Einbinden im server- oder http-Block: include /etc/nginx/conf.d/loglens-deny.conf;
            deny 192.0.2.40;  # 4 Anfragen, POST-Proben & Login-Versuche
            deny 203.0.113.10;  # 4 Anfragen, PHP-Webshells
            deny 203.0.113.55;  # 4 Anfragen, Secrets & Credentials

            """.ReplaceLineEndings("\n"),
            ScannerExporter.ToNginxDenyList(result));
    }

    [Fact]
    public async Task Klartext_enthaelt_nur_die_IPs()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("192.0.2.40\n203.0.113.10\n203.0.113.55\n", ScannerExporter.ToPlainText(result));
    }

    [Fact]
    public async Task Csv_hat_Kopfzeile_und_invariante_Werte()
    {
        var result = await FixtureLog.AnalyzeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            """
            ip,requests,individual_attacks,first_seen_utc,last_seen_utc,burst_seconds,main_category
            192.0.2.40,4,3,2026-09-21T03:53:55Z,2026-09-21T03:53:58Z,3,POST-Proben & Login-Versuche
            203.0.113.10,4,3,2026-08-27T10:16:58Z,2026-08-27T10:17:00Z,2,PHP-Webshells
            203.0.113.55,4,3,2026-09-09T07:37:26Z,2026-09-09T07:37:29Z,3,Secrets & Credentials

            """.ReplaceLineEndings("\n"),
            ScannerExporter.ToCsv(result.Scanners));
    }

    [Fact]
    public void Csv_setzt_Felder_mit_Komma_oder_Anfuehrungszeichen_in_Anfuehrungszeichen()
    {
        var at = DateTimeOffset.Parse("2026-09-21T03:53:55Z", System.Globalization.CultureInfo.InvariantCulture);

        var csv = ScannerExporter.ToCsv([new Scanner("192.0.2.1", 3, 3, at, at, "Proben, \"sonstige\"")]);

        Assert.EndsWith(",0,\"Proben, \"\"sonstige\"\"\"\n", csv);
    }

    [Fact]
    public void IPv4_steht_vor_IPv6_und_Adressen_werden_numerisch_sortiert()
    {
        var at = DateTimeOffset.Parse("2026-09-21T03:53:55Z", System.Globalization.CultureInfo.InvariantCulture);
        Scanner Make(string ip) => new(ip, 3, 3, at, at, "Aufklärung");

        var text = ScannerExporter.ToPlainText([Make("2001:db8::1"), Make("192.0.2.100"), Make("192.0.2.9")]);

        Assert.Equal("192.0.2.9\n192.0.2.100\n2001:db8::1\n", text);
    }

    [Fact]
    public void Ohne_Scanner_bleibt_die_deny_Liste_gueltig()
    {
        var text = ScannerExporter.ToNginxDenyList([], ["leer.log"], period: null);

        Assert.Equal(
            "# Scanner-IPs aus leer.log\n# Keine Scanner gefunden, erzeugt mit LogLens\n",
            text);
    }
}
