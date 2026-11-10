using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Migrator;

/// <summary>The adapter reports entry point changes as rewrite notes.</summary>
[Trait("Category", "Migrator")]
public class EntryPointSourceRewriterTests
{
    private static readonly string[] _windows = ["WINDOWS", "DEBUG", "TRACE"];

    private const string _program =
        "namespace MyGame\n" +
        "{\n" +
        "#if WINDOWS || XBOX\n" +
        "    static class Program\n" +
        "    {\n" +
        "        static void Main(string[] args)\n" +
        "        {\n" +
        "            using (Game1 game = new Game1())\n" +
        "            {\n" +
        "                game.Run();\n" +
        "            }\n" +
        "        }\n" +
        "    }\n" +
        "#endif\n" +
        "}\n";

    private readonly EntryPointSourceRewriter _rewriter = new();

    private static SourceRewriteContext Context(string text) => new("Program.cs", text, _windows);

    [Fact]
    public void Rule_id_is_the_pseudo_id()
    {
        Assert.Equal("ENTRYPOINT", _rewriter.RuleId);
    }

    [Fact]
    public void Xna_template_is_unwrapped_with_a_note()
    {
        var context = Context(_program);

        var result = _rewriter.Rewrite(context);

        Assert.True(result.Changed(context));
        Assert.DoesNotContain("#if", result.Text);
        Assert.DoesNotContain("#endif", result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("ENTRYPOINT", note.RuleId);
        Assert.False(note.NeedsManualWork);
        Assert.Equal("Removed '#if WINDOWS || XBOX' around the entry point.", note.Message);
    }

    [Fact]
    public void Skipped_conditional_keeps_the_text_and_asks_for_manual_work()
    {
        var text = _program.Replace("#if WINDOWS || XBOX", "#if WINDOWS || MYSYMBOL");
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.False(result.Changed(context));
        Assert.Same(text, result.Text);
        var note = Assert.Single(result.Notes);
        Assert.True(note.NeedsManualWork);
        Assert.Contains("MYSYMBOL", note.Message);
    }

    [Theory]
    [InlineData("class A { }\n")]
    [InlineData("class P { static void Main() { } }\n")]
    public void No_entry_point_or_no_conditional_gives_no_note(string text)
    {
        var context = Context(text);

        var result = _rewriter.Rewrite(context);

        Assert.False(result.Changed(context));
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void Engine_runs_the_adapter_only_when_enabled()
    {
        var registry = new RewriterRegistry();
        registry.Register(_rewriter);
        var engine = new SourceRewriteEngine(registry);

        var off = engine.Rewrite("Program.cs", _program, _windows, new HashSet<string>());
        var on = engine.Rewrite("Program.cs", _program, _windows, new HashSet<string> { "ENTRYPOINT" });

        Assert.False(off.Changed);
        Assert.True(on.Changed);
        Assert.Equal(["ENTRYPOINT"], on.AppliedRuleIds);
    }

    [Fact]
    public void Null_context_throws()
    {
        Assert.Throws<ArgumentNullException>(() => _rewriter.Rewrite(null!));
    }
}
