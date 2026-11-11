using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public sealed class FlxTextButtonTests
{
    private static AsClass Font => AsClass.FromAsset("fnt/main", AssetKind.Font);

    private static FlxTestHost HostWithFont()
    {
        var host = new FlxTestHost();
        host.Fonts["fnt/main"] = (10, 20);
        return host;
    }

    [Fact]
    public void A_sprite_font_draws_text_scaled_so_a_line_is_size_pixels_high()
    {
        var host = HostWithFont();
        var t = new FlxText(10, 5, 100, "Hi") { size = 40, font = Font };
        t.scrollFactor.make(0, 0);
        var state = new FlxState();
        state.add(t);
        host.Start(state);
        host.Frame();
        Assert.Contains("text fnt/main 'Hi' at=10,5 scale=2 ffffffff", host.Drawn);
        Assert.Equal(40, t.height);
    }

    [Theory]
    [InlineData("left", 10)]
    [InlineData("center", 40)]
    [InlineData("right", 70)]
    public void Alignment_uses_the_measured_width_of_each_line(string alignment, double expectedX)
    {
        var host = HostWithFont();
        var t = new FlxText(10, 0, 100, "Hi") { size = 40, font = Font, alignment = alignment };
        t.scrollFactor.make(0, 0);
        var state = new FlxState();
        state.add(t);
        host.Start(state);
        host.Frame();
        Assert.Contains($"text fnt/main 'Hi' at={expectedX},0 scale=2 ffffffff", host.Drawn);
    }

    [Fact]
    public void Colour_alpha_and_shadow_reach_the_renderer_and_the_shadow_is_drawn_first()
    {
        var host = HostWithFont();
        var t = new FlxText(0, 0, 100, "A") { size = 20, font = Font, color = 0xff0000, alpha = 0.5, shadow = 0x00ff00 };
        t.scrollFactor.make(0, 0);
        var state = new FlxState();
        state.add(t);
        host.Start(state);
        host.Frame();
        var texts = host.Drawn.Where(d => d.StartsWith("text", StringComparison.Ordinal)).ToList();
        Assert.Equal(["text fnt/main 'A' at=1,1 scale=1 8000ff00", "text fnt/main 'A' at=0,0 scale=1 80ff0000"], texts);
    }

    [Fact]
    public void Several_lines_stack_by_line_height_and_grow_the_height()
    {
        var host = HostWithFont();
        var t = new FlxText(0, 0, 100, "a\nbb") { size = 20, font = Font };
        t.scrollFactor.make(0, 0);
        var state = new FlxState();
        state.add(t);
        host.Start(state);
        host.Frame();
        Assert.Equal(40, t.height);
        Assert.Contains("text fnt/main 'bb' at=0,20 scale=1 ffffffff", host.Drawn);
        t.text = "one";
        Assert.Equal(20, t.height);
    }

    [Fact]
    public void A_missing_font_falls_back_to_the_built_in_font()
    {
        var host = new FlxTestHost();
        var t = new FlxText(0, 0, 100, "A") { font = AsClass.FromAsset("fnt/none", AssetKind.Font) };
        t.scrollFactor.make(0, 0);
        var state = new FlxState();
        state.add(t);
        host.Start(state);
        host.Frame();
        Assert.DoesNotContain(host.Drawn, d => d.StartsWith("text", StringComparison.Ordinal));
        Assert.Contains(host.Drawn, d => d.StartsWith("quad", StringComparison.Ordinal));
        Assert.Equal(8, t.height);
    }

    [Fact]
    public void A_tile_block_repeats_tiles_over_its_area_and_clips_the_last_ones()
    {
        var host = new FlxTestHost();
        host.Textures["t"] = (20, 10);
        var block = new FlxTileblock(5, 0, 25, 10).loadTiles(AsClass.FromAsset("t", AssetKind.Texture), 10, 10);
        block.scrollFactor.make(0, 0);
        var state = new FlxState();
        state.add(block);
        host.Start(state);
        host.Frame();
        Assert.Contains("frame t src=0,0,10,10 at=5,0,10,10", host.Drawn);
        Assert.Contains("frame t src=10,0,10,10 at=15,0,10,10", host.Drawn);
        Assert.Contains("frame t src=0,0,5,10 at=25,0,5,10", host.Drawn);
    }

    private sealed class ButtonRig
    {
        public int Over;
        public int Out;
        public int Down;
        public int Click;
        public FlxButton Button;
        public FlxTestHost Host = new();

        public ButtonRig(string? label = null)
        {
            Button = new FlxButton(10, 10, label, new AsFunction(new Action(() => Click++)))
            {
                onOver = new AsFunction(new Action(() => Over++)),
                onOut = new AsFunction(new Action(() => Out++)),
                onDown = new AsFunction(new Action(() => Down++)),
            };
            var state = new FlxState();
            state.add(Button);
            Host.Start(state);
            Host.Frame();
        }
    }

    [Fact]
    public void A_button_goes_through_highlight_and_pressed_and_clicks_on_release_over_it()
    {
        var rig = new ButtonRig();
        Assert.Equal(FlxButton.NORMAL, rig.Button.status);
        rig.Host.MoveMouse(15, 15);
        rig.Host.Frame();
        Assert.Equal(FlxButton.HIGHLIGHT, rig.Button.status);
        Assert.Equal(1, rig.Over);
        rig.Host.LeftDown = true;
        rig.Host.Frame();
        Assert.Equal(FlxButton.PRESSED, rig.Button.status);
        Assert.Equal(1, rig.Down);
        Assert.Equal(0, rig.Click);
        Assert.Contains("quad 10,10,80,20 ff4c4c4c", rig.Host.Drawn);
        rig.Host.LeftDown = false;
        rig.Host.Frame();
        Assert.Equal(1, rig.Click);
        Assert.Equal(FlxButton.HIGHLIGHT, rig.Button.status);
        rig.Host.MoveMouse(200, 200);
        rig.Host.Frame();
        Assert.Equal(FlxButton.NORMAL, rig.Button.status);
        Assert.Equal(1, rig.Out);
    }

    [Fact]
    public void Pressing_outside_and_releasing_over_the_button_is_not_a_click()
    {
        var rig = new ButtonRig();
        rig.Host.MoveMouse(200, 200);
        rig.Host.LeftDown = true;
        rig.Host.Frame();
        rig.Host.MoveMouse(15, 15);
        rig.Host.Frame();
        rig.Host.LeftDown = false;
        rig.Host.Frame();
        Assert.Equal(0, rig.Click);
        Assert.Equal(0, rig.Down);
    }

    [Fact]
    public void A_button_ignores_the_camera_scroll_by_default()
    {
        var rig = new ButtonRig();
        FlxG.camera.scroll.make(500, 500);
        rig.Host.MoveMouse(15, 15);
        rig.Host.Frame();
        Assert.Equal(FlxButton.HIGHLIGHT, rig.Button.status);
    }

    [Fact]
    public void The_label_is_centred_on_the_button_and_drawn_with_it()
    {
        var rig = new ButtonRig("OK");
        Assert.NotNull(rig.Button.label);
        Assert.Equal(10, rig.Button.label!.x);
        Assert.Equal(16, rig.Button.label.y);
        Assert.Contains(rig.Host.Drawn, d => d.StartsWith("quad", StringComparison.Ordinal) && d != "quad 10,10,80,20 ff7f7f7f");
        rig.Button.destroy();
        Assert.Null(rig.Button.label);
    }

    [Fact]
    public void A_three_frame_sheet_shows_the_frame_of_the_status()
    {
        var rig = new ButtonRig();
        rig.Host.Textures["btn"] = (120, 20);
        rig.Button.loadGraphic(AsClass.FromAsset("btn", AssetKind.Texture), true, false, 40, 20);
        rig.Host.MoveMouse(15, 15);
        rig.Host.Frame();
        Assert.Equal(1, rig.Button.frame);
        rig.Host.LeftDown = true;
        rig.Host.Frame();
        Assert.Equal(2, rig.Button.frame);
        Assert.DoesNotContain(rig.Host.Drawn, d => d.StartsWith("quad", StringComparison.Ordinal));
    }
}
