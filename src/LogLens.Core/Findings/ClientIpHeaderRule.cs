using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Findings;

/// <summary>
/// SPEC 7, „Client-IP nur im Header": stehen im Log ausschließlich private Absender
/// (RFC 1918), kommt die echte Adresse nur über <c>X-Forwarded-For</c>. Ohne
/// <c>real_ip</c> steht in allen anderen nginx-Modulen die Proxy-Adresse – Rate-Limits
/// und Blocklisten greifen dann ins Leere.
/// </summary>
public sealed class ClientIpHeaderRule : IFindingRule
{
    public string Id => "client-ip-header";

    public Finding? Evaluate(AnalysisResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var proxies = result.ServerHealth.ProxyInstances;
        if (proxies.Count == 0 || !proxies.All(p => IpNetwork.IsPrivate(p.ProxyIp)))
        {
            return null;
        }

        // Fehlt das Feld ganz, ist die Besucher-IP unwiederbringlich weg: dann wiegt
        // der Befund schwerer als bei bloß fehlender real_ip-Konfiguration.
        var headerMissing = result.Diagnostics.ClientIpHeaderMissing;

        var reason = headerMissing
            ? $"Alle {FindingText.Number(proxies.Count)} Absender im Log sind private Adressen, und "
              + $"{FindingText.Requests(result.Diagnostics.LinesWithoutClientIpHeader)} hatten kein "
              + "X-Forwarded-For-Feld. Für diese Zeilen ist die Besucher-IP nicht mehr feststellbar."
            : $"Alle {FindingText.Number(proxies.Count)} Absender im Log sind private Adressen "
              + $"({string.Join(", ", proxies.Select(p => p.ProxyIp))}). Die Besucher-IP steht nur im "
              + "X-Forwarded-For-Feld, nicht im Adressfeld der Zeile.";

        return new Finding(
            Id,
            headerMissing ? FindingPriority.High : FindingPriority.Medium,
            "Client-IP nur im Header",
            reason,
            "Dem Proxy vertrauen und die echte Adresse übernehmen, damit nginx selbst mit ihr arbeitet.")
        {
            Snippet = new FindingSnippet("nginx-Konfiguration", "nginx",
                """
                set_real_ip_from 10.0.0.0/8;
                set_real_ip_from 172.16.0.0/12;
                set_real_ip_from 192.168.0.0/16;
                real_ip_header X-Forwarded-For;
                real_ip_recursive on;
                """),
        };
    }
}
