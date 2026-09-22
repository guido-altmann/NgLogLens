namespace LogLens.Core.Classification;

/// <summary>
/// Such- und sonstige Bots (SPEC 5.6). Wird erst geprüft, wenn weder Angriff (5.1/5.2)
/// noch KI-Agent (5.5) zutrifft. Alles, was hier nicht hängen bleibt, ist ein Mensch –
/// unabhängig vom Statuscode (SPEC 5.7).
/// </summary>
public sealed class BotPatternSet
{
    private readonly HashSet<string> _exactUserAgents;

    internal BotPatternSet(BotPatternDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        Updated = document.Updated;
        EmptyUserAgentIsBot = document.EmptyUserAgentIsBot;
        ExactUserAgents = document.ExactUserAgents;
        UserAgentPatterns = document.UserAgentPatterns;
        _exactUserAgents = new HashSet<string>(document.ExactUserAgents, StringComparer.OrdinalIgnoreCase);
    }

    public string Updated { get; }

    public bool EmptyUserAgentIsBot { get; }

    /// <summary>User-Agents, die exakt so lauten müssen, z. B. das nackte <c>Mozilla/5.0</c>.</summary>
    public IReadOnlyList<string> ExactUserAgents { get; }

    public IReadOnlyList<string> UserAgentPatterns { get; }

    /// <param name="reason">Kurzer Grund für die Rohdaten-Ansicht (SPEC 4).</param>
    public bool IsBot(string? userAgent, out string reason)
    {
        var value = userAgent?.Trim();

        if (string.IsNullOrEmpty(value) || value == "-")
        {
            reason = "Kein User-Agent";
            return EmptyUserAgentIsBot;
        }

        if (_exactUserAgents.Contains(value))
        {
            reason = $"User-Agent ist nur „{value}\"";
            return true;
        }

        foreach (var pattern in UserAgentPatterns)
        {
            if (value.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                reason = $"Bot-Kennung im User-Agent: {pattern}";
                return true;
            }
        }

        reason = string.Empty;
        return false;
    }
}
