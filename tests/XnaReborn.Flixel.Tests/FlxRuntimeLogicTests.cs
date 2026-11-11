using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public class FlxRuntimeLogicTests
{
    [Fact]
    public void Rect_edges_overlap_and_copy()
    {
        var a = new FlxRect(0, 0, 10, 10);
        Assert.Equal((0d, 10d, 0d, 10d), (a.left, a.right, a.top, a.bottom));
        Assert.True(a.overlaps(new FlxRect(5, 5, 10, 10)));
        Assert.False(a.overlaps(new FlxRect(10, 0, 5, 5)));
        Assert.False(a.overlaps(new FlxRect(20, 20, 5, 5)));
        var b = a.copyTo();
        Assert.Equal(10, b.width);
        Assert.Equal(3, new FlxRect().make(1, 2, 3, 4).copyFrom(new FlxRect(0, 0, 3, 0)).width);
    }

    [Fact]
    public void Angles_distance_and_rotation_match_hand_values()
    {
        var o = new FlxPoint();
        Assert.Equal(0, FlxU.getAngle(o, new FlxPoint(0, -5)), 6);
        Assert.Equal(90, FlxU.getAngle(o, new FlxPoint(5, 0)), 6);
        Assert.Equal(180, FlxU.getAngle(o, new FlxPoint(0, 5)), 6);
        Assert.Equal(-90, FlxU.getAngle(o, new FlxPoint(-5, 0)), 6);
        Assert.Equal(0, FlxU.getAngle(o, o));
        Assert.Equal(5, FlxU.getDistance(o, new FlxPoint(3, 4)), 6);
        var p = FlxU.rotatePoint(1, 0, 0, 0, 90);
        Assert.Equal(0, p.x, 6);
        Assert.Equal(1, p.y, 6);
        Assert.Equal((2d, 3d, 4d), (FlxU.min(2, 3), FlxU.max(2, 3), FlxU.abs(-4)));
    }

    [Fact]
    public void Time_is_formatted_as_minutes_seconds_and_hundredths()
    {
        Assert.Equal("0:05", FlxU.formatTime(5));
        Assert.Equal("1:05", FlxU.formatTime(65.5));
        Assert.Equal("1:05.50", FlxU.formatTime(65.5, true));
        Assert.Equal("10:00", FlxU.formatTime(600));
    }

    [Fact]
    public void Random_repeats_for_one_seed_and_stays_in_range()
    {
        FlxG.globalSeed = 0.25;
        var first = new[] { FlxG.random(), FlxG.random(), FlxG.random() };
        FlxG.globalSeed = 0.25;
        var second = new[] { FlxG.random(), FlxG.random(), FlxG.random() };
        Assert.Equal(first, second);
        Assert.All(first, v => Assert.InRange(v, 0, 0.9999999));
        Assert.Equal((69621L * (long)(0.25 * 0x7FFFFFFF) % 0x7FFFFFFF) / (double)0x7FFFFFFF, first[0]);
        var items = new AsArray("a", "b", "c");
        Assert.Contains(FlxG.getRandom(items), new object?[] { "a", "b", "c" });
        Assert.Equal("c", FlxG.getRandom(items, 2));
        Assert.Null(FlxG.getRandom(new AsArray()));
    }

    [Fact]
    public void A_timer_fires_after_its_time_and_stops_after_its_loops()
    {
        var host = new FlxTestHost(step: 0.25);
        host.Start(new FlxState());
        host.Frame();
        var calls = 0;
        var timer = new FlxTimer().start(1, 2, new AsFunction(new Action<FlxTimer>(_ => calls++)));
        host.Frame(3);
        Assert.Equal(0, calls);
        host.Frame();
        Assert.Equal(1, calls);
        Assert.False(timer.finished);
        host.Frame(4);
        Assert.Equal(2, calls);
        Assert.True(timer.finished);
        Assert.Equal(0, FlxTimer.manager.count);
        host.Frame(8);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void A_timer_with_zero_loops_repeats_and_a_paused_timer_waits()
    {
        var host = new FlxTestHost(step: 0.5);
        host.Start(new FlxState());
        host.Frame();
        var calls = 0;
        var timer = new FlxTimer().start(1, 0, new AsFunction(new Action(() => calls++)));
        host.Frame(6);
        Assert.Equal(3, calls);
        timer.paused = true;
        host.Frame(6);
        Assert.Equal(3, calls);
        timer.paused = false;
        host.Frame(2);
        Assert.Equal(4, calls);
        timer.stop();
        host.Frame(4);
        Assert.Equal(4, calls);
    }

    [Fact]
    public void Timers_are_dropped_when_the_state_changes_and_a_zero_wait_does_not_hang()
    {
        var host = new FlxTestHost(step: 0.5);
        host.Start(new FlxState());
        host.Frame();
        var calls = 0;
        new FlxTimer().start(1, 1, new AsFunction(new Action(() => calls++)));
        FlxG.switchState(new FlxState());
        host.Frame(4);
        Assert.Equal(0, calls);
        new FlxTimer().start(0, 0, new AsFunction(new Action(() => calls++)));
        host.Frame(2);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Path_keeps_its_points_in_order()
    {
        var path = new FlxPath();
        path.add(1, 1);
        path.add(3, 3);
        path.addAt(2, 2, 1);
        path.addAt(9, 9, 99);
        Assert.Equal(new double[] { 1, 2, 3, 9 }, path.nodes.Select(n => n.x));
        Assert.Equal(1, path.head()!.x);
        Assert.Equal(9, path.tail()!.x);
        Assert.Equal(2, path.removeAt(1)!.x);
        Assert.Null(path.removeAt(7));
        var extra = new FlxPoint(5, 5);
        path.addPoint(extra);
        Assert.Same(extra, path.remove(extra));
        Assert.Null(path.remove(extra));
        path.destroy();
        Assert.Null(path.head());
    }
}
