using XnaReborn.As3;

namespace org.flixel;

/// <summary>A game object with a picture: a solid rectangle, one texture, or a sheet of animation frames.</summary>
public class FlxSprite : FlxObject
{
    private sealed record Animation(string Name, int[] Frames, double Delay, bool Looped);

    private readonly Dictionary<string, Animation> _animations = new(StringComparer.Ordinal);
    private Animation? _current;
    private int _index;
    private double _frameTimer;
    private AssetRef? _graphic;
    private uint _fill;
    private bool _reverse;

    /// <summary>Where the picture is drawn relative to the collision box.</summary>
    public FlxPoint offset = new();

    /// <summary>Draw scale.</summary>
    public FlxPoint scale = new(1, 1);

    /// <summary>Tint as 0xRRGGBB. White means no tint.</summary>
    public uint color = 0xffffff;

    /// <summary>Opacity from 0 to 1.</summary>
    public double alpha = 1;

    /// <summary>The side the sprite faces: <see cref="FlxObject.LEFT"/> or <see cref="FlxObject.RIGHT"/>.</summary>
    public uint facing = RIGHT;

    /// <summary>Width of one frame in pixels.</summary>
    public int frameWidth;

    /// <summary>Height of one frame in pixels.</summary>
    public int frameHeight;

    /// <summary>Index of the frame that is drawn.</summary>
    public int frame;

    /// <summary>True when the current non-looping animation reached its end.</summary>
    public bool finished;

    /// <summary>Creates a sprite, optionally with a single-frame picture.</summary>
    public FlxSprite(double X = 0, double Y = 0, AsClass? SimpleGraphic = null)
        : base(X, Y)
    {
        if (SimpleGraphic is not null)
        {
            loadGraphic(SimpleGraphic);
        }
    }

    /// <summary>The name of the animation that is playing, or null.</summary>
    public string? currentAnimation => _current?.Name;

    /// <summary>Loads an embedded picture. With <paramref name="Animated"/> the picture is a sheet of equal frames.</summary>
    public FlxSprite loadGraphic(AsClass Graphic, bool Animated = false, bool Reverse = false, int Width = 0, int Height = 0, bool Unique = false)
    {
        _graphic = Graphic.Asset;
        _fill = 0;
        _reverse = Reverse;
        var size = _graphic is null ? null : FlxG.assets?.GetTextureSize(_graphic);
        var texWidth = size?.Width ?? Width;
        var texHeight = size?.Height ?? Height;
        if (Animated)
        {
            frameHeight = Height > 0 ? Height : texHeight;
            frameWidth = Width > 0 ? Width : frameHeight;
        }
        else
        {
            frameWidth = texWidth;
            frameHeight = texHeight;
        }

        width = frameWidth;
        height = frameHeight;
        frame = 0;
        return this;
    }

    /// <summary>Makes the sprite a solid rectangle.</summary>
    public FlxSprite makeGraphic(int Width, int Height, uint Color = 0xffffffff, bool Unique = false, string? Key = null)
    {
        _graphic = null;
        _fill = Color;
        frameWidth = Width;
        frameHeight = Height;
        width = Width;
        height = Height;
        frame = 0;
        return this;
    }

    /// <summary>Adds a named animation made of frame numbers. A frame rate of 0 or less means the animation does not advance.</summary>
    public void addAnimation(string Name, AsArray Frames, double FrameRate = 0, bool Looped = true)
    {
        var frames = Frames.Select(f => AsOps.ToInt(f)).ToArray();
        _animations[Name] = new Animation(Name, frames, FrameRate <= 0 ? 0 : 1 / FrameRate, Looped);
    }

    /// <summary>Plays an animation. Without <paramref name="Force"/> an animation that is already playing is not restarted.</summary>
    public void play(string AnimName, bool Force = false)
    {
        if (!_animations.TryGetValue(AnimName, out var anim))
        {
            return;
        }

        if (!Force && _current == anim && !finished)
        {
            return;
        }

        _current = anim;
        _index = 0;
        _frameTimer = 0;
        finished = false;
        frame = anim.Frames.Length > 0 ? anim.Frames[0] : 0;
    }

    /// <inheritdoc />
    public override void update()
    {
        base.update();
        if (_current is not { Delay: > 0 } || finished)
        {
            return;
        }

        _frameTimer += FlxG.elapsed;
        while (_frameTimer > _current.Delay)
        {
            _frameTimer -= _current.Delay;
            if (_index + 1 < _current.Frames.Length)
            {
                _index++;
            }
            else if (_current.Looped)
            {
                _index = 0;
            }
            else
            {
                finished = true;
                break;
            }

            frame = _current.Frames[_index];
        }
    }

    /// <inheritdoc />
    public override void draw()
    {
        if (!visible || !exists || !flickerVisible || FlxG.renderer is not { } r)
        {
            return;
        }

        var dx = x - offset.x - (FlxG.camera.scroll.x * scrollFactor.x);
        var dy = y - offset.y - (FlxG.camera.scroll.y * scrollFactor.y);
        var dw = frameWidth * scale.x;
        var dh = frameHeight * scale.y;
        if (_graphic is not null && frameWidth > 0 && frameHeight > 0)
        {
            var size = FlxG.assets?.GetTextureSize(_graphic);
            var perRow = size is { } s ? Math.Max(1, s.Width / frameWidth) : 1;
            r.DrawFrame(_graphic, frame % perRow * frameWidth, frame / perRow * frameHeight, frameWidth, frameHeight, dx, dy, dw, dh, color, alpha, _reverse && facing == LEFT);
        }
        else if (_fill != 0)
        {
            var a = (uint)Math.Round(((_fill >> 24) & 0xff) * Math.Clamp(alpha, 0, 1));
            r.DrawQuad(dx, dy, dw, dh, (a << 24) | (_fill & 0xffffff));
        }
    }

