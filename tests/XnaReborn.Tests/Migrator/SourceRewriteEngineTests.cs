using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Migrator;

/// <summary>The engine runs the enabled rewriters in rule ID order and isolates failures.</summary>
[Trait("Category", "Migrator")]
public class SourceRewriteEngineTests
{
    private static readonly string[] _symbols = ["WINDOWS"];

    private sealed class Delegating(string ruleId, Func<SourceRewriteContext, SourceRewriteResult> rewrite) : ISourceRewriter
    {
        public string RuleId { get; } = ruleId;

        public SourceRewriteResult Rewrite(SourceRewriteContext context) => rewrite(context);
    }

    private static Delegating Append(string ruleId, string suffix, List<string>? log = null) =>
        new(ruleId, c =>
        {
            log?.Add(ruleId);
            return new SourceRewriteResult(c.Text + suffix, [new RewriteNote(ruleId, "added " + suffix, false)]);
        });

    private static SourceRewriteEngine Engine(params ISourceRewriter[] rewriters)
    {
        var registry = new RewriterRegistry();
        foreach (var rewriter in rewriters)
        {
            registry.Register(rewriter);
        }

        return new SourceRewriteEngine(registry);
    }

    private static HashSet<string> Ids(params string[] ids) => new(ids, StringComparer.Ordinal);

    [Fact]
    public void Rewriters_run_in_rule_id_order_on_the_previous_output()
    {
        var log = new List<string>();
        var engine = Engine(Append("B", "b", log), Append("C", "c", log), Append("A", "a", log));

        var result = engine.Rewrite("File.cs", "x", _symbols, Ids("A", "B", "C"));

        Assert.Equal("xabc", result.Text);
        Assert.True(result.Changed);
        Assert.Equal(["A", "B", "C"], log);
        Assert.Equal(["A", "B", "C"], result.AppliedRuleIds);
        Assert.Equal(["A", "B", "C"], result.Notes.Select(n => n.RuleId));
        Assert.Empty(result.Failures);
    }

    [Fact]
    public void Only_enabled_rewriters_run()
    {
        var log = new List<string>();
        var engine = Engine(Append("A", "a", log), Append("B", "b", log), Append("C", "c", log));

        var result = engine.Rewrite("File.cs", "x", _symbols, Ids("C", "A", "Z"));

        Assert.Equal("xac", result.Text);
        Assert.Equal(["A", "C"], log);
    }

    [Fact]
    public void Nothing_enabled_returns_the_input_string()
    {
        var engine = Engine(Append("A", "a"));
        var input = new string(['x', 'y']);

        var result = engine.Rewrite("File.cs", input, _symbols, Ids());

        Assert.Same(input, result.Text);
        Assert.False(result.Changed);
        Assert.Empty(result.AppliedRuleIds);
    }

    [Fact]
    public void Context_carries_path_symbols_and_current_text()
    {
        SourceRewriteContext? seen = null;
        var engine = Engine(new Delegating("A", c =>
        {
            seen = c;
            return SourceRewriteResult.Unchanged(c);
        }));

        engine.Rewrite("Sub/File.cs", "text", _symbols, Ids("A"));

        Assert.NotNull(seen);
        Assert.Equal("Sub/File.cs", seen.FilePath);
        Assert.Equal("text", seen.Text);
        Assert.Equal(_symbols, seen.OriginalSymbols);
    }

    [Fact]
    public void Equal_text_in_a_new_instance_returns_the_input_instance_and_applies_nothing()
    {
        var engine = Engine(new Delegating("A", c => new SourceRewriteResult(new string(c.Text.AsSpan()), [])));
        var input = new string("same".AsSpan());

        var result = engine.Rewrite("File.cs", input, _symbols, Ids("A"));

        Assert.Same(input, result.Text);
        Assert.False(result.Changed);
        Assert.Empty(result.AppliedRuleIds);
    }

    [Fact]
    public void Notes_of_a_rewriter_that_changes_nothing_are_kept()
    {
        var engine = Engine(new Delegating("A", c => new SourceRewriteResult(c.Text, [new RewriteNote("A", "check by hand", true)])));

        var result = engine.Rewrite("File.cs", "x", _symbols, Ids("A"));

        Assert.False(result.Changed);
        var note = Assert.Single(result.Notes);
        Assert.True(note.NeedsManualWork);
    }

    [Fact]
    public void Throwing_rewriter_is_isolated()
    {
        var engine = Engine(
            Append("A", "a"),
            new Delegating("B", _ => throw new InvalidOperationException("boom")),
            Append("C", "c"));

        var result = engine.Rewrite("File.cs", "x", _symbols, Ids("A", "B", "C"));

        Assert.Equal("xac", result.Text);
        Assert.Equal(["A", "C"], result.AppliedRuleIds);
        var failure = Assert.Single(result.Failures);
        Assert.Equal("B", failure.RuleId);
        Assert.Equal("boom", failure.Message);
        Assert.DoesNotContain("XNAREBORN", result.Text);
    }

    [Fact]
    public void Only_throwing_rewriter_leaves_input_and_no_notes()
    {
        var engine = Engine(new Delegating("A", _ => throw new InvalidOperationException("late")));
        var input = "x";

        var result = engine.Rewrite("File.cs", input, _symbols, Ids("A"));

        Assert.Same(input, result.Text);
        Assert.False(result.Changed);
        Assert.Empty(result.Notes);
        Assert.Single(result.Failures);
    }

    [Fact]
    public void Rewriter_returning_null_is_a_failure()
    {
        var engine = Engine(new Delegating("A", _ => null!));

        var result = engine.Rewrite("File.cs", "x", _symbols, Ids("A"));

        Assert.Equal("x", result.Text);
        Assert.Equal("A", Assert.Single(result.Failures).RuleId);
    }

    [Fact]
    public void Cancellation_passes_through()
    {
        var engine = Engine(new Delegating("A", _ => throw new OperationCanceledException()));

        Assert.Throws<OperationCanceledException>(() => engine.Rewrite("File.cs", "x", _symbols, Ids("A")));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var engine = Engine();

        Assert.Throws<ArgumentNullException>(() => new SourceRewriteEngine(null!));
        Assert.Throws<ArgumentNullException>(() => engine.Rewrite(null!, "x", _symbols, Ids()));
        Assert.Throws<ArgumentNullException>(() => engine.Rewrite("f", null!, _symbols, Ids()));
        Assert.Throws<ArgumentNullException>(() => engine.Rewrite("f", "x", null!, Ids()));
        Assert.Throws<ArgumentNullException>(() => engine.Rewrite("f", "x", _symbols, null!));
    }
}
