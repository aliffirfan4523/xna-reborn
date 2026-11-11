using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Core.Toolkit;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Rewrites the Toolkit graphics device calls that have a MonoGame form (SDX040 to SDX044, SDX060, one instance per rule).
/// A use that has no MonoGame form under the same name (<c>BlendStates.Default</c>, <c>RasterizerStates.*</c>, <c>BackBuffer</c> without
/// <c>.Width</c> or <c>.Height</c>) gets a marker and stays as it is. A call on anything that is not named <c>GraphicsDevice</c> is not touched.
/// </summary>
public sealed class ToolkitGraphicsRewriter : ISourceRewriter
{
    /// <summary>The rule ID of the state collection rewriter.</summary>
    public const string StateCollectionsId = "SDX040";

    /// <summary>The rule ID of the Set*State rewriter.</summary>
    public const string SetStateId = "SDX041";

    /// <summary>The rule ID of the single render target rewriter.</summary>
    public const string SetRenderTargetId = "SDX042";

    /// <summary>The rule ID of the back buffer size rewriter.</summary>
    public const string BackBufferId = "SDX044";

    /// <summary>The rule ID of the ToDisposeContent rewriter.</summary>
    public const string ToDisposeContentId = "SDX060";

    /// <summary>The message of the SDX040 marker.</summary>
    public const string StateMarker = "This device state has no MonoGame member with the same name. Use a BlendState, SamplerState, DepthStencilState or RasterizerState object. Check face culling after the change of winding order.";

    /// <summary>The message of the SDX044 marker.</summary>
    public const string BackBufferMarker = "BackBuffer is a Toolkit render target. MonoGame keeps only its size (GraphicsDevice.PresentationParameters). Render to your own RenderTarget2D, or pass null to SetRenderTarget to draw to the back buffer.";

    private const string _setRenderTargets = "SetRenderTargets";
    private const string _toDispose = "ToDisposeContent";

    private readonly ToolkitMap _map;

    /// <summary>Creates the rewriter for one rule.</summary>
    /// <param name="ruleId">SDX040, SDX041, SDX042, SDX044 or SDX060.</param>
    /// <param name="map">The rename map. Defaults to <see cref="ToolkitMap.Default"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="ruleId"/> is not one of the five rules.</exception>
    public ToolkitGraphicsRewriter(string ruleId, ToolkitMap? map = null)
    {
        if (!RuleIds.Contains(ruleId))
        {
            throw new ArgumentException($"{ruleId} is not a graphics rule of the Toolkit migration.", nameof(ruleId));
        }

        RuleId = ruleId;
        _map = map ?? ToolkitMap.Default;
    }

    /// <summary>The rule IDs this class handles.</summary>
    public static IReadOnlyList<string> RuleIds { get; } = [StateCollectionsId, SetStateId, SetRenderTargetId, BackBufferId, ToDisposeContentId];

    /// <inheritdoc />
    public string RuleId { get; }

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var first = Analyze(context.Text, context.OriginalSymbols);
        if (first.Edits.Count == 0 && first.Sites.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var text = ToolkitSyntax.Apply(context.Text, first.Edits.Select(e => e.Edit));
        var notes = first.Edits.Select(e => new RewriteNote(RuleId, e.Note, NeedsManualWork: false)).ToList();

        // Markers go in one at a time from the bottom of the file, so the offsets above stay valid.
        var sites = Analyze(text, context.OriginalSymbols).Sites.OrderByDescending(s => s.Position).ToList();
        foreach (var site in sites)
        {
            var marked = MarkerInserter.InsertMarker(text, site.Position, RuleId, site.Message, context.OriginalSymbols);
            if (!string.Equals(marked, text, StringComparison.Ordinal))
            {
                text = marked;
                notes.Add(new RewriteNote(RuleId, $"Left a use unchanged and added a marker: {site.Snippet}", NeedsManualWork: true));
            }
        }

        return string.Equals(text, context.Text, StringComparison.Ordinal) && notes.Count == 0
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(text, notes);
    }

