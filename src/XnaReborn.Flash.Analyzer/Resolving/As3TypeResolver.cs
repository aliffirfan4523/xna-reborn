using System.Globalization;
using XnaReborn.Flash.Analyzer.As3;
using XnaReborn.Flash.Analyzer.Reading;

namespace XnaReborn.Flash.Analyzer.Resolving;

/// <summary>Names visible while resolving types inside one method: parameters and local variables.</summary>
/// <param name="Class">The enclosing class, or null.</param>
/// <param name="Locals">Declared type names of parameters and locals. A null value means "untyped".</param>
public sealed record As3Scope(As3TypeDeclaration? Class, IReadOnlyDictionary<string, string?> Locals);

/// <summary>
/// A light type resolver for AS3 expressions. It answers with a type name such as "int", "Number", "FlxSprite" or
/// "Vector.&lt;int&gt;", or null when it cannot tell. It is not a full type checker: callers must treat null conservatively.
/// </summary>
public sealed class As3TypeResolver
{
    // shortcut: a hand-written map of the Flixel members the converter and rules need. Task 10.1 replaces it with a map generated from the runtime.
    private static readonly Dictionary<string, string> _flixelBase = new(StringComparer.Ordinal)
    {
        ["FlxSprite"] = "FlxObject",
        ["FlxObject"] = "FlxBasic",
        ["FlxGroup"] = "FlxBasic",
        ["FlxState"] = "FlxGroup",
        ["FlxText"] = "FlxSprite",
        ["FlxTileblock"] = "FlxSprite",
        ["FlxButton"] = "FlxSprite",
        ["FlxEmitter"] = "FlxGroup",
        ["FlxParticle"] = "FlxSprite",
        ["FlxTilemap"] = "FlxObject",
    };

    private static readonly Dictionary<string, string> _flixelMembers = new(StringComparer.Ordinal)
    {
        ["FlxG.width"] = "int",
        ["FlxG.height"] = "int",
        ["FlxG.elapsed"] = "Number",
        ["FlxG.score"] = "int",
        ["FlxG.level"] = "int",
        ["FlxG.keys"] = "Keyboard",
        ["FlxG.mouse"] = "Mouse",
        ["FlxG.bgColor"] = "uint",
        ["FlxG.volume"] = "Number",
        ["FlxG.collide"] = "Boolean",
        ["FlxG.overlap"] = "Boolean",
        ["FlxG.random"] = "Number",
        ["FlxG.play"] = "FlxSound",
        ["FlxG.worldBounds"] = "FlxRect",
        ["FlxPoint.x"] = "Number",
        ["FlxPoint.y"] = "Number",
        ["FlxBasic.exists"] = "Boolean",
        ["FlxBasic.alive"] = "Boolean",
        ["FlxBasic.active"] = "Boolean",
        ["FlxBasic.visible"] = "Boolean",
        ["FlxBasic.kill"] = "void",
        ["FlxBasic.revive"] = "void",
        ["FlxBasic.update"] = "void",
        ["FlxBasic.draw"] = "void",
        ["FlxObject.x"] = "Number",
        ["FlxObject.y"] = "Number",
        ["FlxObject.width"] = "Number",
        ["FlxObject.height"] = "Number",
        ["FlxObject.angle"] = "Number",
        ["FlxObject.health"] = "Number",
        ["FlxObject.facing"] = "uint",
        ["FlxObject.velocity"] = "FlxPoint",
        ["FlxObject.acceleration"] = "FlxPoint",
        ["FlxObject.drag"] = "FlxPoint",
        ["FlxObject.maxVelocity"] = "FlxPoint",
        ["FlxObject.offset"] = "FlxPoint",
        ["FlxObject.scrollFactor"] = "FlxPoint",
        ["FlxObject.touching"] = "uint",
        ["FlxObject.isTouching"] = "Boolean",
        ["FlxObject.hurt"] = "void",
        ["FlxGroup.members"] = "Array",
        ["FlxGroup.length"] = "uint",
        ["FlxGroup.add"] = "FlxBasic",
        ["FlxGroup.recycle"] = "FlxBasic",
        ["FlxGroup.countLiving"] = "int",
        ["FlxText.text"] = "String",
        ["FlxText.color"] = "uint",
        ["FlxText.alignment"] = "String",
        ["FlxText.size"] = "Number",
        ["FlxSprite.color"] = "uint",
        ["FlxSprite.alpha"] = "Number",
        ["FlxSprite.scale"] = "FlxPoint",
    };

    private static readonly HashSet<string> _numeric = new(StringComparer.Ordinal) { "int", "uint", "Number" };

    private readonly Dictionary<string, (As3TypeDeclaration Type, string? Package)> _classes = new(StringComparer.Ordinal);

    /// <summary>Creates a resolver over the classes of a project. Bundled Flixel sources are ignored.</summary>
    /// <param name="files">Parsed source files.</param>
    public As3TypeResolver(IEnumerable<FlashSourceFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        foreach (var f in files.Where(f => !f.IsFlixelSource))
        {
            foreach (var t in f.Unit.Members.OfType<As3TypeDeclaration>())
            {
                _classes.TryAdd(t.Name, (t, f.Unit.PackageName));
            }
        }
    }

