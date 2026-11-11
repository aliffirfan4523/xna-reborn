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

    /// <summary>Makes a sound that can be played, paused and stopped. Textures and sounds are loaded from the Content folder.</summary>
    IFlxSoundInstance CreateSound(AssetRef sound);

    /// <summary>Reads an embedded data file as bytes.</summary>
    byte[] ReadData(AssetRef data);
}

/// <summary>Global game state and helpers, like Flixel's <c>FlxG</c>.</summary>
public static class FlxG
{
    private static FlxState? _requested;
    private static double _volume = 1;
    private static bool _mute;

    /// <summary>Width of the game view in game pixels.</summary>
    public static int width = 320;

    /// <summary>Height of the game view in game pixels.</summary>
    public static int height = 240;

    /// <summary>Seconds since the last frame.</summary>
    public static double elapsed;

    /// <summary>Background colour as 0xAARRGGBB.</summary>
    public static uint bgColor
    {
        get => camera.bgColor;
        set => camera.bgColor = value;
    }

    /// <summary>The camera.</summary>
    public static FlxCamera camera { get; private set; } = new();

    /// <summary>The keyboard.</summary>
    public static FlxKeyboard keys = new();

    /// <summary>The mouse.</summary>
    public static FlxMouse mouse = new();

    /// <summary>Gamepads. A stub: nothing is ever pressed.</summary>
    public static FlxGamepad[] gamepads = [new FlxGamepad()];

    /// <summary>Seed of <see cref="random"/>, a number from 0 to 1. Set it to repeat a sequence.</summary>
    public static double globalSeed = Random.Shared.NextDouble();

    /// <summary>The box that collisions are tested in. Objects entirely outside it are ignored.</summary>
    public static FlxRect worldBounds = new(-10, -10, 330, 260);

    /// <summary>How many times the world box is split by the quad tree.</summary>
    public static int worldDivisions = 6;

