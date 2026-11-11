namespace org.flixel;

/// <summary>Helper functions, a few of the Flixel <c>FlxU</c> set.</summary>
public static class FlxU
{
    /// <summary>One step of velocity under acceleration, drag and a maximum, as Flixel computes it.</summary>
    public static double computeVelocity(double Velocity, double Acceleration = 0, double Drag = 0, double Max = 10000)
    {
        if (Acceleration != 0)
        {
            Velocity += Acceleration * FlxG.elapsed;
        }
        else if (Drag != 0)
        {
            var d = Drag * FlxG.elapsed;
            if (Velocity - d > 0)
            {
                Velocity -= d;
            }
            else if (Velocity + d < 0)
            {
                Velocity += d;
            }
            else
            {
                Velocity = 0;
            }
        }

        if (Velocity != 0 && Max != 10000)
        {
            if (Velocity > Max)
            {
                Velocity = Max;
            }
            else if (Velocity < -Max)
            {
                Velocity = -Max;
            }
        }

        return Velocity;
    }

    /// <summary>Rounds down to a whole number.</summary>
    public static double floor(double Value) => Math.Floor(Value);

    /// <summary>Rounds up to a whole number.</summary>
    public static double ceil(double Value) => Math.Ceiling(Value);

    /// <summary>The smaller of two numbers.</summary>
    public static double min(double Number1, double Number2) => Number1 <= Number2 ? Number1 : Number2;

    /// <summary>The larger of two numbers.</summary>
    public static double max(double Number1, double Number2) => Number1 >= Number2 ? Number1 : Number2;

    /// <summary>The absolute value.</summary>
    public static double abs(double Value) => Value > 0 ? Value : -Value;

    /// <summary>The distance between two points.</summary>
    public static double getDistance(FlxPoint Point1, FlxPoint Point2)
    {
        var dx = Point1.x - Point2.x;
        var dy = Point1.y - Point2.y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>
    /// The angle in degrees from <paramref name="Point1"/> to <paramref name="Point2"/>. 0 points straight up and the angle grows clockwise,
    /// so right is 90 and down is 180 (or -180 when it comes from the left). Equal points give 0.
    /// </summary>
    public static double getAngle(FlxPoint Point1, FlxPoint Point2)
    {
        var dx = Point2.x - Point1.x;
        var dy = Point2.y - Point1.y;
        return dx == 0 && dy == 0 ? 0 : Math.Atan2(dx, -dy) * 180 / Math.PI;
    }

    /// <summary>Turns a point around a pivot. A positive angle in degrees turns clockwise on the screen (y points down).</summary>
    /// <returns><paramref name="Point"/> when given, otherwise a new point.</returns>
    public static FlxPoint rotatePoint(double X, double Y, double PivotX, double PivotY, double Angle, FlxPoint? Point = null)
    {
        var radians = -Angle / 180 * Math.PI;
        var dx = X - PivotX;
        var dy = PivotY - Y;
        Point ??= new FlxPoint();
        Point.x = PivotX + (Math.Cos(radians) * dx) - (Math.Sin(radians) * dy);
        Point.y = PivotY - ((Math.Sin(radians) * dx) + (Math.Cos(radians) * dy));
        return Point;
    }

    /// <summary>Formats seconds as <c>m:ss</c>, or <c>m:ss.cc</c> (hundredths) with <paramref name="ShowMS"/>.</summary>
    public static string formatTime(double Seconds, bool ShowMS = false)
    {
        var whole = (int)Seconds;
        var text = $"{whole / 60}:{whole % 60:00}";
        return ShowMS ? $"{text}.{(int)((Seconds - whole) * 100):00}" : text;
    }
}

/// <summary>A basic game object: a position, a size, motion and collision. Written against the public Flixel 2.x API.</summary>
public class FlxObject : FlxBasic
{
    /// <summary>Collision side flag: left.</summary>
    public const uint LEFT = 0x0001;

    /// <summary>Collision side flag: right.</summary>
    public const uint RIGHT = 0x0010;

