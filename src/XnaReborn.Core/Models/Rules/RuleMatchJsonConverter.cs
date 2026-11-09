using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models.Rules;

/// <summary>
/// Reads and writes <see cref="RuleMatch"/> using the "type" discriminator.
/// Unlike built-in polymorphism on .NET 8, "type" may appear after other properties.
/// </summary>
public sealed class RuleMatchJsonConverter : JsonConverter<RuleMatch>
{
    private static readonly Dictionary<string, Type> _typesByName = new(StringComparer.Ordinal)
    {
        ["reference"] = typeof(ReferenceMatch),
        ["namespace"] = typeof(NamespaceMatch),
        ["type"] = typeof(TypeMatch),
        ["member"] = typeof(MemberMatch),
        ["projectProperty"] = typeof(ProjectPropertyMatch),
        ["projectItem"] = typeof(ProjectItemMatch),
        ["filePattern"] = typeof(FilePatternMatch),
        ["contentItem"] = typeof(ContentItemMatch),
        ["spriteFontFamily"] = typeof(SpriteFontFamilyMatch),
    };

    private static readonly Dictionary<Type, string> _namesByType = _typesByName.ToDictionary(p => p.Value, p => p.Key);

    /// <inheritdoc />
    public override RuleMatch? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A match must be a JSON object.");
        }

        if (!element.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            throw new JsonException("A match must have a string 'type' property.");
        }

        var name = typeElement.GetString()!;
        if (!_typesByName.TryGetValue(name, out var target))
        {
            throw new JsonException($"Unknown match type '{name}'.");
        }

        // Drop the discriminator, then bind the remaining properties to the derived record.
        var node = JsonNode.Parse(element.GetRawText())!.AsObject();
        node.Remove("type");
        return (RuleMatch?)node.Deserialize(target, options);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RuleMatch value, JsonSerializerOptions options)
    {
        var runtimeType = value.GetType();
        if (!_namesByType.TryGetValue(runtimeType, out var name))
        {
            throw new JsonException($"Unsupported match type {runtimeType.Name}.");
        }

        var node = JsonSerializer.SerializeToNode(value, runtimeType, options)!.AsObject();
        writer.WriteStartObject();
        writer.WriteString("type", name);
        foreach (var property in node)
        {
            writer.WritePropertyName(property.Key);
            if (property.Value is null)
            {
                writer.WriteNullValue();
            }
            else
            {
                property.Value.WriteTo(writer, options);
            }
        }

        writer.WriteEndObject();
    }
}

