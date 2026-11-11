using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Core.Toolkit;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Renames SharpDX math calls to their MonoGame names. SDX030 renames the right-handed <c>Matrix</c> factories
/// (<c>Matrix.RotationY(a)</c> becomes <c>Matrix.CreateRotationY(a)</c>). SDX031 turns <c>MathUtil.X</c> into <c>MathHelper.Y</c>.
/// Only calls on the type name itself are renamed, so an instance named <c>matrix</c>, a property named <c>Translation</c>,
/// and the left-handed factories (SDX032) are left alone.
/// </summary>
public sealed class ToolkitMathRewriter : ISourceRewriter
{
    /// <summary>The rule ID of the Matrix factory rewriter.</summary>
    public const string MatrixRuleId = "SDX030";

    /// <summary>The rule ID of the MathUtil rewriter.</summary>
    public const string MathUtilRuleId = "SDX031";

    private readonly string _typeName;
    private readonly bool _invocationsOnly;
    private readonly Dictionary<string, string> _renames;

    private ToolkitMathRewriter(string ruleId, string typeName, bool invocationsOnly, IEnumerable<ToolkitRename> renames)
    {
        RuleId = ruleId;
        _typeName = typeName;
        _invocationsOnly = invocationsOnly;
        _renames = renames.ToDictionary(r => r.From, r => r.To, StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <summary>Creates the SDX030 rewriter for the <c>Matrix</c> factories.</summary>
    /// <param name="map">The rename map. Defaults to <see cref="ToolkitMap.Default"/>.</param>
    public static ToolkitMathRewriter ForMatrix(ToolkitMap? map = null) =>
        new(MatrixRuleId, "Matrix", invocationsOnly: true, (map ?? ToolkitMap.Default).MatrixFactories);

    /// <summary>Creates the SDX031 rewriter for the <c>MathUtil</c> members.</summary>
    /// <param name="map">The rename map. Defaults to <see cref="ToolkitMap.Default"/>.</param>
    public static ToolkitMathRewriter ForMathUtil(ToolkitMap? map = null) =>
        new(MathUtilRuleId, "MathUtil", invocationsOnly: false, (map ?? ToolkitMap.Default).MathUtilMembers);

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var root = ToolkitSyntax.Parse(context.Text, context.OriginalSymbols);
        if (!ToolkitSyntax.HasFrameworkContext(root))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var edits = new List<TextEdit>();
        var notes = new List<RewriteNote>();
        var mathHelperName = _typeName == "MathUtil" ? "MathHelper" : null;
        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (!access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                || access.Name is not IdentifierNameSyntax name
                || !_renames.TryGetValue(name.Identifier.ValueText, out var target)
                || !ToolkitSyntax.IsTypeName(access.Expression, _typeName))
            {
                continue;
            }

            if (_invocationsOnly && !(access.Parent is InvocationExpressionSyntax call && call.Expression == access))
            {
                continue;
            }

            edits.Add(new TextEdit(name.SpanStart, name.Span.End, target));
            if (mathHelperName is not null)
            {
                edits.Add(ReceiverEdit(root, access.Expression, mathHelperName));
            }

            notes.Add(new RewriteNote(RuleId, $"Renamed {_typeName}.{name.Identifier.ValueText} to {(mathHelperName ?? _typeName)}.{target}.", NeedsManualWork: false));
        }

        if (edits.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        return new SourceRewriteResult(ToolkitSyntax.Apply(context.Text, edits), notes);
    }

    // MathUtil becomes MathHelper. A qualified receiver (SharpDX.MathUtil) becomes the MonoGame name in full, because the namespace in front is wrong.
    private static TextEdit ReceiverEdit(SyntaxNode root, ExpressionSyntax receiver, string mathHelper)
    {
        if (receiver is IdentifierNameSyntax)
        {
            return new TextEdit(receiver.SpanStart, receiver.Span.End, mathHelper);
        }

        return new TextEdit(receiver.SpanStart, receiver.Span.End, ToolkitSyntax.Imports(root, ToolkitSyntax.MonoGameNamespace) ? mathHelper : ToolkitSyntax.MonoGameNamespace + "." + mathHelper);
    }
}
