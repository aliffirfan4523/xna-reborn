using Microsoft.CodeAnalysis.CSharp;
using XnaReborn.Analyzer.Source;
using XnaReborn.Core.Models;
using XnaReborn.Core.Models.Rules;

namespace XnaReborn.Tests.Source;

/// <summary>Syntax-only matching of namespace, type, and member rules by <see cref="SourceRuleMatcher"/>.</summary>
[Trait("Category", "Rules")]
public sealed class SourceRuleMatcherTests
{
    private const string _gs = "Microsoft.Xna.Framework.GamerServices";
    private const string _fw = "Microsoft.Xna.Framework";

    private static Rule MakeRule(string id, RuleMatch match) =>
        new(id, "Title " + id, Severity.Warning, Classification.Manual, null, null, match, "why", "how", null);

    private static Rule Ns(string ns, bool includeChildren = true, string id = "NS") =>
        MakeRule(id, new NamespaceMatch(ns, includeChildren));

    private static Rule Type(string name, string[] namespaces, string id = "TY") =>
        MakeRule(id, new TypeMatch(name, namespaces));

    private static Rule Member(
        string name,
        string[] namespaces,
        string? receiver = null,
        MemberMatchKind kind = MemberMatchKind.Any,
        ArgumentRange? arguments = null,
        string id = "ME") =>
        MakeRule(id, new MemberMatch(name, namespaces, receiver, kind, arguments));

    private static IReadOnlyList<SourceMatch> Run(string code, params Rule[] rules) =>
        SourceRuleMatcher.Match(CSharpSyntaxTree.ParseText(code), rules);

    private static string[] Where(IReadOnlyList<SourceMatch> matches) =>
        matches.Select(m => $"{m.Line}:{m.Column}").ToArray();

    // ---------- namespace rules ----------
    [Fact]
    public void Namespace_using_directive_matches_at_the_directive()
    {
        var matches = Run($"using System;\nusing {_gs};\nclass C {{ }}\n", Ns(_gs));

        var match = Assert.Single(matches);
        Assert.Equal(2, match.Line);
        Assert.Equal(1, match.Column);
        Assert.Equal($"using {_gs};", match.Snippet);
        Assert.Equal("NS", match.Rule.Id);
    }

    [Fact]
    public void Namespace_qualified_use_in_code_matches_at_the_chain_start()
    {
        const string code = """
            class C
            {
                Microsoft.Xna.Framework.GamerServices.Guide guide;
            }
            """;

        var match = Assert.Single(Run(code, Ns(_gs)));

        Assert.Equal(3, match.Line);
        Assert.Equal(5, match.Column);
    }

    [Fact]
    public void Namespace_qualified_use_in_an_expression_matches_once_per_chain()
    {
        const string code = """
            class C
            {
                void M() { var c = Microsoft.Xna.Framework.Graphics.Color.Red; }
            }
            """;

        var match = Assert.Single(Run(code, Ns(_fw + ".Graphics")));

        Assert.Equal(3, match.Line);
        Assert.Equal(24, match.Column);
    }

    [Fact]
    public void Namespace_global_qualified_use_matches()
    {
        var matches = Run("class C { global::Microsoft.Xna.Framework.Game g; }", Ns(_fw));

        Assert.Single(matches);
    }

    [Fact]
    public void Namespace_child_matches_when_children_are_included()
    {
        var matches = Run($"using {_fw}.Graphics;\n", Ns(_fw, includeChildren: true));

        Assert.Single(matches);
    }

    [Fact]
    public void Namespace_child_does_not_match_when_children_are_excluded()
    {
        var matches = Run($"using {_fw}.Graphics;\nclass C {{ {_fw}.Graphics.Color c; }}\n", Ns(_fw, includeChildren: false));

        Assert.Empty(matches);
    }

    [Fact]
    public void Namespace_exact_name_matches_when_children_are_excluded()
    {
        var matches = Run($"using {_fw};\nclass C {{ {_fw}.Game g; }}\nclass D {{ {_fw} x; }}\n", Ns(_fw, includeChildren: false));

        Assert.Equal(["1:1", "3:11"], Where(matches));
    }

