using LogLens.Core.Classification;
using LogLens.Core.Models;

namespace LogLens.Core.Aggregation;

/// <summary>
/// KI-Agenten je Name mit Verteilung der Verdikte (SPEC 6, 8.5) und den Seiten, die
/// verifizierte Agenten abgerufen haben. Getarnte Anfragen stehen in der Klasse
/// <see cref="TrafficClass.Attack"/> und zählen hier trotzdem mit.
/// </summary>
public sealed class AiAgentAggregator(AiAgentSet agents, AggregationOptions options)
{
    public IReadOnlyList<AiAgentStatistics> Aggregate(IReadOnlyList<ClassifiedEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var counters = new Dictionary<string, AgentCounter>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.AgentName is not { } name || entry.AiVerdict is not { } verdict)
            {
                continue;
            }

            if (!counters.TryGetValue(name, out var counter))
            {
                counters[name] = counter = new AgentCounter();
            }

            counter.Verdicts[(int)verdict]++;

            if (verdict != AiVerdict.Verified)
            {
                continue;
            }

            if (entry.Entry.Path is null)
            {
                counter.VerifiedWithoutPath++;
            }
            else
            {
                Ranking.Increment(counter.Pages, PagePath.Normalize(entry.Entry.Path));
            }
        }

        return
        [
            .. counters
                .Select(p => new AiAgentStatistics(
                    Name: p.Key,
                    Provider: agents.Agents.FirstOrDefault(a => a.Name == p.Key)?.Provider,
                    Verified: p.Value.Verdicts[(int)AiVerdict.Verified],
                    Unverified: p.Value.Verdicts[(int)AiVerdict.Unverified],
                    Spoofed: p.Value.Verdicts[(int)AiVerdict.Spoofed],
                    VerifiedPages: Ranking.Top(p.Value.Pages, options.TopListSize),
                    VerifiedWithoutPath: p.Value.VerifiedWithoutPath))
                .OrderByDescending(a => a.Requests)
                .ThenBy(a => a.Name, StringComparer.Ordinal),
        ];
    }

    private sealed class AgentCounter
    {
        public int[] Verdicts { get; } = new int[Enum.GetValues<AiVerdict>().Length];

        public Dictionary<string, int> Pages { get; } = new(StringComparer.Ordinal);

        public int VerifiedWithoutPath { get; set; }
    }
}
