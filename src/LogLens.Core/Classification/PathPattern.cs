using System.Text.Json;
using System.Text.Json.Serialization;

namespace LogLens.Core.Classification;

/// <summary>
/// Ein Muster aus <c>attack-patterns.json</c>. Absichtlich kein Regex: die Muster
/// kommen aus einer Datenliste, und Teilzeichenketten sind unter WebAssembly deutlich
/// billiger als ein zur Laufzeit interpretierter Ausdruck (CLAUDE.md).
/// Alle gesetzten Bedingungen müssen zutreffen (UND).
/// </summary>
/// <param name="Contains">Teilzeichenkette an beliebiger Stelle.</param>
/// <param name="StartsWith">Muss am Anfang stehen.</param>
/// <param name="EndsWith">Muss am Ende stehen.</param>
/// <param name="RootOnly">Nur Pfade mit einem einzigen Segment, z. B. <c>/docker-compose.yml</c>.</param>
[JsonConverter(typeof(PathPatternConverter))]
public sealed record PathPattern(
    string? Contains = null,
    string? StartsWith = null,
    string? EndsWith = null,
    bool RootOnly = false)
{
    private const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

    /// <summary>Kurze Beschreibung für den <c>Reason</c> eines Eintrags.</summary>
    public string Label { get; } =
        Contains ?? EndsWith ?? StartsWith ?? "?";

    public bool Matches(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (RootOnly && value.LastIndexOf('/') != 0)
        {
            return false;
        }

        return (Contains is null || value.Contains(Contains, Comparison))
            && (StartsWith is null || value.StartsWith(StartsWith, Comparison))
            && (EndsWith is null || value.EndsWith(EndsWith, Comparison));
    }
}

/// <summary>
/// Erlaubt in der JSON-Liste die Kurzform <c>".env"</c> neben der Langform
/// <c>{ "contains": "key", "endsWith": ".json" }</c>. Das hält die Musterlisten lesbar.
/// </summary>
public sealed class PathPatternConverter : JsonConverter<PathPattern>
{
    public override PathPattern Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return new PathPattern(Contains: reader.GetString());
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Ein Muster muss eine Zeichenkette oder ein Objekt sein.");
        }

        string? contains = null, startsWith = null, endsWith = null;
        var rootOnly = false;

        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var name = reader.GetString();
            reader.Read();

            switch (name)
            {
                case "contains":
                    contains = reader.GetString();
                    break;
                case "startsWith":
                    startsWith = reader.GetString();
                    break;
                case "endsWith":
                    endsWith = reader.GetString();
                    break;
                case "rootOnly":
                    rootOnly = reader.GetBoolean();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (contains is null && startsWith is null && endsWith is null)
        {
            throw new JsonException("Ein Muster braucht mindestens 'contains', 'startsWith' oder 'endsWith'.");
        }

        return new PathPattern(contains, startsWith, endsWith, rootOnly);
    }

    public override void Write(Utf8JsonWriter writer, PathPattern value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        if (value is { StartsWith: null, EndsWith: null, RootOnly: false, Contains: not null })
        {
            writer.WriteStringValue(value.Contains);
            return;
        }

        writer.WriteStartObject();
        WriteIfSet(writer, "contains", value.Contains);
        WriteIfSet(writer, "startsWith", value.StartsWith);
        WriteIfSet(writer, "endsWith", value.EndsWith);
        if (value.RootOnly)
        {
            writer.WriteBoolean("rootOnly", true);
        }

        writer.WriteEndObject();
    }

    private static void WriteIfSet(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }
}
