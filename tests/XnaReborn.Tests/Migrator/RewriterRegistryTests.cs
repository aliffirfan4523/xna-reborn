using XnaReborn.Migrator.Source;

namespace XnaReborn.Tests.Migrator;

/// <summary>The registry keys rewriters by rule ID and lists them in a stable order.</summary>
[Trait("Category", "Migrator")]
public class RewriterRegistryTests
{
    private sealed class Stub(string ruleId) : ISourceRewriter
    {
        public string RuleId { get; } = ruleId;

        public SourceRewriteResult Rewrite(SourceRewriteContext context) => SourceRewriteResult.Unchanged(context);
    }

    [Fact]
    public void Empty_registry_has_nothing()
    {
        var registry = new RewriterRegistry();

        Assert.Empty(registry.RuleIds);
        Assert.Empty(registry.All);
        Assert.False(registry.TryGet("XNA001", out _));
    }

    [Fact]
    public void Registered_rewriter_is_found_by_rule_id()
    {
        var registry = new RewriterRegistry();
        var rewriter = new Stub("XNA001");
        registry.Register(rewriter);

        Assert.True(registry.TryGet("XNA001", out var found));
        Assert.Same(rewriter, found);
    }

    [Fact]
    public void Lookup_is_case_sensitive()
    {
        var registry = new RewriterRegistry();
        registry.Register(new Stub("XNA001"));

        Assert.False(registry.TryGet("xna001", out _));
    }

    [Fact]
    public void Rule_ids_and_rewriters_are_in_ordinal_order()
    {
        var registry = new RewriterRegistry();
        registry.Register(new Stub("XNA010"));
        registry.Register(new Stub("ENTRYPOINT"));
        registry.Register(new Stub("XNA002"));
        registry.Register(new Stub("Xna000"));

        Assert.Equal(["ENTRYPOINT", "XNA002", "XNA010", "Xna000"], registry.RuleIds);
        Assert.Equal(registry.RuleIds, registry.All.Select(r => r.RuleId));
    }

    [Fact]
    public void Duplicate_rule_id_throws()
    {
        var registry = new RewriterRegistry();
        registry.Register(new Stub("XNA001"));

        Assert.Throws<ArgumentException>(() => registry.Register(new Stub("XNA001")));
        Assert.Single(registry.RuleIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Blank_rule_id_throws(string ruleId)
    {
        Assert.Throws<ArgumentException>(() => new RewriterRegistry().Register(new Stub(ruleId)));
    }

    [Fact]
    public void Null_arguments_throw()
    {
        var registry = new RewriterRegistry();

        Assert.Throws<ArgumentNullException>(() => registry.Register(null!));
        Assert.Throws<ArgumentNullException>(() => registry.TryGet(null!, out _));
    }
}
