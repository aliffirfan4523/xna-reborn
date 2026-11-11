using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public class FlxCameraTests
{
    private static FlxTestHost Host(double step = 0.5)
    {
        var host = new FlxTestHost(100, 80, step);
        host.Start(new FlxState());
        host.Frame();
        return host;
    }

    [Fact]
    public void Lockon_follow_centres_the_target_and_bounds_limit_the_view()
    {
        var host = Host();
        var player = new FlxObject(500, 300, 10, 10);
        FlxG.camera.follow(player);
        host.Frame();
        Assert.Equal((455d, 265d), (FlxG.camera.scroll.x, FlxG.camera.scroll.y));
        FlxG.camera.setBounds(0, 0, 520, 320);
        host.Frame();
        Assert.Equal((420d, 240d), (FlxG.camera.scroll.x, FlxG.camera.scroll.y));
        player.x = -50;
        player.y = -50;
        host.Frame();
        Assert.Equal((0d, 0d), (FlxG.camera.scroll.x, FlxG.camera.scroll.y));
    }

    [Fact]
    public void Platformer_follow_keeps_the_target_inside_its_dead_zone()
    {
        var host = Host();
        var player = new FlxObject(50, 40, 8, 8);
        FlxG.camera.follow(player, FlxCamera.STYLE_PLATFORMER);
        host.Frame();
        var start = FlxG.camera.scroll.x;
        player.x -= 1;
        host.Frame();
        Assert.Equal(start, FlxG.camera.scroll.x);
        player.x += 100;
        host.Frame();
        Assert.True(FlxG.camera.scroll.x > start);
        var screenRight = player.x - FlxG.camera.scroll.x + player.width;
        Assert.InRange(screenRight, 0, 100);
    }

    [Fact]
    public void Scroll_moves_world_sprites_but_not_a_hud_with_scroll_factor_zero()
    {
        var host = Host();
        var world = new FlxSprite(50, 20).makeGraphic(4, 4, 0xffff0000);
        var hud = new FlxSprite(5, 5).makeGraphic(4, 4, 0xff00ff00);
        hud.scrollFactor.make();
        FlxG.state!.add(world);
        FlxG.state!.add(hud);
        FlxG.camera.scroll.make(10, 3);
        host.Frame();
        Assert.Contains("quad 40,17,4,4 ffff0000", host.Drawn);
        Assert.Contains("quad 5,5,4,4 ff00ff00", host.Drawn);
    }

    [Fact]
    public void Flash_fades_out_calls_back_once_and_draws_over_the_view()
    {
        var host = Host(0.25);
        var done = 0;
        FlxG.camera.flash(0xffffffff, 1, new AsFunction(new Action(() => done++)));
        host.Frame();
        Assert.Contains(host.Drawn, d => d.StartsWith("quad 0,0,100,80 ", StringComparison.Ordinal));
        host.Frame(3);
        Assert.Equal(1, done);
        host.Frame(2);
        Assert.Equal(1, done);
        Assert.DoesNotContain(host.Drawn, d => d.StartsWith("quad 0,0,100,80 ", StringComparison.Ordinal));
    }

    [Fact]
    public void Fade_out_keeps_the_colour_and_fade_in_clears_it()
    {
        var host = Host(0.5);
        var done = 0;
        FlxG.camera.fade(0xff000000, 1, new AsFunction(new Action(() => done++)));
        host.Frame(2);
        Assert.Equal(1, done);
        Assert.Contains("quad 0,0,100,80 ff000000", host.Drawn);
        FlxG.camera.fade(0xff000000, 1, null, true, true);
        host.Frame(2);
        Assert.DoesNotContain(host.Drawn, d => d.StartsWith("quad 0,0,100,80 ", StringComparison.Ordinal));
    }

    [Fact]
    public void Shake_moves_the_drawing_then_stops_and_calls_back()
    {
        var host = Host(0.25);
        var world = new FlxSprite(50, 20).makeGraphic(4, 4, 0xffff0000);
        FlxG.state!.add(world);
        var done = 0;
        FlxG.globalSeed = 0.3;
        FlxG.camera.shake(0.1, 0.5, new AsFunction(new Action(() => done++)));
        var moved = false;
        for (var i = 0; i < 1; i++)
        {
            host.Frame();
            moved |= !host.Drawn.Contains("quad 50,20,4,4 ffff0000");
        }

        Assert.True(moved);
        host.Frame(3);
        Assert.Equal(1, done);
        Assert.Equal((0d, 0d), (FlxG.camera.shakeOffset.x, FlxG.camera.shakeOffset.y));
        Assert.Contains("quad 50,20,4,4 ffff0000", host.Drawn);
    }

    [Fact]
    public void Background_colour_belongs_to_the_camera_and_init_resets_it()
    {
        var host = Host();
        FlxG.bgColor = 0xff123456;
        Assert.Equal(0xff123456u, FlxG.camera.bgColor);
        host.Frame();
        Assert.Contains("clear ff123456", host.Drawn);
        FlxG.init(10, 10);
        Assert.Equal(0xff000000u, FlxG.bgColor);
    }
}