    /// <summary>Draws a texture in a grid of tiles. Used by <see cref="FlxTileblock"/>.</summary>
    protected void drawTiles(AssetRef texture, int tileWidth, int tileHeight, int firstFrame)
    {
        if (FlxG.renderer is not { } r || tileWidth <= 0 || tileHeight <= 0)
        {
            return;
        }

        var originX = x - (FlxG.camera.scroll.x * scrollFactor.x);
        var originY = y - (FlxG.camera.scroll.y * scrollFactor.y);
        var size = FlxG.assets?.GetTextureSize(texture) ?? (tileWidth, tileHeight);
        var perRow = Math.Max(1, size.Item1 / tileWidth);
        var frames = Math.Max(1, perRow * Math.Max(1, size.Item2 / tileHeight) - firstFrame);
        for (var cy = 0; cy * tileHeight < height; cy++)
        {
            for (var cx = 0; cx * tileWidth < width; cx++)
            {
                var f = firstFrame + ((cx + cy) % frames);
                var w = Math.Min(tileWidth, width - cx * tileWidth);
                var h = Math.Min(tileHeight, height - cy * tileHeight);
                r.DrawFrame(texture, f % perRow * tileWidth, f / perRow * tileHeight, (int)w, (int)h, originX + (cx * tileWidth), originY + (cy * tileHeight), w, h, color, alpha, false);
            }
        }
    }
}

/// <summary>A rectangle filled with tiles from a sheet. It does not move, and other objects collide with it.</summary>
public class FlxTileblock : FlxSprite
{
    private AssetRef? _tiles;
    private int _tileWidth;
    private int _tileHeight;
    private int _empties;

    /// <summary>Creates a block.</summary>
    public FlxTileblock(double X, double Y, double Width, double Height)
        : base(X, Y)
    {
        width = Width;
        height = Height;
        immovable = true;
        moves = false;
    }

    /// <summary>Chooses the tile sheet. <paramref name="Empties"/> is the number of leading frames that are left out.</summary>
    public FlxTileblock loadTiles(AsClass TileGraphic, int TileWidth = 0, int TileHeight = 0, int Empties = 0)
    {
        _tiles = TileGraphic.Asset;
        _tileWidth = TileWidth;
        _tileHeight = TileHeight;
        _empties = Empties;
        return this;
    }

    /// <inheritdoc />
    public override void draw()
    {
        if (visible && exists && _tiles is not null)
        {
            drawTiles(_tiles, _tileWidth, _tileHeight, _empties);
        }
    }
}

/// <summary>Text drawn with a built-in 5 by 7 pixel font. Letters are shown in capitals.</summary>
public class FlxText : FlxSprite
{
    private string _text;

    /// <summary>Creates a text object.</summary>
    public FlxText(double X, double Y, double Width, string? Text = null, bool EmbeddedFont = true)
        : base(X, Y)
    {
        this.width = Width;
        _text = Text ?? string.Empty;
        size = 8;
        alignment = "left";
        updateHeight();
    }

    /// <summary>Text size in pixels. The built-in font is drawn at whole multiples of 8.</summary>
    public double size { get; set; }

    /// <summary>"left", "center" or "right".</summary>
    public string alignment { get; set; }

    /// <summary>Shadow colour as 0xRRGGBB, or 0 for none.</summary>
    public uint shadow { get; set; }

    /// <summary>The text. Use \n for a new line.</summary>
    public string text
    {
        get => _text;
        set
        {
            _text = value ?? string.Empty;
            updateHeight();
        }
    }

    /// <inheritdoc />
    public override void draw()
    {
        if (!visible || !exists || FlxG.renderer is not { } r)
        {
            return;
        }

        var scaleFactor = PixelFont.ScaleFor(size);
        var lineHeight = PixelFont.LineHeight * scaleFactor;
        var argb = ((uint)Math.Round(255 * Math.Clamp(alpha, 0, 1)) << 24) | (color & 0xffffff);
        var originX = x - (FlxG.camera.scroll.x * scrollFactor.x);
        var py = y - (FlxG.camera.scroll.y * scrollFactor.y);
        foreach (var line in _text.Split('\n'))
        {
            var lineWidth = PixelFont.Measure(line, scaleFactor);
            var px = alignment switch
            {
                "center" => originX + ((width - lineWidth) / 2),
                "right" => originX + width - lineWidth,
                _ => originX,
            };
            if (shadow != 0)
            {
                PixelFont.Draw(r, line, px + 1, py + 1, scaleFactor, (argb & 0xff000000) | (shadow & 0xffffff));
            }

            PixelFont.Draw(r, line, px, py, scaleFactor, argb);
            py += lineHeight;
        }
    }

    private void updateHeight() => height = _text.Split('\n').Length * PixelFont.LineHeight * PixelFont.ScaleFor(size);
}