    /// <summary>Master loudness from 0 to 1, for the music and every sound.</summary>
    public static double volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 1);
            ApplyVolume();
        }
    }

    /// <summary>True silences everything without changing <see cref="volume"/>.</summary>
    public static bool mute
    {
        get => _mute;
        set
        {
            _mute = value;
            ApplyVolume();
        }
    }

    /// <summary>The sounds started by <see cref="play"/>.</summary>
    public static FlxGroup sounds = new();

    /// <summary>The song started by <see cref="playMusic"/>, or null.</summary>
    public static FlxSound? music;

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
        camera = new FlxCamera();
        worldBounds = new FlxRect(-10, -10, Width + 20, Height + 20);
        worldDivisions = 6;
        keys = new FlxKeyboard();
        mouse = new FlxMouse();
        destroySounds(true);
        _volume = 1;
        _mute = false;
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

    /// <summary>Runs one frame: starts a requested state, reads the keys and mouse, updates the state.</summary>
    public static void step(double Elapsed, IFlxKeyState? keyState = null, IFlxMouseState? mouseState = null)
    {
        elapsed = Elapsed;
        if (_requested is not null)
        {
            state?.destroy();
            destroySounds();
            FlxTimer.manager.clear();
            state = _requested;
            _requested = null;
            state.create();
        }

        if (keyState is not null)
        {
            keys.update(keyState);
        }

        if (mouseState is not null)
        {
            mouse.update(mouseState);
        }

        FlxTimer.manager.update();
        state?.update();
        sounds.update();
        music?.update();
        camera.update();
    }

    private sealed class ShiftedRenderer(IFlxRenderer inner, double dx, double dy) : IFlxRenderer
    {
        public void Clear(uint argb) => inner.Clear(argb);

        public void DrawQuad(double x, double y, double width, double height, uint argb) => inner.DrawQuad(x + dx, y + dy, width, height, argb);

        public void DrawFrame(AssetRef texture, int srcX, int srcY, int srcWidth, int srcHeight, double x, double y, double width, double height, uint tintRgb, double alpha, bool flipHorizontal) =>
            inner.DrawFrame(texture, srcX, srcY, srcWidth, srcHeight, x + dx, y + dy, width, height, tintRgb, alpha, flipHorizontal);
    }

    /// <summary>Draws one frame.</summary>
    public static void drawFrame()
    {
        if (renderer is not { } real)
        {
            return;
        }

        real.Clear(bgColor);
        var shake = camera.shakeOffset;
        if (shake.x != 0 || shake.y != 0)
        {
            renderer = new ShiftedRenderer(real, shake.x, shake.y);
            try
            {
                state?.draw();
            }
            finally
            {
                renderer = real;
            }
        }
        else
        {
            state?.draw();
        }

        camera.drawEffects(real);
    }

    /// <summary>Reads an embedded data file (a class made by an <c>[Embed]</c> of a text or binary file) as bytes.</summary>
    public static byte[] loadBytes(AsClass Data)
    {
        var asset = Data.Asset ?? throw new ArgumentException("The class is not an embedded asset.", nameof(Data));
        return (assets ?? throw new InvalidOperationException("The game has not started.")).ReadData(asset);
    }

    /// <summary>Reads an embedded data file as UTF-8 text, without a byte order mark.</summary>
    public static string loadText(AsClass Data)
    {
        var bytes = loadBytes(Data);
        var skip = bytes is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
        return System.Text.Encoding.UTF8.GetString(bytes, skip, bytes.Length - skip);
    }

    /// <summary>Plays an embedded sound once, or in a loop. A sound that ended is reused by the next call when <paramref name="AutoDestroy"/> is true.</summary>
    public static FlxSound play(AsClass EmbeddedSound, double Volume = 1, bool Looped = false, bool AutoDestroy = true)
    {
        var sound = (FlxSound)sounds.recycle(AsClass.Of<FlxSound>())!;
        sound.loadEmbedded(EmbeddedSound, Looped, AutoDestroy);
        sound.volume = Volume;
        sound.play();
        return sound;
    }

    /// <summary>Plays one looping song. A song that is already playing is replaced.</summary>
    public static void playMusic(AsClass Music, double Volume = 1)
    {
        music ??= new FlxSound();
        music.loadEmbedded(Music, true);
        music.volume = Volume;
        music.play();
    }

    /// <summary>Pauses the music and every sound.</summary>
    public static void pauseSounds()
    {
        music?.pause();
        sounds.callAll(s => ((FlxSound)s).pause());
    }

    /// <summary>Carries on with the music and every sound that <see cref="pauseSounds"/> paused.</summary>
    public static void resumeSounds()
    {
        music?.Resume();
        sounds.callAll(s => ((FlxSound)s).Resume());
    }

    /// <summary>Stops and drops every sound. The music stays unless <paramref name="ForceDestroyMusic"/> is true.</summary>
    public static void destroySounds(bool ForceDestroyMusic = false)
    {
        if (ForceDestroyMusic)
        {
            music?.destroy();
            music = null;
        }

        sounds.destroy();
    }
    /// <summary>Separates every pair of objects that overlap. <paramref name="NotifyCallback"/> is called with each separated pair.</summary>
    public static bool collide(FlxBasic? ObjectOrGroup1 = null, FlxBasic? ObjectOrGroup2 = null, AsFunction? NotifyCallback = null) =>
        Run(ObjectOrGroup1, ObjectOrGroup2, NotifyCallback, FlxObject.separate);

    /// <summary>
    /// Calls <paramref name="NotifyCallback"/> for each pair of objects that overlap. With <paramref name="ProcessCallback"/>
    /// a pair only counts when the callback returns true.
    /// </summary>
    public static bool overlap(FlxBasic? ObjectOrGroup1 = null, FlxBasic? ObjectOrGroup2 = null, AsFunction? NotifyCallback = null, AsFunction? ProcessCallback = null) =>
        Run(ObjectOrGroup1, ObjectOrGroup2, NotifyCallback, ProcessCallback is null ? null : (a, b) => AsOps.ToBoolean(ProcessCallback.Call(a, b)));

    private static void ApplyVolume()
    {
        music?.Apply();
        sounds.callAll(s => ((FlxSound)s).Apply());
    }

    private static bool Run(FlxBasic? ObjectOrGroup1, FlxBasic? ObjectOrGroup2, AsFunction? NotifyCallback, Func<FlxObject, FlxObject, bool>? ProcessCallback)
    {
        ObjectOrGroup1 ??= state;
        var tree = new FlxQuadTree(worldBounds.x, worldBounds.y, worldBounds.width, worldBounds.height);
        FlxQuadTree.divisions = worldDivisions;
        tree.load(ObjectOrGroup1, ObjectOrGroup2, NotifyCallback, ProcessCallback);
        return tree.execute();
    }
}
