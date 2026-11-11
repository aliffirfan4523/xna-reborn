using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Flash.Analyzer.Resolving;

/// <summary>One parameter of a runtime method, with its type written as AS3 would.</summary>
/// <param name="Name">Parameter name.</param>
/// <param name="Type">AS3 type name such as Number, int, String or FlxSprite.</param>
public sealed record FlixelApiParameter(string Name, string Type);

/// <summary>One public member of a runtime type.</summary>
/// <param name="Name">Member name.</param>
/// <param name="Kind">field, property or method.</param>
/// <param name="Type">AS3 type of the field or property, or the return type of the method.</param>
/// <param name="Static">True for static members and constants.</param>
/// <param name="Parameters">Method parameters, or null for fields and properties.</param>
public sealed record FlixelApiMember(
    string Name,
    string Kind,
    string Type,
    bool Static,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<FlixelApiParameter>? Parameters = null);

/// <summary>One public type of the runtime.</summary>
/// <param name="Name">Type name without generic arguments.</param>
/// <param name="Base">Base class name, or null.</param>
/// <param name="Members">Public members, sorted.</param>
public sealed record FlixelApiType(
    string Name,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Base,
    IReadOnlyList<FlixelApiMember> Members);

/// <summary>
/// The member types of the Flixel and AS3 runtime (<c>src/XnaReborn.Flixel</c>), written as AS3 type names. The checked-in file
/// <c>flixel-api.json</c> is generated from the runtime sources with Roslyn (syntax only) and a test fails when it is out of date.
/// </summary>
/// <param name="SchemaVersion">Version of the file format. Currently 1.</param>
/// <param name="Types">Public types, sorted by name.</param>
public sealed record FlixelApiMap(int SchemaVersion, IReadOnlyList<FlixelApiType> Types)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private static readonly Dictionary<string, string> _typeNames = new(StringComparer.Ordinal)
    {
        ["float"] = "Number",
        ["double"] = "Number",
        ["decimal"] = "Number",
        ["int"] = "int",
        ["long"] = "int",
        ["short"] = "int",
        ["byte"] = "int",
        ["uint"] = "uint",
        ["ulong"] = "uint",
        ["ushort"] = "uint",
        ["bool"] = "Boolean",
        ["string"] = "String",
        ["object"] = "Object",
        ["void"] = "void",
        ["AsArray"] = "Array",
        ["AsObject"] = "Object",
        ["AsFunction"] = "Function",
        ["AsClass"] = "Class",
        ["AsDictionary"] = "Dictionary",
    };

    /// <summary>Loads the map compiled into this assembly (<c>flixel-api.json</c>).</summary>
    public static FlixelApiMap LoadBuiltIn()
    {
        using var stream = typeof(FlixelApiMap).Assembly.GetManifestResourceStream("XnaReborn.Flash.Analyzer.flixel-api.json")
            ?? throw new InvalidOperationException("The Flixel API map is not embedded.");
        return Parse(new StreamReader(stream).ReadToEnd());
    }

    /// <summary>Reads a map from JSON text.</summary>
    /// <param name="json">The file text.</param>
    public static FlixelApiMap Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return JsonSerializer.Deserialize<FlixelApiMap>(json, _json) ?? throw new InvalidOperationException("The Flixel API map is empty.");
    }

    /// <summary>Writes the map as indented JSON with LF line endings and a final newline.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, _json).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    /// <summary>Builds the map from every <c>.cs</c> file under a runtime folder. Folders named bin and obj are skipped.</summary>
    /// <param name="folder">The runtime project folder.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public static FlixelApiMap FromFolder(string folder, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var sep = Path.DirectorySeparatorChar;
        var files = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(sep + "bin" + sep, StringComparison.Ordinal) && !f.Contains(sep + "obj" + sep, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        return FromSources(files.Select(File.ReadAllText), cancellationToken);
    }

    /// <summary>Builds the map from C# source texts. Only public top-level types are read, and only their public members.</summary>
    /// <param name="sources">The file texts.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    public static FlixelApiMap FromSources(IEnumerable<string> sources, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var types = new Dictionary<string, (string? Base, List<FlixelApiMember> Members)>(StringComparer.Ordinal);
        foreach (var text in sources)
        {
            var root = CSharpSyntaxTree.ParseText(text, cancellationToken: cancellationToken).GetRoot(cancellationToken);
            foreach (var type in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(IsPublicTopLevel))
            {
                var entry = types.TryGetValue(type.Identifier.Text, out var existing) ? existing : (Base: null, Members: []);
                var baseName = entry.Base ?? BaseClassOf(type);
                types[type.Identifier.Text] = (baseName, entry.Members);
                entry.Members.AddRange(MembersOf(type));
            }
        }

        return new FlixelApiMap(
            1,
            [.. types.OrderBy(t => t.Key, StringComparer.Ordinal).Select(t => new FlixelApiType(
                t.Key,
                t.Value.Base,
                [.. t.Value.Members
                    .OrderBy(m => m.Name, StringComparer.Ordinal)
                    .ThenBy(m => m.Kind, StringComparer.Ordinal)
                    .ThenBy(m => string.Join(",", m.Parameters?.Select(p => p.Type) ?? []), StringComparer.Ordinal)]))]);
    }

    private static bool IsPublicTopLevel(BaseTypeDeclarationSyntax type) =>
        type.Modifiers.Any(SyntaxKind.PublicKeyword) && type.Parent is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax;

    private static string? BaseClassOf(BaseTypeDeclarationSyntax type)
    {
        // shortcut: syntax only, so an interface is told from a class by the I-prefix convention. Fine for this runtime, revisit if a base class starts with I.
        var first = type.BaseList?.Types.FirstOrDefault()?.Type;
        if (type is InterfaceDeclarationSyntax or EnumDeclarationSyntax || first is null)
        {
            return null;
        }

        var name = MapType(first);
        return name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]) ? null : name;
    }

    private static IEnumerable<FlixelApiMember> MembersOf(BaseTypeDeclarationSyntax type)
    {
        var inInterface = type is InterfaceDeclarationSyntax;
        if (type is EnumDeclarationSyntax e)
        {
            return e.Members.Select(m => new FlixelApiMember(m.Identifier.Text, "field", e.Identifier.Text, true));
        }

        var members = new List<FlixelApiMember>();
        foreach (var node in ((TypeDeclarationSyntax)type).Members)
        {
            var isStatic = node.Modifiers.Any(SyntaxKind.StaticKeyword);
            if (!inInterface && !node.Modifiers.Any(SyntaxKind.PublicKeyword))
            {
                continue;
            }

            switch (node)
            {
                case FieldDeclarationSyntax f:
                    var constant = f.Modifiers.Any(SyntaxKind.ConstKeyword);
                    members.AddRange(f.Declaration.Variables.Select(v => new FlixelApiMember(v.Identifier.Text, "field", MapType(f.Declaration.Type), isStatic || constant)));
                    break;
                case PropertyDeclarationSyntax p:
                    members.Add(new FlixelApiMember(p.Identifier.Text, "property", MapType(p.Type), isStatic));
                    break;
                case MethodDeclarationSyntax m:
                    members.Add(new FlixelApiMember(m.Identifier.Text, "method", MapType(m.ReturnType), isStatic, [.. m.ParameterList.Parameters.Select(p => new FlixelApiParameter(p.Identifier.Text, p.Type is null ? "Object" : MapType(p.Type)))]));
                    break;
            }
        }

        return members;
    }

    private static string MapType(TypeSyntax type)
    {
        switch (type)
        {
            case NullableTypeSyntax n:
                return MapType(n.ElementType);
            case ArrayTypeSyntax:
                return "Array";
            case PredefinedTypeSyntax p:
                return _typeNames.GetValueOrDefault(p.Keyword.Text, p.Keyword.Text);
            case GenericNameSyntax { Identifier.Text: "AsVector" } g:
                return "Vector.<" + MapType(g.TypeArgumentList.Arguments[0]) + ">";
            case GenericNameSyntax { Identifier.Text: "Action" or "Func" }:
                return "Function";
            case GenericNameSyntax g:
                return g.Identifier.Text;
            case QualifiedNameSyntax q:
                return MapType(q.Right);
            default:
                var text = type.ToString();
                return _typeNames.GetValueOrDefault(text, text);
        }
    }
}
