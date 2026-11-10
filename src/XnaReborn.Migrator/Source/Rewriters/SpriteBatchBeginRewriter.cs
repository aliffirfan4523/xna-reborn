using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Rewrites the XNA 3.1 <c>SpriteBatch.Begin(SpriteBlendMode, ...)</c> overloads to the XNA 4.0 and MonoGame
/// <c>Begin(SpriteSortMode, BlendState, ...)</c> overloads (rule XNA022).
/// Only whole argument spans are edited, so every other byte of the file stays as it was.
/// </summary>
public sealed class SpriteBatchBeginRewriter : ISourceRewriter
{
    /// <summary>The rule ID of this rewriter.</summary>
    public const string Id = "XNA022";

    /// <summary>The rule ID used for the marker about the removed <c>SaveStateMode.SaveState</c> behavior.</summary>
    public const string SaveStateRuleId = "XNA023";

    private const string _graphicsNamespace = "Microsoft.Xna.Framework.Graphics";
    private const string _blendModeType = "SpriteBlendMode";

    private const string _manualMessage =
        "Replace this SpriteBlendMode use with a BlendState; SpriteBatch.Begin now takes SpriteSortMode and BlendState.";

    private const string _saveStateMessage =
        "SpriteBatch no longer restores device state; set the states you need after End().";

