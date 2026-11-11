namespace org.flixel;

/// <summary>
/// A rectangle with public fields, like Flixel's <c>FlxRect</c>.
/// Written for XNA Reborn against the public Flixel 2.x API; see THIRD-PARTY-NOTICES.md for the Flixel (MIT) notice.
/// </summary>
public class FlxRect
{
    /// <summary>The left edge.</summary>
    public double x;

    /// <summary>The top edge.</summary>
    public double y;

    /// <summary>The width.</summary>
    public double width;

    /// <summary>The height.</summary>
    public double height;

    /// <summary>Creates a rectangle.</summary>
    public FlxRect(double X = 0, double Y = 0, double Width = 0, double Height = 0)
    {
        x = X;
        y = Y;
        width = Width;
        height = Height;
    }

    /// <summary>The left edge. Same as <see cref="x"/>.</summary>
    public double left => x;

    /// <summary>The right edge: x plus width.</summary>
    public double right => x + width;

    /// <summary>The top edge. Same as <see cref="y"/>.</summary>
    public double top => y;

    /// <summary>The bottom edge: y plus height.</summary>
    public double bottom => y + height;

    /// <summary>Sets all four values and returns this rectangle.</summary>
    public FlxRect make(double X = 0, double Y = 0, double Width = 0, double Height = 0)
    {
        x = X;
        y = Y;
        width = Width;
        height = Height;
        return this;
    }

    /// <summary>Copies another rectangle into this one.</summary>
    public FlxRect copyFrom(FlxRect rect)
    {
        x = rect.x;
        y = rect.y;
        width = rect.width;
        height = rect.height;
        return this;
    }

    /// <summary>Copies this rectangle into <paramref name="rect"/>, or into a new one when null.</summary>
    public FlxRect copyTo(FlxRect? rect = null)
    {
        rect ??= new FlxRect();
        return rect.copyFrom(this);
    }

    /// <summary>True when the two rectangles overlap. Rectangles that only touch do not overlap.</summary>
    public bool overlaps(FlxRect rect) => rect.x + rect.width > x && rect.x < x + width && rect.y + rect.height > y && rect.y < y + height;
}