    /// <summary>Collision side flag: up.</summary>
    public const uint UP = 0x0100;

    /// <summary>Collision side flag: down.</summary>
    public const uint DOWN = 0x1000;

    /// <summary>No collision side.</summary>
    public const uint NONE = 0;

    /// <summary>Same as <see cref="UP"/>.</summary>
    public const uint CEILING = UP;

    /// <summary>Same as <see cref="DOWN"/>.</summary>
    public const uint FLOOR = DOWN;

    /// <summary>Left and right.</summary>
    public const uint WALL = LEFT | RIGHT;

    /// <summary>Every side.</summary>
    public const uint ANY = LEFT | RIGHT | UP | DOWN;

    /// <summary>Extra overlap allowed before two objects count as touching, in pixels.</summary>
    public static double OVERLAP_BIAS = 4;

    /// <summary>Horizontal position.</summary>
    public double x;

    /// <summary>Vertical position.</summary>
    public double y;

    /// <summary>Width of the collision box.</summary>
    public double width;

    /// <summary>Height of the collision box.</summary>
    public double height;

    /// <summary>Position at the start of this frame.</summary>
    public FlxPoint last = new();

    /// <summary>How much the camera scroll moves this object: 1 moves with the world, 0 stays on screen (a HUD).</summary>
    public FlxPoint scrollFactor = new(1, 1);

    /// <summary>Speed in pixels per second.</summary>
    public FlxPoint velocity = new();

    /// <summary>Acceleration in pixels per second squared.</summary>
    public FlxPoint acceleration = new();

    /// <summary>Deceleration applied when there is no acceleration.</summary>
    public FlxPoint drag = new();

    /// <summary>Speed limit.</summary>
    public FlxPoint maxVelocity = new(10000, 10000);

    /// <summary>How much of its speed an object keeps when it bounces. 0 stops it.</summary>
    public double elasticity;

    /// <summary>Used when two movable objects collide.</summary>
    public double mass = 1;

    /// <summary>True means the object is not moved by collisions.</summary>
    public bool immovable;

    /// <summary>False means velocity and acceleration are ignored.</summary>
    public bool moves = true;

    /// <summary>Sides touched during the last collision test.</summary>
    public uint touching = NONE;

    /// <summary>Sides touched the frame before.</summary>
    public uint wasTouching = NONE;

    /// <summary>Sides that collide at all.</summary>
    public uint allowCollisions = ANY;

    /// <summary>Hit points. At zero or below <see cref="hurt"/> kills the object.</summary>
    public double health = 1;

    private double _flickerTimer;

    /// <summary>Creates an object.</summary>
    public FlxObject(double X = 0, double Y = 0, double Width = 0, double Height = 0)
    {
        x = X;
        y = Y;
        width = Width;
        height = Height;
        last.make(x, y);
    }

    /// <summary>True while <see cref="flicker"/> is running.</summary>
    public bool flickering => _flickerTimer > 0;

    /// <inheritdoc />
    public override void preUpdate()
    {
        last.x = x;
        last.y = y;
    }

    /// <summary>Counts down the flicker timer.</summary>
    public override void update()
    {
        if (_flickerTimer > 0)
        {
            _flickerTimer -= FlxG.elapsed;
            if (_flickerTimer < 0)
            {
                _flickerTimer = 0;
            }
        }
    }

    /// <summary>Applies motion, then remembers the touched sides and clears them for the next collision test.</summary>
    public override void postUpdate()
    {
        if (moves)
        {
            updateMotion();
        }

        wasTouching = touching;
        touching = NONE;
    }

    /// <summary>Moves the object by its velocity for one frame.</summary>
    protected virtual void updateMotion()
    {
        var delta = (FlxU.computeVelocity(velocity.x, acceleration.x, drag.x, maxVelocity.x) - velocity.x) / 2;
        velocity.x += delta;
        var move = velocity.x * FlxG.elapsed;
        velocity.x += delta;
        x += move;

        delta = (FlxU.computeVelocity(velocity.y, acceleration.y, drag.y, maxVelocity.y) - velocity.y) / 2;
        velocity.y += delta;
        move = velocity.y * FlxG.elapsed;
        velocity.y += delta;
        y += move;
    }

