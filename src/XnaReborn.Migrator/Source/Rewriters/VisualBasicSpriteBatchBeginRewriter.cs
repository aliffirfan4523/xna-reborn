using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using XnaReborn.Core.Models;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// The Visual Basic version of <see cref="SpriteBatchBeginRewriter"/> (rule XNA022). It rewrites <c>Begin(SpriteBlendMode.X)</c>, with or without the
/// argument name, to <c>Begin(SpriteSortMode.Deferred, BlendState.Y)</c>, and the three argument form with <c>SaveStateMode</c>.
/// Every other use of <c>SpriteBlendMode</c> gets a marker. Only whole argument spans are edited.
/// </summary>
public sealed class VisualBasicSpriteBatchBeginRewriter : ISourceRewriter
{
    private const string _blendModeType = "SpriteBlendMode";
    private const string _graphicsNamespace = "Microsoft.Xna.Framework.Graphics";
    private const string _manualMessage = "Replace this SpriteBlendMode use with a BlendState; SpriteBatch.Begin now takes SpriteSortMode and BlendState.";
    private const string _saveStateMessage = "SpriteBatch no longer restores device state; set the states you need after End().";

    private readonly VisualBasicMarkerInserter _inserter = new();

    /// <inheritdoc />
    public string RuleId => SpriteBatchBeginRewriter.Id;

