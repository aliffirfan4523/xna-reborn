using XnaReborn.Flash.Analyzer.As3;
using XnaReborn.Flash.Analyzer.Reading;
using XnaReborn.Flash.Analyzer.Resolving;

namespace XnaReborn.Flash.Tests;

public class As3TypeResolverTests
{
    private const string _source = @"
package {
  public class Hero extends FlxSprite {
    public var speed:Number;
    public var lives:int;
    private var list:Vector.<FlxSprite>;
    public function get label():String { return ''; }
    public function ratio(a:int, b:int):Number { return a / b; }
    public function probe(count:int, total:uint, ratio:Number, name:String, any):void { var local:int = 1; }
  }
}";

    private static (As3TypeResolver Resolver, As3Scope Scope) Setup()
    {
        var unit = As3Parser.Parse(_source);
        var file = new FlashSourceFile("Hero.as", _source, unit, false);
        var cls = unit.Members.OfType<As3TypeDeclaration>().Single();
        var probe = cls.Members.OfType<As3Method>().Single(m => m.Name == "probe");
        return (new As3TypeResolver([file]), As3TypeResolver.ScopeFor(cls, probe.Parameters, probe.Body));
    }

    private static string? TypeOf(string expression)
    {
        var (resolver, scope) = Setup();
        var unit = As3Parser.Parse("package { class T { function f() { x = " + expression + "; } } }");
        var cls = (As3TypeDeclaration)unit.Members[0];
        var stmt = (As3ExpressionStatement)((As3Method)cls.Members[0]).Body!.Statements[0];
        return resolver.TypeOf(((As3Assignment)stmt.Expression).Value, scope);
    }

    [Theory]
    [InlineData("1", "int")]
    [InlineData("1.5", "Number")]
    [InlineData("0xFF", "int")]
    [InlineData("0xFFFFFFFF", "uint")]
    [InlineData("'s'", "String")]
    [InlineData("true", "Boolean")]
    [InlineData("count", "int")]
    [InlineData("total", "uint")]
    [InlineData("local", "int")]
    [InlineData("count + count", "int")]
    [InlineData("count + total", "int")]
    [InlineData("count / count", "Number")]
    [InlineData("count * ratio", "Number")]
    [InlineData("name + count", "String")]
    [InlineData("count > 1", "Boolean")]
    [InlineData("count >>> 1", "uint")]
    [InlineData("~count", "int")]
    [InlineData("speed", "Number")]
    [InlineData("this.lives", "int")]
    [InlineData("label", "String")]
    [InlineData("ratio(1, 2)", "Number")]
    [InlineData("list", "Vector.<FlxSprite>")]
    [InlineData("list[0]", "FlxSprite")]
    [InlineData("list.length", "uint")]
    [InlineData("new Hero()", "Hero")]
    [InlineData("int(ratio)", "int")]
    [InlineData("FlxG.width", "int")]
    [InlineData("FlxG.elapsed", "Number")]
    [InlineData("any", null)]
    [InlineData("unknownThing", null)]
    [InlineData("count ? 1 : 2.5", "Number")]
    public void Expressions_resolve_to_the_expected_type(string expression, string? expected)
    {
        Assert.Equal(expected, TypeOf(expression));
    }
}