    /// <summary>True when the boxes of the two objects overlap.</summary>
    public bool overlaps(FlxObject Object) =>
        Object.x + Object.width > x && Object.x < x + width && Object.y + Object.height > y && Object.y < y + height;

    /// <summary>True when the object touched the given side during the last collision test.</summary>
    public bool isTouching(uint Direction) => (touching & Direction) > NONE;

    /// <summary>True when the side was touched this frame but not the frame before.</summary>
    public bool justTouched(uint Direction) => (touching & Direction) > NONE && (wasTouching & Direction) <= NONE;

    /// <summary>True when any part of the object is inside the game view.</summary>
    public bool onScreen() => x + width > 0 && x < FlxG.width && y + height > 0 && y < FlxG.height;

    /// <summary>Starts to blink for <paramref name="Duration"/> seconds.</summary>
    public void flicker(double Duration = 1) => _flickerTimer = Duration;

    /// <summary>Whether a blinking object should be drawn right now.</summary>
    protected bool flickerVisible => _flickerTimer <= 0 || ((int)(_flickerTimer * 25)) % 2 == 0;

    /// <summary>Takes hit points away and kills the object at zero or below.</summary>
    public virtual void hurt(double Damage)
    {
        health -= Damage;
        if (health <= 0)
        {
            kill();
        }
    }

    /// <summary>Puts the object at a position and brings it back to life.</summary>
    public virtual void reset(double X, double Y)
    {
        revive();
        touching = NONE;
        wasTouching = NONE;
        x = X;
        y = Y;
        last.x = x;
        last.y = y;
        velocity.x = 0;
        velocity.y = 0;
    }

    /// <summary>Separates two objects that overlap on either axis. Returns true if anything moved.</summary>
    public static bool separate(FlxObject Object1, FlxObject Object2)
    {
        var separatedX = separateX(Object1, Object2);
        var separatedY = separateY(Object1, Object2);
        return separatedX || separatedY;
    }

    /// <summary>Separates two objects along the x axis.</summary>
    public static bool separateX(FlxObject Object1, FlxObject Object2)
    {
        if (Object1.immovable && Object2.immovable)
        {
            return false;
        }

        double overlap = 0;
        var delta1 = Object1.x - Object1.last.x;
        var delta2 = Object2.x - Object2.last.x;
        if (delta1 != delta2)
        {
            var abs1 = Math.Abs(delta1);
            var abs2 = Math.Abs(delta2);
            var r1x = Object1.x - (delta1 > 0 ? delta1 : 0);
            var r2x = Object2.x - (delta2 > 0 ? delta2 : 0);
            if (r1x + Object1.width + abs1 > r2x && r1x < r2x + Object2.width + abs2
                && Object1.last.y + Object1.height > Object2.last.y && Object1.last.y < Object2.last.y + Object2.height)
            {
                var maxOverlap = abs1 + abs2 + OVERLAP_BIAS;
                if (delta1 > delta2)
                {
                    overlap = Object1.x + Object1.width - Object2.x;
                    if (overlap > maxOverlap || (Object1.allowCollisions & RIGHT) == 0 || (Object2.allowCollisions & LEFT) == 0)
                    {
                        overlap = 0;
                    }
                    else
                    {
                        Object1.touching |= RIGHT;
                        Object2.touching |= LEFT;
                    }
                }
                else if (delta1 < delta2)
                {
                    overlap = Object1.x - Object2.width - Object2.x;
                    if (-overlap > maxOverlap || (Object1.allowCollisions & LEFT) == 0 || (Object2.allowCollisions & RIGHT) == 0)
                    {
                        overlap = 0;
                    }
                    else
                    {
                        Object1.touching |= LEFT;
                        Object2.touching |= RIGHT;
                    }
                }
            }
        }

        if (overlap == 0)
        {
            return false;
        }

        var v1 = Object1.velocity.x;
        var v2 = Object2.velocity.x;
        if (!Object1.immovable && !Object2.immovable)
        {
            overlap *= 0.5;
            Object1.x -= overlap;
            Object2.x += overlap;
            (Object1.velocity.x, Object2.velocity.x) = Bounce(v1, v2, Object1, Object2);
        }
        else if (!Object1.immovable)
        {
            Object1.x -= overlap;
            Object1.velocity.x = v2 - v1 * Object1.elasticity;
        }
        else if (!Object2.immovable)
        {
            Object2.x += overlap;
            Object2.velocity.x = v1 - v2 * Object2.elasticity;
        }

        return true;
    }

