using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Replaces XNA 3.1 <c>device.RenderState.Property = value;</c> statements with the XNA 4.0 state objects
/// (<c>BlendState</c>, <c>DepthStencilState</c> and <c>RasterizerState</c>) for the three properties that map one to one.
/// Every other <c>RenderState</c> use gets a marker comment and stays as it is.
/// </summary>
public sealed class RenderStateRewriter : ISourceRewriter
{
    /// <summary>The rule ID this rewriter handles.</summary>
    public const string Id = "XNA024";

    /// <summary>The message of the marker added above a <c>RenderState</c> use that is not rewritten.</summary>
    public const string MarkerMessage =
        "Replace this RenderState use with BlendState, DepthStencilState, RasterizerState or SamplerState objects.";

    private const string _graphicsNamespace = "Microsoft.Xna.Framework.Graphics";
    private const string _renderState = "RenderState";
    private const int _snippetLength = 60;

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var first = Analyze(context.Text, context.OriginalSymbols);
        if (first is null || (first.Edits.Count == 0 && first.Sites.Count == 0))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var notes = new List<RewriteNote>();
        var text = context.Text;
        foreach (var edit in first.Edits.OrderByDescending(e => e.Start))
        {
            text = text.Remove(edit.Start, edit.End - edit.Start).Insert(edit.Start, edit.NewText);
        }

        notes.AddRange(first.Edits.Select(e => new RewriteNote(Id, e.Note, NeedsManualWork: false)));

        // Markers are added one at a time, because every insertion moves the positions of the sites below it.
        // The number and order of sites do not change when a comment line is added.
        for (var index = 0; ; index++)
        {
            var analysis = Analyze(text, context.OriginalSymbols);
            if (analysis is null || index >= analysis.Sites.Count)
            {
                break;
            }

            var site = analysis.Sites[index];
            var marked = MarkerInserter.InsertMarker(text, site.Position, Id, MarkerMessage);
            if (!string.Equals(marked, text, StringComparison.Ordinal))
            {
                text = marked;
                notes.Add(MarkedNote(site));
            }
            else if (!HasMarkerAbove(text, site.TargetLineStart))
            {
                notes.Add(MarkedNote(site, markerAdded: false));
            }
        }

