using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace XnaReborn.Core.Content;

/// <summary>
/// The stock XNA 4.0 content pipeline importers and processors, and whether MonoGame can build them.
/// Loaded from <c>rules/importer-map.json</c>, which is embedded in this assembly. All name comparisons ignore case.
/// </summary>
public sealed class ImporterMap
{
    private const string _resourceName = "XnaReborn.Core.Content.importer-map.json";

    private static readonly Lazy<ImporterMap> _default = new(LoadDefault);

    private readonly HashSet<string> _importers = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _processors = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<(string, string)> _allPairs = new(PairComparer.Instance);
    private readonly Dictionary<(string, string), ImporterPair> _pairIndex = new(PairComparer.Instance);
    private readonly Dictionary<(string, string), ImporterAlias> _aliasIndex = new(PairComparer.Instance);

    private ImporterMap(IReadOnlyList<ImporterPair> pairs, IReadOnlyList<ImporterPair> stockUnsupported, IReadOnlyList<ImporterAlias> aliases)
    {
        Pairs = pairs;
        StockUnsupported = stockUnsupported;
        Aliases = aliases;
        foreach (var alias in aliases)
        {
            _aliasIndex.TryAdd((alias.Importer, alias.Processor), alias);
        }

        foreach (var pair in pairs)
        {
            _pairIndex.TryAdd((pair.Importer, pair.Processor), pair);
        }

        foreach (var pair in pairs.Concat(stockUnsupported))
        {
            _importers.Add(pair.Importer);
            _processors.Add(pair.Processor);
            _allPairs.Add((pair.Importer, pair.Processor));
        }
    }

    /// <summary>The map shipped with XNA Reborn, loaded once from the embedded resource.</summary>
    public static ImporterMap Default => _default.Value;

    /// <summary>Stock XNA pairs that MonoGame can build with the same names.</summary>
    public IReadOnlyList<ImporterPair> Pairs { get; }

    /// <summary>Stock XNA pairs that MonoGame cannot build.</summary>
    public IReadOnlyList<ImporterPair> StockUnsupported { get; }

    /// <summary>Processors of XNA Game Studio 1.0 that were merged into a current one. They count as stock; <see cref="Resolve"/> gives the replacement.</summary>
    public IReadOnlyList<ImporterAlias> Aliases { get; }

    /// <summary>Reads a map from JSON text.</summary>
    /// <param name="json">The JSON text of an importer map, version 1.</param>
    /// <returns>The parsed map.</returns>
    /// <exception cref="FormatException">The text is not valid JSON or does not have the importer map shape.</exception>
    public static ImporterMap Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException("Importer map must be a JSON object.");
            }

            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != 1)
            {
                throw new FormatException("Importer map 'version' must be 1.");
            }

            return new ImporterMap(ReadPairs(root, "pairs", required: true), ReadPairs(root, "stockUnsupported", required: false), ReadAliases(root));
        }
        catch (JsonException exception)
        {
            throw new FormatException("Importer map is not valid JSON: " + exception.Message, exception);
        }
    }

    /// <summary>Tells whether the importer and processor belong to stock XNA. A null side is not checked.</summary>
    /// <param name="importer">The importer name, or null.</param>
    /// <param name="processor">The processor name, or null.</param>
    /// <returns>True when nothing is set, when each name set is a stock name, and when a set pair is a stock pair.</returns>
    public bool IsStock(string? importer, string? processor)
    {
        if (importer is null && processor is null)
        {
            return true;
        }

        if (importer is not null && processor is not null)
        {
            return _allPairs.Contains((importer, processor)) || _aliasIndex.ContainsKey((importer, processor));
        }

        return importer is not null ? _importers.Contains(importer) : _processors.Contains(processor!) || _aliasIndex.Keys.Any(k => string.Equals(k.Item2, processor, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Replaces a legacy XNA 1.0 processor name by the one that merged it. Any other pair is returned unchanged.</summary>
    /// <param name="importer">The importer name.</param>
    /// <param name="processor">The processor name.</param>
    /// <param name="alias">The alias that applied, or null when the pair is not an alias.</param>
    /// <returns>The processor name to use.</returns>
    public string Resolve(string importer, string processor, out ImporterAlias? alias)
    {
        alias = _aliasIndex.GetValueOrDefault((importer, processor));
        return alias?.MapsTo ?? processor;
    }

    /// <summary>Tells whether the item comes from a custom pipeline assembly. An item with neither name is never custom.</summary>
    /// <param name="importer">The importer name, or null.</param>
    /// <param name="processor">The processor name, or null.</param>
    /// <returns>True when a name is set and the combination is not stock.</returns>
    public bool IsCustom(string? importer, string? processor) =>
        (importer is not null || processor is not null) && !IsStock(importer, processor);

    /// <summary>Looks up a pair that MonoGame can build under the same names.</summary>
    /// <param name="importer">The importer name.</param>
    /// <param name="processor">The processor name.</param>
    /// <param name="pair">The entry in <see cref="Pairs"/>, or null when not found.</param>
    /// <returns>True when the pair is in <see cref="Pairs"/>. Pairs in <see cref="StockUnsupported"/> return false.</returns>
    public bool TryGetPair(string importer, string processor, [NotNullWhen(true)] out ImporterPair? pair) =>
        _pairIndex.TryGetValue((importer, processor), out pair);

    private static ImporterMap LoadDefault()
    {
        using var stream = typeof(ImporterMap).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {_resourceName} is missing.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static List<ImporterPair> ReadPairs(JsonElement root, string name, bool required)
    {
        if (!root.TryGetProperty(name, out var array))
        {
            return required ? throw new FormatException($"Importer map is missing '{name}'.") : [];
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException($"Importer map '{name}' must be an array.");
        }

        var result = new List<ImporterPair>();
        foreach (var item in array.EnumerateArray())
        {
            var where = $"{name}[{result.Count}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Importer map '{where}' must be an object.");
            }

            result.Add(new ImporterPair(
                ReadString(item, "importer", where, required: true)!,
                ReadString(item, "processor", where, required: true)!,
                ReadString(item, "note", where, required: false)));
        }

        return result;
    }

    private static List<ImporterAlias> ReadAliases(JsonElement root)
    {
        var result = new List<ImporterAlias>();
        if (!root.TryGetProperty("aliases", out var array))
        {
            return result;
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("Importer map 'aliases' must be an array.");
        }

        foreach (var item in array.EnumerateArray())
        {
            var where = $"aliases[{result.Count}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"Importer map '{where}' must be an object.");
            }

            result.Add(new ImporterAlias(
                ReadString(item, "importer", where, required: true)!,
                ReadString(item, "processor", where, required: true)!,
                ReadString(item, "mapsTo", where, required: true)!,
                ReadString(item, "note", where, required: false)));
        }

        return result;
    }

    private static string? ReadString(JsonElement item, string property, string where, bool required)
    {
        if (!item.TryGetProperty(property, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return required ? throw new FormatException($"Importer map '{where}' needs '{property}'.") : null;
        }

        if (value.ValueKind != JsonValueKind.String || (required && string.IsNullOrWhiteSpace(value.GetString())))
        {
            throw new FormatException($"Importer map '{where}.{property}' must be a non-empty string.");
        }

        return value.GetString();
    }

    private sealed class PairComparer : IEqualityComparer<(string, string)>
    {
        public static PairComparer Instance { get; } = new();

        public bool Equals((string, string) x, (string, string) y) =>
            string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string, string) obj) =>
            HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2));
    }
}