    private Analysis Analyze(string text, IReadOnlyCollection<string> symbols)
    {
        var root = ToolkitSyntax.Parse(text, symbols);
        var result = new Analysis([], []);
        if (!ToolkitSyntax.HasFrameworkContext(root))
        {
            return result;
        }

        var graphics = ToolkitSyntax.Imports(root, ToolkitSyntax.MonoGameGraphics) ? string.Empty : ToolkitSyntax.MonoGameGraphics + ".";
        foreach (var node in root.DescendantNodes())
        {
            switch (RuleId)
            {
                case StateCollectionsId when node is MemberAccessExpressionSyntax access:
                    StateCollection(access, graphics, result);
                    break;
                case SetStateId when node is InvocationExpressionSyntax call:
                    SetState(call, result);
                    break;
                case SetRenderTargetId when node is InvocationExpressionSyntax call:
                    SetRenderTarget(call, result);
                    break;
                case BackBufferId when node is MemberAccessExpressionSyntax access:
                    BackBuffer(access, result);
                    break;
                case ToDisposeContentId when node is InvocationExpressionSyntax call:
                    ToDispose(call, text, result);
                    break;
            }
        }

        return result;
    }

    // GraphicsDevice.BlendStates.Opaque -> BlendState.Opaque. Anything else on a state collection is marked.
    private void StateCollection(MemberAccessExpressionSyntax access, string graphics, Analysis result)
    {
        if (!access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || !ToolkitSyntax.IsGraphicsDevice(access.Expression)
            || !IsStateCollectionName(access.Name.Identifier.ValueText))
        {
            return;
        }

        var collection = _map.StateCollections.FirstOrDefault(c => c.Property == access.Name.Identifier.ValueText);
        if (collection is not null
            && access.Parent is MemberAccessExpressionSyntax member
            && member.Expression == access
            && member.Name is IdentifierNameSyntax memberName
            && collection.Members.Contains(memberName.Identifier.ValueText, StringComparer.Ordinal))
        {
            var replacement = graphics + collection.Type + "." + memberName.Identifier.ValueText;
            result.Edits.Add(new Rewritten(
                new TextEdit(access.Expression.SpanStart, member.Span.End, replacement),
                $"Replaced '{member}' with '{replacement}'."));
            return;
        }

        result.Sites.Add(new Site(access.SpanStart, StateMarker, access.ToString()));
    }

    private bool IsStateCollectionName(string name) =>
        name == "RasterizerStates" || _map.StateCollections.Any(c => c.Property == name);

    // GraphicsDevice.SetBlendState(state); -> GraphicsDevice.BlendState = state;
    private void SetState(InvocationExpressionSyntax call, Analysis result)
    {
        if (call.Expression is not MemberAccessExpressionSyntax access
            || !access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || !ToolkitSyntax.IsGraphicsDevice(access.Expression)
            || call.ArgumentList.Arguments.Count != 1)
        {
            return;
        }

        var setter = _map.StateSetters.FirstOrDefault(s => s.Call == access.Name.Identifier.ValueText);
        var argument = call.ArgumentList.Arguments[0];
        if (setter is null || argument.NameColon is not null || argument.RefKindKeyword.RawKind != 0)
        {
            return;
        }

        if (call.Parent is not ExpressionStatementSyntax)
        {
            result.Sites.Add(new Site(call.SpanStart, $"Set the {setter.Property} property of the device instead of calling {setter.Call}.", call.ToString()));
            return;
        }

        var replacement = $"{access.Expression}.{setter.Property} = {argument.Expression}";
        result.Edits.Add(new Rewritten(
            new TextEdit(call.SpanStart, call.Span.End, replacement),
            $"Replaced '{setter.Call}(...)' with an assignment to '{setter.Property}'."));
    }

    // GraphicsDevice.SetRenderTargets(target) -> GraphicsDevice.SetRenderTarget(target). Other argument counts belong to SDX043.
    private static void SetRenderTarget(InvocationExpressionSyntax call, Analysis result)
    {
        if (call.Expression is MemberAccessExpressionSyntax access
            && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            && access.Name.Identifier.ValueText == _setRenderTargets
            && ToolkitSyntax.IsGraphicsDevice(access.Expression)
            && call.ArgumentList.Arguments.Count == 1)
        {
            result.Edits.Add(new Rewritten(
                new TextEdit(access.Name.SpanStart, access.Name.Span.End, "SetRenderTarget"),
                "Renamed SetRenderTargets to SetRenderTarget."));
        }
    }

