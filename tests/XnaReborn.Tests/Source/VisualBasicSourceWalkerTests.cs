using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Source;

/// <summary>The Visual Basic walker and the shared rule logic: names without case, project imports, aliases, calls without parentheses, and With blocks.</summary>
[Trait("Category", "Rules")]
public sealed class VisualBasicSourceWalkerTests
{
    private const string _fw = "Microsoft.Xna.Framework";
    private const string _gfx = "Microsoft.Xna.Framework.Graphics";

    private static Rule MakeRule(string id, RuleMatch match) =>
        new(id, "Title " + id, Severity.Warning, Classification.Manual, null, null, match, "why", "how", null);

    private static IReadOnlyList<SourceMatch> RunVb(string code, IReadOnlyList<string>? implicitImports, params Rule[] rules) =>
        SourceRuleMatcher.Match(VisualBasicSyntaxTree.ParseText(code), rules, implicitImports);

    private static IReadOnlyList<SourceMatch> RunCs(string code, params Rule[] rules) =>
        SourceRuleMatcher.Match(CSharpSyntaxTree.ParseText(code), rules);

    private static Rule Type(string name, string ns = _gfx) => MakeRule("TY", new TypeMatch(name, [ns]));

    private static Rule Member(string name, string? receiver = null, MemberMatchKind kind = MemberMatchKind.Any, ArgumentRange? arguments = null) =>
        MakeRule("ME", new MemberMatch(name, [_fw, _gfx], receiver, kind, arguments));

    [Fact]
    public void Type_is_found_with_a_file_import_and_with_different_casing()
    {
        var code = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Dim m As spriteblendmode\nEnd Class\n";

        var match = Assert.Single(RunVb(code, null, Type("SpriteBlendMode")));

        Assert.Equal(3, match.Line);
    }

    [Fact]
    public void Type_is_found_through_a_project_level_import_and_not_without_any_import()
    {
        const string code = "Class C\n    Dim m As SpriteBlendMode\nEnd Class\n";

        Assert.Single(RunVb(code, [_gfx], Type("SpriteBlendMode")));
        Assert.Empty(RunVb(code, null, Type("SpriteBlendMode")));
        Assert.Empty(RunVb(code, ["System"], Type("SpriteBlendMode")));
    }

    [Fact]
    public void A_user_type_with_the_same_name_and_no_gate_gives_no_finding()
    {
        const string code = "Class SpriteBlendMode\nEnd Class\nClass C\n    Dim m As SpriteBlendMode\nEnd Class\n";

        Assert.Empty(RunVb(code, null, Type("SpriteBlendMode")));
    }

    [Fact]
    public void Qualified_type_is_found_without_an_import()
    {
        const string code = "Class C\n    Dim m As Microsoft.Xna.Framework.Graphics.SpriteBlendMode\nEnd Class\n";

        Assert.Single(RunVb(code, null, Type("SpriteBlendMode")));
    }

    [Fact]
    public void Alias_qualified_type_is_found_and_an_alias_of_another_namespace_is_not()
    {
        var hit = "Imports Xna = Microsoft.Xna.Framework\nClass C\n    Dim m As Xna.Graphics.SpriteBlendMode\nEnd Class\n";
        var miss = "Imports Io = System.IO\nClass C\n    Dim m As Io.SpriteBlendMode\nEnd Class\n";

        Assert.Single(RunVb(hit, null, Type("SpriteBlendMode")));
        Assert.Empty(RunVb(miss, null, Type("SpriteBlendMode")));
    }

    [Fact]
    public void Csharp_alias_qualified_type_is_found_and_another_alias_is_not()
    {
        const string hit = "using Gfx = Microsoft.Xna.Framework.Graphics;\nclass C { Gfx.SpriteBlendMode m; }\n";
        const string miss = "using Io = System.IO;\nclass C { Io.SpriteBlendMode m; }\n";

        Assert.Single(RunCs(hit, Type("SpriteBlendMode")), m => m.Column > 1);
        Assert.Empty(RunCs(miss, Type("SpriteBlendMode")));
    }