    /// <inheritdoc />
    public SourceLanguage Language => SourceLanguage.VisualBasic;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = context.Text;
        if (!text.Contains(_blendModeType, StringComparison.OrdinalIgnoreCase))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var symbols = context.OriginalSymbols.Select(s => new KeyValuePair<string, object>(s, true)).ToList();
        var tree = VisualBasicSyntaxTree.ParseText(text, new VisualBasicParseOptions(preprocessorSymbols: symbols));
        var root = tree.GetRoot();
        var uses = root.DescendantNodes().OfType<IdentifierNameSyntax>().Where(i => IsName(i.Identifier.ValueText, _blendModeType)).ToList();
        if (uses.Count == 0 || !(ImportsGraphics(root, context.ImplicitImports) || uses.Any(IsGraphicsQualified)))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var edits = new List<(int Start, int Length, string Text)>();
        var markerPositions = new List<(int Position, string RuleId, string Message)>();
        var notes = new List<RewriteNote>();
        var handled = new HashSet<IdentifierNameSyntax>();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(IsBeginCall))
        {
            var plan = TryPlan(call, text, tree);
            if (plan is null)
            {
                continue;
            }

            handled.Add(plan.BlendModeName);
            edits.AddRange(plan.Edits);
            notes.Add(new RewriteNote(RuleId, plan.Message, NeedsManualWork: false));
            if (plan.SaveState)
            {
                markerPositions.Add((call.SpanStart, SpriteBatchBeginRewriter.SaveStateRuleId, _saveStateMessage));
            }
        }

        foreach (var use in uses.Where(u => !handled.Contains(u)))
        {
            markerPositions.Add((use.SpanStart, RuleId, _manualMessage));
        }

        if (edits.Count == 0 && markerPositions.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        // Markers first, from the end, on the original text; then the edits are worked out again on the result.
        var current = text;
        var markedLines = 0;
        foreach (var marker in markerPositions.OrderByDescending(m => m.Position))
        {
            var next = _inserter.InsertMarker(current, marker.Position, marker.RuleId, marker.Message, context.OriginalSymbols);
            if (!string.Equals(next, current, StringComparison.Ordinal))
            {
                markedLines++;
                current = next;
            }
        }

        if (markedLines > 0)
        {
            notes.Add(new RewriteNote(RuleId, $"{markedLines} SpriteBlendMode or SaveStateMode use(s) in {context.FilePath} need a BlendState by hand; they carry a marker.", NeedsManualWork: true));
        }

        if (edits.Count > 0)
        {
            var edited = Replan(current, symbols);
            current = edited;
        }

        return string.Equals(current, text, StringComparison.Ordinal) ? SourceRewriteResult.Unchanged(context) : new SourceRewriteResult(current, notes);
    }

    // Works out the argument edits on the text that already holds the markers, so no offset is stale.
    private static string Replan(string text, List<KeyValuePair<string, object>> symbols)
    {
        var tree = VisualBasicSyntaxTree.ParseText(text, new VisualBasicParseOptions(preprocessorSymbols: symbols));
        var edits = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(IsBeginCall)
            .Select(c => TryPlan(c, text, tree))
            .Where(p => p is not null)
            .SelectMany(p => p!.Edits)
            .OrderByDescending(e => e.Start);
        foreach (var edit in edits)
        {
            text = text.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Text);
        }

        return text;
    }

    private static Plan? TryPlan(InvocationExpressionSyntax call, string text, SyntaxTree tree)
    {
        var args = call.ArgumentList?.Arguments.OfType<SimpleArgumentSyntax>().ToList();
        if (args is null || args.Count != call.ArgumentList!.Arguments.Count || args.Count is not (1 or 3 or 4))
        {
            return null;
        }

        // A name is allowed only on a single argument: Begin(blendMode:=SpriteBlendMode.AlphaBlend).
        if (args.Count > 1 && args.Any(a => a.IsNamed))
        {
            return null;
        }

        if (!TryReadBlendMode(args[0].Expression, out var blendName, out var member) || !BlendModeMap.TryGet(member, out var blend))
        {
            return null;
        }

        var prefix = string.Concat(text[args[0].Expression.SpanStart..blendName.SpanStart].Where(c => !char.IsWhiteSpace(c)));
        var blendText = $"{prefix}BlendState.{blend}";
        var line = tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(call.SpanStart, 0)).StartLinePosition.Line + 1;
        var advice = member.Equals("AlphaBlend", StringComparison.OrdinalIgnoreCase)
            ? " XNA 4.0 and MonoGame premultiply alpha in the content pipeline by default, so BlendState.AlphaBlend matches; textures created at runtime may need BlendState.NonPremultiplied."
            : string.Empty;

        if (args.Count == 1)
        {
            var span = args[0].Span;
            return new Plan(
                blendName,
                [(span.Start, span.Length, $"{prefix}SpriteSortMode.Deferred, {blendText}")],
                $"SpriteBatch.Begin at line {line}: replaced SpriteBlendMode.{member} with SpriteSortMode.Deferred and BlendState.{blend}.{advice}",
                SaveState: false);
        }

        if (!TryReadSaveState(args[2].Expression, out var saveMember))
        {
            return null;
        }

        var sortText = text[args[1].Expression.SpanStart..args[1].Expression.Span.End];
        var message = $"SpriteBatch.Begin at line {line}: replaced SpriteBlendMode.{member} and SaveStateMode.{saveMember} with BlendState.{blend}.{advice}";
        var saveState = saveMember.Equals("SaveState", StringComparison.OrdinalIgnoreCase);
        if (args.Count == 3)
        {
            var removed = text[args[1].Span.End..args[2].Span.End];
            if (removed.Contains('\'', StringComparison.Ordinal) || removed.Contains('_', StringComparison.Ordinal))
            {
                return null;
            }

            return new Plan(
                blendName,
                [(args[0].Expression.SpanStart, args[0].Expression.Span.Length, sortText), (args[1].SpanStart, args[2].Span.End - args[1].SpanStart, blendText)],
                message,
                saveState);
        }

        return new Plan(
            blendName,
            [
                (args[0].Expression.SpanStart, args[0].Expression.Span.Length, sortText),
                (args[1].Expression.SpanStart, args[1].Expression.Span.Length, blendText),
                (args[2].Expression.SpanStart, args[2].Expression.Span.Length, "Nothing, Nothing, Nothing, Nothing"),
            ],
            message,
            saveState);
    }

    private static bool IsName(string actual, string expected) => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

    private static bool TryReadBlendMode(ExpressionSyntax expression, out IdentifierNameSyntax blendName, out string member)
    {
        blendName = null!;
        member = string.Empty;
        if (expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression))
        {
            return false;
        }

        var typeName = access.Expression switch
        {
            IdentifierNameSyntax id => id,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax qualified } => qualified,
            QualifiedNameSyntax { Right: IdentifierNameSyntax right } => right,
            _ => null,
        };
        if (typeName is null || !IsName(typeName.Identifier.ValueText, _blendModeType))
        {
            return false;
        }

        blendName = typeName;
        member = CanonicalMember(name.Identifier.ValueText);
        return true;
    }

    // Visual Basic names compare without case, so the member is mapped to the spelling the map knows.
    private static string CanonicalMember(string member) =>
        new[] { "AlphaBlend", "Additive", "None" }.FirstOrDefault(m => IsName(m, member)) ?? member;

    private static bool TryReadSaveState(ExpressionSyntax expression, out string member)
    {
        member = string.Empty;
        if (expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || !IsName(LastName(access.Expression) ?? string.Empty, "SaveStateMode")
            || !(IsName(name.Identifier.ValueText, "SaveState") || IsName(name.Identifier.ValueText, "None")))
        {
            return false;
        }

        member = IsName(name.Identifier.ValueText, "SaveState") ? "SaveState" : "None";
        return true;
    }

    private static string? LastName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
        _ => null,
    };

    private static bool IsBeginCall(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => IsName(name.Identifier.ValueText, "Begin"),
        IdentifierNameSyntax name => IsName(name.Identifier.ValueText, "Begin"),
        _ => false,
    };

    private static bool ImportsGraphics(SyntaxNode root, IReadOnlyList<string>? implicitImports) =>
        (implicitImports ?? []).Any(i => IsName(i.Trim(), _graphicsNamespace))
        || root.DescendantNodes().OfType<SimpleImportsClauseSyntax>().Any(c => IsName(c.Name.ToString().Replace(" ", string.Empty, StringComparison.Ordinal), _graphicsNamespace));

    private static bool IsGraphicsQualified(IdentifierNameSyntax name) =>
        (name.Parent is QualifiedNameSyntax q && q.Right == name && q.Left.ToString().EndsWith(_graphicsNamespace, StringComparison.OrdinalIgnoreCase))
        || (name.Parent is MemberAccessExpressionSyntax a && a.Name == name && a.Expression?.ToString().EndsWith(_graphicsNamespace, StringComparison.OrdinalIgnoreCase) == true);

    private sealed record Plan(IdentifierNameSyntax BlendModeName, IReadOnlyList<(int Start, int Length, string Text)> Edits, string Message, bool SaveState);
}