    [Fact]
    public void Namespace_name_sharing_only_a_prefix_does_not_match()
    {
        var matches = Run($"using {_fw}Extras;\nclass C {{ {_fw}Extras.Foo f; }}\n", Ns(_fw));

        Assert.Empty(matches);
    }

    [Fact]
    public void Namespace_rule_without_the_namespace_in_the_file_does_not_match()
    {
        var matches = Run("using System;\nusing System.Linq;\nclass C { System.Text.StringBuilder sb; }\n", Ns(_gs));

        Assert.Empty(matches);
    }

    [Fact]
    public void Namespace_using_inside_a_namespace_block_matches()
    {
        const string code = """
            namespace Game
            {
                using Microsoft.Xna.Framework.GamerServices;

                class C { }
            }
            """;

        var match = Assert.Single(Run(code, Ns(_gs)));

        Assert.Equal(3, match.Line);
        Assert.Equal(5, match.Column);
    }

    [Fact]
    public void Namespace_using_inside_a_file_scoped_namespace_matches()
    {
        var matches = Run($"namespace Game;\nusing {_gs};\nclass C {{ }}\n", Ns(_gs));

        Assert.Equal(["2:1"], Where(matches));
    }

    [Fact]
    public void Namespace_alias_using_matches_on_its_target()
    {
        var matches = Run($"using _gs = {_gs};\nclass C {{ _gs.Guide g; }}\n", Ns(_gs));

        var match = Assert.Single(matches);
        Assert.Equal(1, match.Line);
    }

    [Fact]
    public void Namespace_alias_using_of_another_namespace_does_not_match()
    {
        var matches = Run("using Io = System.IO;\nclass C { Io.File f; }\n", Ns(_gs));

        Assert.Empty(matches);
    }

    [Fact]
    public void Namespace_declaration_with_the_same_name_is_not_a_use()
    {
        var matches = Run($"namespace {_gs}.Mine\n{{\n    class C {{ }}\n}}\n", Ns(_gs));

        Assert.Empty(matches);
    }

    [Fact]
    public void Namespace_using_directive_is_reported_once()
    {
        var matches = Run($"using {_gs};\n", Ns(_gs), Ns(_fw, id: "NS2"));

        Assert.Equal(["NS", "NS2"], matches.Select(m => m.Rule.Id).ToArray());
        Assert.All(matches, m => Assert.Equal(1, m.Line));
    }

