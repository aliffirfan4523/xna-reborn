using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// The Visual Basic version of <see cref="RenderStateRewriter"/> (rule XNA024). It replaces the direct statements
/// <c>device.RenderState.AlphaBlendEnable = True</c>, <c>DepthBufferEnable</c> and <c>CullMode = CullMode.X</c> with the XNA 4.0 state objects.
/// Every other <c>RenderState</c> use, including <c>With device.RenderState</c> blocks, keeps its code and gets a marker from
/// <see cref="VisualBasicMarkerRewriter"/>.
/// </summary>
public sealed class VisualBasicRenderStateRewriter : ISourceRewriter
{
    private readonly VisualBasicMarkerRewriter _markers = VisualBasicMarkerRewriter.CreateAll().Single(m => m.RuleId == RenderStateRewriter.Id);

    /// <inheritdoc />
    public string RuleId => RenderStateRewriter.Id;

    /// <inheritdoc />
    public SourceLanguage Language => SourceLanguage.VisualBasic;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = context.Text;
        var symbols = context.OriginalSymbols.Select(s => new KeyValuePair<string, object>(s, true)).ToList();
        var root = VisualBasicSyntaxTree.ParseText(text, new VisualBasicParseOptions(preprocessorSymbols: symbols)).GetRoot();
        var notes = new List<RewriteNote>();
        if (VisualBasicGraphicsImport.Imports(root, context.ImplicitImports))
        {
            var edits = root.DescendantNodes().OfType<AssignmentStatementSyntax>()
                .Select(a => TryBuildEdit(root, text, a))
                .Where(e => e is not null)
                .Select(e => e!)
                .OrderByDescending(e => e.Start)
                .ToList();
            foreach (var edit in edits)
            {
                text = text.Remove(edit.Start, edit.End - edit.Start).Insert(edit.Start, edit.NewText);
            }

            notes.AddRange(edits.OrderBy(e => e.Start).Select(e => new RewriteNote(RuleId, e.Note, NeedsManualWork: false)));
        }

        // What is left (With blocks, other properties, other values) gets the marker.
        var marked = _markers.Rewrite(context with { Text = text });
        notes.AddRange(marked.Notes);
        return string.Equals(marked.Text, context.Text, StringComparison.Ordinal) ? SourceRewriteResult.Unchanged(context) : new SourceRewriteResult(marked.Text, notes);
    }

    private static Edit? TryBuildEdit(SyntaxNode root, string text, AssignmentStatementSyntax assignment)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentStatement)
            || assignment.Left is not MemberAccessExpressionSyntax property
            || !property.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || property.Expression is not MemberAccessExpressionSyntax renderState
            || !renderState.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || renderState.Expression is null
            || !VisualBasicGraphicsImport.Same(renderState.Name.Identifier.ValueText, "RenderState"))
        {
            return null;
        }

        var replacement = Map(property.Name.Identifier.ValueText, assignment.Right);
        if (replacement is null)
        {
            return null;
        }

        var span = Microsoft.CodeAnalysis.Text.TextSpan.FromBounds(renderState.Name.SpanStart, assignment.Right.Span.End);
        if (root.DescendantTrivia(span).Any(t => !t.IsKind(SyntaxKind.WhitespaceTrivia)))
        {
            return null;
        }

        var before = string.Join(' ', text[span.Start..span.End].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return new Edit(span.Start, span.End, replacement.Value.Text, $"Replaced '{before}' with '{replacement.Value.Text}'.{replacement.Value.Hint}");
    }

    private static (string Text, string Hint)? Map(string property, ExpressionSyntax value)
    {
        if (VisualBasicGraphicsImport.Same(property, "AlphaBlendEnable") && TryGetBool(value) is { } blend)
        {
            return (
                "BlendState = BlendState." + (blend ? "AlphaBlend" : "Opaque"),
                blend ? " BlendState.AlphaBlend expects premultiplied alpha; use BlendState.NonPremultiplied for straight alpha." : string.Empty);
        }

        if (VisualBasicGraphicsImport.Same(property, "DepthBufferEnable") && TryGetBool(value) is { } depth)
        {
            return ("DepthStencilState = DepthStencilState." + (depth ? "Default" : "None"), string.Empty);
        }

        if (VisualBasicGraphicsImport.Same(property, "CullMode") && TryGetCullMember(value) is { } cull)
        {
            return ("RasterizerState = RasterizerState." + cull, string.Empty);
        }

        return null;
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

        var type = access.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
            _ => null,
        };
        if (!VisualBasicGraphicsImport.Same(type, "CullMode"))
        {
            return null;
        }

        var member = access.Name.Identifier.ValueText;
        if (VisualBasicGraphicsImport.Same(member, "None"))
        {
            return "CullNone";
        }

        if (VisualBasicGraphicsImport.Same(member, "CullClockwiseFace"))
        {
            return "CullClockwise";
        }

        return VisualBasicGraphicsImport.Same(member, "CullCounterClockwiseFace") ? "CullCounterClockwise" : null;
    }

    private sealed record Edit(int Start, int End, string NewText, string Note);
}
