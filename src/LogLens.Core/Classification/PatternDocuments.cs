using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogLens.Core.Classification;

/// <summary>
/// Abbild von <c>Resources/attack-patterns.json</c>. Die Dokumenttypen bleiben dicht
/// an der Datei; die ausgewerteten Formen stehen in <see cref="AttackPatternSet"/> und
/// den übrigen Mengen.
/// </summary>
internal sealed record AttackPatternDocument(
    string Updated,
    IReadOnlyList<string> AgentDiscoveryPaths,
    string ReconnaissanceCategory,
    string FallbackCategory,
    IReadOnlyList<AttackCategoryDocument> Categories);

internal sealed record AttackCategoryDocument(
    string Name,
    bool AppliesToNonReadMethods,
    bool Injection,
    IReadOnlyList<PathPattern>? PathPatterns,
    IReadOnlyList<PathPattern>? QueryPatterns);

/// <summary>Abbild von <c>Resources/bot-patterns.json</c> (SPEC 5.6).</summary>
internal sealed record BotPatternDocument(
    string Updated,
    bool EmptyUserAgentIsBot,
    IReadOnlyList<string> ExactUserAgents,
    IReadOnlyList<string> UserAgentPatterns);

/// <summary>Abbild von <c>Resources/ai-agents.json</c> (SPEC 5.5).</summary>
internal sealed record AiAgentDocument(
    string Updated,
    IReadOnlyList<AiAgentEntryDocument> Agents);

internal sealed record AiAgentEntryDocument(
    string Name,
    string Provider,
    IReadOnlyList<string>? UserAgentPatterns,
    IReadOnlyList<string>? ContactDomains);

/// <summary>Abbild von <c>Resources/ai-ip-ranges.json</c> (SPEC 5.5, docs/ip-range-sources.md).</summary>
internal sealed record AiIpRangeDocument(
    string Updated,
    IReadOnlyList<AiIpRangeProviderDocument> Providers);

internal sealed record AiIpRangeProviderDocument(
    string Provider,
    IReadOnlyList<string> Agents,
    string Verification,
    string Source,
    string? SourceUpdated,
    string? Retrieved,
    string? Note,
    IReadOnlyList<string> Prefixes);

/// <summary>
/// Quellgenerierte Deserialisierung: unter WebAssembly wird getrimmt, und Reflexion
/// über Modelltypen wäre dort weder sicher noch schnell.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AttackPatternDocument))]
[JsonSerializable(typeof(BotPatternDocument))]
[JsonSerializable(typeof(AiAgentDocument))]
[JsonSerializable(typeof(AiIpRangeDocument))]
internal sealed partial class PatternJsonContext : JsonSerializerContext;
