using XnaReborn.As3;

namespace org.flixel;

/// <summary>A node of a singly linked list of objects, like Flixel's <c>FlxList</c>.</summary>
public sealed class FlxList
{
    /// <summary>The object in this node, or null.</summary>
    public FlxObject? @object;

    /// <summary>The next node, or null at the end.</summary>
    public FlxList? next;
}

/// <summary>
/// Splits the world into a tree of boxes so that only objects that share a box are tested against each other.
/// Written for XNA Reborn against the public Flixel 2.x API. Objects that do not touch the root box are ignored.
/// </summary>
public sealed class FlxQuadTree : FlxRect
{
    /// <summary>How many times a box may be split into four. Set by <c>FlxG.worldDivisions</c>.</summary>
    public static int divisions = 6;

    private readonly int _depth;
    private readonly List<FlxObject> _a = new();
    private readonly List<FlxObject> _b = new();
    private readonly FlxQuadTree?[] _children = new FlxQuadTree?[4];
    private readonly Dictionary<FlxObject, int> _order;
    private readonly HashSet<(FlxObject, FlxObject)> _done = new();
    private bool _sameSet;
    private AsFunction? _notify;
    private Func<FlxObject, FlxObject, bool>? _process;

    /// <summary>Creates a tree (or, with a parent, one of its boxes) that covers a rectangle.</summary>
    public FlxQuadTree(double X, double Y, double Width, double Height, FlxQuadTree? Parent = null)
        : base(X, Y, Width, Height)
    {
        _depth = Parent is null ? 0 : Parent._depth + 1;
        _order = Parent?._order ?? new Dictionary<FlxObject, int>();
    }

    /// <summary>Puts the objects in the tree. With no second set, the first set is tested against itself.</summary>
    /// <param name="ObjectOrGroup1">An object or a group.</param>
    /// <param name="ObjectOrGroup2">Another object or group, or null.</param>
    /// <param name="NotifyCallback">Called with each pair that passes <paramref name="ProcessCallback"/>.</param>
    /// <param name="ProcessCallback">Decides whether a pair counts. Null means every overlapping pair counts.</param>
    public void load(FlxBasic? ObjectOrGroup1, FlxBasic? ObjectOrGroup2 = null, AsFunction? NotifyCallback = null, Func<FlxObject, FlxObject, bool>? ProcessCallback = null)
    {
        _notify = NotifyCallback;
        _process = ProcessCallback;
        _sameSet = ObjectOrGroup2 is null || ReferenceEquals(ObjectOrGroup1, ObjectOrGroup2);
        var first = Flatten(ObjectOrGroup1);
        foreach (var o in first)
        {
            Add(o, _a);
        }

        foreach (var o in _sameSet ? first : Flatten(ObjectOrGroup2))
        {
            Add(o, _b);
        }
    }

    /// <summary>Tests the pairs in each box. Returns true when at least one pair passed the process callback.</summary>
    public bool execute()
    {
        _done.Clear();
        return Visit(this, new List<FlxObject>(), new List<FlxObject>());
    }

    /// <summary>The objects of an object or group that exist and can collide, in order.</summary>
    internal static List<FlxObject> Flatten(FlxBasic? root)
    {
        var list = new List<FlxObject>();
        void Walk(FlxBasic? b)
        {
            switch (b)
            {
                case FlxGroup g when g.exists:
                    foreach (var m in g.members)
                    {
                        Walk(m);
                    }

                    break;
                case FlxObject o when o.exists && o.allowCollisions != FlxObject.NONE:
                    list.Add(o);
                    break;
            }
        }

        Walk(root);
        return list;
    }

    private void Add(FlxObject o, List<FlxObject> list)
    {
        if (!_order.ContainsKey(o))
        {
            _order[o] = _order.Count;
        }

        if (_depth == 0 && !overlaps(new FlxRect(o.x, o.y, o.width, o.height)))
        {
            return;
        }

        Place(o, list);
    }

    private void Place(FlxObject o, List<FlxObject> list)
    {
        if (_depth < divisions)
        {
            var midX = x + (width / 2);
            var midY = y + (height / 2);
            var left = o.x >= x && o.x + o.width <= midX;
            var right = o.x >= midX && o.x + o.width <= x + width;
            var top = o.y >= y && o.y + o.height <= midY;
            var bottom = o.y >= midY && o.y + o.height <= y + height;
            var index = (left, right, top, bottom) switch
            {
                (true, _, true, _) => 0,
                (_, true, true, _) => 1,
                (true, _, _, true) => 2,
                (_, true, _, true) => 3,
                _ => -1,
            };
            if (index >= 0)
            {
                var cw = width / 2;
                var ch = height / 2;
                _children[index] ??= new FlxQuadTree(x + ((index & 1) * cw), y + ((index >> 1) * ch), cw, ch, this);
                _children[index]!.Place(o, list == _a ? _children[index]!._a : _children[index]!._b);
                return;
            }
        }

        list.Add(o);
    }

    private bool Visit(FlxQuadTree node, List<FlxObject> upA, List<FlxObject> upB)
    {
        var hit = false;
        foreach (var a in node._a)
        {
            foreach (var b in node._b.Concat(upB))
            {
                hit |= Test(a, b);
            }
        }

        foreach (var b in node._b)
        {
            foreach (var a in upA)
            {
                hit |= Test(a, b);
            }
        }

        var nextA = upA.Concat(node._a).ToList();
        var nextB = upB.Concat(node._b).ToList();
        foreach (var child in node._children)
        {
            if (child is not null)
            {
                hit |= Visit(child, nextA, nextB);
            }
        }

        return hit;
    }

    private bool Test(FlxObject a, FlxObject b)
    {
        if (ReferenceEquals(a, b) || !a.exists || !b.exists || a.allowCollisions == FlxObject.NONE || b.allowCollisions == FlxObject.NONE)
        {
            return false;
        }

        if (_sameSet && _order[a] > _order[b])
        {
            (a, b) = (b, a);
        }

        if (_sameSet && !_done.Add((a, b)))
        {
            return false;
        }

        if (!HullsOverlap(a, b) || (_process is not null && !_process(a, b)))
        {
            return false;
        }

        _notify?.Call(a, b);
        return true;
    }

    // Each object is boxed together with where it was last frame, so a fast object cannot skip through a thin one.
    private static bool HullsOverlap(FlxObject a, FlxObject b)
    {
        var ax = Math.Min(a.x, a.last.x);
        var aw = a.width + Math.Abs(a.x - a.last.x);
        var ay = Math.Min(a.y, a.last.y);
        var ah = a.height + Math.Abs(a.y - a.last.y);
        var bx = Math.Min(b.x, b.last.x);
        var bw = b.width + Math.Abs(b.x - b.last.x);
        var by = Math.Min(b.y, b.last.y);
        var bh = b.height + Math.Abs(b.y - b.last.y);
        return ax + aw > bx && ax < bx + bw && ay + ah > by && ay < by + bh;
    }
}
