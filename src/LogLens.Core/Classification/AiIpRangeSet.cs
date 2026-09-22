using System.Net;

namespace LogLens.Core.Classification;

/// <summary>Wie sich der Agent eines Anbieters überhaupt prüfen lässt.</summary>
public enum AiVerificationMethod
{
    /// <summary>Anbieter veröffentlicht eine IP-Liste; nur hier ist „Verified" möglich.</summary>
    PublishedRanges,

    /// <summary>Nur Reverse-DNS (z. B. Amazonbot) – im Browser nicht prüfbar.</summary>
    ReverseDns,

    /// <summary>Nur über die Herkunfts-ASN (z. B. Meta) – im Browser nicht prüfbar.</summary>
    Asn,

    /// <summary>Anbieter veröffentlicht nichts.</summary>
    None,
}

/// <summary>Die IP-Bereiche eines Anbieters samt Quelle und Stand (SPEC 5.5).</summary>
public sealed record AiIpRangeProvider(
    string Provider,
    IReadOnlyList<string> Agents,
    AiVerificationMethod Verification,
    string Source,
    string? SourceUpdated,
    string? Retrieved,
    string? Note,
    IReadOnlyList<IpNetwork> Networks);

/// <summary>
/// Veröffentlichte IP-Bereiche der KI-Anbieter. Die Liste ist eingebettet und wird
/// nicht zur Laufzeit nachgeladen (SPEC 5.5); Quellen und Aktualisierung stehen in
/// <c>docs/ip-range-sources.md</c>.
/// </summary>
public sealed class AiIpRangeSet
{
    private readonly Dictionary<string, List<AiIpRangeProvider>> _byAgent;
    private readonly Dictionary<string, List<AiIpRangeProvider>> _byProvider;

    internal AiIpRangeSet(AiIpRangeDocument document)
        : this(
            (document ?? throw new ArgumentNullException(nameof(document))).Updated,
            document.Providers.Select(Convert).ToList())
    {
    }

    /// <summary>Für Tests und für später vom Nutzer gepflegte Bereiche.</summary>
    public AiIpRangeSet(string updated, IReadOnlyList<AiIpRangeProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        Updated = updated;
        Providers = providers;

        _byProvider = providers
            .GroupBy(p => p.Provider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        _byAgent = new Dictionary<string, List<AiIpRangeProvider>>(StringComparer.OrdinalIgnoreCase);
        foreach (var provider in providers)
        {
            foreach (var agent in provider.Agents)
            {
                if (!_byAgent.TryGetValue(agent, out var list))
                {
                    _byAgent[agent] = list = [];
                }

                list.Add(provider);
            }
        }
    }

    public string Updated { get; }

    public IReadOnlyList<AiIpRangeProvider> Providers { get; }

    /// <summary>
    /// Liegt die Client-IP in einem veröffentlichten Bereich des Agenten? Gesucht wird
    /// zuerst über den Agentnamen, sonst über den Anbieter aus <c>ai-agents.json</c>.
    /// </summary>
    public bool IsPublishedAddress(AiAgent agent, string? clientIp, out AiIpRangeProvider? provider)
    {
        ArgumentNullException.ThrowIfNull(agent);

        provider = null;
        if (!IPAddress.TryParse(clientIp, out var address))
        {
            return false;
        }

        if (!_byAgent.TryGetValue(agent.Name, out var candidates)
            && !_byProvider.TryGetValue(agent.Provider, out candidates))
        {
            return false;
        }

        foreach (var candidate in candidates)
        {
            foreach (var network in candidate.Networks)
            {
                if (network.Contains(address))
                {
                    provider = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static AiIpRangeProvider Convert(AiIpRangeProviderDocument document) =>
        new(
            document.Provider,
            document.Agents,
            ParseMethod(document.Verification),
            document.Source,
            document.SourceUpdated,
            document.Retrieved,
            document.Note,
            [.. document.Prefixes.Select(IpNetwork.Parse)]);

    private static AiVerificationMethod ParseMethod(string? value) => value switch
    {
        "published-ranges" => AiVerificationMethod.PublishedRanges,
        "reverse-dns" => AiVerificationMethod.ReverseDns,
        "asn" => AiVerificationMethod.Asn,
        "none" => AiVerificationMethod.None,
        _ => throw new InvalidOperationException($"Unbekanntes Prüfverfahren '{value}' in ai-ip-ranges.json."),
    };
}
