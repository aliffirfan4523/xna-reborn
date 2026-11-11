using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Points game code at the generated XACT stand-in (<c>XnaReborn.Compat.Xact</c>). For each of <c>AudioEngine</c>, <c>AudioCategory</c>,
/// <c>Cue</c>, <c>SoundBank</c> and <c>WaveBank</c> that a file uses, it adds a using alias next to
/// <c>using Microsoft.Xna.Framework.Audio;</c> (in the same compilation unit or namespace), re-points an existing alias, and replaces
/// fully qualified uses. An alias wins over the type that the using-namespace directive imports, so the rest of the file stays as it is.
/// A file that declares a type with one of these names is left alone with a note. Edits are by position, so the rest of the file is byte for byte identical.
/// </summary>
public sealed partial class XactAliasRewriter : ISourceRewriter
{
    /// <summary>The rule ID this rewriter handles (code uses the XACT runtime).</summary>
    public const string Id = "XNA015";

    /// <summary>The namespace of the types that are replaced.</summary>
    public const string XnaNamespace = "Microsoft.Xna.Framework.Audio";

    /// <summary>The namespace of the stand-in.</summary>
    public const string ShimNamespace = "XnaReborn.Compat.Xact";

    /// <summary>The five type names that the stand-in replaces, sorted.</summary>
    public static readonly IReadOnlyList<string> TypeNames = ["AudioCategory", "AudioEngine", "Cue", "SoundBank", "WaveBank"];

    /// <inheritdoc />
    public string RuleId => Id;

    [GeneratedRegex(@"^\s*using\s+(?<alias>\w+)\s*=\s*" + @"XnaReborn\.Compat\.Xact\.\k<alias>\s*;", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex AliasLine();

    /// <summary>Finds the stand-in types a rewritten file aliases.</summary>
    /// <param name="text">The text of a file after rewriting.</param>
    /// <returns>The alias names, sorted and distinct. Empty when none.</returns>
    public static IReadOnlyList<string> AliasedTypes(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return AliasLine().Matches(text).Select(m => m.Groups["alias"].Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!TypeNames.Any(n => context.Text.Contains(n, StringComparison.Ordinal)))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var root = CSharpSyntaxTree.ParseText(context.Text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols)).GetRoot();
        var declared = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Select(t => t.Identifier.ValueText)
            .Concat(root.DescendantNodes().OfType<DelegateDeclarationSyntax>().Select(d => d.Identifier.ValueText))
            .Where(n => TypeNames.Contains(n, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (declared.Count > 0)
        {
            return new SourceRewriteResult(
                context.Text,
                [new RewriteNote(Id, $"The file declares its own {string.Join(", ", declared)}, so the XACT types were not pointed at the stand-in. Check by hand which type the code means.", NeedsManualWork: true)]);
        }

        var edits = new List<(int Start, int Length, string Text)>();

        // Fully qualified uses: Microsoft.Xna.Framework.Audio.AudioEngine becomes XnaReborn.Compat.Xact.AudioEngine.
        var qualifiedLefts = new List<ExpressionSyntax>();
        foreach (var node in root.DescendantNodes().Where(n => !n.Ancestors().OfType<UsingDirectiveSyntax>().Any()))
        {
            switch (node)
            {
                case QualifiedNameSyntax { Right: IdentifierNameSyntax right } qualified when TypeNames.Contains(right.Identifier.ValueText, StringComparer.Ordinal) && Compact(qualified.Left) == XnaNamespace:
                    qualifiedLefts.Add(qualified.Left);
                    break;
                case MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access when TypeNames.Contains(name.Identifier.ValueText, StringComparer.Ordinal) && Compact(access.Expression) == XnaNamespace:
                    qualifiedLefts.Add(access.Expression);
                    break;
            }
        }

        foreach (var left in qualifiedLefts)
        {
            edits.Add((left.SpanStart, left.Span.Length, ShimNamespace));
        }

        // Aliases: one block next to each plain `using Microsoft.Xna.Framework.Audio;`.
        var aliasCount = 0;
        var used = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (directive.Alias is not null || !directive.StaticKeyword.IsKind(SyntaxKind.None) || directive.Name is null || Compact(directive.Name) != XnaNamespace)
            {
                continue;
            }

            var scope = directive.Parent!;
            var existing = scope switch
            {
                CompilationUnitSyntax unit => unit.Usings,
                BaseNamespaceDeclarationSyntax ns => ns.Usings,
                _ => default,
            };
            var inScope = TypesUsedIn(scope);
            foreach (var type in inScope)
            {
                used.Add(type);
            }

            var missing = inScope.Where(type => !existing.Any(u => u.Alias?.Name.Identifier.ValueText == type)).ToList();
            if (missing.Count == 0)
            {
                continue;
            }

            var indent = new string(directive.GetLeadingTrivia().ToFullString().Reverse().TakeWhile(c => c is ' ' or '\t').Reverse().ToArray());
            var newline = context.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = missing.Select(type => $"{indent}using {type} = {ShimNamespace}.{type};");

            // The block goes after the whole line of the directive, so a comment at the end of that line stays on it.
            var endsLine = directive.GetTrailingTrivia().LastOrDefault().IsKind(SyntaxKind.EndOfLineTrivia);
            edits.Add((directive.FullSpan.End, 0, endsLine ? string.Concat(lines.Select(l => l + newline)) : newline + string.Join(newline, lines)));
            aliasCount += missing.Count;
        }

        // Existing aliases that point at the MonoGame type are re-pointed.
        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (directive.Alias?.Name.Identifier.ValueText is { } alias
                && TypeNames.Contains(alias, StringComparer.Ordinal)
                && directive.Name is not null
                && Compact(directive.Name) == XnaNamespace + "." + alias)
            {
                edits.Add((directive.Name.SpanStart, directive.Name.Span.Length, ShimNamespace + "." + alias));
                used.Add(alias);
                aliasCount++;
            }
        }

        if (edits.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var text = context.Text;
        foreach (var edit in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.Length))
        {
            text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        }

        var names = used.Count == 0 ? "qualified names" : string.Join(", ", used);
        return new SourceRewriteResult(
            text,
            [new RewriteNote(Id, $"Pointed the XACT types ({names}) at the XnaReborn.Compat.Xact stand-in: {aliasCount} using alias(es) and {qualifiedLefts.Count} qualified name(s).", NeedsManualWork: false)]);
    }

    private static string Compact(SyntaxNode node) => string.Concat(node.ToString().Where(c => !char.IsWhiteSpace(c)));

    /// <summary>The XACT type names that a scope mentions as a plain name (not as the right side of a qualified name).</summary>
    private static List<string> TypesUsedIn(SyntaxNode scope) =>
        scope.DescendantNodes().OfType<IdentifierNameSyntax>()
            .Where(n => TypeNames.Contains(n.Identifier.ValueText, StringComparer.Ordinal))
            .Where(n => n.Parent is not QualifiedNameSyntax { Right: var r } || r != n)
            .Where(n => n.Parent is not MemberAccessExpressionSyntax { Name: var m } || m != n)
            .Where(n => n.Ancestors().OfType<UsingDirectiveSyntax>().FirstOrDefault() is null)
            .Select(n => n.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
}
