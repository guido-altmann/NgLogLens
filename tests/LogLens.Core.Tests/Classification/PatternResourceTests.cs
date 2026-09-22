using LogLens.Core.Classification;

namespace LogLens.Core.Tests.Classification;

/// <summary>
/// Die eingebetteten Musterlisten müssen ladbar und in sich stimmig sein. Sie sind
/// Daten, keine Klassen – ein Tippfehler darf nicht erst zur Laufzeit auffallen.
/// </summary>
public sealed class PatternResourceTests
{
    [Fact]
    public void Angriffsmuster_werden_geladen_und_halten_die_Reihenfolge_der_Spezifikation()
    {
        var patterns = PatternResources.LoadAttackPatterns();

        Assert.Equal(
            [
                "POST-Proben & Login-Versuche",
                "WordPress",
                "Vite-Dev-Server",
                "Secrets & Credentials",
                "PHP-Webshells",
                "Framework-Endpunkte",
                "Path Traversal & Injection",
            ],
            patterns.Categories.Select(c => c.Name));

        Assert.Equal("Aufklärung", patterns.ReconnaissanceCategory);
        Assert.Equal("Sonstige Proben", patterns.FallbackCategory);
    }

    [Theory]
    [InlineData("/llms.txt")]
    [InlineData("/llms-full.txt")]
    [InlineData("/.well-known/agents.json")]
    [InlineData("/.well-known/agent-card.json")]
    [InlineData("/.well-known/mcp/server-card.json")]
    [InlineData("/.well-known/ai-plugin.json")]
    public void Agent_Discovery_Pfade_gelten_nicht_als_Angriffsmuster(string path)
    {
        var patterns = PatternResources.LoadAttackPatterns();

        Assert.True(patterns.IsAgentDiscovery(path));
    }

    [Theory]
    [InlineData("/.env", "Secrets & Credentials")]
    [InlineData("/serviceaccount-key.json", "Secrets & Credentials")]
    [InlineData("/docker-compose.yml", "Secrets & Credentials")]
    [InlineData("/wp-login.php", "WordPress")]
    [InlineData("/@vite/env", "Vite-Dev-Server")]
    [InlineData("/shell.php", "PHP-Webshells")]
    [InlineData("/actuator/health", "Framework-Endpunkte")]
    [InlineData("/cgi-bin/luci", "Path Traversal & Injection")]
    [InlineData("/login", "POST-Proben & Login-Versuche")]
    [InlineData("/ueber-uns", "Sonstige Proben")]
    public void Kategorien_greifen_in_der_Reihenfolge_der_Spezifikation(string path, string expected)
    {
        var patterns = PatternResources.LoadAttackPatterns();

        var category = patterns.Categorize(new RequestTarget(path, null), "GET", isReadMethod: true);

        Assert.Equal(expected, category);
    }

    [Fact]
    public void Eine_yml_Datei_in_einem_Unterordner_ist_kein_Secrets_Treffer()
    {
        // Das Muster gilt laut SPEC 5.3 nur im Wurzelverzeichnis.
        var patterns = PatternResources.LoadAttackPatterns();

        Assert.False(patterns.MatchesAttackPath(new RequestTarget("/docs/anleitung.yml", null), out _));
        Assert.True(patterns.MatchesAttackPath(new RequestTarget("/compose.yml", null), out _));
    }

    [Theory]
    [InlineData("cmd=whoami")]
    [InlineData("command=%60id%60")]
    [InlineData("x=;echo+hi")]
    [InlineData("q=GSCAN_CMDI")]
    public void Injection_Muster_werden_in_der_Query_erkannt(string query)
    {
        var patterns = PatternResources.LoadAttackPatterns();

        Assert.True(patterns.MatchesInjectionQuery(new RequestTarget("/", query), out _));
    }

