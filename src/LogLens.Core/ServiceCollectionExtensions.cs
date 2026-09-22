using LogLens.Core.Aggregation;
using LogLens.Core.Classification;
using LogLens.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LogLens.Core;

/// <summary>
/// Registrierung der Kernbausteine. Die Bibliothek bleibt frei von UI- und I/O-Details.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLogLensCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(new ParserOptions());

        // Reihenfolge ist Fachlogik: die vollständigen Formen gewinnen vor den gekürzten
        // (SPEC 2.3). TryAddEnumerable bewahrt die Reihenfolge der Registrierung.
        services.TryAddEnumerable(
        [
            ServiceDescriptor.Singleton<ILogLineParser, AccessLineParser>(),
            ServiceDescriptor.Singleton<ILogLineParser, TruncatedAccessLineParser>(),
            ServiceDescriptor.Singleton<ILogLineParser, ErrorLineParser>(),
            ServiceDescriptor.Singleton<ILogLineParser, TruncatedErrorLineParser>(),
        ]);

        services.TryAddSingleton<LogLineParserChain>();
        services.TryAddSingleton<ErrorLineDeduplicator>();
        services.TryAddSingleton<LogParseService>();

        // Musterlisten einmal aus den eingebetteten Ressourcen lesen (SPEC 5.3–5.6).
        services.TryAddSingleton(new ClassificationOptions());
        services.TryAddSingleton(_ => PatternResources.LoadAttackPatterns());
        services.TryAddSingleton(_ => PatternResources.LoadBotPatterns());
        services.TryAddSingleton(_ => PatternResources.LoadAiAgents());
        services.TryAddSingleton(_ => PatternResources.LoadAiIpRanges());
        services.TryAddSingleton<MonitoringDetector>();
        services.TryAddSingleton<TrafficClassifier>();

        services.TryAddSingleton(new AggregationOptions());
        services.TryAddSingleton<VisitorAggregator>();
        services.TryAddSingleton<AttackAggregator>();
        services.TryAddSingleton<AiAgentAggregator>();
        services.TryAddSingleton<ServerHealthAggregator>();
        services.TryAddSingleton<AnalysisAggregator>();
        services.TryAddSingleton<LogAnalysisPipeline>();

        return services;
    }
}
