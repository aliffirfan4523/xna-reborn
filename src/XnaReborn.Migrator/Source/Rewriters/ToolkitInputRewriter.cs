using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace XnaReborn.Migrator.Source.Rewriters;

/// <summary>
/// Replaces the Toolkit <c>KeyboardManager</c> (SDX025). The field and its <c>new KeyboardManager(this)</c> assignment are removed and
/// <c>keyboard.GetState()</c> becomes <c>Keyboard.GetState()</c>. When the manager is used in any other way (passed to a method, stored,
/// read through a property) nothing is changed for that field and a marker is added above the field. Mouse and touch managers are not rewritten.
/// </summary>
public sealed class ToolkitInputRewriter : ISourceRewriter
{
    /// <summary>The rule ID this rewriter handles.</summary>
    public const string Id = "SDX025";

    /// <summary>The message of the marker added when the manager is used in a way that cannot be rewritten.</summary>
    public const string MarkerMessage = "This KeyboardManager is used in a way that cannot be rewritten. MonoGame has the static Keyboard.GetState(); change the code that uses this manager.";

    private const string _managerType = "KeyboardManager";

    /// <inheritdoc />
    public string RuleId => Id;

    /// <inheritdoc />
    public SourceRewriteResult Rewrite(SourceRewriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var plan = Analyze(context.Text, context.OriginalSymbols);
        if (plan.Edits.Count == 0 && plan.Sites.Count == 0)
        {
            return SourceRewriteResult.Unchanged(context);
        }

        var text = ToolkitSyntax.Apply(context.Text, plan.Edits);
        var notes = plan.Notes.Select(n => new RewriteNote(Id, n, NeedsManualWork: false)).ToList();
        foreach (var site in Analyze(text, context.OriginalSymbols).Sites.OrderByDescending(s => s))
        {
            var marked = MarkerInserter.InsertMarker(text, site, Id, MarkerMessage, context.OriginalSymbols);
            if (!string.Equals(marked, text, StringComparison.Ordinal))
            {
                text = marked;
                notes.Add(new RewriteNote(Id, "Left a KeyboardManager in place with a marker: " + MarkerMessage, NeedsManualWork: true));
            }
        }

        return string.Equals(text, context.Text, StringComparison.Ordinal) && notes.Count == 0
            ? SourceRewriteResult.Unchanged(context)
            : new SourceRewriteResult(text, notes);
    }

    private static Changes Analyze(string text, IReadOnlyCollection<string> symbols)
    {
        var plan = new Changes([], [], []);
        var root = ToolkitSyntax.Parse(text, symbols);
        var input = ToolkitSyntax.Imports(root, ToolkitSyntax.MonoGameInput) ? string.Empty : ToolkitSyntax.MonoGameInput + ".";
        foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
            {
                if (IsManagerType(field.Declaration.Type) && field.Declaration.Variables.Count == 1)
                {
                    PlanField(text, type, field, input, plan);
                }
            }
        }

        return plan;
    }

    private static void PlanField(string text, TypeDeclarationSyntax type, FieldDeclarationSyntax field, string input, Changes plan)
    {
        var variable = field.Declaration.Variables[0];
        var name = variable.Identifier.ValueText;
        var edits = new List<TextEdit> { ToolkitSyntax.RemoveLines(text, field, includeLeadingComments: true) };
        var clean = true;
        foreach (var use in UsesOf(type, field, name))
        {
            // Unit is the whole use: `keyboard`, or `this.keyboard`.
            ExpressionSyntax unit = use.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } qualified && qualified.Name == use ? qualified : use;
            if (unit.Parent is AssignmentExpressionSyntax assignment
                && assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.Left == unit
                && assignment.Right is ObjectCreationExpressionSyntax creation
                && IsManagerType(creation.Type)
                && assignment.Parent is ExpressionStatementSyntax statement)
            {
                edits.Add(ToolkitSyntax.RemoveLines(text, statement, includeLeadingComments: false));
            }
            else if (unit.Parent is MemberAccessExpressionSyntax getState
                && getState.Expression == unit
                && getState.Name.Identifier.ValueText == "GetState"
                && getState.Parent is InvocationExpressionSyntax call
                && call.Expression == getState
                && call.ArgumentList.Arguments.Count == 0)
            {
                edits.Add(new TextEdit(call.SpanStart, call.Span.End, input + "Keyboard.GetState()"));
            }
            else
            {
                clean = false;
                break;
            }
        }

        if (clean)
        {
            plan.Edits.AddRange(edits);
            plan.Notes.Add($"Removed the KeyboardManager field '{name}' and replaced its GetState() calls with Keyboard.GetState().");
        }
        else
        {
            plan.Sites.Add(field.SpanStart);
        }
    }

    // Every identifier in the type that names the field. Local variables and parameters of the same name hide the field, so they are skipped.
    private static IEnumerable<IdentifierNameSyntax> UsesOf(TypeDeclarationSyntax type, FieldDeclarationSyntax field, string name)
    {
        foreach (var id in type.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (id.Identifier.ValueText != name || id.Ancestors().Contains(field))
            {
                continue;
            }

            // `x.keyboard` is a member of another object, not this field.
            if (id.Parent is MemberAccessExpressionSyntax access && access.Name == id && access.Expression is not ThisExpressionSyntax)
            {
                continue;
            }

            // `this.keyboard` is the field even when a local of that name is in scope.
            var thisQualified = id.Parent is MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax };
            if (!thisQualified && IsShadowed(id, name))
            {
                continue;
            }

            yield return id;
        }
    }

    private static bool IsShadowed(IdentifierNameSyntax id, string name)
    {
        foreach (var ancestor in id.Ancestors())
        {
            switch (ancestor)
            {
                case BaseMethodDeclarationSyntax method when method.ParameterList.Parameters.Any(p => p.Identifier.ValueText == name):
                    return true;
                case BlockSyntax block when block.Statements.OfType<LocalDeclarationStatementSyntax>().Any(l => l.Declaration.Variables.Any(v => v.Identifier.ValueText == name)):
                    return true;
                case TypeDeclarationSyntax:
                    return false;
            }
        }

        return false;
    }

    private static bool IsManagerType(TypeSyntax? type) => type switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText == _managerType,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText == _managerType,
        _ => false,
    };

    private sealed record Changes(List<TextEdit> Edits, List<int> Sites, List<string> Notes);
}
