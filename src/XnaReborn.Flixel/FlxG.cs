using Microsoft.Xna.Framework.Input;
using XnaReborn.As3;

namespace org.flixel;

/// <summary>Draws things for the game. FlxGame implements it with SpriteBatch; tests use a recording fake.</summary>
public interface IFlxRenderer
{
    /// <summary>Fills the whole view.</summary>
    void Clear(uint argb);

    /// <summary>Draws a solid rectangle in game pixels.</summary>
    void DrawQuad(double x, double y, double width, double height, uint argb);

    /// <summary>Draws part of an embedded texture.</summary>
    void DrawFrame(AssetRef texture, int srcX, int srcY, int srcWidth, int srcHeight, double x, double y, double width, double height, uint tintRgb, double alpha, bool flipHorizontal);
}

/// <summary>Loads and plays embedded assets.</summary>
public interface IFlxAssets
{
    /// <summary>Size of a texture in pixels, or null when it cannot be found.</summary>
    (int Width, int Height)? GetTextureSize(AssetRef texture);

    /// <summary>Plays a sound once, or in a loop.</summary>
    void PlaySound(AssetRef sound, double volume, bool looped);

    /// <summary>Reads an embedded data file as bytes.</summary>
    byte[] ReadData(AssetRef data);
}

/// <summary>Reports which keys are down. The game uses the MonoGame keyboard; tests use a fake.</summary>
public interface IFlxKeyState
{
    /// <summary>True when the key is down now.</summary>
    bool IsDown(Keys key);
}

/// <summary>Keyboard state with Flixel's names: <c>FlxG.keys.LEFT</c>, <c>FlxG.keys.justPressed("X")</c>.</summary>
public sealed class FlxKeyboard
{
    private readonly Dictionary<string, Keys> _map = BuildMap();
    private readonly HashSet<Keys> _down = new();
    private readonly HashSet<Keys> _previous = new();

    /// <summary>Reads the new key state. Call once per frame.</summary>
    public void update(IFlxKeyState state)
    {
        _previous.Clear();
        _previous.UnionWith(_down);
        _down.Clear();
        foreach (var key in _map.Values.Distinct().Where(state.IsDown))
        {
            _down.Add(key);
        }
    }

    /// <summary>True while the key is held. Names are Flixel key names such as "LEFT", "SPACE" or "X".</summary>
    public bool pressed(string KeyName) => _map.TryGetValue(KeyName.ToUpperInvariant(), out var k) && _down.Contains(k);

    /// <summary>True on the frame the key went down.</summary>
    public bool justPressed(string KeyName) => _map.TryGetValue(KeyName.ToUpperInvariant(), out var k) && _down.Contains(k) && !_previous.Contains(k);

    /// <summary>True on the frame the key went up.</summary>
    public bool justReleased(string KeyName) => _map.TryGetValue(KeyName.ToUpperInvariant(), out var k) && !_down.Contains(k) && _previous.Contains(k);

    /// <summary>Left arrow.</summary>
    public bool LEFT => pressed("LEFT");

    /// <summary>Right arrow.</summary>
    public bool RIGHT => pressed("RIGHT");

    /// <summary>Up arrow.</summary>
    public bool UP => pressed("UP");

    /// <summary>Down arrow.</summary>
    public bool DOWN => pressed("DOWN");

    /// <summary>Space bar.</summary>
    public bool SPACE => pressed("SPACE");

    /// <summary>Enter key.</summary>
    public bool ENTER => pressed("ENTER");

    /// <summary>Escape key.</summary>
    public bool ESCAPE => pressed("ESCAPE");

    /// <summary>Shift key.</summary>
    public bool SHIFT => pressed("SHIFT");

    /// <summary>The A key.</summary>
    public bool A => pressed("A");

    /// <summary>The C key.</summary>
    public bool C => pressed("C");

    /// <summary>The D key.</summary>
    public bool D => pressed("D");

    /// <summary>The S key.</summary>
    public bool S => pressed("S");

    /// <summary>The W key.</summary>
    public bool W => pressed("W");

    /// <summary>The X key.</summary>
    public bool X => pressed("X");

    /// <summary>The Z key.</summary>
    public bool Z => pressed("Z");