    /// <summary>Separates two objects along the y axis.</summary>
    public static bool separateY(FlxObject Object1, FlxObject Object2)
    {
        if (Object1.immovable && Object2.immovable)
        {
            return false;
        }

        double overlap = 0;
        var delta1 = Object1.y - Object1.last.y;
        var delta2 = Object2.y - Object2.last.y;
        if (delta1 != delta2)
        {
            var abs1 = Math.Abs(delta1);
            var abs2 = Math.Abs(delta2);
            var r1y = Object1.y - (delta1 > 0 ? delta1 : 0);
            var r2y = Object2.y - (delta2 > 0 ? delta2 : 0);
            if (r1y + Object1.height + abs1 > r2y && r1y < r2y + Object2.height + abs2
                && Object1.x + Object1.width > Object2.x && Object1.x < Object2.x + Object2.width)
            {
                var maxOverlap = abs1 + abs2 + OVERLAP_BIAS;
                if (delta1 > delta2)
                {
                    overlap = Object1.y + Object1.height - Object2.y;
                    if (overlap > maxOverlap || (Object1.allowCollisions & DOWN) == 0 || (Object2.allowCollisions & UP) == 0)
                    {
                        overlap = 0;
                    }
                    else
                    {
                        Object1.touching |= DOWN;
                        Object2.touching |= UP;
                    }
                }
                else if (delta1 < delta2)
                {
                    overlap = Object1.y - Object2.height - Object2.y;
                    if (-overlap > maxOverlap || (Object1.allowCollisions & UP) == 0 || (Object2.allowCollisions & DOWN) == 0)
                    {
                        overlap = 0;
                    }
                    else
                    {
                        Object1.touching |= UP;
                        Object2.touching |= DOWN;
                    }
                }
            }
        }

        if (overlap == 0)
        {
            return false;
        }

        var v1 = Object1.velocity.y;
        var v2 = Object2.velocity.y;
        if (!Object1.immovable && !Object2.immovable)
        {
            overlap *= 0.5;
            Object1.y -= overlap;
            Object2.y += overlap;
            (Object1.velocity.y, Object2.velocity.y) = Bounce(v1, v2, Object1, Object2);
        }
        else if (!Object1.immovable)
        {
            Object1.y -= overlap;
            Object1.velocity.y = v2 - v1 * Object1.elasticity;
            if (Object2.active && Object2.moves && delta1 > delta2)
            {
                Object1.x += Object2.x - Object2.last.x;
            }
        }
        else if (!Object2.immovable)
        {
            Object2.y += overlap;
            Object2.velocity.y = v1 - v2 * Object2.elasticity;
            if (Object1.active && Object1.moves && delta1 < delta2)
            {
                Object2.x += Object1.x - Object1.last.x;
            }
        }

        return true;
    }

    private static (double, double) Bounce(double v1, double v2, FlxObject o1, FlxObject o2)
    {
        var n1 = Math.Sqrt(v2 * v2 * o2.mass / o1.mass) * (v2 > 0 ? 1 : -1);
        var n2 = Math.Sqrt(v1 * v1 * o1.mass / o2.mass) * (v1 > 0 ? 1 : -1);
        var average = (n1 + n2) * 0.5;
        n1 -= average;
        n2 -= average;
        return (average + n1 * o1.elasticity, average + n2 * o2.elasticity);
    }
}
