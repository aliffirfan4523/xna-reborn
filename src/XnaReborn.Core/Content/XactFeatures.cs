using System.Text.Json;

namespace XnaReborn.Core.Content;

/// <summary>One fact about XACT, with where it was read and whether it is confirmed.</summary>
/// <param name="Group">The kind of fact: unit, loopCount, instanceBehavior, variationType or unsupported.</param>
/// <param name="Key">The name or value inside the group.</param>
/// <param name="Meaning">What it means.</param>
/// <param name="Status">"confirmed" or "expected, unverified".</param>
/// <param name="Source">Where the fact was read.</param>
public sealed record XactFeature(string Group, string Key, string Meaning, string Status, string Source)
{
    /// <summary>The status text of a confirmed fact.</summary>
    public const string Confirmed = "confirmed";

    /// <summary>Whether <see cref="Status"/> is <see cref="Confirmed"/>.</summary>
    public bool IsConfirmed => string.Equals(Status, Confirmed, StringComparison.Ordinal);
}

/// <summary>
/// XACT feature facts loaded from <c>rules/xact-features.json</c>, which is embedded in this assembly.
/// </summary>
public sealed class XactFeatures
{
    private const string _resourceName = "XnaReborn.Core.Content.xact-features.json";

    private static readonly Lazy<XactFeatures> _default = new(LoadDefault);

    private XactFeatures(IReadOnlyList<XactFeature> features) => Features = features;

    /// <summary>The facts shipped with XNA Reborn, loaded once from the embedded resource.</summary>
    public static XactFeatures Default => _default.Value;

    /// <summary>Every fact, in file order.</summary>
    public IReadOnlyList<XactFeature> Features { get; }

    /// <summary>The names (block or key words) that the converter does not carry over, in file order.</summary>
    public IEnumerable<string> UnsupportedTerms => ForGroup("unsupported").Select(f => f.Key);

    /// <summary>Facts of one group, in file order.</summary>
    /// <param name="group">The group name.</param>
    public IEnumerable<XactFeature> ForGroup(string group) =>
        Features.Where(f => string.Equals(f.Group, group, StringComparison.Ordinal));

    /// <summary>Reads the facts from JSON text.</summary>
    /// <param name="json">The JSON text, version 1.</param>
    /// <exception cref="FormatException">The text is not valid JSON or does not have the expected shape.</exception>
    public static XactFeatures Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1)
            {
                throw new FormatException("XACT features 'version' must be 1.");
            }

            if (!root.TryGetProperty("features", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException("XACT features need a 'features' array.");
            }

            var result = new List<XactFeature>();
            foreach (var item in array.EnumerateArray())
            {
                var where = $"features[{result.Count}]";
                result.Add(new XactFeature(
                    Read(item, "group", where),
                    Read(item, "key", where),
                    Read(item, "meaning", where),
                    Read(item, "status", where),
                    Read(item, "source", where)));
            }

            return new XactFeatures(result);
        }
        catch (JsonException exception)
        {
            throw new FormatException("XACT features are not valid JSON: " + exception.Message, exception);
        }
    }

    private static string Read(JsonElement item, string property, string where)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty(property, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new FormatException($"XACT features '{where}' needs a non-empty string '{property}'.");
        }

        return value.GetString()!;
    }

    private static XactFeatures LoadDefault()
    {
        using var stream = typeof(XactFeatures).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {_resourceName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
