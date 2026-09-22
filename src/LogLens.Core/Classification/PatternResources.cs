using System.Reflection;
using System.Text.Json;

namespace LogLens.Core.Classification;

/// <summary>
/// Lädt die eingebetteten Musterlisten aus <c>Resources/</c>. Die App holt zur Laufzeit
/// nichts nach: keine HTTP-Aufrufe, offline benutzbar (SPEC 5.5, Datenschutz).
/// </summary>
public static class PatternResources
{
    private const string Prefix = "LogLens.Core.Resources.";

    public static AttackPatternSet LoadAttackPatterns() =>
        new(Read(PatternJsonContext.Default.AttackPatternDocument, "attack-patterns.json"));

    public static BotPatternSet LoadBotPatterns() =>
        new(Read(PatternJsonContext.Default.BotPatternDocument, "bot-patterns.json"));

    public static AiAgentSet LoadAiAgents() =>
        new(Read(PatternJsonContext.Default.AiAgentDocument, "ai-agents.json"));

    public static AiIpRangeSet LoadAiIpRanges() =>
        new(Read(PatternJsonContext.Default.AiIpRangeDocument, "ai-ip-ranges.json"));

    /// <summary>Rohtext einer Musterliste, etwa für Diagnose oder Tests.</summary>
    public static string ReadText(string fileName)
    {
        using var stream = Open(fileName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static T Read<T>(System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, string fileName)
    {
        using var stream = Open(fileName);
        return JsonSerializer.Deserialize(stream, typeInfo)
            ?? throw new InvalidOperationException($"Die Musterliste '{fileName}' ist leer.");
    }

    private static Stream Open(string fileName)
    {
        var name = Prefix + fileName;
        return typeof(PatternResources).GetTypeInfo().Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException(
                $"Die eingebettete Ressource '{name}' fehlt. Ist sie als EmbeddedResource eingetragen?");
    }
}
