using XnaReborn.Analyzer.IO;
using XnaReborn.Core.Models;
using XnaReborn.Flash.Analyzer.As3;
using XnaReborn.Flash.Analyzer.Reading;
using XnaReborn.Flash.Analyzer.Resolving;

namespace XnaReborn.Flash.Analyzer.Rules;

/// <summary>Runs a <see cref="FlashRuleCatalog"/> over a project. It reads nothing from disk except to check that embed files exist.</summary>
public static class FlashRuleEngine
{
    private sealed record Hit(string File, SourceSpan? Span);

    /// <summary>Finds every match of every rule.</summary>
    /// <param name="project">The project, from <see cref="FlashProjectReader"/>.</param>
    /// <param name="catalog">The rules.</param>
    /// <param name="fileSystem">Used to check embed files.</param>
    /// <param name="cancellationToken">Cancels at file boundaries.</param>
    /// <returns>Findings sorted by severity (worst first), rule id, file, line and column.</returns>
    public static IReadOnlyList<Finding> Run(FlashProject project, FlashRuleCatalog catalog, IReadOnlyFileSystem fileSystem, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(catalog);
        var facts = Collect(project, fileSystem, cancellationToken);
        var projectName = project.ProjectFile ?? ".";
        var texts = project.Files.ToDictionary(f => f.Path, f => f.Text.Split('\n'), StringComparer.Ordinal);
        var findings = new List<Finding>();
        foreach (var rule in catalog.Rules)
        {
            foreach (var hit in Match(rule.Match, facts, project).DistinctBy(h => (h.File, h.Span)))
            {
                SourceLocation? location = null;
                string? snippet = null;
                if (hit.Span is { } s)
                {
                    location = new SourceLocation(hit.File, s.Line, s.Column);
                    if (texts.TryGetValue(hit.File, out var lines) && s.Line - 1 < lines.Length)
                    {
                        snippet = lines[s.Line - 1].Trim();
                        if (snippet.Length > 160)
                        {
                            snippet = snippet[..160];
                        }
                    }
                }

                findings.Add(new Finding(rule.Id, rule.Title, rule.Severity, rule.Classification, projectName, location, snippet));
            }
        }

        return findings
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.RuleId, StringComparer.Ordinal)
            .ThenBy(f => f.Location?.File, StringComparer.Ordinal)
            .ThenBy(f => f.Location?.Line ?? 0)
            .ThenBy(f => f.Location?.Column ?? 0)
            .ToList();
    }

    private sealed class Facts
    {
        public List<(string File, string Name, SourceSpan Span)> Imports { get; } = new();

        public Dictionary<string, List<Hit>> Constructs { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<Hit>> Names { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, List<Hit>> Qualified { get; } = new(StringComparer.Ordinal);

        public List<(string File, As3Metadata Meta)> Metadata { get; } = new();

        public IReadOnlyList<FlashEmbed> Embeds { get; set; } = Array.Empty<FlashEmbed>();

        public void Construct(string name, string file, SourceSpan span)
        {
            if (!Constructs.TryGetValue(name, out var list))
            {
                Constructs[name] = list = new List<Hit>();
            }

            list.Add(new Hit(file, span));
        }

        public void Add(Dictionary<string, List<Hit>> map, string key, string file, SourceSpan span)
        {
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = new List<Hit>();
            }

            list.Add(new Hit(file, span));
        }
    }

    private static Facts Collect(FlashProject project, IReadOnlyFileSystem fs, CancellationToken ct)
    {
        var facts = new Facts { Embeds = FlashEmbeds.Find(project, fs) };
        var resolver = new As3TypeResolver(project.Files);
        foreach (var file in project.Files.Where(f => !f.IsFlixelSource))
        {
            ct.ThrowIfCancellationRequested();
            foreach (var i in file.Unit.Imports)
            {
                facts.Imports.Add((file.Path, i.Name + (i.IsWildcard ? ".*" : string.Empty), i.Span));
            }

            var rootScope = new As3Scope(null, new Dictionary<string, string?>());
            Visit(file.Unit, file.Path, null, rootScope, resolver, facts);
        }

        return facts;
    }

    private static void Visit(As3Node node, string file, As3TypeDeclaration? cls, As3Scope scope, As3TypeResolver resolver, Facts f)
    {
        switch (node)
        {
            case As3TypeDeclaration t:
                if ((t.Modifiers & As3Modifier.Dynamic) != 0)
                {
                    f.Construct("dynamicClass", file, t.Span);
                }

                foreach (var m in t.Metadata)
                {
                    f.Metadata.Add((file, m));
                }

                foreach (var child in t.Members)
                {
                    Visit(child, file, t, new As3Scope(t, new Dictionary<string, string?>()), resolver, f);
                }

                return;
            case As3Method me:
                foreach (var m in me.Metadata)
                {
                    f.Metadata.Add((file, m));
                }

                {
                    var inner = As3TypeResolver.ScopeFor(cls, me.Parameters, me.Body);
                    foreach (var p in me.Parameters)
                    {
                        Visit(p, file, cls, inner, resolver, f);
                    }

                    if (me.ReturnType is { Name: "*" } rt)
                    {
                        f.Construct("untyped", file, rt.Span);
                    }

                    if (me.Body is not null)
                    {
                        Visit(me.Body, file, cls, inner, resolver, f);
                    }
                }

                return;
            case As3Field fld:
                foreach (var m in fld.Metadata)
                {
                    f.Metadata.Add((file, m));
                }

                Declared(fld.Span, fld.Type, fld.Initializer, fld.Name, file, f);
                break;
            case As3Declarator d:
                Declared(d.Span, d.Type, d.Initializer, d.Name, file, f);
                break;
            case As3Parameter p:
                if (!p.IsRest)
                {
                    TypeUse(p.Span, p.Type, file, f);
                }

                break;
            case As3UnsupportedStatement us:
                f.Construct(us.Reason, file, us.Span);
                break;
            case As3UnsupportedExpression ue:
                f.Construct(ue.Reason, file, ue.Span);
                break;
            case As3UnsupportedMember um:
                f.Construct(um.Reason, file, um.Span);
                break;
            case As3FunctionExpression fe:
                f.Construct("closure", file, fe.Span);
                {
                    var inner = As3TypeResolver.ScopeFor(cls, fe.Parameters, fe.Body);
                    inner = new As3Scope(cls, scope.Locals.Concat(inner.Locals).GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Last().Value));
                    foreach (var p in fe.Parameters)
                    {
                        Visit(p, file, cls, inner, resolver, f);
                    }

                    Visit(fe.Body, file, cls, inner, resolver, f);
                }

                return;
            case As3Literal { Kind: As3LiteralKind.Regex } rx:
                f.Construct("regex", file, rx.Span);
                break;
            case As3ObjectLiteral ol:
                f.Construct("objectBag", file, ol.Span);
                break;
            case As3New { Target: As3Identifier { Name: "Object" } } no:
                f.Construct("objectBag", file, no.Span);
                break;
            case As3Identifier id:
                if (id.Name == "arguments" && !scope.Locals.ContainsKey("arguments"))
                {
                    f.Construct("arguments", file, id.Span);
                }

                if (id.Name is "getDefinitionByName" or "ApplicationDomain")
                {
                    f.Construct("reflection", file, id.Span);
                }

                f.Add(f.Names, id.Name, file, id.Span);
                break;
            case As3MemberAccess ma:
                f.Add(f.Names, ma.Name, file, ma.Span);
                if (ma.Target is As3Identifier ti)
                {
                    f.Add(f.Qualified, ti.Name + "." + ma.Name, file, ma.Span);
                }

                break;
            case As3Binary { Operator: "/" } div:
                if (As3TypeResolver.IsInteger(resolver.TypeOf(div.Left, scope)) && As3TypeResolver.IsInteger(resolver.TypeOf(div.Right, scope)))
                {
                    f.Construct("intDivision", file, div.Span);
                }

                break;
            case As3Assignment { Operator: "/=" } da:
                if (As3TypeResolver.IsInteger(resolver.TypeOf(da.Target, scope)) && As3TypeResolver.IsInteger(resolver.TypeOf(da.Value, scope)))
                {
                    f.Construct("intDivision", file, da.Span);
                }

                break;
            case As3IndexAccess ix:
                if (resolver.TypeOf(ix.Target, scope) == "Object")
                {
                    f.Construct("objectBag", file, ix.Span);
                }

                break;
        }

        foreach (var child in As3Walker.Children(node))
        {
            Visit(child, file, cls, scope, resolver, f);
        }
    }

    private static void Declared(SourceSpan span, As3TypeRef? type, As3Expression? init, string name, string file, Facts f)
    {
        _ = name;
        TypeUse(span, type, file, f);
        if (type?.Name == "Number" && init is null)
        {
            f.Construct("numberDefault", file, span);
        }
    }

    private static void TypeUse(SourceSpan span, As3TypeRef? type, string file, Facts f)
    {
        if (type is null || type.Name == "*")
        {
            f.Construct("untyped", file, span);
        }
        else if (type.Name == "Object")
        {
            f.Construct("objectBag", file, span);
        }
        else if (type.Name == "Function")
        {
            f.Construct("closure", file, span);
        }
    }

    private static IEnumerable<Hit> Match(FlashRuleMatch m, Facts facts, FlashProject project)
    {
        switch (m.Type)
        {
            case "as3Import":
                var patterns = (m.Pattern ?? string.Empty).Split('|');
                return facts.Imports.Where(i => patterns.Any(p => ImportMatches(p, i.Name))).Select(i => new Hit(i.File, i.Span));
            case "as3Syntax":
                return facts.Constructs.TryGetValue(m.Construct ?? string.Empty, out var c) ? c : [];
            case "as3Metadata":
                return MetadataHits(m, facts);
            case "as3Api":
                {
                    var hits = new List<Hit>();
                    foreach (var n in m.Names ?? [])
                    {
                        if (facts.Names.TryGetValue(n, out var l))
                        {
                            hits.AddRange(l);
                        }
                    }

                    if (m.Qualified is not null && facts.Qualified.TryGetValue(m.Qualified, out var q))
                    {
                        hits.AddRange(q);
                    }

                    return hits;
                }

            case "project":
                return ProjectHits(m.Probe, project);
            default:
                throw new InvalidOperationException($"Unknown Flash rule match type '{m.Type}'.");
        }
    }

    private static bool ImportMatches(string pattern, string import)
    {
        if (pattern.EndsWith(".*", StringComparison.Ordinal))
        {
            var prefix = pattern[..^2];
            return import == prefix + ".*" || import.StartsWith(prefix + ".", StringComparison.Ordinal);
        }

        return import == pattern;
    }

    private static IEnumerable<Hit> MetadataHits(FlashRuleMatch m, Facts facts)
    {
        if (m.Tag == "Embed")
        {
            foreach (var e in facts.Embeds)
            {
                var hit = m.EmbedKind switch
                {
                    "image" => e.Kind == FlashEmbedKind.Image && e.Exists,
                    "audio" => e.Kind == FlashEmbedKind.Audio && e.Exists,
                    "data" => e.Kind == FlashEmbedKind.Data && e.Exists,
                    "font" => e.Kind == FlashEmbedKind.Font,
                    "symbol" => e.Kind == FlashEmbedKind.Symbol,
                    "missing" => !e.Exists,
                    _ => false,
                };
                if (hit)
                {
                    yield return new Hit(e.File, e.Span);
                }
            }

            yield break;
        }

        foreach (var (file, meta) in facts.Metadata.Where(x => x.Meta.Name == m.Tag))
        {
            yield return new Hit(file, meta.Span);
        }
    }

    private static IEnumerable<Hit> ProjectHits(string? probe, FlashProject project)
    {
        var d = project.Detection;
        switch (probe)
        {
            case "flixelNot2":
                if (project.FlixelVersion is { } v && !v.StartsWith("2", StringComparison.Ordinal))
                {
                    yield return new Hit(project.ProjectFile ?? ".", null);
                }

                break;
            case "swfWithoutSource":
                if (d.HasSwf && d.SourceFiles.Count == 0)
                {
                    yield return new Hit(".", null);
                }

                break;
            case "flashPro":
                if (d.HasFla)
                {
                    yield return new Hit(".", null);
                }

                break;
            case "swc":
                foreach (var s in d.SwcFiles)
                {
                    yield return new Hit(s, null);
                }

                break;
            case "entryCount":
                if (project.UsesFlixel && project.EntryClasses.Count != 1)
                {
                    yield return new Hit(project.EntryClasses.Count > 1 ? project.EntryClasses[1].File : ".", null);
                }

                break;
            default:
                throw new InvalidOperationException($"Unknown Flash project probe '{probe}'.");
        }
    }
}
