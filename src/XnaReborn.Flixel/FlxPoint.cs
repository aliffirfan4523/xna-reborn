namespace org.flixel;

/// <summary>
/// A simple 2D point with public fields, like Flixel's <c>FlxPoint</c>.
/// Written for XNA Reborn against the public Flixel 2.x API; see THIRD-PARTY-NOTICES.md for the Flixel (MIT) notice.
/// </summary>
public class FlxPoint
{
    /// <summary>The horizontal position.</summary>
    public double x;

    /// <summary>The vertical position.</summary>
    public double y;

    /// <summary>Creates a point.</summary>
    /// <param name="X">The horizontal position.</param>
    /// <param name="Y">The vertical position.</param>
    public FlxPoint(double X = 0, double Y = 0)
    {
        x = X;
        y = Y;
    }

    /// <summary>Sets both coordinates and returns this point.</summary>
    /// <param name="X">The horizontal position.</param>
    /// <param name="Y">The vertical position.</param>
    public FlxPoint make(double X = 0, double Y = 0)
    {
        x = X;
        y = Y;
        return this;
    }

    /// <summary>Copies the coordinates of <paramref name="point"/> into this point.</summary>
    /// <param name="point">The source point.</param>
    public FlxPoint copyFrom(FlxPoint point)
    {
        x = point.x;
        y = point.y;
        return this;
    }

    /// <summary>Copies this point into <paramref name="point"/>, or into a new point when null.</summary>
    /// <param name="point">The target point, or null.</param>
    public FlxPoint copyTo(FlxPoint? point = null)
    {
        point ??= new FlxPoint();
        point.x = x;
        point.y = y;
        return point;
    }
}
