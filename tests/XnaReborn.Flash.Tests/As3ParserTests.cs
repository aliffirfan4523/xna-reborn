using XnaReborn.Flash.Analyzer.As3;

namespace XnaReborn.Flash.Tests;

public class As3ParserTests
{
    private static As3CompilationUnit Parse(string s) => As3Parser.Parse(s);

    private static As3TypeDeclaration Class(string body) =>
        Assert.IsType<As3TypeDeclaration>(Assert.Single(Parse("package { public class C { " + body + " } }").Members));

    private static As3Method Method(string body)
    {
        var cls = Class("public function f():void { " + body + " }");
        return Assert.IsType<As3Method>(Assert.Single(cls.Members));
    }

    [Theory]
    [MemberData(nameof(FlashFixtures.AsFileRows), MemberType = typeof(FlashFixtures))]
    public void Fixture_files_parse_without_diagnostics(string relative)
    {
        var unit = Parse(File.ReadAllText(Path.Combine(FlashFixtures.Root, relative)));
        Assert.Empty(unit.Diagnostics);
    }

    [Fact]
    public void Package_imports_class_and_metadata_are_read()
    {
        var unit = Parse("package a.b { import org.flixel.*; import x.Y; [SWF(width=\"640\", height=\"480\")] public class G extends FlxGame implements I, J { } }");
        Assert.Equal("a.b", unit.PackageName);
        Assert.Collection(unit.Imports, i => Assert.True(i.IsWildcard), i => Assert.Equal("x.Y", i.Name));
        var cls = Assert.IsType<As3TypeDeclaration>(Assert.Single(unit.Members));
        Assert.Equal("FlxGame", cls.BaseType?.Name);
        Assert.Equal(2, cls.Interfaces.Count);
        var swf = Assert.Single(cls.Metadata);
        Assert.Equal("640", swf.Get("width"));
    }

    [Fact]
    public void Members_modifiers_accessors_and_constructor_are_classified()
    {
        var cls = Class("[Embed(source=\"a.png\")] private var Img:Class; static public const K:int = 3; public function C(a:int, b:Number = 2, ...rest) {} public function get v():int { return 1; } public function set v(x:int):void {} override protected function u():void {}");
        var field = Assert.IsType<As3Field>(cls.Members[0]);
        Assert.Equal("a.png", Assert.Single(field.Metadata).Get("source"));
        var k = Assert.IsType<As3Field>(cls.Members[1]);
        Assert.True(k.IsConst);
        Assert.Equal(As3Modifier.Static | As3Modifier.Public, k.Modifiers);
        var ctor = Assert.IsType<As3Method>(cls.Members[2]);
        Assert.Equal(As3MethodKind.Constructor, ctor.Kind);
        Assert.True(ctor.Parameters[2].IsRest);
        Assert.NotNull(ctor.Parameters[1].Default);
        Assert.Equal(As3MethodKind.Getter, Assert.IsType<As3Method>(cls.Members[3]).Kind);
        Assert.Equal(As3MethodKind.Setter, Assert.IsType<As3Method>(cls.Members[4]).Kind);
        Assert.Equal(As3Modifier.Override | As3Modifier.Protected, Assert.IsType<As3Method>(cls.Members[5]).Modifiers);
    }

    [Fact]
    public void Vector_types_and_nested_generics_split_the_closing_angles()
    {
        var cls = Class("private var v:Vector.<Vector.<int>>; function f() { var x = new Vector.<FlxSprite>(3); }");
        var field = Assert.IsType<As3Field>(cls.Members[0]);
        Assert.Equal("Vector", field.Type?.Name);
        Assert.Equal("Vector", field.Type?.TypeArguments[0].Name);
        Assert.Equal("int", field.Type?.TypeArguments[0].TypeArguments[0].Name);
        Assert.Empty(Parse("package { class C { var v:Vector.<Vector.<int>>; } }").Diagnostics);
    }

    [Fact]
    public void Control_flow_statements_are_parsed()
    {
        var m = Method("for (var i:int = 0; i < 3; i++) { x(); } for each (var o:Object in list) y(); for (var k:String in obj) z(); while (a) b(); do { c(); } while (d); if (a) b(); else c(); switch (n) { case 1: p(); break; default: q(); } try { t(); } catch (e:Error) { } finally { f(); }");
        var kinds = m.Body!.Statements.Select(s => s.GetType().Name).ToList();
        Assert.Equal(
            ["As3ForStatement", "As3ForInStatement", "As3ForInStatement", "As3WhileStatement", "As3DoStatement", "As3IfStatement", "As3SwitchStatement", "As3TryStatement"],
            kinds);
        Assert.True(Assert.IsType<As3ForInStatement>(m.Body.Statements[1]).IsEach);
        Assert.False(Assert.IsType<As3ForInStatement>(m.Body.Statements[2]).IsEach);
    }

    [Fact]
    public void Automatic_semicolon_insertion_works_at_line_ends_and_before_a_brace()
    {
        var m = Method("var a:int = 1\nvar b:int = 2\nreturn\nb");
        Assert.Equal(4, m.Body!.Statements.Count);
        Assert.Null(Assert.IsType<As3ReturnStatement>(m.Body.Statements[2]).Value);
        Assert.Empty(Parse("package { class C { function f() { return 1 } } }").Diagnostics);
    }

