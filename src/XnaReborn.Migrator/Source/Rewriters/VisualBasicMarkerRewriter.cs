using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Adds a marker comment above Visual Basic statements that use an XNA 3.x API without a safe automatic fix: <c>RenderState</c> (XNA024),
/// <c>EffectPass.Begin</c> and <c>End</c> (XNA039) and <c>Effect.Begin</c> and <c>End</c> (XNA040). The code is not changed.
/// The check is by name: the file (or the project) must import <c>Microsoft.Xna.Framework</c> or <c>Microsoft.Xna.Framework.Graphics</c>.
/// </summary>
public sealed class VisualBasicMarkerRewriter : ISourceRewriter
{
    /// <summary>The rule IDs that have a Visual Basic marker rewriter.</summary>
    public static readonly IReadOnlyList<string> RuleIds = ["XNA024", "XNA039", "XNA040"];

    private static readonly string[] _gateNamespaces = ["Microsoft.Xna.Framework", "Microsoft.Xna.Framework.Graphics"];

    private readonly string[] _members;
    private readonly string? _receiver;
    private readonly string _message;
    private readonly VisualBasicMarkerInserter _inserter = new();

    private VisualBasicMarkerRewriter(string ruleId, string[] members, string? receiver, string message)
    {
        RuleId = ruleId;
        _members = members;
        _receiver = receiver;
        _message = message;
    }

    /// <inheritdoc />
    public string RuleId { get; }

    /// <inheritdoc />
    public SourceLanguage Language => SourceLanguage.VisualBasic;

    /// <summary>Creates the three marker rewriters.</summary>
    /// <returns>A new list.</returns>
    public static IReadOnlyList<VisualBasicMarkerRewriter> CreateAll() =>
    [
        new("XNA024", ["RenderState"], null, "RenderState was removed in XNA 4.0 and MonoGame. Replace it with BlendState, DepthStencilState and RasterizerState."),
        new("XNA039", ["Begin", "End"], "pass", "EffectPass.Begin and End were replaced by EffectPass.Apply in XNA 4.0 and MonoGame."),
        new("XNA040", ["Begin", "End"], "effect", "Effect.Begin and End were removed in XNA 4.0 and MonoGame. Loop over CurrentTechnique.Passes and call pass.Apply()."),
    ];

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var symbols = context.OriginalSymbols.Select(s => new KeyValuePair<string, object>(s, true)).ToList();
        var root = VisualBasicSyntaxTree.ParseText(context.Text, new VisualBasicParseOptions(preprocessorSymbols: symbols)).GetRoot();
        if (!IsGateOpen(root, context.ImplicitImports))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var positions = root.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
            .Where(a => a.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                && _members.Contains(a.Name.Identifier.ValueText, StringComparer.OrdinalIgnoreCase)
                && (_receiver is null || string.Equals(Receiver(a.Expression), _receiver, StringComparison.OrdinalIgnoreCase)))
            .Select(a => a.SpanStart)
            .Distinct()
            .OrderByDescending(p => p)
            .ToList();
        if (positions.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var text = context.Text;
        foreach (var position in positions)
        {
            text = _inserter.InsertMarker(text, position, RuleId, _message, context.OriginalSymbols);
        }

        return string.Equals(text, context.Text, StringComparison.Ordinal)
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(text, [new RewriteNote(RuleId, $"{_message} Marked in {context.FilePath}; the code was not changed.", NeedsManualWork: true)]);
    }

    private static string? Receiver(ExpressionSyntax? expression) => expression switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText,
        MemberAccessExpressionSyntax access when access.IsKind(SyntaxKind.SimpleMemberAccessExpression) => access.Name.Identifier.ValueText,
        InvocationExpressionSyntax call => Receiver(call.Expression),
        _ => null,
    };

    private static bool IsGateOpen(SyntaxNode root, IReadOnlyList<string>? implicitImports)
    {
        var imported = root.DescendantNodes().OfType<ImportsStatementSyntax>()
            .SelectMany(s => s.ImportsClauses.OfType<SimpleImportsClauseSyntax>())
            .Select(c => c.Name.ToString().Replace(" ", string.Empty, StringComparison.Ordinal))
            .Concat(implicitImports ?? []);
        return imported.Any(i => _gateNamespaces.Contains(i, StringComparer.OrdinalIgnoreCase))
            || root.ToString().Contains("Microsoft.Xna.Framework", StringComparison.OrdinalIgnoreCase);
    }
}
