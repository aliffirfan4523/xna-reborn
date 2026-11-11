using org.flixel;

namespace XnaReborn.Flixel.Tests;

public class FlxPointTests
{
    [Fact]
    public void MakeAndCopyMoveCoordinates()
    {
        var p = new FlxPoint(1, 2);
        var q = new FlxPoint().copyFrom(p).make(5, 6);
        Assert.Equal((1d, 2d), (p.x, p.y));
        Assert.Equal((5d, 6d), (q.x, q.y));
        var r = q.copyTo();
        Assert.Equal((5d, 6d), (r.x, r.y));
    }
}
