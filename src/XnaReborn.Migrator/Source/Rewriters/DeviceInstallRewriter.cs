using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Adds <c>Components.Add(new XnaReborn.Compat.Input.DeviceInput(this));</c> as the last statement of the constructor of a
/// <c>Game</c> subclass in a device project. The component turns the mouse into a touch point, shows the cursor, starts the
/// game in a window and drives the accelerometer stand-in. The line is added once; a file that has it is left alone.
/// A <c>Game</c> subclass without a usable constructor gets a marker instead.
/// </summary>
public sealed class DeviceInstallRewriter : ISourceRewriter
{
    /// <summary>The pseudo rule ID of the install. It is not driven by one analyzer finding: every migrated device project gets it.</summary>
    public const string Id = "DEVICEINPUT";

    /// <summary>The statement that is added.</summary>
    public const string Statement = "Components.Add(new XnaReborn.Compat.Input.DeviceInput(this));";

    private const string _marker = "Add " + Statement + " to the constructor of this Game class by hand, so the mouse, tilt keys and window setup work.";

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!DevicePlatforms.IsDevice(context.Platform)
            || context.Text.Contains("XnaReborn.Compat.Input.DeviceInput(", StringComparison.Ordinal))
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var root = CSharpSyntaxTree.ParseText(context.Text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: context.OriginalSymbols)).GetRoot();
        var game = root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(IsGameSubclass);
        if (game is null)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var constructor = game.Members.OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault(c => !c.Modifiers.Any(SyntaxKind.StaticKeyword) && !(c.Initializer?.IsKind(SyntaxKind.ThisConstructorInitializer) ?? false));
        var insertAt = constructor?.Body is { } body ? CloseBraceLineStart(context.Text, body) : null;
        if (insertAt is null)
        {
            var marked = MarkerInserter.InsertMarker(context.Text, game.SpanStart, Id, _marker, context.OriginalSymbols);
            return marked == context.Text
                ? SourceRewriteResult.Unchanged(context)
                : new SourceRewriteResult(marked, [new RewriteNote(Id, $"{game.Identifier.ValueText} has no constructor the component could be added to. {_marker}", NeedsManualWork: true)]);
        }

        var indent = IndentOf(context.Text, constructor!.Body!);
        var newLine = context.Text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var text = context.Text.Insert(insertAt.Value, indent + Statement + newLine);
        return new SourceRewriteResult(text, [new RewriteNote(Id, $"Added the DeviceInput component to the {game.Identifier.ValueText} constructor.", NeedsManualWork: false)]);
    }

    private static bool IsGameSubclass(ClassDeclarationSyntax type) =>
        type.BaseList?.Types.Any(t => t.Type switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText == "Game",
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == "Game",
            _ => false,
        }) ?? false;

    // The start of the line of the closing brace, when that brace is the first thing on its line and the body is not one line.
    private static int? CloseBraceLineStart(string text, BlockSyntax body)
    {
        var close = body.CloseBraceToken.SpanStart;
        var lineStart = text.LastIndexOf('\n', Math.Max(0, close - 1)) + 1;
        if (lineStart <= body.OpenBraceToken.SpanStart || !string.IsNullOrWhiteSpace(text[lineStart..close]))
        {
            return null;
        }

        return lineStart;
    }

    // The indentation of the last statement, or one level deeper than the closing brace when the body is empty.
    private static string IndentOf(string text, BlockSyntax body)
    {
        if (body.Statements.Count > 0)
        {
            var start = body.Statements[^1].SpanStart;
            var lineStart = text.LastIndexOf('\n', Math.Max(0, start - 1)) + 1;
            return Leading(text, lineStart);
        }

        var braceLine = text.LastIndexOf('\n', Math.Max(0, body.CloseBraceToken.SpanStart - 1)) + 1;
        return Leading(text, braceLine) + "    ";
    }

    private static string Leading(string text, int lineStart)
    {
        var end = lineStart;
        while (end < text.Length && text[end] is ' ' or '\t')
        {
            end++;
        }

        return text[lineStart..end];
    }
}
