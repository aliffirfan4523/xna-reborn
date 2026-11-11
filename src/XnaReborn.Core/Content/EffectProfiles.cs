using System.Text.Json;

namespace XnaReborn.Core.Content;

/// <summary>How one shader model name is written for each MonoGame graphics API.</summary>
/// <param name="Model">The model as written in an XNA effect, such as <c>ps_2_0</c>.</param>
/// <param name="OpenGl">The model to use when the OpenGL compiler runs.</param>
/// <param name="DirectX">The model to use when the DirectX compiler runs.</param>
/// <param name="Status">"confirmed" or "expected, unverified".</param>
public sealed record EffectProfile(string Model, string OpenGl, string DirectX, string Status);

/// <summary>
/// Shader profile data loaded from <c>rules/effect-profiles.json</c>, which is embedded in this assembly:
/// the model mapping, the DirectX escalation ladder, and the pass state and sampler state allow lists.
/// </summary>
public sealed class EffectProfiles
{
    private const string _resourceName = "XnaReborn.Core.Content.effect-profiles.json";

    private static readonly Lazy<EffectProfiles> _default = new(LoadDefault);

    private readonly Dictionary<string, EffectProfile> _byModel;

    private EffectProfiles(
        IReadOnlyList<EffectProfile> profiles,
        IReadOnlyList<string> escalationLevels,
        string hiDefFrom,
        IReadOnlySet<string> passStates,
        IReadOnlySet<string> samplerStates)
    {
        Profiles = profiles;
        EscalationLevels = escalationLevels;
        HiDefFrom = hiDefFrom;
        PassStates = passStates;
        SamplerStates = samplerStates;
        _byModel = profiles.ToDictionary(p => p.Model, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The data shipped with XNA Reborn, loaded once from the embedded resource.</summary>
    public static EffectProfiles Default => _default.Value;

    /// <summary>The model mapping, in file order.</summary>
    public IReadOnlyList<EffectProfile> Profiles { get; }

    /// <summary>The DirectX level suffixes from lowest to highest, for example <c>_4_0_level_9_1</c>.</summary>
    public IReadOnlyList<string> EscalationLevels { get; }

    /// <summary>The first level in <see cref="EscalationLevels"/> that needs the HiDef graphics profile.</summary>
    public string HiDefFrom { get; }

    /// <summary>Pass state names the MonoGame effect compiler accepts (ignoring case).</summary>
    public IReadOnlySet<string> PassStates { get; }

    /// <summary>Sampler state names the MonoGame effect compiler accepts (ignoring case).</summary>
    public IReadOnlySet<string> SamplerStates { get; }

    /// <summary>Reads the data from JSON text.</summary>
    /// <param name="json">The JSON text, version 1.</param>
    /// <exception cref="FormatException">The text is not valid JSON or does not have the expected shape.</exception>
    public static EffectProfiles Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number) || number != 1)
            {
                throw new FormatException("Effect profiles 'version' must be 1.");
            }

            var profiles = Array(root, "profiles").Select(e => new EffectProfile(
                Text(e, "model"), Text(e, "openGl"), Text(e, "directX"), Text(e, "status"))).ToList();
            var escalation = root.TryGetProperty("escalation", out var node) && node.ValueKind == JsonValueKind.Object
                ? node
                : throw new FormatException("Effect profiles need an 'escalation' object.");
            var levels = Array(escalation, "levels").Select(e => e.GetString() ?? string.Empty).ToList();
            var hiDef = Text(escalation, "hiDefFrom");
            if (levels.Count == 0 || !levels.Contains(hiDef))
            {
                throw new FormatException("Effect profiles 'escalation.hiDefFrom' must be one of 'escalation.levels'.");
            }

            return new EffectProfiles(
                profiles,
                levels,
                hiDef,
                Array(root, "passStates").Select(e => e.GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase),
                Array(root, "samplerStates").Select(e => e.GetString() ?? string.Empty).ToHashSet(StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException exception)
        {
            throw new FormatException("Effect profiles are not valid JSON: " + exception.Message, exception);
        }
    }

    /// <summary>Looks up a model such as <c>ps_2_0</c>, ignoring case.</summary>
    /// <param name="model">The model name.</param>
    /// <param name="profile">The entry, or null when the model is not in the table.</param>
    public bool TryGet(string model, out EffectProfile profile)
    {
        if (_byModel.TryGetValue(model, out var found))
        {
            profile = found;
            return true;
        }

        profile = null!;
        return false;
    }

    private static IEnumerable<JsonElement> Array(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray()
            : throw new FormatException($"Effect profiles need an array '{name}'.");

    private static string Text(JsonElement item, string property) =>
        item.ValueKind == JsonValueKind.Object
        && item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
        && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new FormatException($"Effect profiles need a non-empty string '{property}'.");

    private static EffectProfiles LoadDefault()
    {
        using var stream = typeof(EffectProfiles).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {_resourceName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
