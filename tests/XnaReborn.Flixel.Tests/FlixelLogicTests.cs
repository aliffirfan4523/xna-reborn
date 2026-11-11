using Microsoft.Xna.Framework.Input;
using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public class FlixelLogicTests
{
    private sealed class Probe : FlxState
    {
        public int Created;
        public int Updates;

        public override void create() => Created++;

        public override void update()
        {
            Updates++;
            base.update();
        }
    }

    private sealed class Other : FlxState
    {
    }

    [Fact]
    public void Velocity_with_acceleration_and_max_matches_hand_computation()
    {
        FlxG.elapsed = 0.5;
        Assert.Equal(10, FlxU.computeVelocity(0, 20));
        Assert.Equal(15, FlxU.computeVelocity(0, 40, 0, 15));
        Assert.Equal(-15, FlxU.computeVelocity(0, -40, 0, 15));
        Assert.Equal(6, FlxU.computeVelocity(10, 0, 8));
        Assert.Equal(0, FlxU.computeVelocity(3, 0, 8));
        Assert.Equal(-6, FlxU.computeVelocity(-10, 0, 8));
    }

    [Fact]
    public void An_object_with_gravity_moves_by_the_averaged_velocity_step()
    {
        _ = new FlxTestHost(step: 0.5);
        FlxG.elapsed = 0.5;
        var o = new FlxObject(0, 0, 10, 10);
        o.acceleration.y = 20;
        o.preUpdate();
        o.postUpdate();
        Assert.Equal(10, o.velocity.y);
        Assert.Equal(2.5, o.y);
        Assert.Equal(0, o.last.y);
    }

    [Fact]
    public void Kill_and_revive_flip_exists_and_alive_and_hurt_kills_at_zero_health()
    {
        var o = new FlxObject { health = 2 };
        o.hurt(1);
        Assert.True(o.alive);
        o.hurt(1);
        Assert.False(o.exists);
        o.reset(5, 6);
        Assert.True(o.alive && o.exists);
        Assert.Equal((5d, 6d), (o.x, o.y));
    }

    [Fact]
    public void Group_recycle_reuses_dead_members_and_creates_new_ones_only_when_needed()
    {
        var g = new FlxGroup();
        var first = (FlxSprite)g.recycle(AsClass.Of<FlxSprite>())!;
        Assert.Equal(1, g.length);
        first.kill();
        Assert.Same(first, g.recycle(AsClass.Of<FlxSprite>()));
        Assert.Equal(1, g.length);
        Assert.NotSame(first, g.recycle(AsClass.Of<FlxSprite>()));
        Assert.Equal(2, g.countLiving());
        Assert.Null(new FlxGroup().recycle());
    }

    [Fact]
    public void Group_remove_leaves_a_slot_that_add_reuses()
    {
        var g = new FlxGroup();
        var a = g.add(new FlxBasic());
        g.add(new FlxBasic());
        g.remove(a);
        Assert.Null(g.members[0]);
        g.add(new FlxBasic());
        Assert.Equal(2, g.length);
        Assert.NotNull(g.members[0]);
    }

    [Fact]
    public void A_falling_object_lands_on_an_immovable_floor_and_reports_touching()
    {
        var host = new FlxTestHost();
        var floor = new FlxTileblock(0, 100, 200, 20);
        var ball = new FlxObject(50, 80, 10, 10);
        ball.acceleration.y = 400;
        var state = new Probe();
        state.add(floor);
        state.add(ball);
        host.Start(state);
        var touched = false;
        for (var i = 0; i < 120; i++)
        {
            host.Frame();
            FlxG.collide(floor, ball);
            touched |= ball.isTouching(FlxObject.FLOOR);
        }

        Assert.True(touched);
        Assert.InRange(ball.y, 89, 91);
        Assert.Equal(100, floor.y);
    }

    [Fact]
    public void Separation_on_x_pushes_the_movable_object_out_of_a_wall_and_sets_side_flags()
    {
        _ = new FlxTestHost();
        var wall = new FlxObject(100, 0, 20, 100) { immovable = true };
        var mover = new FlxObject(85, 10, 18, 10);
        mover.last.make(70, 10);
        Assert.True(FlxObject.separate(wall, mover));
        Assert.Equal(82, mover.x);
        Assert.True(mover.isTouching(FlxObject.RIGHT));
        Assert.True(wall.isTouching(FlxObject.LEFT));
        Assert.Equal(100, wall.x);
    }
    [Fact]
    public void Two_movable_objects_each_give_way_by_half()
    {
        _ = new FlxTestHost();
        var a = new FlxObject(0, 0, 10, 10);
        var b = new FlxObject(8, 0, 10, 10);
        a.last.make(-5, 0);
        a.x = 0;
        b.last.make(8, 0);
        Assert.True(FlxObject.separateX(a, b));
        Assert.Equal(-1, a.x);
        Assert.Equal(9, b.x);
    }

    [Fact]
    public void Overlap_notifies_each_overlapping_pair_once_through_the_callback()
    {
        _ = new FlxTestHost();
        var bullets = new FlxGroup();
        var enemies = new FlxGroup();
        var b1 = (FlxObject)bullets.add(new FlxObject(0, 0, 4, 4));
        bullets.add(new FlxObject(100, 100, 4, 4));
        var e1 = (FlxObject)enemies.add(new FlxObject(2, 2, 10, 10));
        var pairs = new List<(FlxBasic, FlxBasic)>();
        var hit = FlxG.overlap(bullets, enemies, new AsFunction(new Action<FlxObject, FlxObject>((a, b) => pairs.Add((a, b)))));
        Assert.True(hit);
        Assert.Equal([((FlxBasic)b1, (FlxBasic)e1)], pairs);
        b1.kill();
        Assert.False(FlxG.overlap(bullets, enemies));
    }

    [Fact]
    public void States_start_at_the_next_frame_and_switching_runs_create_once_per_state()
    {
        var host = new FlxTestHost();
        var a = new Probe();
        host.Start(a);
        Assert.Equal(0, a.Created);
        host.Frame(3);
        Assert.Equal(1, a.Created);
        Assert.Equal(3, a.Updates);
        var b = new Probe();
        FlxG.switchState(b);
        host.Frame(2);
        Assert.Same(b, FlxG.state);
        Assert.Equal(1, b.Created);
        Assert.Equal(2, b.Updates);
        Assert.Equal(3, a.Updates);
    }

    [Fact]
    public void A_scripted_two_state_run_is_deterministic()
    {
        string Run()
        {
            var host = new FlxTestHost();
            var s = new Probe();
            var mover = new FlxSprite(0, 0);
            mover.makeGraphic(4, 4);
            mover.velocity.x = 30;
            s.add(mover);
            host.Start(s);
            host.Frame(90);
            FlxG.switchState(new Other());
            host.Frame(2);
            return $"{mover.x:R}|{host.Drawn.Count}|{FlxG.state!.GetType().Name}";
        }

        Assert.Equal(Run(), Run());
        Assert.StartsWith("45", Run());
    }

    [Fact]
    public void Keys_report_pressed_just_pressed_and_just_released_by_flixel_names()
    {
        var host = new FlxTestHost();
        host.Start(new Probe());
        host.Frame();
        Assert.False(FlxG.keys.LEFT);
        host.Press(Keys.Left, Keys.X);
        host.Frame();
        Assert.True(FlxG.keys.LEFT && FlxG.keys.X);
        Assert.True(FlxG.keys.justPressed("left"));
        host.Frame();
        Assert.False(FlxG.keys.justPressed("LEFT"));
        host.Release(Keys.Left);
        host.Frame();
        Assert.True(FlxG.keys.justReleased("LEFT"));
        Assert.False(FlxG.keys.pressed("NOPE"));
    }

    [Fact]
    public void Sprite_animation_advances_by_frame_rate_and_loops()
    {
        var host = new FlxTestHost();
        host.Textures["sheet"] = (64, 16);
        var s = new FlxSprite(0, 0);
        s.loadGraphic(AsClass.FromAsset("sheet", AssetKind.Texture), true, true, 16, 16);
        Assert.Equal((16, 16), (s.frameWidth, s.frameHeight));
        s.addAnimation("walk", new AsArray(1, 2, 3), 10, true);
        s.play("walk");
        Assert.Equal(1, s.frame);
        var state = new Probe();
        state.add(s);
        host.Start(state);
        host.Frame(1);
        var seen = new List<int>();
        for (var i = 0; i < 24; i++)
        {
            host.Frame();
            seen.Add(s.frame);
        }

        Assert.Contains(2, seen);
        Assert.Contains(3, seen);
        Assert.Contains(1, seen);
    }

    [Fact]
    public void A_non_looping_animation_stops_on_its_last_frame()
    {
        var host = new FlxTestHost();
        host.Textures["sheet"] = (64, 16);
        var s = new FlxSprite();
        s.loadGraphic(AsClass.FromAsset("sheet", AssetKind.Texture), true, false, 16, 16);
        s.addAnimation("once", new AsArray(0, 1), 30, false);
        s.play("once");
        var state = new Probe();
        state.add(s);
        host.Start(state);
        host.Frame(20);
        Assert.Equal(1, s.frame);
        Assert.True(s.finished);
    }

    [Fact]
    public void Drawing_uses_the_frame_rectangle_and_flips_a_reversed_sprite_that_faces_left()
    {
        var host = new FlxTestHost();
        host.Textures["sheet"] = (64, 16);
        var s = new FlxSprite(10, 20);
        s.loadGraphic(AsClass.FromAsset("sheet", AssetKind.Texture), true, true, 16, 16);
        s.addAnimation("a", new AsArray(2));
        s.play("a");
        s.offset.make(3, 2);
        s.facing = FlxObject.LEFT;
        var state = new Probe();
        state.add(s);
        host.Start(state);
        host.Frame();
        Assert.Contains("frame sheet src=32,0,16,16 at=7,18,16,16 flip", host.Drawn);
    }

    [Fact]
    public void Solid_sprites_draw_one_quad_with_alpha_and_hidden_or_dead_sprites_draw_nothing()
    {
        var host = new FlxTestHost();
        var s = new FlxSprite(1, 2);
        s.makeGraphic(4, 3, 0xffcc3030);
        s.alpha = 0.5;
        var state = new Probe();
        state.add(s);
        host.Start(state);
        host.Frame();
        Assert.Contains("quad 1,2,4,3 80cc3030", host.Drawn);
        s.visible = false;
        host.Frame();
        Assert.DoesNotContain(host.Drawn, d => d.StartsWith("quad", StringComparison.Ordinal));
    }

    [Fact]
    public void Tile_blocks_draw_a_grid_of_tile_frames_and_are_immovable()
    {
        var host = new FlxTestHost();
        host.Textures["tiles"] = (64, 16);
        var t = new FlxTileblock(0, 0, 40, 16);
        t.loadTiles(AsClass.FromAsset("tiles", AssetKind.Texture), 16, 16, 0);
        Assert.True(t.immovable);
        var state = new Probe();
        state.add(t);
        host.Start(state);
        host.Frame();
        Assert.Equal(3, host.Drawn.Count(d => d.StartsWith("frame tiles", StringComparison.Ordinal)));
        Assert.Contains("frame tiles src=32,0,8,16 at=32,0,8,16", host.Drawn);
    }

    [Fact]
    public void Text_draws_pixels_aligned_inside_its_width()
    {
        var host = new FlxTestHost();
        var left = new FlxText(0, 0, 100, "I");
        var centre = new FlxText(0, 20, 100, "I") { alignment = "center" };
        var state = new Probe();
        state.add(left);
        state.add(centre);
        host.Start(state);
        host.Frame();
        Assert.Contains("quad 1,0,1,1 ffffffff", host.Drawn);
        Assert.Contains("quad 48.5,20,1,1 ffffffff", host.Drawn);
        Assert.Equal(8, left.height);
        left.text = "A\nB";
        Assert.Equal(16, left.height);
    }

    [Fact]
    public void Play_goes_through_the_asset_provider_and_returns_a_sound()
    {
        var host = new FlxTestHost();
        var sound = FlxG.play(AsClass.FromAsset("data/jump", AssetKind.Sound), 0.5);
        Assert.Equal(["data/jump"], host.Played);
        Assert.Equal(0.5, sound.volume);
    }

    [Fact]
    public void Flicker_hides_the_sprite_on_alternate_slices_and_ends()
    {
        var host = new FlxTestHost();
        var s = new FlxSprite();
        s.makeGraphic(2, 2);
        s.flicker(0.1);
        var state = new Probe();
        state.add(s);
        host.Start(state);
        var drawn = 0;
        for (var i = 0; i < 12; i++)
        {
            host.Frame();
            drawn += host.Drawn.Count(d => d.StartsWith("quad", StringComparison.Ordinal));
        }

        Assert.InRange(drawn, 7, 11);
        Assert.False(s.flickering);
    }
}
