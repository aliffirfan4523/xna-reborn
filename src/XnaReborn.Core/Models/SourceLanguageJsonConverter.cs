using System.Text.Json;
using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>Reads and writes <see cref="SourceLanguage"/> as "csharp" or "vb".</summary>
public sealed class SourceLanguageJsonConverter : JsonConverter<SourceLanguage>
{
    /// <inheritdoc />
    public override SourceLanguage Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return text switch
        {
            "csharp" => SourceLanguage.CSharp,
            "vb" => SourceLanguage.VisualBasic,
            _ => throw new JsonException($"'{text}' is not a valid source language. Use \"csharp\" or \"vb\"."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, SourceLanguage value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            SourceLanguage.CSharp => "csharp",
            SourceLanguage.VisualBasic => "vb",
            _ => throw new JsonException($"Unsupported source language value {value}."),
        });
    }
}