    // ---------- type rules ----------
    [Fact]
    public void Type_use_after_importing_the_namespace_matches()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void M() { Guide.ShowSignIn(1, false); }
            }
            """;

        var match = Assert.Single(Run(code, Type("Guide", [_gs])));

        Assert.Equal(5, match.Line);
        Assert.Equal(16, match.Column);
        Assert.Equal("TY", match.Rule.Id);
    }

    [Fact]
    public void Type_qualified_use_matches_without_an_import()
    {
        const string code = "class C { Microsoft.Xna.Framework.GamerServices.Guide g; }";

        var match = Assert.Single(Run(code, Type("Guide", [_gs])));

        Assert.Equal(1, match.Line);
        Assert.Equal(49, match.Column);
    }

    [Fact]
    public void Type_qualified_use_in_an_expression_matches_without_an_import()
    {
        var matches = Run($"class C {{ bool V => {_gs}.Guide.IsVisible; }}", Type("Guide", [_gs]));

        Assert.Single(matches);
    }

    [Fact]
    public void Type_generic_name_matches()
    {
        const string code = """
            using Microsoft.Xna.Framework.Content;

            class C
            {
                void M(ContentManager content) { var a = content.Load<Texture2D>("a"); var b = new Cache<int>(); }
            }
            """;

        var matches = Run(code, Type("Cache", [_fw + ".Content"]));

        var match = Assert.Single(matches);
        Assert.Equal("TY", match.Rule.Id);
        Assert.Equal(5, match.Line);
    }

    [Fact]
    public void Type_name_used_as_a_type_argument_matches()
    {
        var matches = Run($"using {_fw}.Graphics;\nclass C {{ System.Collections.Generic.List<Texture2D> l; }}\n", Type("Texture2D", [_fw + ".Graphics"]));

        Assert.Equal(["2:43"], Where(matches));
    }

    [Fact]
    public void Type_same_name_without_the_import_does_not_match()
    {
        const string code = """
            using System;

            class C
            {
                Guide g;
                void M() { Guide.Show(); }
            }
            """;

        Assert.Empty(Run(code, Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_import_of_a_different_namespace_does_not_match()
    {
        Assert.Empty(Run($"using {_fw}.Graphics;\nclass C {{ Guide g; }}\n", Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_right_hand_name_of_a_non_namespace_does_not_match_even_with_the_import()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void M(Services foo) { var v = foo.Guide; foo.Guide = null; Other.Guide.Run(); }
            }
            """;

        Assert.Empty(Run(code, Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_right_hand_name_after_a_different_namespace_does_not_match()
    {
        Assert.Empty(Run($"using {_gs};\nclass C {{ {_fw}.Graphics.Guide g; }}\n", Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_member_access_through_this_does_not_match()
    {
        Assert.Empty(Run($"using {_gs};\nclass C {{ object Guide; object M() => this.Guide; }}\n", Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_conditional_access_does_not_match()
    {
        Assert.Empty(Run($"using {_gs};\nclass C {{ object M(C c) => c?.Guide; object Guide; }}\n", Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_declarations_named_like_the_type_do_not_match()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            namespace Game.Guide
            {
                class Guide { }

                class C
                {
                    int Guide;
                    int Prop { get; set; }
                    void Guide2() { int Guide = 1; }
                    void M(int Guide) { Run(Guide: 1); var o = new { Guide = 2 }; }
                    void Run(int Guide) { }
                }

                enum E { Guide }
                delegate void Guide3();
            }
            """;

        Assert.Empty(Run(code, Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_object_initializer_member_does_not_match()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                object M() => new Holder { Guide = 1 };
            }
            """;

        Assert.Empty(Run(code, Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_name_inside_a_using_directive_does_not_match()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;
            using Alias = Other.Guide;

            class C { }
            """;

        Assert.Empty(Run(code, Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_import_through_an_alias_counts_as_an_import()
    {
        var matches = Run($"using _gs = {_gs};\nclass C {{ Guide g; }}\n", Type("Guide", [_gs]));

        Assert.Equal(["2:11"], Where(matches));
    }

    [Fact]
    public void Type_using_static_does_not_count_as_an_import()
    {
        Assert.Empty(Run($"using static {_gs}.Helpers;\nclass C {{ Guide g; }}\n", Type("Guide", [_gs])));
    }

    [Fact]
    public void Type_any_of_several_namespaces_enables_the_match()
    {
        var rule = Type("Rectangle", [_fw, "Microsoft.Xna.Framework.Graphics"]);

        Assert.Single(Run($"using {_fw};\nclass C {{ Rectangle r; }}\n", rule));
        Assert.Single(Run($"using {_fw}.Graphics;\nclass C {{ Rectangle r; }}\n", rule));
        Assert.Single(Run($"class C {{ {_fw}.Rectangle r; }}\n", rule));
        Assert.Single(Run($"class C {{ {_fw}.Graphics.Rectangle r; }}\n", rule));
    }

    [Fact]
    public void Type_import_of_a_child_namespace_does_not_enable_a_parent_namespace_rule()
    {
        Assert.Empty(Run($"using {_fw}.Graphics;\nclass C {{ Game g; }}\n", Type("Game", [_fw])));
    }

    [Fact]
    public void Type_name_comparison_is_case_sensitive()
    {
        Assert.Empty(Run($"using {_gs};\nclass C {{ guide g; GUIDE h; }}\n", Type("Guide", [_gs])));
    }

    // ---------- member rules ----------
    [Fact]
    public void Member_invocation_and_access_are_told_apart_by_kind()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void M()
                {
                    Guide.ShowSignIn(1, false);
                    var v = Guide.IsVisible;
                    var f = Guide.ShowSignIn;
                }
            }
            """;
        var ns = new[] { _gs };

        var invocations = Run(code, Member("ShowSignIn", ns, kind: MemberMatchKind.Invocation));
        var accesses = Run(code, Member("ShowSignIn", ns, kind: MemberMatchKind.Access));
        var any = Run(code, Member("ShowSignIn", ns, kind: MemberMatchKind.Any));
        var property = Run(code, Member("IsVisible", ns, kind: MemberMatchKind.Access));
        var propertyAsCall = Run(code, Member("IsVisible", ns, kind: MemberMatchKind.Invocation));

        Assert.Equal(["7:9"], Where(invocations));
        Assert.Equal(["9:17"], Where(accesses));
        Assert.Equal(["7:9", "9:17"], Where(any));
        Assert.Equal(["8:17"], Where(property));
        Assert.Empty(propertyAsCall);
    }

    [Fact]
    public void Member_match_is_reported_at_the_start_of_the_receiver()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void M() { this.guide.Show(); }
            }
            """;

        var match = Assert.Single(Run(code, Member("Show", [_gs])));

        Assert.Equal(5, match.Line);
        Assert.Equal(16, match.Column);
    }

    [Fact]
    public void Member_receiver_matches_and_mismatches()
    {
        const string code = """
            using Microsoft.Xna.Framework.Graphics;

            class C
            {
                void M(GraphicsDeviceManager graphics, Other other)
                {
                    graphics.ApplyChanges();
                    other.ApplyChanges();
                    Graphics.ApplyChanges();
                }
            }
            """;

        var matches = Run(code, Member("ApplyChanges", [_fw + ".Graphics"], receiver: "graphics"));

        Assert.Equal(["7:9"], Where(matches));
    }

    [Fact]
    public void Member_receiver_comparison_is_case_sensitive()
    {
        var matches = Run($"using {_fw};\nclass C {{ void M() {{ Graphics.Reset(); }} }}\n", Member("Reset", [_fw], receiver: "graphics"));

        Assert.Empty(matches);
    }

    [Fact]
    public void Member_argument_count_within_the_range_matches()
    {
        const string code = """
            using Microsoft.Xna.Framework.Graphics;

            class C
            {
                void M(SpriteBatch sb)
                {
                    sb.Begin();
                    sb.Begin(a);
                    sb.Begin(a, b);
                    sb.Begin(a, b, c);
                }
            }
            """;
        var ns = new[] { _fw + ".Graphics" };

        var both = Run(code, Member("Begin", ns, arguments: new ArgumentRange(1, 2)));
        var minOnly = Run(code, Member("Begin", ns, arguments: new ArgumentRange(Min: 2)));
        var maxOnly = Run(code, Member("Begin", ns, arguments: new ArgumentRange(Max: 1)));
        var exact = Run(code, Member("Begin", ns, arguments: new ArgumentRange(0, 0)));

        Assert.Equal(["8:9", "9:9"], Where(both));
        Assert.Equal(["9:9", "10:9"], Where(minOnly));
        Assert.Equal(["7:9", "8:9"], Where(maxOnly));
        Assert.Equal(["7:9"], Where(exact));
    }

    [Fact]
    public void Member_argument_count_outside_the_range_does_not_match()
    {
        const string code = """
            using Microsoft.Xna.Framework.Graphics;

            class C
            {
                void M(SpriteBatch sb) { sb.Begin(); sb.Begin(a, b, c, d); }
            }
            """;

        Assert.Empty(Run(code, Member("Begin", [_fw + ".Graphics"], arguments: new ArgumentRange(1, 3))));
    }

    [Fact]
    public void Member_rule_with_arguments_never_matches_a_plain_access()
    {
        var code = $"using {_fw}.Graphics;\nclass C {{ void M(SpriteBatch sb) {{ var f = sb.Begin; }} }}\n";

        Assert.Empty(Run(code, Member("Begin", [_fw + ".Graphics"], arguments: new ArgumentRange(0, 5))));
        Assert.Single(Run(code, Member("Begin", [_fw + ".Graphics"])));
    }

    [Fact]
    public void Member_enum_style_access_matches()
    {
        const string code = """
            using Microsoft.Xna.Framework.Graphics;

            class C
            {
                void M(SpriteBatch spriteBatch)
                {
                    spriteBatch.Begin(SpriteBlendMode.AlphaBlend);
                    spriteBatch.Begin(
                        SpriteBlendMode.Additive,
                        SpriteSortMode.Deferred);
                }
            }
            """;
        var ns = new[] { _fw + ".Graphics" };

        var matches = Run(code, Member("AlphaBlend", ns, receiver: "SpriteBlendMode", kind: MemberMatchKind.Access));
        var wrongReceiver = Run(code, Member("AlphaBlend", ns, receiver: "BlendState"));

        var match = Assert.Single(matches);
        Assert.Equal(7, match.Line);
        Assert.Equal(27, match.Column);
        Assert.Empty(wrongReceiver);
    }

    [Fact]
    public void Member_name_present_but_namespace_not_imported_does_not_match()
    {
        const string code = """
            using System;

            class C
            {
                void M(Guide Guide) { Guide.ShowSignIn(1, false); var v = Guide.IsVisible; }
            }
            """;

        Assert.Empty(Run(code, Member("ShowSignIn", [_gs]), Member("IsVisible", [_gs], id: "VIS")));
    }

    [Fact]
    public void Member_qualification_substitutes_for_the_import()
    {
        var matches = Run($"class C {{ void M() {{ {_gs}.Guide.ShowSignIn(1, false); }} }}", Member("ShowSignIn", [_gs]));

        Assert.Equal(["1:22"], Where(matches));
    }

    [Fact]
    public void Member_qualification_with_a_different_namespace_does_not_match()
    {
        var matches = Run($"class C {{ void M() {{ {_fw}.Graphics.Guide.ShowSignIn(1, false); }} }}", Member("ShowSignIn", [_gs]));

        Assert.Empty(matches);
    }

    [Fact]
    public void Member_import_through_an_alias_counts()
    {
        var matches = Run($"using _gs = {_gs};\nclass C {{ void M() {{ Guide.ShowSignIn(1, false); }} }}", Member("ShowSignIn", [_gs]));

        Assert.Equal(["2:22"], Where(matches));
    }

    [Fact]
    public void Member_receiver_of_a_chain_is_the_identifier_left_of_the_dot()
    {
        const string code = """
            using Microsoft.Xna.Framework.Graphics;

            class C
            {
                void M()
                {
                    a.b.Name();
                }
            }
            """;
        var ns = new[] { _fw + ".Graphics" };

        Assert.Single(Run(code, Member("Name", ns, receiver: "b")));
        Assert.Empty(Run(code, Member("Name", ns, receiver: "a")));
        Assert.Single(Run(code, Member("Name", ns)));
    }

    [Fact]
    public void Member_chain_reports_each_access_with_its_own_name()
    {
        const string code = """
            using Microsoft.Xna.Framework.Graphics;

            class C
            {
                void M() { a.b.Name(); }
            }
            """;
        var ns = new[] { _fw + ".Graphics" };

        var inner = Run(code, Member("b", ns, receiver: "a", kind: MemberMatchKind.Access));

        Assert.Equal(["5:16"], Where(inner));
    }

    [Fact]
    public void Member_receiver_through_this_is_the_field_name()
    {
        var code = $"using {_gs};\nclass C {{ void M() {{ this.guide.Show(); this.Show(); }} }}\n";

        Assert.Single(Run(code, Member("Show", [_gs], receiver: "guide")));
        Assert.Single(Run(code, Member("Show", [_gs], receiver: "guide", kind: MemberMatchKind.Invocation)));
        Assert.Equal(2, Run(code, Member("Show", [_gs])).Count);
        Assert.Empty(Run(code, Member("Show", [_gs], receiver: "this")));
    }

    [Fact]
    public void Member_call_result_receiver_does_not_match_a_receiver_rule()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void M()
                {
                    GetGuide().Show();
                    items[0].Show();
                    (guide).Show();
                    new Guide().Show();
                    guide.Show();
                }
            }
            """;

        var withReceiver = Run(code, Member("Show", [_gs], receiver: "guide"));
        var withoutReceiver = Run(code, Member("Show", [_gs]));

        Assert.Equal(["11:9"], Where(withReceiver));
        Assert.Equal(["7:9", "8:9", "9:9", "10:9", "11:9"], Where(withoutReceiver));
    }

    [Fact]
    public void Member_generic_method_name_matches()
    {
        const string code = """
            using Microsoft.Xna.Framework.Content;

            class C
            {
                void M(ContentManager content) { content.Load<Texture2D>("a"); }
            }
            """;

        var matches = Run(code, Member("Load", [_fw + ".Content"], receiver: "content", kind: MemberMatchKind.Invocation, arguments: new ArgumentRange(1, 1)));

        Assert.Equal(["5:38"], Where(matches));
    }

    [Fact]
    public void Member_name_is_case_sensitive()
    {
        Assert.Empty(Run($"using {_gs};\nclass C {{ void M() {{ Guide.showsignin(1, false); }} }}", Member("ShowSignIn", [_gs])));
    }

    [Fact]
    public void Member_name_in_a_declaration_does_not_match()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void ShowSignIn(int a, bool b) { }
                int IsVisible;
            }
            """;

        Assert.Empty(Run(code, Member("ShowSignIn", [_gs]), Member("IsVisible", [_gs], id: "VIS")));
    }

    // ---------- positions, snippets, ordering ----------
    [Fact]
    public void Line_and_column_are_one_based_and_count_from_the_node_start()
    {
        const string code = """
            using System;
            using Microsoft.Xna.Framework.GamerServices;

            namespace Game
            {
                class C
                {
                    void M()
                    {
                        if (!Guide.IsVisible) { }
                    }
                }
            }
            """;

        var matches = Run(code, Ns(_gs), Type("Guide", [_gs], id: "TY"), Member("IsVisible", [_gs]));

        Assert.Equal(["2:1", "10:18", "10:18"], Where(matches));
        Assert.Equal(["NS", "TY", "ME"], matches.Select(m => m.Rule.Id).ToArray());
    }

    [Fact]
    public void First_character_of_the_file_is_line_1_column_1()
    {
        var matches = Run($"using {_gs};", Ns(_gs));

        Assert.Equal(["1:1"], Where(matches));
    }

    [Fact]
    public void Snippet_is_the_trimmed_line_of_the_match_start()
    {
        const string code = "class C\n{\n    void M()\n    {\n  \t  Guide.ShowSignIn(\n            1,\n            false);   \n    }\n}\n";

        var match = Assert.Single(Run($"using {_gs};\n" + code, Member("ShowSignIn", [_gs])));

        Assert.Equal("Guide.ShowSignIn(", match.Snippet);
        Assert.Equal(6, match.Line);
    }

    [Fact]
    public void Snippet_handles_windows_line_endings()
    {
        var match = Assert.Single(Run($"using {_gs};\r\nclass C {{ }}\r\n", Ns(_gs)));

        Assert.Equal($"using {_gs};", match.Snippet);
    }

    [Fact]
    public void Snippet_is_capped_at_200_characters()
    {
        var padding = new string('x', 300);
        var code = $"using {_gs};\nclass C {{ string s = \"{padding}\"; Guide g; }}\n";

        var match = Assert.Single(Run(code, Type("Guide", [_gs])));

        Assert.Equal(200, match.Snippet.Length);
        Assert.StartsWith("class C { string s = \"xxx", match.Snippet);
    }

    [Fact]
    public void Snippet_of_exactly_200_characters_is_kept_whole()
    {
        const string start = "class C { Guide g; } // ";
        var line = start + new string('y', 200 - start.Length);
        var code = $"using {_gs};\n    {line}   \n";

        var match = Assert.Single(Run(code, Type("Guide", [_gs])));

        Assert.Equal(200, line.Length);
        Assert.Equal(line, match.Snippet);
    }

    [Fact]
    public void Matches_are_in_source_order_regardless_of_rule_order()
    {
        const string code = """
            using Microsoft.Xna.Framework.GamerServices;

            class C
            {
                void M()
                {
                    Guide.ShowSignIn(1, false);
                    var a = Guide.IsVisible;
                }
            }
            """;

        var matches = Run(
            code,
            Member("IsVisible", [_gs], id: "R3"),
            Type("Guide", [_gs], id: "R2"),
            Ns(_gs, id: "R1"));

        Assert.Equal(["1:1", "7:9", "8:17", "8:17"], Where(matches));
        Assert.Equal(["R1", "R2", "R3", "R2"], matches.Select(m => m.Rule.Id).ToArray());
    }

    [Fact]
    public void Matches_at_the_same_position_follow_rule_order()
    {
        var first = Type("Guide", [_gs], id: "A");
        var second = Member("IsVisible", [_gs], id: "B");
        var code = $"using {_gs};\nclass C {{ bool V => Guide.IsVisible; }}\n";

        Assert.Equal(["B", "A"], Run(code, second, first).Select(m => m.Rule.Id).ToArray());
        Assert.Equal(["A", "B"], Run(code, first, second).Select(m => m.Rule.Id).ToArray());
    }

    [Fact]
    public void Matches_have_no_duplicates()
    {
        var rule = Member("ShowSignIn", [_gs, _gs + "2", _fw]);
        var code = $"using {_gs};\nusing {_gs};\nclass C {{ void M() {{ {_gs}.Guide.ShowSignIn(1, false); }} }}\n";

        var matches = Run(code, rule, rule);

        Assert.Single(matches);
    }

    [Fact]
    public void Nested_chains_are_reported_once_for_a_namespace_rule()
    {
        var matches = Run($"class C {{ object o = {_gs}.Guide.Instance.Value.ToString(); }}", Ns(_fw));

        Assert.Single(matches);
    }

    [Fact]
    public void Other_match_types_in_the_rule_list_are_ignored()
    {
        var rules = new[]
        {
            MakeRule("REF", new ReferenceMatch("Microsoft.Xna.Framework")),
            MakeRule("PROP", new ProjectPropertyMatch("XnaFrameworkVersion")),
            MakeRule("ITEM", new ProjectItemMatch("Compile")),
            MakeRule("FILE", new FilePatternMatch("**/*.xap")),
            MakeRule("CONT", new ContentItemMatch(".xnb")),
            Ns(_gs),
        };

        var matches = Run($"using {_gs};\nclass C {{ }}\n", rules);

        Assert.Equal("NS", Assert.Single(matches).Rule.Id);
    }

    [Fact]
    public void Only_non_source_rules_produce_no_matches()
    {
        var matches = Run($"using {_gs};\n", MakeRule("REF", new ReferenceMatch(_gs)));

        Assert.Empty(matches);
    }

    [Fact]
    public void Empty_rule_list_and_empty_source_produce_no_matches()
    {
        Assert.Empty(Run($"using {_gs};\n"));
        Assert.Empty(Run(string.Empty, Ns(_gs), Type("Guide", [_gs]), Member("Show", [_gs])));
    }

    [Fact]
    public void Syntax_errors_do_not_stop_matching()
    {
        var matches = Run($"using {_gs};\nclass C {{ void M() {{ Guide.ShowSignIn(1, ; }}\n", Ns(_gs), Member("ShowSignIn", [_gs]));

        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void Inactive_preprocessor_code_is_not_matched()
    {
        var tree = CSharpSyntaxTree.ParseText(
            $"using {_gs};\n#if XBOX\nclass A {{ Guide g; }}\n#endif\nclass B {{ Guide g; }}\n",
            new CSharpParseOptions(preprocessorSymbols: []));

        var matches = SourceRuleMatcher.Match(tree, [Type("Guide", [_gs])]);

        Assert.Equal(["5:11"], Where(matches));
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var tree = CSharpSyntaxTree.ParseText("class C { }");

        Assert.Throws<ArgumentNullException>(() => SourceRuleMatcher.Match(null!, []));
        Assert.Throws<ArgumentNullException>(() => SourceRuleMatcher.Match(tree, null!));
    }

    [Fact]
    public void Real_world_snippet_from_the_gamerservices_fixture_matches_all_three_kinds()
    {
        const string code = """
            using System;
            using Microsoft.Xna.Framework;
            using Microsoft.Xna.Framework.GamerServices;

            namespace Fixture
            {
                public class Game1 : Game
                {
                    protected override void Update(GameTime gameTime)
                    {
                        if (!Guide.IsVisible)
                        {
                            Guide.ShowSignIn(1, false);
                        }
                    }
                }
            }
            """;

        var matches = Run(
            code,
            Ns(_gs, id: "NS"),
            Type("Guide", [_gs], id: "TY"),
            Member("ShowSignIn", [_gs], receiver: "Guide", kind: MemberMatchKind.Invocation, arguments: new ArgumentRange(2, 2), id: "ME"));

        Assert.Equal(["3:1", "11:18", "13:17", "13:17"], Where(matches));
        Assert.Equal(["NS", "TY", "TY", "ME"], matches.Select(m => m.Rule.Id).ToArray());
    }
}
