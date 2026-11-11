using VerifyXunit;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;
using XnaReborn.Tests.Fixtures;

namespace XnaReborn.Tests.Migrator.Rewriters;

/// <summary>XNA015 points the XACT types at the generated stand-in with using aliases (audio-and-shaders task 7).</summary>
[Trait("Category", "Migrator")]
public sealed class XactAliasRewriterTests
{
    private static readonly string[] _symbols = ["WINDOWS"];

    private readonly XactAliasRewriter _rewriter = new();

    private SourceRewriteResult Run(string text) => _rewriter.Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));

    private static string Alias(string type) => $"using {type} = XnaReborn.Compat.Xact.{type};";

    [Fact]
    public void Rule_id_is_xna015()
    {
        Assert.Equal("XNA015", _rewriter.RuleId);
    }

    [Fact]
    public void Aliases_follow_the_audio_using_for_each_used_type_in_name_order()
    {
        var text = "using System;\nusing Microsoft.Xna.Framework.Audio;\nusing Microsoft.Xna.Framework;\n\nclass G\n{\n    AudioEngine e;\n    SoundBank s;\n    Cue c;\n    SoundEffect fx;\n}\n";

        var result = Run(text);

        Assert.Equal(
            "using System;\nusing Microsoft.Xna.Framework.Audio;\n"
            + Alias("AudioEngine") + "\n" + Alias("Cue") + "\n" + Alias("SoundBank")
            + "\nusing Microsoft.Xna.Framework;\n\nclass G\n{\n    AudioEngine e;\n    SoundBank s;\n    Cue c;\n    SoundEffect fx;\n}\n",
            result.Text);
        var note = Assert.Single(result.Notes);
        Assert.Equal("XNA015", note.RuleId);
        Assert.False(note.NeedsManualWork);
        Assert.Contains("AudioEngine, Cue, SoundBank", note.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Types_that_the_stand_in_does_not_replace_are_left_alone()
    {
        var text = "using Microsoft.Xna.Framework.Audio;\nclass G\n{\n    SoundEffect a;\n    SoundEffectInstance b;\n    AudioListener c;\n    AudioEmitter d;\n}\n";

        var result = Run(text);

        Assert.Same(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void A_file_without_the_xact_types_is_unchanged()
    {
        var text = "using Microsoft.Xna.Framework.Audio;\nclass G { SoundEffect a; }\n";

        Assert.Same(text, Run(text).Text);
    }

    [Fact]
    public void An_unused_type_gets_no_alias_and_a_file_without_the_audio_using_gets_none()
    {
        var withoutUsing = "class G { Cue c; }\n";
        var unused = "using Microsoft.Xna.Framework.Audio;\nclass G { }\n";

        Assert.Same(withoutUsing, Run(withoutUsing).Text);
        Assert.Same(unused, Run(unused).Text);
    }

    [Fact]
    public void Fully_qualified_uses_are_replaced_and_no_alias_is_added()
    {
        var text = "class G\n{\n    Microsoft.Xna.Framework.Audio.AudioEngine e = new Microsoft.Xna.Framework.Audio.AudioEngine(\"a\");\n    int v = Microsoft . Xna . Framework . Audio . AudioEngine.ContentVersion;\n    Microsoft.Xna.Framework.Audio.SoundEffect fx;\n}\n";

        var result = Run(text);

        Assert.Equal(
            "class G\n{\n    XnaReborn.Compat.Xact.AudioEngine e = new XnaReborn.Compat.Xact.AudioEngine(\"a\");\n    int v = XnaReborn.Compat.Xact . AudioEngine.ContentVersion;\n    Microsoft.Xna.Framework.Audio.SoundEffect fx;\n}\n",
            result.Text);
    }

    [Fact]
    public void An_existing_alias_to_the_monogame_type_is_pointed_at_the_stand_in()
    {
        var text = "using Microsoft.Xna.Framework.Audio;\nusing Cue = Microsoft.Xna.Framework.Audio.Cue;\nclass G { Cue c; }\n";

        var result = Run(text);

        Assert.Equal("using Microsoft.Xna.Framework.Audio;\nusing Cue = XnaReborn.Compat.Xact.Cue;\nclass G { Cue c; }\n", result.Text);
    }

    [Fact]
    public void Aliases_go_inside_the_namespace_that_has_the_audio_using_and_keep_its_indent_and_crlf()
    {
        var text = "namespace Game\r\n{\r\n    using Microsoft.Xna.Framework.Audio;\r\n\r\n    class G\r\n    {\r\n        WaveBank w;\r\n    }\r\n}\r\n";

        var result = Run(text);

        Assert.Equal(
            "namespace Game\r\n{\r\n    using Microsoft.Xna.Framework.Audio;\r\n    using WaveBank = XnaReborn.Compat.Xact.WaveBank;\r\n\r\n    class G\r\n    {\r\n        WaveBank w;\r\n    }\r\n}\r\n",
            result.Text);
    }

    [Fact]
    public void A_file_scoped_namespace_works_like_a_block()
    {
        var text = "namespace Game;\n\nusing Microsoft.Xna.Framework.Audio;\n\nclass G { AudioCategory c; }\n";

        Assert.Contains("using Microsoft.Xna.Framework.Audio;\nusing AudioCategory = XnaReborn.Compat.Xact.AudioCategory;\n", Run(text).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_local_type_with_one_of_the_names_leaves_the_file_unchanged_with_a_manual_note()
    {
        var text = "using Microsoft.Xna.Framework.Audio;\nclass Cue { }\nclass G { AudioEngine e; Cue c; }\n";

        var result = Run(text);

        Assert.Same(text, result.Text);
        var note = Assert.Single(result.Notes);
        Assert.True(note.NeedsManualWork);
        Assert.Contains("declares its own Cue", note.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Running_it_twice_changes_nothing_more()
    {
        var text = "using Microsoft.Xna.Framework.Audio;\nclass G\n{\n    AudioEngine e;\n    Microsoft.Xna.Framework.Audio.Cue c;\n}\n";

        var once = Run(text);
        var twice = Run(once.Text);

        Assert.NotEqual(text, once.Text);
        Assert.Same(once.Text, twice.Text);
        Assert.Empty(twice.Notes);
    }

    [Fact]
    public void Aliased_types_are_listed_from_the_rewritten_text()
    {
        var once = Run("using Microsoft.Xna.Framework.Audio;\nclass G { Cue c; WaveBank w; AudioEngine e; }\n");

        Assert.Equal(["AudioEngine", "Cue", "WaveBank"], XactAliasRewriter.AliasedTypes(once.Text));
        Assert.Empty(XactAliasRewriter.AliasedTypes("using System;\n"));
    }

    [Fact]
    public void Text_outside_the_edits_is_byte_for_byte_the_same()
    {
        var head = "\uFEFF// header\t \nusing Microsoft.Xna.Framework.Audio; // keep\n";
        var tail = "\nclass G\n{\n\tCue c;   \n}\n";

        var result = Run(head + tail);

        Assert.Equal(head + "using Cue = XnaReborn.Compat.Xact.Cue;\n" + tail, result.Text);
    }

    [Fact]
    public async Task The_game_of_the_xact_fixture_matches_snapshot()
    {
        var text = await File.ReadAllTextAsync(Path.Combine(FixturePaths.Of("xna40-xact"), "Game1.cs"));

        await Verifier.Verify(Run(text).Text, "txt");
    }
}