    // GraphicsDevice.BackBuffer.Width -> GraphicsDevice.PresentationParameters.BackBufferWidth. Other uses of BackBuffer are marked.
    private void BackBuffer(MemberAccessExpressionSyntax access, Analysis result)
    {
        if (!access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
            || access.Name.Identifier.ValueText != "BackBuffer"
            || !ToolkitSyntax.IsGraphicsDevice(access.Expression))
        {
            return;
        }

        if (access.Parent is MemberAccessExpressionSyntax size
            && size.Expression == access
            && size.Name is IdentifierNameSyntax sizeName
            && _map.BackBuffer.FirstOrDefault(b => b.From == sizeName.Identifier.ValueText) is { } rename)
        {
            result.Edits.Add(new Rewritten(
                new TextEdit(access.Name.SpanStart, size.Name.Span.End, "PresentationParameters." + rename.To),
                $"Replaced 'BackBuffer.{rename.From}' with 'PresentationParameters.{rename.To}'."));
            return;
        }

        result.Sites.Add(new Site(access.SpanStart, BackBufferMarker, access.ToString()));
    }

    // ToDisposeContent(x) -> x. The Toolkit disposed x when the game unloaded. MonoGame does not, and the note says so.
    private static void ToDispose(InvocationExpressionSyntax call, string source, Analysis result)
    {
        var named = call.Expression switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText == _toDispose,
            GenericNameSyntax generic => generic.Identifier.ValueText == _toDispose,
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax or BaseExpressionSyntax, Name: var n } => n.Identifier.ValueText == _toDispose,
            _ => false,
        };
        if (!named || call.ArgumentList.Arguments.Count != 1 || call.ArgumentList.Arguments[0].NameColon is not null)
        {
            return;
        }

        var argument = call.ArgumentList.Arguments[0].Expression;
        if (call.Parent is ExpressionStatementSyntax statement && argument is not (InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax or AssignmentExpressionSyntax))
        {
            // A bare `x;` is not a statement in C#. Registering x for disposal was all the call did, so the statement goes.
            result.Edits.Add(new Rewritten(
                ToolkitSyntax.RemoveLines(source, statement, includeLeadingComments: false),
                "Removed ToDisposeContent(x);. The Toolkit disposed x when the game unloaded; MonoGame does not, so dispose it yourself if it must not live until the process ends."));
            return;
        }

        var text = NeedsParentheses(argument, call) ? "(" + argument + ")" : argument.ToString();
        result.Edits.Add(new Rewritten(
            new TextEdit(call.SpanStart, call.Span.End, text),
            "Replaced ToDisposeContent(x) with x. The Toolkit disposed x when the game unloaded; MonoGame does not, so dispose it yourself if it must not live until the process ends."));
    }

    private static bool NeedsParentheses(ExpressionSyntax argument, InvocationExpressionSyntax call)
    {
        var primary = argument is IdentifierNameSyntax or GenericNameSyntax or LiteralExpressionSyntax or ThisExpressionSyntax
            or ParenthesizedExpressionSyntax or InvocationExpressionSyntax or ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax
            or ElementAccessExpressionSyntax or DefaultExpressionSyntax or TypeOfExpressionSyntax
            || argument is MemberAccessExpressionSyntax;
        if (primary)
        {
            return false;
        }

        return call.Parent is not (ArgumentSyntax or EqualsValueClauseSyntax or ReturnStatementSyntax or ArrowExpressionClauseSyntax or ExpressionStatementSyntax
            or AssignmentExpressionSyntax { Right: not null });
    }

    private sealed record Rewritten(TextEdit Edit, string Note);

    private sealed record Site(int Position, string Message, string Snippet);

    private sealed record Analysis(List<Rewritten> Edits, List<Site> Sites);
}
