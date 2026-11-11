using System.Text.Json;
using Json.Schema;

namespace XnaReborn.Analyzer.Rules;

/// <summary>Validates a catalog document against the embedded schema, then applies the checks a schema cannot express.</summary>
internal static class RuleCatalogValidator
{
    private const string _schemaResource = "XnaReborn.Analyzer.Rules.RuleSchema.json";

    private static readonly Lazy<JsonSchema> _schema = new(LoadSchema);

    private static readonly string[] _matchTypes =
        ["reference", "namespace", "type", "member", "projectProperty", "projectItem", "filePattern", "contentItem", "spriteFontFamily", "projectLinks", "preprocessorSymbol"];

    /// <summary>Returns every problem found in <paramref name="root"/>, in a stable order.</summary>
    /// <param name="root">The parsed catalog.</param>
    internal static IReadOnlyList<string> Validate(JsonElement root)
    {
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string line)
        {
            if (seen.Add(line))
            {
                errors.Add(line);
            }
        }

        var options = new EvaluationOptions { OutputFormat = OutputFormat.List };
        var result = _schema.Value.Evaluate(root, options);
        if (!result.IsValid)
        {
            foreach (var detail in result.Details ?? [])
            {
                if (detail.Errors is null || detail.IsValid)
                {
                    continue;
                }

                var location = detail.InstanceLocation.ToString();
                var evaluationPath = detail.EvaluationPath.ToString();
                if (evaluationPath.Contains("/if/", StringComparison.Ordinal)
                    || IsOtherMatchBranch(root, location, evaluationPath))
                {
                    continue;
                }

                foreach (var (keyword, message) in detail.Errors)
                {
                    var text = Describe(keyword, message, evaluationPath);
                    if (text is not null)
                    {
                        Add($"{Label(root, location)} {Display(location)}: {text}");
                    }
                }
            }
        }

        CheckRules(root, Add);
        return errors;
    }

    // Container keywords only repeat what their children already report.
    private static string? Describe(string keyword, string message, string evaluationPath)
    {
        switch (keyword)
        {
            case "properties":
            case "items":
            case "additionalProperties":
            case "oneOf":
            case "then":
            case "if":
                return null;
            case "anyOf":
                return "at least one of extension, importer, processor, or nonStandardPair is required.";
            case "required" when evaluationPath.Contains("/anyOf/", StringComparison.Ordinal):
                return null;
            default:
                return message == "All values fail against the false schema" ? "property is not allowed." : message;
        }
    }

    private static JsonSchema LoadSchema()
    {
        using var stream = typeof(RuleCatalogValidator).Assembly.GetManifestResourceStream(_schemaResource)
            ?? throw new InvalidOperationException($"Embedded resource {_schemaResource} is missing.");
        using var reader = new StreamReader(stream);
        return JsonSchema.FromText(reader.ReadToEnd());
    }

    private static string Display(string location) => location.Length == 0 ? "/" : location;

    private static string Label(JsonElement root, string location)
    {
        var segments = location.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || segments[0] != "rules" || !int.TryParse(segments[1], out var index))
        {
            return "catalog";
        }

        return LabelFor(root, index);
    }

    private static string LabelFor(JsonElement root, int index)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("rules", out var rules)
            && rules.ValueKind == JsonValueKind.Array
            && index < rules.GetArrayLength()
            && rules[index].ValueKind == JsonValueKind.Object
            && rules[index].TryGetProperty("id", out var id)
            && id.ValueKind == JsonValueKind.String
            && System.Text.RegularExpressions.Regex.IsMatch(id.GetString()!, "^XNA[0-9]{3}$"))
        {
            return id.GetString()!;
        }

        return $"rule #{index}";
    }

    // The match schema is a oneOf. Errors from branches other than the one chosen by "type" only add noise.
    private static bool IsOtherMatchBranch(JsonElement root, string location, string evaluationPath)
    {
        var marker = evaluationPath.IndexOf("/oneOf/", StringComparison.Ordinal);
        if (marker < 0 || !evaluationPath.Contains("/match", StringComparison.Ordinal))
        {
            return false;
        }

        var rest = evaluationPath[(marker + "/oneOf/".Length)..];
        var end = rest.IndexOf('/', StringComparison.Ordinal);
        if (!int.TryParse(end < 0 ? rest : rest[..end], out var branch))
        {
            return false;
        }

        var matchLocation = location;
        var matchIndex = matchLocation.IndexOf("/match", StringComparison.Ordinal);
        if (matchIndex < 0)
        {
            return true;
        }

        var element = Resolve(root, matchLocation[..(matchIndex + "/match".Length)]);
        if (element is { ValueKind: JsonValueKind.Object } match
            && match.TryGetProperty("type", out var type)
            && type.ValueKind == JsonValueKind.String)
        {
            return Array.IndexOf(_matchTypes, type.GetString()) != branch;
        }

        return true;
    }

    private static JsonElement? Resolve(JsonElement root, string pointer)
    {
        var current = root;
        foreach (var segment in pointer.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var next))
            {
                current = next;
            }
            else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var i) && i < current.GetArrayLength())
            {
                current = current[i];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    private static void CheckRules(JsonElement root, Action<string> add)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("rules", out var rules)
            || rules.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var firstIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        var index = 0;
        foreach (var rule in rules.EnumerateArray())
        {
            if (rule.ValueKind == JsonValueKind.Object)
            {
                var label = LabelFor(root, index);
                CheckDuplicateId(rule, index, label, firstIndexById, add);
                CheckMatch(rule, index, label, add);
            }

            index++;
        }
    }

    private static void CheckDuplicateId(JsonElement rule, int index, string label, Dictionary<string, int> firstIndexById, Action<string> add)
    {
        if (!rule.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String)
        {
            return;
        }

        var value = id.GetString()!;
        if (firstIndexById.TryGetValue(value, out var first))
        {
            add($"{label} /rules/{index}/id: duplicate rule id (first defined at /rules/{first}/id).");
        }
        else
        {
            firstIndexById[value] = index;
        }
    }

    private static void CheckMatch(JsonElement rule, int index, string label, Action<string> add)
    {
        if (!rule.TryGetProperty("match", out var match) || match.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (match.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String
            && Array.IndexOf(_matchTypes, type.GetString()) < 0)
        {
            add($"{label} /rules/{index}/match/type: unknown match type '{type.GetString()}'.");
        }

        if (match.TryGetProperty("matches", out var pattern) && pattern.ValueKind == JsonValueKind.String)
        {
            try
            {
                _ = new System.Text.RegularExpressions.Regex(pattern.GetString()!, System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException ex)
            {
                add($"{label} /rules/{index}/match/matches: not a valid regular expression ({ex.Message}).");
            }
        }

        if (match.TryGetProperty("arguments", out var arguments) && arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("min", out var min) && min.ValueKind == JsonValueKind.Number && min.TryGetInt64(out var minValue)
            && arguments.TryGetProperty("max", out var max) && max.ValueKind == JsonValueKind.Number && max.TryGetInt64(out var maxValue)
            && minValue > maxValue)
        {
            add($"{label} /rules/{index}/match/arguments: min ({minValue}) is greater than max ({maxValue}).");
        }
    }
}
