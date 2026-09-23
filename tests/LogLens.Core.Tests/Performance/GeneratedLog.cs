using System.Globalization;
using System.Text;

namespace LogLens.Core.Tests.Performance;

/// <summary>
/// Erzeugt ein synthetisches nginx-Log für den Benchmark aus SPEC 9. Der Zufall ist
/// fest geseedet, damit jeder Lauf dieselbe Datei misst. Die Mischung deckt alle Pfade
/// der Pipeline ab: Menschen, Bots, KI-Agenten, Einzelangriffe, Scanner, 404-Folgefehler,
/// sonstige Error-Zeilen, gekürzte und unbekannte Zeilen.
/// </summary>
/// <remarks>
/// Nur Dokumentations-Adressbereiche (RFC 5737) und <c>example.de</c>, wie bei den
/// Fixtures (CLAUDE.md, Datenschutz). Die Proxy-IPs sind privat wie im echten Export.
/// </remarks>
internal static class GeneratedLog
{
    private const int Seed = 20260923;

    private static readonly DateTimeOffset Start = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly string[] ProxyIps = ["10.0.1.2", "10.0.1.4", "10.0.1.8", "10.0.1.9"];

    private static readonly string[] Pages =
    [
        "/", "/impressum.html", "/datenschutz.html", "/artikel.html", "/ki-integration.html",
        "/leistungen/", "/blog/nginx-logs-lesen.html", "/kontakt", "/download/ki-reifegrad-analyse.pdf",
    ];

    private static readonly string[] Assets =
    [
        "/css/styles.css", "/js/app.js", "/img/logo.svg", "/img/hero.webp", "/favicon.ico",
        "/fonts/inter.woff2", "/apple-touch-icon.png",
    ];