    private static Dictionary<string, Keys> BuildMap()
    {
        var map = new Dictionary<string, Keys>(StringComparer.Ordinal)
        {
            ["LEFT"] = Keys.Left,
            ["RIGHT"] = Keys.Right,
            ["UP"] = Keys.Up,
            ["DOWN"] = Keys.Down,
            ["SPACE"] = Keys.Space,
            ["ENTER"] = Keys.Enter,
            ["ESCAPE"] = Keys.Escape,
            ["SHIFT"] = Keys.LeftShift,
            ["CONTROL"] = Keys.LeftControl,
            ["TAB"] = Keys.Tab,
        };
        for (var c = 'A'; c <= 'Z'; c++)
        {
            map[c.ToString()] = Enum.Parse<Keys>(c.ToString());
        }

        for (var d = 0; d <= 9; d++)
        {
            map["D" + d] = Enum.Parse<Keys>("D" + d);
            map[d switch { 0 => "ZERO", 1 => "ONE", 2 => "TWO", 3 => "THREE", 4 => "FOUR", 5 => "FIVE", 6 => "SIX", 7 => "SEVEN", 8 => "EIGHT", _ => "NINE" }] = Enum.Parse<Keys>("D" + d);
        }

        return map;
    }
}

/// <summary>A sound that was started with <see cref="FlxG.play"/>.</summary>
public sealed class FlxSound
{
    /// <summary>The sound asset.</summary>
    public AssetRef? asset;

    /// <summary>Loudness from 0 to 1.</summary>
    public double volume = 1;

    /// <summary>Whether the sound loops.</summary>
    public bool looped;
}

/// <summary>Global game state and helpers, like Flixel's <c>FlxG</c>.</summary>
public static class FlxG
{
    private static FlxState? _requested;

    /// <summary>Width of the game view in game pixels.</summary>
    public static int width = 320;

    /// <summary>Height of the game view in game pixels.</summary>
    public static int height = 240;

    /// <summary>Seconds since the last frame.</summary>
    public static double elapsed;

    /// <summary>Background colour as 0xAARRGGBB.</summary>
    public static uint bgColor = 0xff000000;

    /// <summary>The keyboard.</summary>
    public static FlxKeyboard keys = new();

    /// <summary>Seed of <see cref="random"/>, a number from 0 to 1. Set it to repeat a sequence.</summary>
    public static double globalSeed = Random.Shared.NextDouble();

    /// <summary>The state that is running, or null before the game starts.</summary>
    public static FlxState? state { get; private set; }

    /// <summary>Draws things. Set by the game.</summary>
    public static IFlxRenderer? renderer { get; set; }

    /// <summary>Loads and plays assets. Set by the game.</summary>
    public static IFlxAssets? assets { get; set; }

    /// <summary>Sets up the view size and clears the state. Called by the game and by test hosts.</summary>
    public static void init(int Width, int Height)
    {
        width = Width;
        height = Height;
        elapsed = 0;
        bgColor = 0xff000000;
        keys = new FlxKeyboard();
        state = null;
        _requested = null;
        FlxTimer.manager.clear();
    }

    /// <summary>The next number of Flixel's seeded sequence, from 0 up to but not including 1.</summary>
    public static double random() => globalSeed = (69621L * (long)(globalSeed * 0x7FFFFFFF) % 0x7FFFFFFF) / (double)0x7FFFFFFF;

    /// <summary>Picks a random item. <paramref name="Length"/> 0 means up to the end.</summary>
    public static object? getRandom(AsArray? Objects, int StartIndex = 0, int Length = 0)
    {
        if (Objects is null || Objects.length == 0)
        {
            return null;
        }

        var start = Math.Clamp(StartIndex, 0, Objects.length - 1);
        var count = Length <= 0 || start + Length > Objects.length ? Objects.length - start : Length;
        return Objects[start + (int)(random() * count)];
    }

    /// <summary>Asks for a new state. It starts at the beginning of the next frame.</summary>
    public static void switchState(FlxState State) => _requested = State;

    /// <summary>Restarts the current state at the beginning of the next frame.</summary>
    public static void resetState()
    {
        if (state is not null)
        {
            _requested = (FlxState)Activator.CreateInstance(state.GetType())!;
        }
    }

