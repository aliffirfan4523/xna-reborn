using XnaReborn.As3;

namespace org.flixel;

/// <summary>
/// A clickable button with an optional text label, like Flixel's <c>FlxButton</c>. Without a picture it is a grey box that
/// changes shade; with <see cref="loadGraphic"/> as a sheet of three frames (normal, highlight, pressed) those frames are used.
/// </summary>
public class FlxButton : FlxSprite
{
    /// <summary>The mouse is not over the button.</summary>
    public const int NORMAL = 0;

    /// <summary>The mouse is over the button.</summary>
    public const int HIGHLIGHT = 1;

    /// <summary>The mouse is over the button and its left button is down.</summary>
    public const int PRESSED = 2;

    private static readonly uint[] _shades = [0xff7f7f7f, 0xffa5a5a5, 0xff4c4c4c];

    private bool _custom;
    private bool _sheet;
    private bool _downOnMe;

    /// <summary>Creates a button. <paramref name="OnClick"/> is called when the mouse button is let go over it.</summary>
    public FlxButton(double X = 0, double Y = 0, string? Label = null, AsFunction? OnClick = null)
        : base(X, Y)
    {
        makeGraphic(80, 20, _shades[NORMAL]);
        scrollFactor.make(0, 0);
        onUp = OnClick;
        if (Label is not null)
        {
            label = new FlxText(0, 0, 80, Label) { alignment = "center" };
            label.scrollFactor.make(0, 0);
        }
    }

    /// <summary>The text on the button, or null.</summary>
    public FlxText? label { get; set; }

    /// <summary>Called when the mouse button is let go over the button.</summary>
    public AsFunction? onUp { get; set; }

    /// <summary>Called when the mouse button goes down over the button.</summary>
    public AsFunction? onDown { get; set; }

    /// <summary>Called when the mouse moves onto the button.</summary>
    public AsFunction? onOver { get; set; }

    /// <summary>Called when the mouse moves off the button.</summary>
    public AsFunction? onOut { get; set; }

    /// <summary>One of <see cref="NORMAL"/>, <see cref="HIGHLIGHT"/> or <see cref="PRESSED"/>.</summary>
    public int status { get; private set; }

    /// <inheritdoc />
    public override FlxSprite loadGraphic(AsClass Graphic, bool Animated = false, bool Reverse = false, int Width = 0, int Height = 0, bool Unique = false)
    {
        _custom = true;
        _sheet = Animated;
        return base.loadGraphic(Graphic, Animated, Reverse, Width, Height, Unique);
    }

    /// <inheritdoc />
    public override void update()
    {
        base.update();
        var mouse = FlxG.mouse;
        var over = mouse.screenX + (FlxG.camera.scroll.x * scrollFactor.x) >= x
            && mouse.screenX + (FlxG.camera.scroll.x * scrollFactor.x) < x + width
            && mouse.screenY + (FlxG.camera.scroll.y * scrollFactor.y) >= y
            && mouse.screenY + (FlxG.camera.scroll.y * scrollFactor.y) < y + height;
        var before = status;
        if (over && mouse.justPressed())
        {
            _downOnMe = true;
            onDown?.Call();
        }

        if (mouse.justReleased())
        {
            var click = _downOnMe && over;
            _downOnMe = false;
            if (click)
            {
                onUp?.Call();
            }
        }

        status = !over ? NORMAL : mouse.pressed() ? PRESSED : HIGHLIGHT;
        if (before == NORMAL && status != NORMAL)
        {
            onOver?.Call();
        }
        else if (before != NORMAL && status == NORMAL)
        {
            onOut?.Call();
        }

        if (status != before)
        {
            if (_sheet)
            {
                frame = status;
            }
            else if (!_custom)
            {
                makeGraphic((int)width, (int)height, _shades[status]);
            }
        }

        if (label is { } l)
        {
            l.x = x;
            l.y = y + ((height - l.height) / 2) + (status == PRESSED ? 1 : 0);
            l.update();
        }
    }

    /// <inheritdoc />
    public override void draw()
    {
        base.draw();
        if (visible && exists)
        {
            label?.draw();
        }
    }

    /// <inheritdoc />
    public override void destroy()
    {
        label?.destroy();
        label = null;
        base.destroy();
    }
}