    private static readonly string[] Browsers =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15",
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_7 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
        "Mozilla/5.0 (X11; Linux x86_64; rv:130.0) Gecko/20100101 Firefox/130.0",
    ];

    private static readonly string[] Bots =
    [
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)",
        "Mozilla/5.0 (compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm)",
        "facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)",
        "curl/8.7.1",
        "python-requests/2.32.3",
    ];

    private static readonly string[] AiAgents =
    [
        "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; ClaudeBot/1.0; +claudebot@anthropic.com)",
        "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; GPTBot/1.2; +https://openai.com/gptbot)",
        "Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; PerplexityBot/1.0; +https://perplexity.ai/perplexitybot)",
    ];

    private static readonly string[] AttackPaths =
    [
        "/.env", "/wp-login.php", "/wp-admin/setup-config.php", "/xmlrpc.php", "/.git/config",
        "/@fs/root/.aws/credentials?raw??", "/appsettings.Production.json", "/phpinfo.php",
        "/cgi-bin/luci/;stok=/locale", "/vendor/phpunit/phpunit/src/Util/PHP/eval-stdin.php",
    ];

    private static readonly string[] Referrers =
    [
        "-", "-", "https://www.google.com/", "https://example.de/", "https://www.bing.com/",
    ];

    /// <summary>Eine erzeugte Logdatei samt ihrer Eckdaten.</summary>
    public sealed record Log(string Text, int Lines, long Bytes);

    /// <summary>Zeilen erzeugen, bis sowohl <paramref name="targetBytes"/> als auch <paramref name="minLines"/> erreicht sind.</summary>
    public static Log Create(long targetBytes, int minLines = 0)
    {
        var random = new Random(Seed);
        var builder = new StringBuilder(capacity: (int)Math.Min(targetBytes + 4_096, int.MaxValue));
        var lines = 0;

        // Im Mittel 30 s Abstand, also gut drei Wochen; die Zeit läuft nur vorwärts wie im echten Log.
        var timestamp = Start;

        while (builder.Length < targetBytes || lines < minLines)
        {
            timestamp = timestamp.AddSeconds(random.Next(1, 60));
            var before = builder.Length;
            AppendLine(builder, random, timestamp);
            lines += CountLineBreaks(builder, before);
        }

        // ASCII-only: Zeichen = Bytes.
        return new Log(builder.ToString(), lines, builder.Length);
    }

    private static int CountLineBreaks(StringBuilder builder, int from)
    {
        var count = 0;
        for (var i = from; i < builder.Length; i++)
        {
            if (builder[i] == '\n')
            {
                count++;
            }
        }

        return count;
    }

    private static void AppendLine(StringBuilder builder, Random random, DateTimeOffset timestamp)
    {
        var proxy = Pick(random, ProxyIps);
        var roll = random.Next(1_000);

        switch (roll)
        {
            case < 450:
                // Mensch: Seite oder Asset aus einem Browser.
                var isPage = random.Next(3) == 0;
                AppendAccess(
                    builder, proxy, timestamp, "GET",
                    isPage ? Pick(random, Pages) : Pick(random, Assets),
                    status: random.Next(20) == 0 ? 404 : 200,
                    bytes: random.Next(500, 60_000),
                    Pick(random, Referrers), Pick(random, Browsers), Visitor(random));
                break;

            case < 600:
                AppendAccess(
                    builder, proxy, timestamp, "GET", Pick(random, Pages), 200, random.Next(500, 9_000),
                    "-", Pick(random, Bots), $"192.0.2.{random.Next(1, 100)}");
                break;

            case < 700:
                AppendAccess(
                    builder, proxy, timestamp, "GET", Pick(random, Pages), 200, random.Next(500, 9_000),
                    "-", Pick(random, AiAgents), $"192.0.2.{random.Next(100, 150)}");
                break;

            case < 850:
                // Angriff, überwiegend von wenigen Scannern, dazu der 404-Folgefehler.
                var scanner = random.Next(4) == 0 ? Visitor(random) : $"203.0.113.{random.Next(200, 220)}";
                var path = Pick(random, AttackPaths);
                AppendAccess(
                    builder, proxy, timestamp, "GET", path, 404, 555, "-", Pick(random, Browsers), scanner);
                AppendNotFoundPageError(builder, proxy, timestamp, path);
                break;

            case < 870:
                AppendAccess(
                    builder, proxy, timestamp, "POST", "/", 405, 157, "-", "Mozilla/5.0", $"203.0.113.{random.Next(220, 240)}");
                break;

            case < 990:
                // Browser-404 auf fehlende Assets samt Folgefehler (Regel „Fehlende Assets").
                AppendAccess(
                    builder, proxy, timestamp, "GET", "/favicon.ico", 404, 555,
                    "https://example.de/", Pick(random, Browsers), Visitor(random));
                AppendNotFoundPageError(builder, proxy, timestamp, "/favicon.ico");
                break;

            case < 995:
                builder
                    .Append(timestamp.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture))
                    .Append(" [error] 30#30: *")
                    .Append(random.Next(1, 99_999).ToString(CultureInfo.InvariantCulture))
                    .Append(" upstream timed out (110: Connection timed out), client: ")
                    .Append(proxy)
                    .Append(", server: , request: \"GET /api HTTP/1.1\", host: \"example.de\"\n");
                break;

            case < 998:
                // Gekürzte Zeile wie im Coolify-Export (SPEC 2.3).
                builder
                    .Append(proxy)
                    .Append(" - - [")
                    .Append(timestamp.ToString("dd/MMM/yyyy:", CultureInfo.InvariantCulture))
                    .Append("<REDACTED>@anthropic.com)\" \"192.0.2.")
                    .Append(random.Next(100, 150).ToString(CultureInfo.InvariantCulture))
                    .Append("\"\n");
                break;

            default:
                builder.Append("Restarting nginx container …\n");
                break;
        }
    }

    private static void AppendAccess(
        StringBuilder builder,
        string proxy,
        DateTimeOffset timestamp,
        string method,
        string path,
        int status,
        int bytes,
        string referrer,
        string userAgent,
        string clientIp)
    {
        builder
            .Append(proxy)
            .Append(" - - [")
            .Append(timestamp.ToString("dd/MMM/yyyy:HH:mm:ss +0000", CultureInfo.InvariantCulture))
            .Append("] \"").Append(method).Append(' ').Append(path).Append(" HTTP/1.1\" ")
            .Append(status.ToString(CultureInfo.InvariantCulture)).Append(' ')
            .Append(bytes.ToString(CultureInfo.InvariantCulture))
            .Append(" \"").Append(referrer).Append("\" \"").Append(userAgent).Append("\" \"")
            .Append(clientIp).Append("\"\n");
    }

    private static void AppendNotFoundPageError(StringBuilder builder, string proxy, DateTimeOffset timestamp, string path)
    {
        builder
            .Append(timestamp.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture))
            .Append(" [error] 30#30: *728 open() \"/usr/share/nginx/html/404.html\" failed (2: No such file or directory), client: ")
            .Append(proxy)
            .Append(", server: , request: \"GET ").Append(path).Append(" HTTP/1.1\", host: \"example.de\"\n");
    }

    /// <summary>Besucher aus zwei Dokumentationsnetzen, damit es mehrere /24-Netze gibt.</summary>
    private static string Visitor(Random random) => random.Next(2) == 0
        ? $"198.51.100.{random.Next(1, 255)}"
        : $"203.0.113.{random.Next(1, 200)}";

    private static string Pick(Random random, string[] values) => values[random.Next(values.Length)];
}
