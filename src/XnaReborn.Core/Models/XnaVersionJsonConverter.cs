using System.Text.Json;
using System.Text.Json.Serialization;

namespace XnaReborn.Core.Models;

/// <summary>Reads and writes <see cref="XnaVersion"/> as "1.0", "2.0", "3.0", "3.1", "4.0", or "Unknown".</summary>
public sealed class XnaVersionJsonConverter : JsonConverter<XnaVersion>
{
    /// <inheritdoc />
    public override XnaVersion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        return text switch
        {
            "1.0" => XnaVersion.V10,
            "2.0" => XnaVersion.V20,
            "3.0" => XnaVersion.V30,
            "3.1" => XnaVersion.V31,
            "4.0" => XnaVersion.V40,
            "Unknown" => XnaVersion.Unknown,
            _ => throw new JsonException($"'{text}' is not a valid XNA version. Use \"1.0\", \"2.0\", \"3.0\", \"3.1\", \"4.0\", or \"Unknown\"."),
        };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, XnaVersion value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value switch
        {
            XnaVersion.V10 => "1.0",
            XnaVersion.V20 => "2.0",
            XnaVersion.V30 => "3.0",
            XnaVersion.V31 => "3.1",
            XnaVersion.V40 => "4.0",
            XnaVersion.Unknown => "Unknown",
            _ => throw new JsonException($"Unsupported XNA version value {value}."),
        });
    }
}