    [Fact]
    public void Ein_prozentkodierter_Traversal_Pfad_wird_roh_und_dekodiert_erkannt()
    {
        var patterns = PatternResources.LoadAttackPatterns();

        Assert.True(patterns.MatchesAttackPath(new RequestTarget("/%2e%2e%2fetc%2fpasswd", null), out _));
    }

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", true)]
    [InlineData("facebookexternalhit/1.1", true)]
    [InlineData("curl/8.4.0", true)]
    [InlineData("Mozilla/5.0", true)]
    [InlineData("-", true)]
    [InlineData("", true)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 Safari/605.1.15", false)]
    public void Bot_Muster_treffen_nur_Bots(string userAgent, bool expected)
    {
        var patterns = PatternResources.LoadBotPatterns();

        Assert.Equal(expected, patterns.IsBot(userAgent, out _));
    }

    [Fact]
    public void Der_User_Agent_gewinnt_vor_der_Kontaktdomain()
    {
        // Sonst bliebe eine vollständige Claude-User-Kennung an der Kontaktdomain
        // von ClaudeBot hängen (SPEC 5.5).
        var agents = PatternResources.LoadAiAgents();

        var agent = agents.Resolve(
            "Mozilla/5.0 (compatible; Claude-User/1.0; +Claude-User@anthropic.com)", out var byDomain);

        Assert.Equal("Claude-User", agent?.Name);
        Assert.False(byDomain);
    }

    [Fact]
    public void Eine_gekuerzte_Zeile_wird_ueber_die_Kontaktdomain_erkannt()
    {
        var agents = PatternResources.LoadAiAgents();

        var agent = agents.Resolve("<REDACTED>@anthropic.com)\"", out var byDomain);

        Assert.Equal("ClaudeBot", agent?.Name);
        Assert.True(byDomain);
    }

    [Fact]
    public void Jede_Musterliste_traegt_einen_Stand()
    {
        Assert.False(string.IsNullOrWhiteSpace(PatternResources.LoadAttackPatterns().Updated));
        Assert.False(string.IsNullOrWhiteSpace(PatternResources.LoadBotPatterns().Updated));
        Assert.False(string.IsNullOrWhiteSpace(PatternResources.LoadAiAgents().Updated));
        Assert.False(string.IsNullOrWhiteSpace(PatternResources.LoadAiIpRanges().Updated));
        Assert.False(string.IsNullOrWhiteSpace(PatternResources.LoadFindingPatterns().Updated));
    }

    [Fact]
    public void Jeder_Anbieter_mit_IP_Bereichen_nennt_Quelle_und_Stand()
    {
        var ranges = PatternResources.LoadAiIpRanges();

        Assert.NotEmpty(ranges.Providers);

        foreach (var provider in ranges.Providers)
        {
            Assert.False(string.IsNullOrWhiteSpace(provider.Source));
            Assert.NotEmpty(provider.Agents);

            if (provider.Verification == AiVerificationMethod.PublishedRanges)
            {
                Assert.NotEmpty(provider.Networks);
                Assert.False(string.IsNullOrWhiteSpace(provider.SourceUpdated));
            }
            else
            {
                // Ohne veröffentlichte Liste bleibt das Verdikt Unverified; der Grund
                // dafür gehört dokumentiert.
                Assert.Empty(provider.Networks);
                Assert.False(string.IsNullOrWhiteSpace(provider.Note));
            }
        }
    }

    [Fact]
    public void Jeder_Agent_aus_den_IP_Bereichen_ist_auch_ein_bekannter_KI_Agent()
    {
        var ranges = PatternResources.LoadAiIpRanges();
        var known = PatternResources.LoadAiAgents().Agents.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Googlebot und facebookexternalhit sind Suchbots (SPEC 5.6) und stehen in den
        // Bereichen nur, damit die Liste vollständig bleibt.
        string[] searchBots = ["Googlebot", "facebookexternalhit"];

        var unknown = ranges.Providers
            .SelectMany(p => p.Agents)
            .Where(a => !known.Contains(a) && !searchBots.Contains(a, StringComparer.OrdinalIgnoreCase))
            .ToList();

        Assert.Empty(unknown);
    }

    [Fact]
    public void Der_Anthropic_Bereich_enthaelt_die_IP_aus_der_Fixture()
    {
        var ranges = PatternResources.LoadAiIpRanges();
        var agent = PatternResources.LoadAiAgents().Resolve("ClaudeBot/1.0");

        Assert.NotNull(agent);
        Assert.True(ranges.IsPublishedAddress(agent, "216.73.216.232", out var provider));
        Assert.Equal("Anthropic", provider?.Provider);
        Assert.False(ranges.IsPublishedAddress(agent, "192.0.2.201", out _));
    }
}