    /// <summary>Builds the scope of a method: its parameters and every local declared in its body.</summary>
    /// <param name="cls">The enclosing class, or null for package-level code.</param>
    /// <param name="parameters">Method or function parameters.</param>
    /// <param name="body">The body, or null.</param>
    public static As3Scope ScopeFor(As3TypeDeclaration? cls, IEnumerable<As3Parameter> parameters, As3Block? body)
    {
        var locals = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var p in parameters)
        {
            locals[p.Name] = p.IsRest ? "Array" : TypeName(p.Type);
        }

        if (body is not null)
        {
            foreach (var node in As3Walker.DescendantsAndSelf(body))
            {
                switch (node)
                {
                    case As3Declarator d:
                        locals[d.Name] = TypeName(d.Type);
                        break;
                    case As3CatchClause c:
                        locals[c.Name] = TypeName(c.Type);
                        break;
                    case As3FunctionExpression fe:
                        foreach (var p in fe.Parameters)
                        {
                            locals[p.Name] = p.IsRest ? "Array" : TypeName(p.Type);
                        }

                        break;
                }
            }
        }

        return new As3Scope(cls, locals);
    }

    /// <summary>The text form of a type annotation, or null when there is none or it is "*".</summary>
    /// <param name="t">The annotation.</param>
    public static string? TypeName(As3TypeRef? t)
    {
        if (t is null || t.Name == "*")
        {
            return null;
        }

        var simple = t.Name.Contains('.') && t.TypeArguments.Count == 0 ? t.Name[(t.Name.LastIndexOf('.') + 1)..] : t.Name;
        return t.TypeArguments.Count == 0 ? simple : $"{simple}.<{string.Join(",", t.TypeArguments.Select(a => TypeName(a) ?? "*"))}>";
    }

    /// <summary>True when the type is int, uint or Number.</summary>
    /// <param name="type">A type name.</param>
    public static bool IsNumeric(string? type) => type is not null && _numeric.Contains(type);

    /// <summary>True when the type is int or uint.</summary>
    /// <param name="type">A type name.</param>
    public static bool IsInteger(string? type) => type is "int" or "uint";

    /// <summary>Resolves the type of an expression.</summary>
    /// <param name="e">The expression.</param>
    /// <param name="scope">Names visible at the expression.</param>
    /// <returns>A type name, "void" for no value, or null when unknown.</returns>
    public string? TypeOf(As3Expression e, As3Scope scope)
    {
        switch (e)
        {
            case As3Literal l:
                return l.Kind switch
                {
                    As3LiteralKind.Number => NumberLiteralType(l.Text),
                    As3LiteralKind.String => "String",
                    As3LiteralKind.Boolean => "Boolean",
                    As3LiteralKind.Regex => "RegExp",
                    _ => null,
                };
            case As3Parenthesized p:
                return TypeOf(p.Inner, scope);
            case As3Identifier id:
                return IdentifierType(id.Name, scope);
            case As3MemberAccess m:
                return MemberType(m, scope);
            case As3IndexAccess ix:
                {
                    var t = TypeOf(ix.Target, scope);
                    return t is not null && t.StartsWith("Vector.<", StringComparison.Ordinal) ? t[8..^1] : null;
                }

            case As3Call c:
                return CallType(c, scope);
            case As3New n:
                return n.Target switch
                {
                    As3Identifier i => i.Name,
                    As3GenericExpression g when g.Target is As3Identifier gi => $"{gi.Name}.<{string.Join(",", g.TypeArguments.Select(a => TypeName(a) ?? "*"))}>",
                    As3MemberAccess ma => ma.Name,
                    _ => null,
                };
            case As3Unary u:
                return UnaryType(u, scope);
            case As3Binary b:
                return BinaryType(b, scope);
            case As3Assignment a:
                return TypeOf(a.Target, scope);
            case As3Conditional c:
                {
                    var x = TypeOf(c.WhenTrue, scope);
                    var y = TypeOf(c.WhenFalse, scope);
                    return x == y ? x : IsNumeric(x) && IsNumeric(y) ? "Number" : null;
                }

            case As3ArrayLiteral:
                return "Array";
            case As3ObjectLiteral:
                return "Object";
            case As3FunctionExpression:
                return "Function";
            default:
                return null;
        }
    }

    private static string NumberLiteralType(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) && h > int.MaxValue ? "uint" : "int";
        }

        if (text.Contains('.') || text.Contains('e') || text.Contains('E'))
        {
            return "Number";
        }

        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var v)
            ? (v <= int.MaxValue ? "int" : v <= uint.MaxValue ? "uint" : "Number")
            : "Number";
    }

    private string? IdentifierType(string name, As3Scope scope)
    {
        if (scope.Locals.TryGetValue(name, out var local))
        {
            return local;
        }

        if (name == "this")
        {
            return scope.Class?.Name;
        }

        if (scope.Class is not null && LookupMember(scope.Class.Name, name, out var memberType))
        {
            return memberType;
        }

        return name is "NaN" ? "Number" : name is "undefined" ? null : null;
    }

    /// <summary>True when <paramref name="name"/> names a class of the project or a known Flixel class.</summary>
    /// <param name="name">A simple type name.</param>
    public bool IsClassName(string name) => _classes.ContainsKey(name) || _flixelBase.ContainsKey(name) || name.StartsWith("Flx", StringComparison.Ordinal);

    private string? MemberType(As3MemberAccess m, As3Scope scope)
    {
        if (m.Target is As3Identifier { Name: var tn } && !scope.Locals.ContainsKey(tn) && IsClassName(tn)
            && !(scope.Class is not null && LookupMember(scope.Class.Name, tn, out _)))
        {
            return LookupMember(tn, m.Name, out var st) ? st : null;
        }

        var targetType = TypeOf(m.Target, scope);
        if (targetType is null)
        {
            return null;
        }

        if (targetType.StartsWith("Vector.<", StringComparison.Ordinal) || targetType == "Array")
        {
            return m.Name == "length" ? "uint" : null;
        }

        if (targetType == "String")
        {
            return m.Name == "length" ? "int" : null;
        }

        return LookupMember(targetType, m.Name, out var t) ? t : null;
    }

    private string? CallType(As3Call c, As3Scope scope)
    {
        switch (c.Target)
        {
            case As3Identifier { Name: "int" or "uint" or "Number" or "String" or "Boolean" or "Array" or "Object" } id when !scope.Locals.ContainsKey(id.Name):
                return id.Name;
            case As3Identifier id:
                return LookupMember(scope.Class?.Name ?? string.Empty, id.Name, out var t) ? t : null;
            case As3MemberAccess m:
                return MemberType(m, scope);
            case As3GenericExpression { Target: As3Identifier { Name: "Vector" } } g:
                return $"Vector.<{string.Join(",", g.TypeArguments.Select(a => TypeName(a) ?? "*"))}>";
            default:
                return null;
        }
    }

    private string? UnaryType(As3Unary u, As3Scope scope)
    {
        if (u.Operator == "!")
        {
            return "Boolean";
        }

        if (u.Operator is "typeof")
        {
            return "String";
        }

        if (u.Operator is "delete")
        {
            return "Boolean";
        }

        var t = TypeOf(u.Operand, scope);
        if (u.Operator == "~")
        {
            return "int";
        }

        return IsNumeric(t) ? (u.Operator == "-" && t == "uint" ? "Number" : t) : null;
    }

    private string? BinaryType(As3Binary b, As3Scope scope)
    {
        switch (b.Operator)
        {
            case "==" or "!=" or "===" or "!==" or "<" or ">" or "<=" or ">=" or "is" or "in" or "instanceof":
                return "Boolean";
            case "as":
                return b.Right is As3Identifier ri ? ri.Name : null;
            case ",":
                return TypeOf(b.Right, scope);
            case "&" or "|" or "^" or "<<" or ">>":
                return "int";
            case ">>>":
                return "uint";
            case "&&" or "||":
                {
                    var x = TypeOf(b.Left, scope);
                    return x == TypeOf(b.Right, scope) ? x : null;
                }
        }

        var l = TypeOf(b.Left, scope);
        var r = TypeOf(b.Right, scope);
        if (b.Operator == "+" && (l == "String" || r == "String"))
        {
            return "String";
        }

        if (!IsNumeric(l) || !IsNumeric(r))
        {
            return b.Operator == "+" ? null : (l is null || r is null ? null : "Number");
        }

        if (b.Operator == "/")
        {
            return "Number";
        }

        return IsInteger(l) && IsInteger(r) ? (l == "int" || r == "int" ? "int" : "uint") : "Number";
    }

    private bool LookupMember(string typeName, string member, out string? type)
    {
        var seen = 0;
        var current = typeName;
        while (!string.IsNullOrEmpty(current) && seen++ < 32)
        {
            if (_classes.TryGetValue(current, out var entry))
            {
                foreach (var m in entry.Type.Members)
                {
                    switch (m)
                    {
                        case As3Field f when f.Name == member:
                            type = TypeName(f.Type);
                            return true;
                        case As3Method { Kind: As3MethodKind.Getter or As3MethodKind.Normal } me when me.Name == member:
                            type = me.Kind == As3MethodKind.Getter ? TypeName(me.ReturnType) : (TypeName(me.ReturnType) ?? "void");
                            return true;
                    }
                }

                current = entry.Type.BaseType is null ? string.Empty : TypeName(entry.Type.BaseType) ?? string.Empty;
                continue;
            }

            if (_flixelMembers.TryGetValue(current + "." + member, out var known))
            {
                type = known;
                return true;
            }

            current = _flixelBase.TryGetValue(current, out var b) ? b : string.Empty;
        }

        type = null;
        return false;
    }
}