    [Fact]
    public void Call_with_and_without_parentheses_is_an_invocation_with_the_right_argument_count()
    {
        const string code = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Sub M()\n        effect.Begin\n        effect.Begin()\n        effect.Begin(1)\n    End Sub\nEnd Class\n";

        var none = RunVb(code, null, Member("Begin", "effect", MemberMatchKind.Invocation, new ArgumentRange(0, 0)));
        var one = RunVb(code, null, Member("Begin", "effect", MemberMatchKind.Invocation, new ArgumentRange(1, 1)));

        Assert.Equal([4, 5], none.Select(m => m.Line));
        Assert.Equal([6], one.Select(m => m.Line));
    }

    [Fact]
    public void A_plain_access_in_expression_position_is_an_access_and_not_a_call()
    {
        const string code = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Sub M()\n        Dim t = effect.Begin\n    End Sub\nEnd Class\n";

        Assert.Empty(RunVb(code, null, Member("Begin", kind: MemberMatchKind.Invocation)));
        Assert.Single(RunVb(code, null, Member("Begin", kind: MemberMatchKind.Access)));
    }

    [Fact]
    public void Member_in_a_with_block_matches_by_name_and_not_with_a_receiver()
    {
        const string code = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Sub M()\n        With device.RenderState\n            .CullMode = 1\n        End With\n    End Sub\nEnd Class\n";

        Assert.Single(RunVb(code, null, Member("CullMode")));
        Assert.Empty(RunVb(code, null, Member("CullMode", "RenderState")));
        Assert.Single(RunVb(code, null, Member("RenderState", "device")));
    }

    [Fact]
    public void Receiver_of_an_indexed_call_is_the_invoked_name()
    {
        const string code = "Imports Microsoft.Xna.Framework.Graphics\nClass C\n    Sub M()\n        device.Vertices(0).SetSource(a, 0, 1)\n    End Sub\nEnd Class\n";

        Assert.Single(RunVb(code, null, Member("SetSource", "Vertices", MemberMatchKind.Invocation)));
    }

    [Fact]
    public void Namespace_rule_matches_imports_and_qualified_names_without_case()
    {
        const string code = "Imports microsoft.xna.framework.gamerservices\nClass C\n    Dim g As Microsoft.Xna.Framework.GamerServices.Guide\nEnd Class\n";
        var rule = MakeRule("NS", new NamespaceMatch("Microsoft.Xna.Framework.GamerServices", true));

        Assert.Equal([1, 3], RunVb(code, null, rule).Select(m => m.Line));
    }

    [Fact]
    public void My_namespace_is_found_and_a_member_called_my_something_is_not()
    {
        var rule = MakeRule("MY", new NamespaceMatch("My", true));

        Assert.Single(RunVb("Class C\n    Sub M()\n        My.Application.Log.WriteEntry(\"x\")\n    End Sub\nEnd Class\n", null, rule));
        Assert.Empty(RunVb("Class C\n    Private MyField As Integer\n    Sub M()\n        Me.MyField = 1\n    End Sub\nEnd Class\n", null, rule));
    }

    [Fact]
    public void Override_method_declaration_is_found_by_the_visual_basic_walker()
    {
        const string code = "Imports Microsoft.Xna.Framework\nClass G\n    Inherits Game\n    Protected Overrides Sub LoadGraphicsContent(ByVal all As Boolean)\n    End Sub\n    Sub Other()\n    End Sub\nEnd Class\n";
        var rule = MakeRule("MD", new MethodDeclarationMatch("loadgraphicscontent", [_fw], Overrides: true));

        Assert.Equal([4], RunVb(code, null, rule).Select(m => m.Line));
    }

    [Fact]
    public void Preprocessor_symbol_in_an_if_directive_is_found_even_in_an_inactive_branch()
    {
        const string code = "#If XBOX Then\nModule A\nEnd Module\n#End If\n";
        var rule = MakeRule("PP", new PreprocessorSymbolMatch(["xbox"]));

        Assert.Single(RunVb(code, null, rule));
    }
}
