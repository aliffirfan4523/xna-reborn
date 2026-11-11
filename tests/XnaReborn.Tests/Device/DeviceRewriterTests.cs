using XnaReborn.Core.Models;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Tests.Device;

/// <summary>Device-to-desktop task 8: the GamePad call rewrite, the DeviceInput install, and the manual markers.</summary>
[Trait("Category", "Migrator")]
public sealed class DeviceRewriterTests
{
    private const string _install = "Components.Add(new XnaReborn.Compat.Input.DeviceInput(this));";

    private static SourceRewriteResult Run(ISourceRewriter rewriter, string text, XnaPlatform platform = XnaPlatform.WindowsPhone) =>
        rewriter.Rewrite(new SourceRewriteContext("Game1.cs", text, [], platform));

    // ---------- GamePadCallRewriter ----------
    [Fact]
    public void GetState_calls_are_redirected_to_the_keyboard_fallback()
    {
        const string code = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    void M()\n    {\n        var a = GamePad.GetState(PlayerIndex.One);\n        var b = GamePad.GetState(PlayerIndex.Two, GamePadDeadZone.None);\n    }\n}\n";

        var result = Run(new GamePadCallRewriter(), code);

        Assert.Equal(code.Replace("GamePad.GetState", "XnaReborn.Compat.Input.CompatGamePad.GetState", StringComparison.Ordinal), result.Text);
        Assert.Single(result.Notes, n => n.Message.StartsWith("Redirected 2", StringComparison.Ordinal));
    }

    [Fact]
    public void The_fully_qualified_receiver_is_redirected_too()
    {
        const string code = "class G { void M() { var a = Microsoft.Xna.Framework.Input.GamePad.GetState(PlayerIndex.One); } }\n";

        Assert.Equal(
            "class G { void M() { var a = XnaReborn.Compat.Input.CompatGamePad.GetState(PlayerIndex.One); } }\n",
            Run(new GamePadCallRewriter(), code).Text);
    }

    [Theory]
    [InlineData("var c = GamePad.GetCapabilities(PlayerIndex.One);")]
    [InlineData("GamePad.SetVibration(PlayerIndex.One, 1f, 1f);")]
    [InlineData("var a = Other.GamePad.GetState(1);")]
    [InlineData("var a = pad.GetState(PlayerIndex.One);")]
    [InlineData("var a = GamePad.GetState();")]
    [InlineData("var a = GamePad.GetState(PlayerIndex.One, GamePadDeadZone.None, 3);")]
    public void Other_calls_are_not_touched(string statement)
    {
        var code = "using Microsoft.Xna.Framework.Input;\nclass G\n{\n    void M()\n    {\n        " + statement + "\n    }\n}\n";

        var result = Run(new GamePadCallRewriter(), code);

        Assert.Equal(code, result.Text);
        Assert.Empty(result.Notes);
    }

    [Fact]
    public void A_file_that_does_not_import_the_input_namespace_is_not_touched()
    {
        const string code = "class G { void M() { var a = GamePad.GetState(PlayerIndex.One); } }\n";

        Assert.Equal(code, Run(new GamePadCallRewriter(), code).Text);
    }

    [Fact]
    public void A_user_class_named_GamePad_is_not_rewritten()
    {
        const string code = "using Microsoft.Xna.Framework.Input;\nclass GamePad { public static int GetState(int p) => p; }\nclass G { void M() { var a = GamePad.GetState(1); } }\n";

        Assert.Equal(code, Run(new GamePadCallRewriter(), code).Text);
    }

    [Theory]
    [InlineData(XnaPlatform.Windows)]
    [InlineData(XnaPlatform.Unknown)]
    public void Windows_and_unknown_projects_keep_their_game_pad_calls(XnaPlatform platform)
    {
        const string code = "using Microsoft.Xna.Framework.Input;\nclass G { void M() { var a = GamePad.GetState(PlayerIndex.One); } }\n";

        Assert.Equal(code, Run(new GamePadCallRewriter(), code, platform).Text);
    }

    [Fact]
    public void The_game_pad_rewrite_is_idempotent_and_keeps_trivia()
    {
        const string code = "using Microsoft.Xna.Framework.Input;\nclass G { void M() { var a = /* pad */ GamePad .GetState ( PlayerIndex.One ) ; } }\n";

        var once = Run(new GamePadCallRewriter(), code);
        var twice = Run(new GamePadCallRewriter(), once.Text);

        Assert.Contains("/* pad */ XnaReborn.Compat.Input.CompatGamePad .GetState ( PlayerIndex.One ) ;", once.Text, StringComparison.Ordinal);
        Assert.Equal(once.Text, twice.Text);
    }

    // ---------- DeviceInstallRewriter ----------
    private const string _game = "using Microsoft.Xna.Framework;\nnamespace N\n{\n    public class Game1 : Game\n    {\n        public Game1()\n        {\n            graphics = new GraphicsDeviceManager(this);\n            Content.RootDirectory = \"Content\";\n        }\n    }\n}\n";

    [Fact]
    public void The_component_line_is_added_as_the_last_statement_of_the_constructor()
    {
        var result = Run(new DeviceInstallRewriter(), _game);

        Assert.Equal(_game.Replace("            Content.RootDirectory = \"Content\";\n", "            Content.RootDirectory = \"Content\";\n            " + _install + "\n", StringComparison.Ordinal), result.Text);
        Assert.Single(result.Notes, n => !n.NeedsManualWork);
    }

    [Fact]
    public void The_install_is_added_once_and_a_second_run_changes_nothing()
    {
        var once = Run(new DeviceInstallRewriter(), _game).Text;
        var twice = Run(new DeviceInstallRewriter(), once);

        Assert.Equal(once, twice.Text);
        Assert.Equal(1, once.Split(_install).Length - 1);
    }

    [Fact]
    public void An_empty_constructor_gets_the_line_one_level_in_and_crlf_is_kept()
    {
        const string code = "using Microsoft.Xna.Framework;\r\nclass G : Microsoft.Xna.Framework.Game\r\n{\r\n    public G()\r\n    {\r\n    }\r\n}\r\n";

        var result = Run(new DeviceInstallRewriter(), code);

        Assert.Equal("using Microsoft.Xna.Framework;\r\nclass G : Microsoft.Xna.Framework.Game\r\n{\r\n    public G()\r\n    {\r\n        " + _install + "\r\n    }\r\n}\r\n", result.Text);
    }

    [Fact]
    public void A_constructor_that_chains_to_another_is_skipped()
    {
        const string code = "class G : Game\n{\n    public G(int a) : this()\n    {\n        x = a;\n    }\n\n    public G()\n    {\n        y = 1;\n    }\n}\n";

        var result = Run(new DeviceInstallRewriter(), code);

        Assert.Contains("        y = 1;\n        " + _install + "\n", result.Text, StringComparison.Ordinal);
        Assert.Equal(1, result.Text.Split(_install).Length - 1);
    }

    [Fact]
    public void A_game_subclass_without_a_constructor_gets_a_marker()
    {
        const string code = "class G : Game\n{\n    int x;\n}\n";

        var result = Run(new DeviceInstallRewriter(), code);

        Assert.StartsWith("// XNAREBORN: DEVICEINPUT: Add " + _install, result.Text, StringComparison.Ordinal);
        Assert.Single(result.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void A_one_line_constructor_gets_a_marker_instead_of_an_edit()
    {
        const string code = "class G : Game\n{\n    public G() { x = 1; }\n}\n";

        var result = Run(new DeviceInstallRewriter(), code);

        Assert.StartsWith("// XNAREBORN: DEVICEINPUT: Add ", result.Text, StringComparison.Ordinal);
        Assert.EndsWith("class G : Game\n{\n    public G() { x = 1; }\n}\n", result.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class G { public G() { } }\n")]
    [InlineData("class G : Base { public G() { } }\n")]
    [InlineData("namespace N { class Program { static void Main() { } } }\n")]
    public void Files_without_a_game_subclass_are_not_touched(string code)
    {
        Assert.Equal(code, Run(new DeviceInstallRewriter(), code).Text);
    }

    [Theory]
    [InlineData(XnaPlatform.Windows)]
    [InlineData(XnaPlatform.Unknown)]
    public void Windows_and_unknown_projects_get_no_component(XnaPlatform platform)
    {
        Assert.Equal(_game, Run(new DeviceInstallRewriter(), _game, platform).Text);
    }

    // ---------- DeviceManualMarkerRewriter ----------
    [Fact]
    public void Media_library_uses_get_a_marker_and_stay_as_they_are()
    {
        const string code = "using Microsoft.Xna.Framework.Media;\nclass G\n{\n    void M()\n    {\n        var lib = new MediaLibrary();\n        var song = MediaPlayer.Queue;\n    }\n}\n";

        var result = Run(DeviceManualMarkerRewriter.MediaLibrary(), code);

        Assert.Equal(
            "using Microsoft.Xna.Framework.Media;\nclass G\n{\n    void M()\n    {\n        // XNAREBORN: XNA056: The device media library has no desktop equivalent. Replace it with your own list of files or remove the feature.\n        var lib = new MediaLibrary();\n        var song = MediaPlayer.Queue;\n    }\n}\n",
            result.Text);
        Assert.Single(result.Notes, n => n.NeedsManualWork);
    }

    [Fact]
    public void Chooser_uses_get_a_marker_but_launchers_do_not()
    {
        const string code = "using Microsoft.Phone.Tasks;\nclass G\n{\n    void M()\n    {\n        var p = new PhotoChooserTask();\n        var w = new WebBrowserTask();\n    }\n}\n";

        var result = Run(DeviceManualMarkerRewriter.Choosers(), code);

        Assert.Contains("// XNAREBORN: XNA059:", result.Text, StringComparison.Ordinal);
        Assert.Equal(1, result.Text.Split("XNAREBORN").Length - 1);
        Assert.Contains("        var w = new WebBrowserTask();", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_type_with_the_same_name_and_no_using_is_not_marked()
    {
        const string code = "class G\n{\n    void M()\n    {\n        var p = new PhotoChooserTask();\n    }\n}\n";

        Assert.Equal(code, Run(DeviceManualMarkerRewriter.Choosers(), code).Text);
    }

    [Fact]
    public void Advertising_usings_are_marked()
    {
        const string code = "using Microsoft.Advertising.Mobile.UI;\nusing System;\nclass G { }\n";

        var result = Run(DeviceManualMarkerRewriter.ThirdParty(), code);

        Assert.StartsWith("// XNAREBORN: XNA063:", result.Text, StringComparison.Ordinal);
        Assert.EndsWith("using Microsoft.Advertising.Mobile.UI;\nusing System;\nclass G { }\n", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Markers_are_not_added_twice()
    {
        const string code = "using Microsoft.Xna.Framework.Media;\nclass G { void M() { var lib = new MediaLibrary(); } }\n";
        var once = Run(DeviceManualMarkerRewriter.MediaLibrary(), code).Text;

        Assert.Equal(once, Run(DeviceManualMarkerRewriter.MediaLibrary(), once).Text);
    }
}
