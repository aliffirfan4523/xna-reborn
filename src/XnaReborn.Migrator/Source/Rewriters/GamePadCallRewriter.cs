using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Redirects <c>GamePad.GetState(...)</c> calls of a device project to <c>XnaReborn.Compat.Input.CompatGamePad.GetState(...)</c>,
/// which returns the real pad when one is connected and keyboard input otherwise (Escape is Back). Only the receiver text changes.
/// The check is by name: the file must import <c>Microsoft.Xna.Framework.Input</c>, declare no type called <c>GamePad</c>,
/// and the call must have one or two arguments. <c>GetCapabilities</c> and <c>SetVibration</c> are never touched.
/// </summary>
public sealed class GamePadCallRewriter : ISourceRewriter
{
    /// <summary>The rule ID this rewriter handles.</summary>
    public const string Id = "XNA062";

    /// <summary>The class that replaces the receiver.</summary>
    public const string Replacement = "XnaReborn.Compat.Input.CompatGamePad";

    private const string _inputNamespace = "Microsoft.Xna.Framework.Input";
    private const string _qualifiedReceiver = _inputNamespace + ".GamePad";

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!DevicePlatforms.IsDevice(context.Platform) || !context.Text.Contains("GamePad", StringComparison.Ordinal))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var root = CSharpSyntaxTree.ParseText(context.Text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols)).GetRoot();
        var importsInput = root.DescendantNodes().OfType<UsingDirectiveSyntax>()
            .Any(u => u.StaticKeyword.IsKind(SyntaxKind.None) && u.Alias is null && u.Name?.ToString().Trim() == _inputNamespace);
        var declaresGamePad = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Any(t => t.Identifier.ValueText == "GamePad")
            || root.DescendantNodes().OfType<DelegateDeclarationSyntax>().Any(t => t.Identifier.ValueText == "GamePad");

        var receivers = new List<ExpressionSyntax>();
        foreach (var call in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (call.Expression is MemberAccessExpressionSyntax access
                && access.IsKind(SyntaxKind.SimpleMemberAccessExpression)
                && access.Name.Identifier.ValueText == "GetState"
                && call.ArgumentList.Arguments.Count is 1 or 2)
            {
                var receiver = access.Expression.WithoutTrivia().ToString();
                if ((receiver == "GamePad" && importsInput && !declaresGamePad) || receiver == _qualifiedReceiver)
                {
                    receivers.Add(access.Expression);
                }
            }
        }

        if (receivers.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var text = context.Text;
        foreach (var receiver in receivers.OrderByDescending(r => r.SpanStart))
        {
            var span = receiver.Span;
            text = text.Remove(span.Start, span.Length).Insert(span.Start, Replacement);
        }

        return new SourceRewriteResult(
            text,
            [new RewriteNote(Id, $"Redirected {receivers.Count} GamePad.GetState call(s) to the keyboard fallback ({Replacement}).", NeedsManualWork: false)]);
    }
}
