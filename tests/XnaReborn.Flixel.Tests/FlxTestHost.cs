using Microsoft.Xna.Framework.Input;
using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

/// <summary>Runs Flixel with no graphics device: a recording renderer, in-memory texture sizes and scripted key presses.</summary>
public sealed class FlxTestHost : IFlxRenderer, IFlxAssets, IFlxKeyState, IFlxMouseState
{
    private readonly HashSet<Keys> _down = new();

    /// <summary>Texture sizes by content name.</summary>
    public Dictionary<string, (int Width, int Height)> Textures { get; } = new();

    /// <summary>Everything drawn since the last <see cref="Frame"/>.</summary>
    public List<string> Drawn { get; } = new();

    /// <summary>Sounds that were played.</summary>
    public List<string> Played { get; } = new();

    /// <summary>Embedded data files by content name.</summary>
    public Dictionary<string, byte[]> Data { get; } = new();

    public (double X, double Y) Position { get; set; }

    public bool LeftDown { get; set; }

    public int WheelTotal { get; set; }

    public FlxTestHost(int width = 320, int height = 240, double step = 1.0 / 60)
    {
        Step = step;
        FlxG.init(width, height);
        FlxG.renderer = this;
        FlxG.assets = this;
    }

    public double Step { get; }

    public void Press(params Keys[] keys) => _down.UnionWith(keys);

    public void MoveMouse(double x, double y) => Position = (x, y);

    public void Release(params Keys[] keys) => _down.ExceptWith(keys);

    public void Start(FlxState first) => FlxG.switchState(first);

    /// <summary>Runs <paramref name="count"/> frames: update, then draw.</summary>
    public void Frame(int count = 1)
    {
        for (var i = 0; i < count; i++)
        {
            Drawn.Clear();
            FlxG.step(Step, this, this);
            FlxG.drawFrame();
        }
    }

    public bool IsDown(Keys key) => _down.Contains(key);

    public void Clear(uint argb) => Drawn.Add($"clear {argb:x8}");

    public void DrawQuad(double x, double y, double width, double height, uint argb) => Drawn.Add($"quad {x},{y},{width},{height} {argb:x8}");

    /// <summary>Sprite fonts by content name: width of one character and height of a line, at scale 1.</summary>
    public Dictionary<string, (double CharWidth, double LineHeight)> Fonts { get; } = new();

    public void DrawText(AssetRef font, string text, double x, double y, double scale, uint argb) => Drawn.Add($"text {font.Name} '{text}' at={x},{y} scale={scale} {argb:x8}");

    public (double Width, double Height)? MeasureText(AssetRef font, string text) =>
        Fonts.TryGetValue(font.Name, out var f) ? (text.Length * f.CharWidth, f.LineHeight) : null;

    public void DrawFrame(AssetRef texture, int srcX, int srcY, int srcWidth, int srcHeight, double x, double y, double width, double height, uint tintRgb, double alpha, bool flipHorizontal) =>
        Drawn.Add($"frame {texture.Name} src={srcX},{srcY},{srcWidth},{srcHeight} at={x},{y},{width},{height}{(flipHorizontal ? " flip" : string.Empty)}");

    public (int Width, int Height)? GetTextureSize(AssetRef texture) => Textures.TryGetValue(texture.Name, out var s) ? s : null;

    /// <summary>Every sound made so far.</summary>
    public List<FakeSound> Sounds { get; } = new();

    public IFlxSoundInstance CreateSound(AssetRef sound)
    {
        var fake = new FakeSound(sound.Name, this);
        Sounds.Add(fake);
        return fake;
    }

    public byte[] ReadData(AssetRef data) => Data.TryGetValue(data.Name, out var bytes) ? bytes : throw new FileNotFoundException(data.Name);
}

/// <summary>A sound that records what it is told.</summary>
public sealed class FakeSound(string name, FlxTestHost host) : IFlxSoundInstance
{
    private bool _paused;

    public string Name { get; } = name;

    public double Volume { get; set; }

    public bool Looped { get; set; }

    public bool IsPlaying { get; set; }

    public int Stops { get; private set; }

    public bool Disposed { get; private set; }

    public void Play()
    {
        IsPlaying = true;
        _paused = false;
        host.Played.Add(Name);
    }

    public void Pause()
    {
        IsPlaying = false;
        _paused = true;
    }

    public void Resume()
    {
        IsPlaying = true;
        _paused = false;
    }

    public void Stop()
    {
        IsPlaying = false;
        _paused = false;
        Stops++;
    }

    public bool Paused => _paused;

    public void Dispose() => Disposed = true;
}
