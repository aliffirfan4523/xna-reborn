using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace XnaReborn.Migrator.Output;

/// <summary>Writes a <see cref="Manifest"/> as indented camelCase JSON.</summary>
public static class ManifestJsonWriter
{
    private static readonly JsonSerializerOptions _options = CreateOptions();

    /// <summary>Converts the manifest to JSON with LF line endings and one trailing newline.</summary>
    /// <param name="manifest">The manifest to write.</param>
    /// <returns>The JSON text.</returns>
    public static string Write(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var json = JsonSerializer.Serialize(manifest, _options);
        return json.Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
