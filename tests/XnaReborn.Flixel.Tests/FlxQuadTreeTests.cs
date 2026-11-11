using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public sealed class FlxQuadTreeTests
{
    private static FlxObject Box(double x, double y, double w = 10, double h = 10) => new(x, y, w, h);

    private static AsFunction Notify(List<(FlxObject, FlxObject)> pairs) =>
        new(new Action<FlxObject, FlxObject>((a, b) => pairs.Add((a, b))));

    [Fact]
    public void Only_overlapping_pairs_reach_the_process_callback()
    {
        var calls = 0;
        var near1 = Box(5, 5);
        var near2 = Box(8, 8);
        var far = Box(300, 220);
        var group = new FlxGroup();
        group.add(near1);
        group.add(near2);
        group.add(far);
        var tree = new FlxQuadTree(0, 0, 320, 240);
        tree.load(group, null, null, (_, _) =>
        {
            calls++;
            return true;
        });

        Assert.True(tree.execute());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void A_big_object_that_spans_boxes_is_tested_against_objects_in_each_of_them()
    {
        _ = new FlxTestHost();
        var pairs = new List<(FlxObject, FlxObject)>();
        var wide = Box(0, 0, 320, 10);
        var left = Box(10, 2);
        var right = Box(300, 2);
        var below = Box(10, 200);
        var tree = new FlxQuadTree(0, 0, 320, 240);
        var targets = new FlxGroup();
        targets.add(left);
        targets.add(right);
        targets.add(below);
        tree.load(wide, targets, Notify(pairs));
        Assert.True(tree.execute());
        Assert.Equal([(wide, left), (wide, right)], pairs.OrderBy(p => p.Item2.x).ToList());
    }

    [Fact]
    public void One_group_against_itself_reports_each_pair_once_in_group_order()
    {
        var pairs = new List<(FlxObject, FlxObject)>();
        var group = new FlxGroup();
        var a = (FlxObject)group.add(Box(0, 0));
        var b = (FlxObject)group.add(Box(5, 5));
        var c = (FlxObject)group.add(Box(100, 100));
        var tree = new FlxQuadTree(0, 0, 320, 240);
        tree.load(group, null, Notify(pairs));
        tree.execute();
        Assert.Equal([(a, b)], pairs);
        Assert.DoesNotContain(pairs, p => p.Item1 == c || p.Item2 == c);
    }

    [Fact]
    public void Objects_outside_the_world_bounds_are_ignored_until_the_bounds_grow()
    {
        _ = new FlxTestHost();
        var a = Box(1000, 10);
        var b = Box(1005, 10);
        Assert.False(FlxG.overlap(a, b));
        FlxU.setWorldBounds(0, 0, 2000, 240);
        Assert.True(FlxG.overlap(a, b));
    }

    [Fact]
    public void Collide_separates_a_mover_from_a_wall_on_x_and_notifies_the_pair()
    {
        _ = new FlxTestHost();
        var wall = new FlxObject(100, 0, 20, 100) { immovable = true };
        var mover = new FlxObject(85, 10, 18, 10);
        mover.last.make(70, 10);
        var pairs = new List<(FlxObject, FlxObject)>();
        Assert.True(FlxG.collide(mover, wall, Notify(pairs)));
        Assert.Equal(82, mover.x);
        Assert.Equal([(mover, wall)], pairs);
    }

    [Fact]
    public void Collide_separates_on_y_between_two_groups()
    {
        _ = new FlxTestHost();
        var floor = new FlxGroup();
        var tile = new FlxObject(0, 100, 100, 10) { immovable = true };
        floor.add(tile);
        var actors = new FlxGroup();
        var actor = new FlxObject(20, 92, 10, 10);
        actor.last.make(20, 88);
        actors.add(actor);
        Assert.True(FlxG.collide(actors, floor));
        Assert.Equal(90, actor.y);
        Assert.True(actor.isTouching(FlxObject.FLOOR));
    }

    [Fact]
    public void A_one_way_platform_stops_a_falling_object_but_lets_a_rising_one_through()
    {
        _ = new FlxTestHost();
        var platform = new FlxObject(0, 100, 100, 5) { immovable = true, allowCollisions = FlxObject.UP };
        var faller = new FlxObject(20, 92, 10, 10);
        faller.last.make(20, 88);
        Assert.True(FlxG.collide(faller, platform));
        Assert.Equal(90, faller.y);

        var riser = new FlxObject(20, 104, 10, 10);
        riser.last.make(20, 110);
        Assert.False(FlxG.collide(riser, platform));
        Assert.Equal(104, riser.y);
    }

    [Fact]
    public void A_process_callback_that_says_no_stops_the_notification()
    {
        _ = new FlxTestHost();
        var pairs = new List<(FlxObject, FlxObject)>();
        var a = Box(0, 0);
        var b = Box(5, 5);
        var no = new AsFunction(new Func<FlxObject, FlxObject, bool>((_, _) => false));
        Assert.False(FlxG.overlap(a, b, Notify(pairs), no));
        Assert.Empty(pairs);
    }

    [Fact]
    public void Dead_objects_and_objects_that_never_collide_are_left_out()
    {
        _ = new FlxTestHost();
        var a = Box(0, 0);
        var dead = Box(5, 5);
        dead.kill();
        var ghost = Box(5, 5);
        ghost.allowCollisions = FlxObject.NONE;
        Assert.False(FlxG.overlap(a, dead));
        Assert.False(FlxG.overlap(a, ghost));
    }

    [Fact]
    public void A_fast_object_cannot_skip_through_a_thin_one()
    {
        _ = new FlxTestHost();
        var thin = Box(100, 0, 2, 50);
        var bullet = Box(150, 10, 4, 4);
        bullet.last.make(50, 10);
        Assert.True(FlxG.overlap(bullet, thin));
    }

    [Fact]
    public void A_list_node_links_to_the_next_one()
    {
        var o = Box(0, 0);
        var list = new FlxList { @object = o, next = new FlxList() };
        Assert.Same(o, list.@object);
        Assert.NotNull(list.next);
        Assert.Null(list.next!.next);
    }
}
