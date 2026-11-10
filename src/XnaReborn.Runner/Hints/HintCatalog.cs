using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XnaReborn.Runner.Hints;

/// <summary>Thrown when a hint catalog is malformed. The message names the hint and the problem.</summary>
public sealed class HintCatalogException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong with the catalog.</param>
    public HintCatalogException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with the error that caused it.</summary>
    /// <param name="message">What is wrong with the catalog.</param>
    /// <param name="innerException">The underlying error.</param>
    public HintCatalogException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// The match conditions of one hint. A build hint uses <see cref="Code"/>, <see cref="MessageContains"/>
/// and <see cref="MessageRegex"/>; a runtime hint uses <see cref="RuntimeContains"/> and <see cref="RuntimeRegex"/>.
/// </summary>
/// <param name="Code">Compiler or tool code such as CS0246, or null.</param>
/// <param name="MessageContains">Case-insensitive text the message must contain, or null.</param>
/// <param name="MessageRegex">Regular expression the message must match (case-insensitive), or null.</param>
/// <param name="RuntimeContains">Case-insensitive text found in the game output, or null.</param>
/// <param name="RuntimeRegex">Regular expression found in the game output (case-insensitive), or null.</param>
public sealed record HintMatch(
    string? Code,
    string? MessageContains,
    string? MessageRegex,
    string? RuntimeContains,
    string? RuntimeRegex)
{
    /// <summary>True when the hint is for a game's runtime output rather than for a build diagnostic.</summary>
    public bool IsRuntime => RuntimeContains is not null || RuntimeRegex is not null;
}

/// <summary>A hint together with its match conditions and compiled expressions.</summary>
public sealed class HintEntry
{
    internal HintEntry(FixHint hint, HintMatch match, Regex? messageRegex, Regex? runtimeRegex)
    {
        Hint = hint;
        Match = match;
        MessageRegex = messageRegex;
        RuntimeRegex = runtimeRegex;
    }

    /// <summary>The hint shown to the user.</summary>
    public FixHint Hint { get; }

    /// <summary>The conditions that select it.</summary>
    public HintMatch Match { get; }

    internal Regex? MessageRegex { get; }

    internal Regex? RuntimeRegex { get; }
}

/// <summary>
/// The catalog of known build and runtime failures with advice for each. Loaded from <c>hints.json</c>.
/// </summary>
public sealed class HintCatalog
{
    /// <summary>The only catalog version this code reads.</summary>
    public const int SupportedVersion = 1;

    /// <summary>Time allowed for one regular expression match.</summary>
    public static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private const string _resourceName = "XnaReborn.Runner.Hints.hints.json";

    private static readonly HashSet<string> _matchFields = new(StringComparer.Ordinal)
    {
        "code", "messageContains", "messageRegex", "runtimeContains", "runtimeRegex",
    };

    private static readonly Lazy<HintCatalog> _default = new(LoadEmbedded);

    private HintCatalog(IReadOnlyList<HintEntry> entries)
    {
        Entries = entries;
    }

    /// <summary>The catalog shipped inside the Runner assembly.</summary>
    public static HintCatalog Default => _default.Value;

    /// <summary>All hints in catalog order.</summary>
    public IReadOnlyList<HintEntry> Entries { get; }

