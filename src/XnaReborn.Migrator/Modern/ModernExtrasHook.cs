using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XnaReborn.Migrator.Source;

namespace XnaReborn.Migrator.Modern;

/// <summary>What <see cref="ModernExtrasHook.Apply"/> did to one file.</summary>
/// <param name="Text">The new text, or the input text when nothing was added.</param>
/// <param name="Applied">True when the hook line was added.</param>
/// <param name="Note">A note for a person when the file has the game's entry point but the hook could not be added, otherwise null.</param>
public sealed record ModernHookResult(string Text, bool Applied, string? Note);

/// <summary>
/// Adds the one hook line, <c>XnaReborn.Modern.ModernExtras.Attach(game);</c>, above <c>game.Run();</c> in the entry point.
/// Only whole lines are inserted, so every other byte of the file stays as it was.
/// </summary>
public static class ModernExtrasHook
{
    /// <summary>The pseudo rule id of the hook in markers and in the manifest.</summary>
    public const string Id = "MODERN";

    /// <summary>Adds the hook to the first <c>name.Run();</c> statement inside <c>Main</c>.</summary>
    /// <param name="text">Text of one .cs file.</param>
    /// <param name="preprocessorSymbols">Symbols defined when parsing, such as WINDOWS. Null means none.</param>
    /// <returns>
    /// The input text, unchanged and without a note, when the file has no <c>Main</c> or no <c>Run()</c> call in it or already has the hook.
    /// A note and the input text when the call has a shape that cannot take a line above it, such as <c>new Game1().Run();</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static ModernHookResult Apply(string text, IReadOnlyCollection<string>? preprocessorSymbols)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Contains(ModernExtrasGenerator.AttachCall, StringComparison.Ordinal))
        {
            return new ModernHookResult(text, false, null);
        }

        var tree = CSharpSyntaxTree.ParseText(text, new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: preprocessorSymbols));
        var root = tree.GetRoot();
        var run = root.DescendantNodes()
            .OfType<ExpressionStatementSyntax>()
            .FirstOrDefault(s => IsRunCall(s) && IsInMain(s));
        if (run is null)
        {
            return new ModernHookResult(text, false, null);
        }

        var call = (InvocationExpressionSyntax)run.Expression;
        var access = (MemberAccessExpressionSyntax)call.Expression;
        if (access.Expression is not IdentifierNameSyntax variable)
        {
            return Skipped(text, "the game is started by an expression such as 'new Game1().Run()'. Add XnaReborn.Modern.ModernExtras.Attach(game); by hand before Run.");
        }

        var lineStart = LineStart(text, run.SpanStart);
        var indent = text[lineStart..run.SpanStart];
        if (run.Parent is not (BlockSyntax or CompilationUnitSyntax or GlobalStatementSyntax) || indent.Any(c => c != ' ' && c != '\t'))
        {
            return Skipped(text, "the Run() call shares its line or is not directly in a block. Add XnaReborn.Modern.ModernExtras.Attach(" + variable.Identifier.ValueText + "); by hand before Run.");
        }

        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = indent + MarkerInserter.Prefix + Id + ": opt-in window extras hook added by XNA Reborn; remove this and the next line to turn it off." + newLine
            + indent + ModernExtrasGenerator.AttachCall + "(" + variable.Identifier.ValueText + ");" + newLine;
        return new ModernHookResult(text.Insert(lineStart, lines), true, null);
    }

    private static ModernHookResult Skipped(string text, string reason) =>
        new(text, false, "The window extras hook was not added: " + reason);

    private static bool IsRunCall(ExpressionStatementSyntax statement) =>
        statement.Expression is InvocationExpressionSyntax
        {
            ArgumentList.Arguments.Count: 0,
            Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Run" },
        };

    private static bool IsInMain(SyntaxNode node)
    {
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            if (parent is MethodDeclarationSyntax method)
            {
                return method.Identifier.ValueText == "Main" && method.Modifiers.Any(SyntaxKind.StaticKeyword);
            }

            if (parent is LocalFunctionStatementSyntax or LambdaExpressionSyntax)
            {
                return false;
            }
        }

        // Top-level statements: no enclosing method.
        return node.Ancestors().OfType<GlobalStatementSyntax>().Any();
    }

    private static int LineStart(string text, int position)
    {
        var i = position;
        while (i > 0 && text[i - 1] != '\n' && text[i - 1] != '\r')
        {
            i--;
        }

        return i;
    }
}
