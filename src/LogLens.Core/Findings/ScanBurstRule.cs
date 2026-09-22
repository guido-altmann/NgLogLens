using System.Globalization;
using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „Scan-Bursts": eine Scanner-IP, die sehr viele Anfragen in kurzer Zeit
/// abfeuert. Hohe Priorität – das ist die einzige Regel, die von laufender Last
/// spricht und nicht von einer Nachlässigkeit in der Konfiguration.
/// </summary>
public sealed class ScanBurstRule(FindingOptions options) : IFindingRule
{
    public string Id => "scan-burst";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var bursts = result.Scanners
            .Where(s => s.Requests >= options.ScanBurstRequests && s.Burst <= options.ScanBurstWindow)
            .OrderByDescending(s => s.Requests)
            .ToList();

        if (bursts.Count == 0)
        {
            return null;
        }

        var (details, more) = FindingText.Take(
            bursts.Select(s =>
                $"{s.ClientIp}: {FindingText.Requests(s.Requests)} in {Duration(s.Burst)} ({s.MainCategory})"),
            options.MaxDetails);

        var window = options.ScanBurstWindow.TotalMinutes.ToString("0.#", CultureInfo.CurrentCulture);

        return new Finding(
            Id,
            FindingPriority.High,
            "Scan-Bursts",
            $"{(bursts.Count == 1 ? "Eine IP" : $"{FindingText.Number(bursts.Count)} IPs")} haben mindestens "
            + $"{FindingText.Number(options.ScanBurstRequests)} Anfragen innerhalb von {window} Minuten gestellt.",
            "Vor der Anwendung begrenzen: eine Rate-Limit-Middleware in Traefik, oder CrowdSec, "
            + "das solche IPs anhand des Logs automatisch sperrt.")
        {
            DetailsCaption = "Betroffene IPs",
            Details = details,
            MoreDetails = more,
            Snippet = new FindingSnippet("Traefik-Middleware (Coolify-Labels)", "yaml",
                """
                labels:
                  - traefik.http.middlewares.loglens-ratelimit.ratelimit.average=30
                  - traefik.http.middlewares.loglens-ratelimit.ratelimit.burst=60
                  - traefik.http.middlewares.loglens-ratelimit.ratelimit.period=1s
                  - traefik.http.routers.<router>.middlewares=loglens-ratelimit
                """),
        };
    }

    private static string Duration(TimeSpan burst) => burst.TotalMinutes >= 1
        ? $"{burst.TotalMinutes.ToString("0.#", CultureInfo.CurrentCulture)} Minuten"
        : $"{burst.TotalSeconds.ToString("0.#", CultureInfo.CurrentCulture)} Sekunden";
}