        return string.Equals(text, context.Text, StringComparison.Ordinal) && notes.Count == 0
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(text, notes);
    }

    private static RewriteNote MarkedNote(Site site, bool markerAdded = true) => new(
        Id,
        markerAdded
            ? $"Left a RenderState use unchanged and added a marker: {site.Snippet}"
            : $"Left a RenderState use unchanged. A marker could not be added here (inactive or commented code): {site.Snippet}",
        NeedsManualWork: true);

    private static Analysis? Analyze(string text, IReadOnlyCollection<string> symbols)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols);
        var tree = CSharpSyntaxTree.ParseText(text, options);
        var root = tree.GetRoot();

        var imports = FindImport(root);
        if (imports == Import.None)
        {
            return null;
        }

        var qualifier = imports == Import.Using ? string.Empty : _graphicsNamespace + ".";
        var edits = new List<Edit>();
        var sites = new List<Site>();
        foreach (var node in root.DescendantNodes())
        {
            if (!IsRenderStateAccess(node))
            {
                continue;
            }

            var edit = node is MemberAccessExpressionSyntax access ? TryBuildEdit(root, text, access, qualifier) : null;
            if (edit is not null)
            {
                edits.Add(edit);
            }
            else
            {
                sites.Add(BuildSite(text, node));
            }
        }

        return new Analysis(edits, sites);
    }

    private static bool IsRenderStateAccess(SyntaxNode node) => node switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText == _renderState,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText == _renderState,
        _ => false,
    };

    private static Edit? TryBuildEdit(SyntaxNode root, string text, MemberAccessExpressionSyntax renderState, string qualifier)
    {
        if (renderState.Parent is not MemberAccessExpressionSyntax property
            || property.Expression != renderState
            || property.Parent is not AssignmentExpressionSyntax assignment
            || !assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
            || assignment.Left != property
            || assignment.Parent is not ExpressionStatementSyntax)
        {
            return null;
        }

        var name = property.Name.Identifier.ValueText;
        var replacement = Map(name, assignment.Right, qualifier);
        if (replacement is null)
        {
            return null;
        }

        var span = TextSpan.FromBounds(renderState.Name.SpanStart, assignment.Right.Span.End);
        if (root.DescendantTrivia(span).Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia)))
        {
            return null;
        }

        var before = Collapse(text[span.Start..span.End]);
        var after = replacement.Value.Text;
        return new Edit(span.Start, span.End, after, $"Replaced '{before}' with '{after}'.{replacement.Value.Hint}");
    }

    private static (string Text, string Hint)? Map(string property, ExpressionSyntax value, string qualifier)
    {
        switch (property)
        {
            case "AlphaBlendEnable" when TryGetBool(value) is { } on:
                return (
                    "BlendState = " + qualifier + "BlendState." + (on ? "AlphaBlend" : "Opaque"),
                    on ? " BlendState.AlphaBlend expects premultiplied alpha; use BlendState.NonPremultiplied for straight alpha." : string.Empty);
            case "DepthBufferEnable" when TryGetBool(value) is { } on:
                return ("DepthStencilState = " + qualifier + "DepthStencilState." + (on ? "Default" : "None"), string.Empty);
            case "CullMode" when TryGetCullMember(value) is { } member:
                return ("RasterizerState = " + qualifier + "RasterizerState." + member, string.Empty);
            default:
                return null;
        }
    }

    private static bool? TryGetBool(ExpressionSyntax value) => value.Kind() switch
    {
        SyntaxKind.TrueLiteralExpression => true,
        SyntaxKind.FalseLiteralExpression => false,
        _ => null,
    };

    private static string? TryGetCullMember(ExpressionSyntax value)
    {
        if (value is not MemberAccessExpressionSyntax access || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression))
        {
            return null;
        }

        var isCullMode = access.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "CullMode",
            MemberAccessExpressionSyntax inner => inner.Name.Identifier.ValueText == "CullMode",
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == "CullMode",
            _ => false,
        };
        if (!isCullMode)
        {
            return null;
        }

        return access.Name.Identifier.ValueText switch
        {
            "None" => "CullNone",
            "CullClockwiseFace" => "CullClockwise",
            "CullCounterClockwiseFace" => "CullCounterClockwise",
            _ => null,
        };
    }

    private static Import FindImport(SyntaxNode root)
    {
        var result = Import.None;
        foreach (var node in root.DescendantNodes())
        {
            if (node is UsingDirectiveSyntax directive)
            {
                if (directive.Name is not null && IsGraphicsNamespace(directive.Name))
                {
                    if (directive.Alias is null && directive.StaticKeyword.IsKind(SyntaxKind.None))
                    {
                        return Import.Using;
                    }

                    result = Import.Qualified;
                }
            }
            else if (node is QualifiedNameSyntax or MemberAccessExpressionSyntax && IsGraphicsNamespace(node))
            {
                result = Import.Qualified;
            }
        }

        return result;
    }

    private static bool IsGraphicsNamespace(SyntaxNode node)
    {
        var builder = new StringBuilder();
        foreach (var token in node.DescendantTokens())
        {
            builder.Append(token.Text);
        }

        var name = builder.ToString();
        return name == _graphicsNamespace || name == "global::" + _graphicsNamespace;
    }

    private static Site BuildSite(string text, SyntaxNode node)
    {
        var target = FindTarget(node);
        var targetStart = target?.SpanStart ?? node.SpanStart;
        var snippet = Collapse((target ?? node).ToString());
        if (snippet.Length > _snippetLength)
        {
            snippet = snippet[.._snippetLength] + "...";
        }

        return new Site(node.SpanStart, LineStart(text, targetStart), snippet);
    }

    private static SyntaxNode? FindTarget(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is BlockSyntax)
            {
                continue;
            }

            if (current is StatementSyntax or MemberDeclarationSyntax)
            {
                return current;
            }
        }

        return null;
    }

    private static string Collapse(string value)
    {
        var builder = new StringBuilder(value.Length);
        var space = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    private static int LineStart(string text, int position)
    {
        var i = Math.Min(position, text.Length);
        while (i > 0 && text[i - 1] != '\n' && text[i - 1] != '\r')
        {
            i--;
        }

        return i;
    }

    private static bool HasMarkerAbove(string text, int lineStart)
    {
        var marker = MarkerInserter.Prefix.TrimEnd() + " " + Id + ":";
        var end = lineStart;
        while (end > 0)
        {
            var contentEnd = end;
            if (text[contentEnd - 1] == '\n')
            {
                contentEnd--;
            }

            if (contentEnd > 0 && text[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            var previousStart = LineStart(text, contentEnd);
            var line = text[previousStart..contentEnd].Trim();
            if (!line.StartsWith(MarkerInserter.Prefix.TrimEnd(), StringComparison.Ordinal))
            {
                return false;
            }

            if (line.StartsWith(marker, StringComparison.Ordinal))
            {
                return true;
            }

            end = previousStart;
        }

        return false;
    }

    private enum Import
    {
        None,
        Qualified,
        Using,
    }

    private sealed record Edit(int Start, int End, string NewText, string Note);

    private sealed record Site(int Position, int TargetLineStart, string Snippet);

    private sealed record Analysis(IReadOnlyList<Edit> Edits, IReadOnlyList<Site> Sites);
}
