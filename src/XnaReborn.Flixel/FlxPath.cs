namespace org.flixel;

/// <summary>
/// A list of points an object can follow, like Flixel's <c>FlxPath</c>. This runtime keeps the data only: nothing moves an object along it.
/// Written for XNA Reborn against the public Flixel 2.x API; see THIRD-PARTY-NOTICES.md for the Flixel (MIT) notice.
/// </summary>
public class FlxPath
{
    /// <summary>The points, in order.</summary>
    public List<FlxPoint> nodes;

    /// <summary>Colour used when debug drawing. Not drawn here.</summary>
    public uint debugColor = 0xffffffff;

    /// <summary>Creates a path, optionally from points (they are copied by reference).</summary>
    public FlxPath(List<FlxPoint>? Nodes = null) => nodes = Nodes ?? new List<FlxPoint>();

    /// <summary>Adds a point at the end.</summary>
    public void add(double X, double Y) => nodes.Add(new FlxPoint(X, Y));

    /// <summary>Adds a point at an index. An index past the end adds it at the end.</summary>
    public void addAt(double X, double Y, int Index) => nodes.Insert(Math.Clamp(Index, 0, nodes.Count), new FlxPoint(X, Y));

    /// <summary>Adds an existing point at the end.</summary>
    public void addPoint(FlxPoint Node) => nodes.Add(Node);

    /// <summary>Removes the first point equal to this object, and returns it, or null.</summary>
    public FlxPoint? remove(FlxPoint Node) => nodes.Remove(Node) ? Node : null;

    /// <summary>Removes the point at an index and returns it, or null for a bad index.</summary>
    public FlxPoint? removeAt(int Index)
    {
        if (Index < 0 || Index >= nodes.Count)
        {
            return null;
        }

        var node = nodes[Index];
        nodes.RemoveAt(Index);
        return node;
    }

    /// <summary>The first point, or null for an empty path.</summary>
    public FlxPoint? head() => nodes.Count > 0 ? nodes[0] : null;

    /// <summary>The last point, or null for an empty path.</summary>
    public FlxPoint? tail() => nodes.Count > 0 ? nodes[^1] : null;

    /// <summary>Empties the path.</summary>
    public void destroy() => nodes.Clear();
}
