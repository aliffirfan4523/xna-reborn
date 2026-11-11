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

    public void DrawFrame(AssetRef texture, int srcX, int srcY, int srcWidth, int srcHeight, double x, double y, double width, double height, uint tintRgb, double alpha, bool flipHorizontal) =>
        Drawn.Add($"frame {texture.Name} src={srcX},{srcY},{srcWidth},{srcHeight} at={x},{y},{width},{height}{(flipHorizontal ? " flip" : string.Empty)}");

    public (int Width, int Height)? GetTextureSize(AssetRef texture) => Textures.TryGetValue(texture.Name, out var s) ? s : null;

    public void PlaySound(AssetRef sound, double volume, bool looped) => Played.Add(sound.Name);

    public byte[] ReadData(AssetRef data) => Data.TryGetValue(data.Name, out var bytes) ? bytes : throw new FileNotFoundException(data.Name);
}
