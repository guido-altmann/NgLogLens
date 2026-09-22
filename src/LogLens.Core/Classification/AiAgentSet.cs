namespace LogLens.Core.Classification;

/// <summary>Ein KI-Agent aus <c>ai-agents.json</c> (SPEC 5.5).</summary>
/// <param name="Provider">Schlüssel in <c>ai-ip-ranges.json</c> für die IP-Verifikation.</param>
/// <param name="ContactDomains">
/// Kontaktdomain aus der Agent-Kennung. Bei gekürzten Zeilen ist oft nur sie noch
/// lesbar (SPEC 2.3), der Agentname aber nicht mehr.
/// </param>
public sealed record AiAgent(
    string Name,
    string Provider,
    IReadOnlyList<string> UserAgentPatterns,
    IReadOnlyList<string> ContactDomains);

/// <summary>
/// Erkennung von KI-Agenten (SPEC 5.5). Zwei Durchläufe: erst der User-Agent, dann –
/// nur falls dort nichts passt – die Kontaktdomain. Sonst würde eine vollständige
/// <c>Claude-User</c>-Kennung an der Kontaktdomain von ClaudeBot hängen bleiben.
/// </summary>
public sealed class AiAgentSet
{
    internal AiAgentSet(AiAgentDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Updated = document.Updated;
        Agents =
        [
            .. document.Agents.Select(a => new AiAgent(
                a.Name, a.Provider, a.UserAgentPatterns ?? [], a.ContactDomains ?? []))
        ];
    }

    public string Updated { get; }

    public IReadOnlyList<AiAgent> Agents { get; }

    /// <param name="matchedByContactDomain">
    /// True, wenn der Agent nur über die Kontaktdomain gefunden wurde – bei gekürzten
    /// Zeilen der Normalfall.
    /// </param>
    public AiAgent? Resolve(string? userAgent, out bool matchedByContactDomain)
    {
        matchedByContactDomain = false;

        var value = userAgent?.Trim();
        if (string.IsNullOrEmpty(value) || value == "-")
        {
            return null;
        }

        foreach (var agent in Agents)
        {
            if (ContainsAny(value, agent.UserAgentPatterns))
            {
                return agent;
            }
        }

        foreach (var agent in Agents)
        {
            if (ContainsAny(value, agent.ContactDomains))
            {
                matchedByContactDomain = true;
                return agent;
            }
        }

        return null;
    }

    public AiAgent? Resolve(string? userAgent) => Resolve(userAgent, out _);

    private static bool ContainsAny(string value, IReadOnlyList<string> needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