    private const string _alphaBlendAdvice =
        " XNA 4.0 and MonoGame premultiply alpha in the content pipeline by default, so BlendState.AlphaBlend matches;"
        + " textures created at runtime may need BlendState.NonPremultiplied.";

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var text = context.Text;
        if (!text.Contains(_blendModeType, StringComparison.Ordinal))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols);
        var tree = CSharpSyntaxTree.ParseText(text, options);
        var root = tree.GetRoot();

        var uses = root.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(i => i.Identifier.ValueText == _blendModeType)
            .ToList();
        if (uses.Count == 0 || !(ImportsGraphics(root) || uses.Any(IsGraphicsQualified)))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var edits = new List<Edit>();
        var markers = new List<Marker>();
        var applied = new List<RewriteNote>();
        var handled = new HashSet<IdentifierNameSyntax>();
        var beginCalls = new List<InvocationExpressionSyntax>();

        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (!IsBeginCall(call))
            {
                continue;
            }

            beginCalls.Add(call);
            var plan = TryPlan(call, text, tree);
            if (plan is null)
            {
                continue;
            }

            handled.Add(plan.BlendModeName);
            edits.AddRange(plan.Edits);
            applied.Add(new RewriteNote(Id, plan.Message, NeedsManualWork: false));
            if (plan.SaveState)
            {
                markers.Add(new Marker(call.SpanStart, SaveStateRuleId, _saveStateMessage, Line(tree, call.SpanStart)));
            }
        }

        var remaining = uses.Where(u => !handled.Contains(u)).ToList();
        foreach (var use in remaining)
        {
            markers.Add(new Marker(use.SpanStart, Id, _manualMessage, Line(tree, use.SpanStart)));
        }

        if (remaining.Count > 0)
        {
            foreach (var call in beginCalls)
            {
                if (handled.Any(h => call.Span.Contains(h.Span)) || remaining.Any(r => call.Span.Contains(r.Span)))
                {
                    continue;
                }

                if (LooksLikeSpriteBlendModeCall(call))
                {
                    markers.Add(new Marker(call.SpanStart, Id, _manualMessage, Line(tree, call.SpanStart)));
                }
            }
        }

        if (edits.Count == 0 && markers.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var current = text;
        foreach (var edit in edits.OrderByDescending(e => e.Start))
        {
            current = current.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.NewText);
        }

        var notes = new List<RewriteNote>(applied);
        var shift = 0;
        foreach (var marker in markers.OrderBy(m => m.Position))
        {
            var position = marker.Position + edits.Where(e => e.Start < marker.Position).Sum(e => e.NewText.Length - e.Length) + shift;
            var next = MarkerInserter.InsertMarker(current, position, marker.RuleId, marker.Message);
            if (string.Equals(next, current, StringComparison.Ordinal))
            {
                continue;
            }

            shift += next.Length - current.Length;
            current = next;
            var note = marker.RuleId == SaveStateRuleId
                ? $"SpriteBatch.Begin at line {marker.Line} used SaveStateMode.SaveState. {_saveStateMessage}"
                : $"Line {marker.Line} still uses SpriteBlendMode and was not changed. Replace it with a BlendState by hand.";
            notes.Add(new RewriteNote(marker.RuleId, note, NeedsManualWork: true));
        }

        return string.Equals(current, text, StringComparison.Ordinal)
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(current, notes);
    }

    private static Plan? TryPlan(InvocationExpressionSyntax call, string text, SyntaxTree tree)
    {
        var args = call.ArgumentList.Arguments;
        if (args.Count is not (1 or 3 or 4) || args.Any(a => a.NameColon is not null || !a.RefKindKeyword.IsKind(SyntaxKind.None)))
        {
            return null;
        }

        if (!TryReadBlendMode(args[0].Expression, out var blendName, out var member))
        {
            return null;
        }

        var blend = member switch
        {
            "AlphaBlend" => "AlphaBlend",
            "Additive" => "Additive",
            "None" => "Opaque",
            _ => null,
        };
        if (blend is null)
        {
            return null;
        }

        var prefix = string.Concat(text[args[0].Expression.SpanStart..blendName.SpanStart].Where(c => !char.IsWhiteSpace(c)));
        var blendText = $"{prefix}BlendState.{blend}";
        var original = $"SpriteBlendMode.{member}";
        var line = Line(tree, call.SpanStart);
        var advice = member == "AlphaBlend" ? _alphaBlendAdvice : string.Empty;

        if (args.Count == 1)
        {
            var replacement = $"{prefix}SpriteSortMode.Deferred, {blendText}";
            return new Plan(
                blendName,
                [Replace(args[0].Expression, replacement)],
                $"SpriteBatch.Begin at line {line}: replaced {original} with SpriteSortMode.Deferred and BlendState.{blend}.{advice}",
                SaveState: false);
        }

        var save = args[2].Expression;
        if (!TryReadSaveState(save, out var saveMember))
        {
            return null;
        }

        var sortText = text[args[1].Expression.Span.Start..args[1].Expression.Span.End];
        var message = $"SpriteBatch.Begin at line {line}: replaced {original} and SaveStateMode.{saveMember} with BlendState.{blend}.{advice}";
        var saveState = saveMember == "SaveState";
        if (args.Count == 3)
        {
            var start = args[1].Span.End;
            var removed = text[start..args[2].Span.End];
            if (removed.Contains("//", StringComparison.Ordinal) || removed.Contains("/*", StringComparison.Ordinal))
            {
                return null;
            }

            return new Plan(
                blendName,
                [Replace(args[0].Expression, sortText), new Edit(args[1].SpanStart, args[2].Span.End - args[1].SpanStart, blendText)],
                message,
                saveState);
        }

        return new Plan(
            blendName,
            [
                Replace(args[0].Expression, sortText),
                Replace(args[1].Expression, blendText),
                Replace(save, "null, null, null, null"),
            ],
            message,
            saveState);
    }

    private static bool TryReadBlendMode(ExpressionSyntax expression, out IdentifierNameSyntax blendName, out string member)
    {
        blendName = null!;
        member = string.Empty;
        if (expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression))
        {
            return false;
        }

        var typeName = access.Expression switch
        {
            IdentifierNameSyntax id => id,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax qualified } => qualified,
            _ => null,
        };
        if (typeName is null || typeName.Identifier.ValueText != _blendModeType)
        {
            return false;
        }

        blendName = typeName;
        member = name.Identifier.ValueText;
        return true;
    }

    private static bool TryReadSaveState(ExpressionSyntax expression, out string member)
    {
        member = string.Empty;
        if (expression is not MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || LastName(access.Expression) != "SaveStateMode"
            || name.Identifier.ValueText is not ("SaveState" or "None"))
        {
            return false;
        }

        member = name.Identifier.ValueText;
        return true;
    }

    private static bool LooksLikeSpriteBlendModeCall(InvocationExpressionSyntax call)
    {
        var args = call.ArgumentList.Arguments;
        if (args.Count == 0)
        {
            return false;
        }

        var first = args[0].Expression;
        if (first.IsKind(SyntaxKind.NullLiteralExpression))
        {
            return false;
        }

        return first is not MemberAccessExpressionSyntax access
            || LastName(access.Expression) is not ("SpriteSortMode" or "SaveStateMode");
    }

    private static string? LastName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText,
        _ => null,
    };

    private static bool IsBeginCall(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } => name.Identifier.ValueText == "Begin",
        IdentifierNameSyntax name => name.Identifier.ValueText == "Begin",
        _ => false,
    };

    private static bool ImportsGraphics(SyntaxNode root) =>
        root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(u =>
            u.StaticKeyword.IsKind(SyntaxKind.None)
            && u.Name is not null
            && Normalize(u.Name.ToString()) == _graphicsNamespace);

    private static bool IsGraphicsQualified(IdentifierNameSyntax name) =>
        name.Parent is MemberAccessExpressionSyntax access
        && access.Name == name
        && Normalize(access.Expression.ToString()).EndsWith(_graphicsNamespace, StringComparison.Ordinal);

    private static string Normalize(string name)
    {
        var compact = string.Concat(name.Where(c => !char.IsWhiteSpace(c)));
        return compact.StartsWith("global::", StringComparison.Ordinal) ? compact["global::".Length..] : compact;
    }

    private static Edit Replace(SyntaxNode node, string newText) => new(node.SpanStart, node.Span.Length, newText);

    private static int Line(SyntaxTree tree, int position) => tree.GetLineSpan(new Microsoft.CodeAnalysis.Text.TextSpan(position, 0)).StartLinePosition.Line + 1;

    private sealed record Edit(int Start, int Length, string NewText);

    private sealed record Marker(int Position, string RuleId, string Message, int Line);

    private sealed record Plan(IdentifierNameSyntax BlendModeName, IReadOnlyList<Edit> Edits, string Message, bool SaveState);
}