    /// <summary>Runs one frame: starts a requested state, reads the keys, updates the state.</summary>
    public static void step(double Elapsed, IFlxKeyState? keyState = null)
    {
        elapsed = Elapsed;
        if (_requested is not null)
        {
            state?.destroy();
            FlxTimer.manager.clear();
            state = _requested;
            _requested = null;
            state.create();
        }

        if (keyState is not null)
        {
            keys.update(keyState);
        }

        FlxTimer.manager.update();
        state?.update();
    }

    /// <summary>Draws one frame.</summary>
    public static void drawFrame()
    {
        renderer?.Clear(bgColor);
        state?.draw();
    }

    /// <summary>Plays an embedded sound once, or in a loop.</summary>
    public static FlxSound play(AsClass EmbeddedSound, double Volume = 1, bool Looped = false)
    {
        var sound = new FlxSound { asset = EmbeddedSound.Asset, volume = Volume, looped = Looped };
        if (sound.asset is not null)
        {
            assets?.PlaySound(sound.asset, Volume, Looped);
        }

        return sound;
    }

    /// <summary>Separates every pair of objects that overlap. <paramref name="NotifyCallback"/> is called with each separated pair.</summary>
    public static bool collide(FlxBasic? ObjectOrGroup1 = null, FlxBasic? ObjectOrGroup2 = null, AsFunction? NotifyCallback = null) =>
        Run(ObjectOrGroup1, ObjectOrGroup2, NotifyCallback, FlxObject.separate);

    /// <summary>
    /// Calls <paramref name="NotifyCallback"/> for each pair of objects that overlap. With <paramref name="ProcessCallback"/>
    /// a pair only counts when the callback returns true.
    /// </summary>
    // shortcut: tests every pair (O(n*m)) instead of Flixel's quad tree; fine for a few hundred objects. Add FlxQuadTree if a game needs more.
    public static bool overlap(FlxBasic? ObjectOrGroup1 = null, FlxBasic? ObjectOrGroup2 = null, AsFunction? NotifyCallback = null, AsFunction? ProcessCallback = null) =>
        Run(ObjectOrGroup1, ObjectOrGroup2, NotifyCallback, ProcessCallback is null ? null : (a, b) => AsOps.ToBoolean(ProcessCallback.Call(a, b)));

    private static bool Run(FlxBasic? ObjectOrGroup1, FlxBasic? ObjectOrGroup2, AsFunction? NotifyCallback, Func<FlxObject, FlxObject, bool>? ProcessCallback)
    {
        ObjectOrGroup1 ??= state;
        var first = Flatten(ObjectOrGroup1);
        var same = ObjectOrGroup2 is null || ReferenceEquals(ObjectOrGroup1, ObjectOrGroup2);
        var second = same ? first : Flatten(ObjectOrGroup2);
        var hit = false;
        for (var i = 0; i < first.Count; i++)
        {
            for (var j = same ? i + 1 : 0; j < second.Count; j++)
            {
                var a = first[i];
                var b = second[j];
                if (ReferenceEquals(a, b) || !a.exists || !b.exists || a.allowCollisions == FlxObject.NONE || b.allowCollisions == FlxObject.NONE)
                {
                    continue;
                }

                if (ProcessCallback is null ? !a.overlaps(b) : !SweptOverlap(a, b))
                {
                    continue;
                }

                if (ProcessCallback is not null && !ProcessCallback(a, b))
                {
                    continue;
                }

                hit = true;
                NotifyCallback?.Call(a, b);
            }
        }

        return hit;
    }

    private static bool SweptOverlap(FlxObject a, FlxObject b)
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

    private static List<FlxObject> Flatten(FlxBasic? root)
    {
        var list = new List<FlxObject>();
        void Add(FlxBasic? b)
        {
            switch (b)
            {
                case FlxGroup g when g.exists:
                    foreach (var m in g.members)
                    {
                        Add(m);
                    }

                    break;
                case FlxObject o when o.exists:
                    list.Add(o);
                    break;
            }
        }

        Add(root);
        return list;
    }
}