    /// <summary>Reads and validates a catalog from JSON text.</summary>
    /// <param name="json">The catalog JSON.</param>
    /// <returns>The validated catalog.</returns>
    /// <exception cref="HintCatalogException">The JSON is invalid or a hint breaks a rule.</exception>
    public static HintCatalog Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip });
            return Build(doc.RootElement);
        }
        catch (JsonException ex)
        {
            throw new HintCatalogException("The hint catalog is not valid JSON: " + ex.Message, ex);
        }
    }

    /// <summary>Reads and validates a catalog file.</summary>
    /// <param name="path">Path of the catalog file.</param>
    /// <returns>The validated catalog.</returns>
    /// <exception cref="HintCatalogException">The JSON is invalid or a hint breaks a rule.</exception>
    public static HintCatalog Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Parse(File.ReadAllText(path));
    }

    private static HintCatalog LoadEmbedded()
    {
        using var stream = typeof(HintCatalog).Assembly.GetManifestResourceStream(_resourceName)
            ?? throw new HintCatalogException("The embedded hint catalog " + _resourceName + " is missing from the Runner assembly.");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }

    private static HintCatalog Build(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new HintCatalogException("The hint catalog must be a JSON object with \"version\" and \"hints\".");
        }

        if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var v))
        {
            throw new HintCatalogException("The hint catalog needs a numeric \"version\".");
        }

        if (v != SupportedVersion)
        {
            throw new HintCatalogException($"The hint catalog version is {v}, but only version {SupportedVersion} is supported.");
        }

        if (!root.TryGetProperty("hints", out var hints) || hints.ValueKind != JsonValueKind.Array)
        {
            throw new HintCatalogException("The hint catalog needs a \"hints\" array.");
        }

        var entries = new List<HintEntry>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        foreach (var element in hints.EnumerateArray())
        {
            index++;
            var entry = BuildEntry(element, index);
            if (!ids.Add(entry.Hint.Id))
            {
                throw new HintCatalogException($"Hint id {entry.Hint.Id} is used more than once. Hint ids must be unique.");
            }

            entries.Add(entry);
        }

        return new HintCatalog(entries);
    }

    private static HintEntry BuildEntry(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new HintCatalogException($"Hint number {index} must be a JSON object.");
        }

        var id = RequiredString(element, "id", $"hint number {index}");
        var label = "hint " + id;
        var title = RequiredString(element, "title", label);
        var advice = RequiredString(element, "advice", label);
        var ruleId = OptionalString(element, "ruleId", label);

        if (!element.TryGetProperty("match", out var matchElement) || matchElement.ValueKind != JsonValueKind.Object)
        {
            throw new HintCatalogException($"{Capitalize(label)} needs a \"match\" object.");
        }

        foreach (var property in matchElement.EnumerateObject())
        {
            if (!_matchFields.Contains(property.Name))
            {
                throw new HintCatalogException($"{Capitalize(label)} has an unknown match field \"{property.Name}\". Known fields: {string.Join(", ", _matchFields.Order(StringComparer.Ordinal))}.");
            }
        }

        var match = new HintMatch(
            OptionalString(matchElement, "code", label),
            OptionalString(matchElement, "messageContains", label),
            OptionalString(matchElement, "messageRegex", label),
            OptionalString(matchElement, "runtimeContains", label),
            OptionalString(matchElement, "runtimeRegex", label));

        var isBuild = match.Code is not null || match.MessageContains is not null || match.MessageRegex is not null;
        if (!isBuild && !match.IsRuntime)
        {
            throw new HintCatalogException($"{Capitalize(label)} has no match field. Give it at least one of {string.Join(", ", _matchFields.Order(StringComparer.Ordinal))}.");
        }

        if (isBuild && match.IsRuntime)
        {
            throw new HintCatalogException($"{Capitalize(label)} mixes build fields (code, messageContains, messageRegex) with runtime fields (runtimeContains, runtimeRegex). Split it into two hints.");
        }

        return new HintEntry(
            new FixHint(id, title, advice, ruleId),
            match,
            Compile(match.MessageRegex, "messageRegex", label),
            Compile(match.RuntimeRegex, "runtimeRegex", label));
    }

    private static Regex? Compile(string? pattern, string field, string label)
    {
        if (pattern is null)
        {
            return null;
        }

        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new HintCatalogException($"{Capitalize(label)} has a {field} that is not a valid regular expression: {ex.Message}", ex);
        }
    }

    private static string RequiredString(JsonElement element, string name, string label)
    {
        return OptionalString(element, name, label)
            ?? throw new HintCatalogException($"{Capitalize(label)} needs a non-empty \"{name}\".");
    }

    private static string? OptionalString(JsonElement element, string name, string label)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            throw new HintCatalogException($"{Capitalize(label)}: \"{name}\" must be a string.");
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static string Capitalize(string text) => char.ToUpperInvariant(text[0]) + text[1..];
}