    [Fact]
    public void Operator_precedence_and_associativity_follow_the_language()
    {
        var m = Method("a = b + c * d; x = y ? 1 : 2; n = !a && b || c; v = o as T; w = (p is Q);");
        var assign = Assert.IsType<As3Assignment>(Assert.IsType<As3ExpressionStatement>(m.Body!.Statements[0]).Expression);
        var plus = Assert.IsType<As3Binary>(assign.Value);
        Assert.Equal("+", plus.Operator);
        Assert.Equal("*", Assert.IsType<As3Binary>(plus.Right).Operator);
        Assert.IsType<As3Conditional>(Assert.IsType<As3Assignment>(Assert.IsType<As3ExpressionStatement>(m.Body.Statements[1]).Expression).Value);
        var or = Assert.IsType<As3Binary>(Assert.IsType<As3Assignment>(Assert.IsType<As3ExpressionStatement>(m.Body.Statements[2]).Expression).Value);
        Assert.Equal("||", or.Operator);
        Assert.Equal("as", Assert.IsType<As3Binary>(Assert.IsType<As3Assignment>(Assert.IsType<As3ExpressionStatement>(m.Body.Statements[3]).Expression).Value).Operator);
    }

    [Fact]
    public void Literals_and_function_expressions_are_parsed()
    {
        var m = Method("var o:Object = {a: 1, \"b\": [1, 2, 3], c: function(x:int):int { return x; }}; var r = /x/g;");
        var decl = Assert.IsType<As3VarStatement>(m.Body!.Statements[0]).Declarators[0];
        var obj = Assert.IsType<As3ObjectLiteral>(decl.Initializer);
        Assert.Equal(["a", "b", "c"], obj.Properties.Select(p => p.Key));
        Assert.IsType<As3ArrayLiteral>(obj.Properties[1].Value);
        Assert.IsType<As3FunctionExpression>(obj.Properties[2].Value);
        var regex = Assert.IsType<As3Literal>(Assert.IsType<As3VarStatement>(m.Body.Statements[1]).Declarators[0].Initializer);
        Assert.Equal(As3LiteralKind.Regex, regex.Kind);
    }

    [Fact]
    public void Unsupported_constructs_keep_their_text()
    {
        var m = Method("with (o) { x = 1; } var l = xml.child.(@id == 1); var v = <a><b/></a>; use namespace ns; include \"x.as\";");
        var with = Assert.IsType<As3UnsupportedStatement>(m.Body!.Statements[0]);
        Assert.Equal("with", with.Reason);
        Assert.StartsWith("with (o)", with.RawText);
        var e4x = Assert.IsType<As3UnsupportedExpression>(Assert.IsType<As3VarStatement>(m.Body.Statements[1]).Declarators[0].Initializer);
        Assert.Equal("e4x", e4x.Reason);
        Assert.IsType<As3UnsupportedExpression>(Assert.IsType<As3VarStatement>(m.Body.Statements[2]).Declarators[0].Initializer);
        Assert.Equal("namespace", Assert.IsType<As3UnsupportedStatement>(m.Body.Statements[3]).Reason);
        Assert.Equal("include", Assert.IsType<As3UnsupportedStatement>(m.Body.Statements[4]).Reason);
    }

    [Fact]
    public void A_syntax_error_is_recorded_and_parsing_continues()
    {
        var unit = Parse("package { class C { function f() { var = ; good(); } function g():void {} } }");
        Assert.NotEmpty(unit.Diagnostics);
        var cls = Assert.IsType<As3TypeDeclaration>(Assert.Single(unit.Members));
        Assert.Equal(2, cls.Members.Count);
        var f = Assert.IsType<As3Method>(cls.Members[0]);
        Assert.Contains(f.Body!.Statements, s => s is As3UnsupportedStatement { Reason: "syntax-error" });
        Assert.Contains(f.Body.Statements, s => s is As3ExpressionStatement);
    }

    [Fact]
    public void A_bad_member_does_not_hide_the_next_member()
    {
        var unit = Parse("package { class C { 12 garbage; function ok():void {} } }");
        Assert.NotEmpty(unit.Diagnostics);
        var cls = Assert.IsType<As3TypeDeclaration>(Assert.Single(unit.Members));
        Assert.Contains(cls.Members, m => m is As3Method { Name: "ok" });
    }

    [Fact]
    public void Deep_nesting_and_random_text_never_throw()
    {
        Parse("package { class C { function f() { x = " + new string('(', 5000) + "1" + new string(')', 5000) + "; } } }");
        var rng = new Random(7);
        var words = new[] { "package", "class", "function", "{", "}", "(", ")", ";", "var", "x", ":", "int", "=", "1", "if", "else", ".", "<", ">", "[", "]", "for", "in", "new", "\"s", "/", "@" };
        for (var i = 0; i < 300; i++)
        {
            var text = string.Join(' ', Enumerable.Range(0, rng.Next(1, 40)).Select(_ => words[rng.Next(words.Length)]));
            Parse(text);
        }
    }
}
