using System.Text.Json;

namespace XnaReborn.Core.Toolkit;

/// <summary>A SharpDX Toolkit namespace and the MonoGame namespace that replaces it.</summary>
/// <param name="RuleId">The catalog rule (SDX020 to SDX024) the mapping belongs to.</param>
/// <param name="From">The Toolkit namespace.</param>
/// <param name="To">The MonoGame namespace.</param>
/// <param name="QualifiedTypes">Types that are rewritten when written with <paramref name="From"/> in front. Other children of the namespace are left alone.</param>
public sealed record ToolkitNamespaceMapping(string RuleId, string From, string To, IReadOnlyList<string> QualifiedTypes);

/// <summary>A member that is renamed.</summary>
/// <param name="From">The Toolkit or SharpDX name.</param>
/// <param name="To">The MonoGame name.</param>
public sealed record ToolkitRename(string From, string To);

/// <summary>A <c>GraphicsDevice.XStates</c> collection and the MonoGame type with the same static members.</summary>
/// <param name="Property">The collection property on the Toolkit device, such as BlendStates.</param>
/// <param name="Type">The MonoGame type, such as BlendState.</param>
/// <param name="Members">The member names that exist on both sides under the same name.</param>
public sealed record ToolkitStateCollection(string Property, string Type, IReadOnlyList<string> Members);

/// <summary>A <c>Set*State</c> call and the MonoGame device property that replaces it.</summary>
/// <param name="Call">The Toolkit method name, such as SetBlendState.</param>
/// <param name="Property">The MonoGame property name, such as BlendState.</param>
public sealed record ToolkitStateSetter(string Call, string Property);

/// <summary>
/// The rename tables of the SharpDX Toolkit migration, loaded from <c>rules/sharpdx-toolkit-map.json</c>, which is embedded in this assembly.
/// The file is data: a rename is a data change. Name comparisons are ordinal.
/// </summary>
public sealed class ToolkitMap
{
    private const string _resourceName = "XnaReborn.Core.Toolkit.sharpdx-toolkit-map.json";

    private static readonly Lazy<ToolkitMap> _default = new(LoadDefault);

    private ToolkitMap(
        IReadOnlyList<string> sources,
        IReadOnlyList<ToolkitNamespaceMapping> namespaces,
        IReadOnlyList<ToolkitRename> matrixFactories,
        IReadOnlyList<ToolkitRename> mathUtilMembers,
        IReadOnlyList<ToolkitStateCollection> stateCollections,
        IReadOnlyList<ToolkitStateSetter> stateSetters,
        IReadOnlyList<ToolkitRename> backBuffer)
    {
        Sources = sources;
        Namespaces = namespaces;
        MatrixFactories = matrixFactories;
        MathUtilMembers = mathUtilMembers;
        StateCollections = stateCollections;
        StateSetters = stateSetters;
        BackBuffer = backBuffer;
    }

    /// <summary>The map shipped with XNA Reborn, loaded once from the embedded resource.</summary>
    public static ToolkitMap Default => _default.Value;

    /// <summary>Web pages the map was written from.</summary>
    public IReadOnlyList<string> Sources { get; }

    /// <summary>Namespace mappings, in file order.</summary>
    public IReadOnlyList<ToolkitNamespaceMapping> Namespaces { get; }

    /// <summary>Right-handed <c>Matrix</c> factory renames (SDX030).</summary>
    public IReadOnlyList<ToolkitRename> MatrixFactories { get; }

    /// <summary><c>MathUtil</c> to <c>MathHelper</c> member renames (SDX031).</summary>
    public IReadOnlyList<ToolkitRename> MathUtilMembers { get; }

    /// <summary>State collections that map to static members of MonoGame state types (SDX040).</summary>
    public IReadOnlyList<ToolkitStateCollection> StateCollections { get; }

    /// <summary><c>Set*State</c> calls that map to device properties (SDX041).</summary>
    public IReadOnlyList<ToolkitStateSetter> StateSetters { get; }

    /// <summary><c>BackBuffer.Width</c> and <c>Height</c> renames onto <c>PresentationParameters</c> (SDX044).</summary>
    public IReadOnlyList<ToolkitRename> BackBuffer { get; }

    /// <summary>Reads a map from JSON text.</summary>
    /// <param name="json">The JSON text of a map, version 1.</param>
    /// <returns>The parsed map.</returns>
    /// <exception cref="FormatException">The text is not valid JSON or does not have the map shape.</exception>
    public static ToolkitMap Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Toolkit map must be a JSON object.");
            }

            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
            {
                throw new FormatException("Toolkit map 'version' must be 1.");
            }

            var sources = Strings(root, "sources");
            if (sources.Count == 0)
            {
                throw new FormatException("Toolkit map 'sources' must list at least one source.");
            }

            return new ToolkitMap(
                sources,
                Objects(root, "namespaces", e => new ToolkitNamespaceMapping(Text(e, "ruleId"), Text(e, "from"), Text(e, "to"), Strings(e, "qualifiedTypes"))),
                Renames(root, "matrixFactories"),
                Renames(root, "mathUtilMembers"),
                Objects(root, "stateCollections", e => new ToolkitStateCollection(Text(e, "property"), Text(e, "type"), Strings(e, "members"))),
                Objects(root, "stateSetters", e => new ToolkitStateSetter(Text(e, "call"), Text(e, "property"))),
                Renames(root, "backBuffer"));
        }
        catch (JsonException exception)
        {
            throw new FormatException("Toolkit map is not valid JSON: " + exception.Message, exception);
        }
    }

    private static ToolkitMap LoadDefault()
    {
        using var stream = typeof(ToolkitMap).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {_resourceName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static List<ToolkitRename> Renames(JsonElement root, string name) =>
        Objects(root, name, e => new ToolkitRename(Text(e, "from"), Text(e, "to")));

    private static List<T> Objects<T>(JsonElement root, string name, Func<JsonElement, T> read)
    {
        if (!root.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"Toolkit map '{name}' must be an array.");
        }

        var list = new List<T>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Every entry of '{name}' must be an object.");
            }

            list.Add(read(element));
        }

        return list;
    }

    private static List<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"Toolkit map '{name}' must be an array of strings.");
        }

        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
            {
                throw new FormatException($"Every entry of '{name}' must be a non-empty string.");
            }

            list.Add(item.GetString()!);
        }

        return list;
    }

    private static string Text(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new FormatException($"Toolkit map entry needs a non-empty string '{name}'.");
        }

        return value.GetString()!;
    }
}
