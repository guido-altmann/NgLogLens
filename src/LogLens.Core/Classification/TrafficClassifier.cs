using LogLens.Core.Models;

namespace LogLens.Core.Classification;

/// <summary>
/// Stufe „Classify" der Pipeline (SPEC 5). Zwei Durchläufe sind nötig, weil die
/// Scanner-IPs erst feststehen, wenn alle Einzelangriffe gezählt sind (SPEC 5.2).
/// Die Reihenfolge der Prüfungen ist verbindlich: Angriff vor KI-Agent vor Bot vor Mensch.
/// </summary>
public sealed class TrafficClassifier(
    AttackPatternSet attackPatterns,
    BotPatternSet botPatterns,
    AiAgentSet aiAgents,
    AiIpRangeSet aiIpRanges,
    MonitoringDetector monitoringDetector,
    ClassificationOptions options)
{
    public Task<ClassificationResult> ClassifyAsync(
        ParseResult parseResult,
        IProgress<ClassifyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        return ClassifyAsync(parseResult.AccessEntries, progress, cancellationToken);
    }

    public async Task<ClassificationResult> ClassifyAsync(
        IReadOnlyList<AccessEntry> entries,
        IProgress<ClassifyProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // Durchlauf 1: Einzelangriffe (SPEC 5.1) erkennen und je Client-IP zählen.
        var signals = new AttackSignal[entries.Count];
        var attacksPerIp = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < entries.Count; i++)
        {
            signals[i] = DetectIndividualAttack(entries[i]);

            if (signals[i].IsAttack)
            {
                var ip = entries[i].ClientIp;
                attacksPerIp[ip] = attacksPerIp.TryGetValue(ip, out var count) ? count + 1 : 1;
            }

            await YieldIfDueAsync(i + 1, entries.Count, progress, cancellationToken).ConfigureAwait(false);
        }

        // Scanner-Regel (SPEC 5.2): alle Anfragen dieser IPs gelten als Angriff.
        var scannerIps = new HashSet<string>(
            attacksPerIp.Where(p => p.Value >= options.ScannerThreshold).Select(p => p.Key),
            StringComparer.OrdinalIgnoreCase);

        var monitoringIps = monitoringDetector.Detect(entries);

        // Durchlauf 2: Klasse, Kategorie, Agent, Verdikt, Seitenaufruf.
        var classified = new List<ClassifiedEntry>(entries.Count);

        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            classified.Add(Classify(entries[i], signals[i], scannerIps, monitoringIps));

            await YieldIfDueAsync(i + 1, entries.Count, progress, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(new ClassifyProgress(entries.Count, entries.Count));

        var scanners = ScannerList.Build(classified, scannerIps, attackPatterns.ReconnaissanceCategory);

        return new ClassificationResult(classified, scanners, scannerIps, monitoringIps);
    }

    /// <summary>Ist die Anfrage für sich allein schon ein Angriff? SPEC 5.1.</summary>
    private AttackSignal DetectIndividualAttack(AccessEntry entry)
    {
        var target = RequestTarget.From(entry);

        // Agent-Discovery ist nie ein Angriffsmuster, auch nicht bei 404 (SPEC 5.4).
        // Die Scanner-Regel bleibt davon unberührt; sie greift erst im zweiten Durchlauf.
        var isAgentDiscovery = attackPatterns.IsAgentDiscovery(entry.Path);

        if (entry.Method is not null && !IsReadMethod(entry.Method))
        {
            return new AttackSignal(true, $"Methode {entry.Method}");
        }

        if (entry.Status is int status && options.AttackStatusCodes.Contains(status))
        {
            return new AttackSignal(true, $"Status {status}");
        }

        if (!isAgentDiscovery
            && entry.Status == options.AttackPathStatusCode
            && attackPatterns.MatchesAttackPath(target, out var pathPattern))
        {
            return new AttackSignal(true, $"{options.AttackPathStatusCode} auf Angriffspfad: {pathPattern}");
        }

        // Unabhängig vom Status: nginx liefert bei statischen Seiten 200, weil es die
        // Query ignoriert (SPEC 5.1.4).
        if (!isAgentDiscovery && attackPatterns.MatchesInjectionQuery(target, out var queryPattern))
        {
            return new AttackSignal(true, $"Injection-Muster in Query: {queryPattern}");
        }

        return new AttackSignal(false, isAgentDiscovery ? $"Agent-Discovery: {entry.Path}" : string.Empty);
    }

    private ClassifiedEntry Classify(
        AccessEntry entry,
        AttackSignal signal,
        HashSet<string> scannerIps,
        IReadOnlySet<string> monitoringIps)
    {
        var agent = aiAgents.Resolve(entry.UserAgent, out var byContactDomain);
        var isScanner = scannerIps.Contains(entry.ClientIp);
        var isAttack = signal.IsAttack || isScanner;

        string? category = null;
        AiVerdict? verdict = null;
        TrafficClass trafficClass;
        string reason;

        if (isAttack)
        {
            trafficClass = TrafficClass.Attack;

            // Nur wer für sich allein schon auffällig war, bekommt eine eigene
            // Kategorie; der Rest der Scanner-IP ist Aufklärung (SPEC 5.2, 5.3).
            if (signal.IsAttack)
            {
                category = attackPatterns.Categorize(
                    RequestTarget.From(entry), entry.Method, IsReadMethod(entry.Method));
                reason = signal.Reason;
            }
            else
            {
                category = attackPatterns.ReconnaissanceCategory;
                reason = $"Scanner-IP {entry.ClientIp}";
            }

            if (agent is not null)
            {
                verdict = AiVerdict.Spoofed;
                reason = $"{reason}; gibt sich als {agent.Name} aus";
            }
        }
        else if (agent is not null)
        {
            trafficClass = TrafficClass.AiAgent;

            if (aiIpRanges.IsPublishedAddress(agent, entry.ClientIp, out var provider))
            {
                verdict = AiVerdict.Verified;
                reason = $"KI-Agent {agent.Name}, IP im veröffentlichten Bereich von {provider!.Provider}";
            }
            else
            {
                verdict = AiVerdict.Unverified;
                reason = $"KI-Agent {agent.Name}, IP in keinem veröffentlichten Bereich";
            }

            if (byContactDomain)
            {
                reason = $"{reason} (erkannt an der Kontaktdomain)";
            }
        }
        else if (botPatterns.IsBot(entry.UserAgent, out var botReason))
        {
            trafficClass = TrafficClass.Bot;
            reason = botReason;
        }
        else
        {
            trafficClass = TrafficClass.Human;

            // Auch ein 404 ändert daran nichts: ein Browser mit fehlendem Asset ist
            // ein Mensch, kein Bot (SPEC 5.7, 5.9).
            reason = entry.Status is int status && !PagePath.IsSuccess(status)
                ? $"Browser, Status {status}"
                : "Browser";
        }

        return new ClassifiedEntry(
            Entry: entry,
            Class: trafficClass,
            AttackCategory: category,
            AgentName: agent?.Name,
            AiVerdict: verdict,
            IsPageView: PagePath.IsPageView(entry, trafficClass, options),
            IsMonitoringSuspected: monitoringIps.Contains(entry.ClientIp),
            Reason: reason);
    }

    private bool IsReadMethod(string? method) =>
        method is not null && options.ReadMethods.Contains(method, StringComparer.OrdinalIgnoreCase);

    private async ValueTask YieldIfDueAsync(
        int done,
        int total,
        IProgress<ClassifyProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (done % options.YieldInterval != 0)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(new ClassifyProgress(done, total));
        await Task.Yield();
    }

    /// <param name="Reason">Kurzer Grund, der später in <see cref="ClassifiedEntry.Reason"/> landet.</param>
    private readonly record struct AttackSignal(bool IsAttack, string Reason);
}
