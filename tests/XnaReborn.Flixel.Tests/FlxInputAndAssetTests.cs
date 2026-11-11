using System.Text;
using Microsoft.Xna.Framework.Input;
using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public sealed class FlxInputAndAssetTests
{
    [Fact]
    public void Keys_report_pressed_just_pressed_and_just_released_by_name()
    {
        var host = new FlxTestHost();
        host.Frame();
        host.Press(Keys.Left, Keys.X);
        host.Frame();
        Assert.True(FlxG.keys.LEFT);
        Assert.True(FlxG.keys.justPressed("X"));
        Assert.True(FlxG.keys.any());
        host.Frame();
        Assert.True(FlxG.keys.pressed("left"));
        Assert.False(FlxG.keys.justPressed("X"));
        host.Release(Keys.X);
        host.Frame();
        Assert.True(FlxG.keys.justReleased("X"));
        Assert.False(FlxG.keys.RIGHT);
    }

    [Fact]
    public void Either_shift_key_counts_as_SHIFT_and_unknown_names_are_never_pressed()
    {
        var host = new FlxTestHost();
        host.Press(Keys.RightShift);
        host.Frame();
        Assert.True(FlxG.keys.SHIFT);
        Assert.False(FlxG.keys.pressed("NOSUCHKEY"));
    }

    [Fact]
    public void Every_flixel_key_name_maps_to_a_key()
    {
        var host = new FlxTestHost();
        foreach (var name in new[] { "ZERO", "NINE", "NUMPADFIVE", "F1", "F12", "BACKSPACE", "PAGEUP", "COMMA", "ALT", "CONTROL" })
        {
            Assert.Contains(name, FlxKeyboard.KeyNames);
        }

        host.Press(Keys.F5, Keys.D3, Keys.NumPad7);
        host.Frame();
        Assert.True(FlxG.keys.pressed("F5"));
        Assert.True(FlxG.keys.pressed("THREE"));
        Assert.True(FlxG.keys.pressed("NUMPADSEVEN"));
    }

    [Fact]
    public void Reset_forgets_held_keys()
    {
        var host = new FlxTestHost();
        host.Press(Keys.Space);
        host.Frame();
        FlxG.keys.reset();
        Assert.False(FlxG.keys.SPACE);
        Assert.False(FlxG.keys.any());
    }

    [Fact]
    public void Mouse_tracks_position_buttons_and_the_camera_scroll()
    {
        var host = new FlxTestHost();
        FlxG.camera.scroll.make(100, 50);
        host.MoveMouse(20, 30);
        host.Frame();
        Assert.Equal(20, FlxG.mouse.screenX);
        Assert.Equal(30, FlxG.mouse.screenY);
        Assert.Equal(120, FlxG.mouse.x);
        Assert.Equal(80, FlxG.mouse.y);
        Assert.False(FlxG.mouse.pressed());

        host.LeftDown = true;
        host.Frame();
        Assert.True(FlxG.mouse.justPressed());
        host.Frame();
        Assert.True(FlxG.mouse.pressed());
        Assert.False(FlxG.mouse.justPressed());
        host.LeftDown = false;
        host.Frame();
        Assert.True(FlxG.mouse.justReleased());
    }

    [Fact]
    public void Mouse_wheel_is_the_change_since_the_last_frame_and_the_first_frame_is_zero()
    {
        var host = new FlxTestHost();
        host.WheelTotal = 240;
        host.Frame();
        Assert.Equal(0, FlxG.mouse.wheel);
        host.WheelTotal = 360;
        host.Frame();
        Assert.Equal(120, FlxG.mouse.wheel);
        host.Frame();
        Assert.Equal(0, FlxG.mouse.wheel);
    }

    [Fact]
    public void Mouse_can_be_hidden_and_shown_and_the_gamepad_stub_never_presses()
    {
        _ = new FlxTestHost();
        FlxG.mouse.hide();
        Assert.False(FlxG.mouse.visible);
        FlxG.mouse.show();
        Assert.True(FlxG.mouse.visible);
        Assert.False(FlxG.gamepads[0].pressed("A"));
        Assert.False(FlxG.gamepads[0].justPressed("A"));
    }

    [Fact]
    public void Data_loads_as_bytes_and_as_text_without_a_byte_order_mark()
    {
        var host = new FlxTestHost();
        var map = AsClass.FromAsset("data/map.csv", AssetKind.Data);
        host.Data["data/map.csv"] = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("1,0\n0,1")];
        Assert.Equal(10, FlxG.loadBytes(map).Length);
        Assert.Equal("1,0\n0,1", FlxG.loadText(map));
    }

    [Fact]
    public void Loading_data_from_a_type_class_or_a_missing_file_fails_clearly()
    {
        _ = new FlxTestHost();
        Assert.Throws<ArgumentException>(() => FlxG.loadBytes(AsClass.Of<FlxSprite>()));
        Assert.Throws<FileNotFoundException>(() => FlxG.loadBytes(AsClass.FromAsset("data/none.bin", AssetKind.Data)));
    }

    [Fact]
    public void Sprites_size_themselves_from_the_texture_the_provider_reports_and_a_missing_texture_is_zero_sized()
    {
        var host = new FlxTestHost();
        host.Textures["img/hero"] = (64, 16);
        var sheet = new FlxSprite().loadGraphic(AsClass.FromAsset("img/hero", AssetKind.Texture), true, false, 16, 16);
        Assert.Equal((16, 16), (sheet.frameWidth, sheet.frameHeight));
        var missing = new FlxSprite().loadGraphic(AsClass.FromAsset("img/none", AssetKind.Texture));
        Assert.Equal((0, 0), (missing.frameWidth, missing.frameHeight));
    }
}
