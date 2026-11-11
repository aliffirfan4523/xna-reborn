using XnaReborn.As3;

namespace org.flixel;

/// <summary>
/// The one camera of the game: scroll, bounds, follow, flash, fade, shake and the background colour, like Flixel's <c>FlxCamera</c>.
/// Objects shift by <c>scroll * scrollFactor</c> when they draw. Written for XNA Reborn against the public Flixel 2.x API;
/// see THIRD-PARTY-NOTICES.md for the Flixel (MIT) notice. shortcut: one camera and no zoom; add a camera list if a game needs more.
/// </summary>
public class FlxCamera
{
    /// <summary>Follow style: the target stays in the centre.</summary>
    public const int STYLE_LOCKON = 0;

    /// <summary>Follow style: a small dead zone, a little above the centre.</summary>
    public const int STYLE_PLATFORMER = 1;

    /// <summary>Follow style: a square dead zone in the centre.</summary>
    public const int STYLE_TOPDOWN = 2;

    /// <summary>Follow style: a small square dead zone in the centre.</summary>
    public const int STYLE_TOPDOWN_TIGHT = 3;

    /// <summary>Shake direction: both axes.</summary>
    public const uint SHAKE_BOTH_AXES = 0;

    /// <summary>Shake direction: horizontal only.</summary>
    public const uint SHAKE_HORIZONTAL_ONLY = 1;

    /// <summary>Shake direction: vertical only.</summary>
    public const uint SHAKE_VERTICAL_ONLY = 2;

    private uint _flashColor;
    private double _flashDuration;
    private double _flashTime;
    private AsFunction? _flashDone;
    private uint _fadeColor;
    private double _fadeDuration;
    private double _fadeTime;
    private bool _fadeIn;
    private bool _fading;
    private AsFunction? _fadeDone;
    private double _shakeIntensity;
    private double _shakeTime;
    private AsFunction? _shakeDone;
    private uint _shakeDirection;
    private FlxRect? _deadzone;

    /// <summary>The top left corner of the view in world pixels.</summary>
    public FlxPoint scroll = new();

    /// <summary>Background colour as 0xAARRGGBB.</summary>
    public uint bgColor = 0xff000000;

    /// <summary>The area the view may not leave, or null for no limit.</summary>
    public FlxRect? bounds;

    /// <summary>The object the camera follows, or null.</summary>
    public FlxObject? target;

    /// <summary>The current shake offset in pixels. Added to everything that is drawn.</summary>
    public FlxPoint shakeOffset = new();

    /// <summary>The view width in pixels.</summary>
    public int width => FlxG.width;

    /// <summary>The view height in pixels.</summary>
    public int height => FlxG.height;

    /// <summary>Follows an object. The camera moves in <see cref="update"/>.</summary>
    /// <param name="Target">The object to keep in view.</param>
    /// <param name="Style">One of the STYLE constants.</param>
    public void follow(FlxObject? Target, int Style = STYLE_LOCKON)
    {
        target = Target;
        double w;
        double h;
        switch (Style)
        {
            case STYLE_PLATFORMER:
                w = width / 8.0;
                h = height / 3.0;
                _deadzone = new FlxRect((width - w) / 2, ((height - h) / 2) - (h * 0.25), w, h);
                break;
            case STYLE_TOPDOWN:
                w = Math.Max(width, height) / 4.0;
                _deadzone = new FlxRect((width - w) / 2, (height - w) / 2, w, w);
                break;
            case STYLE_TOPDOWN_TIGHT:
                w = Math.Max(width, height) / 8.0;
                _deadzone = new FlxRect((width - w) / 2, (height - w) / 2, w, w);
                break;
            default:
                _deadzone = null;
                break;
        }
    }

    /// <summary>Limits the view to a world rectangle.</summary>
    public void setBounds(double X = 0, double Y = 0, double Width = 0, double Height = 0) => bounds = new FlxRect(X, Y, Width, Height);

    /// <summary>Shows a colour that fades out. Does nothing while a flash runs unless <paramref name="Force"/> is true.</summary>
    public void flash(uint Color = 0xffffffff, double Duration = 1, AsFunction? OnComplete = null, bool Force = false)
    {
        if (!Force && _flashTime > 0)
        {
            return;
        }

        _flashColor = Color;
        _flashDuration = Duration <= 0 ? 1.0 / 1000 : Duration;
        _flashTime = _flashDuration;
        _flashDone = OnComplete;
    }

    /// <summary>Fades the view to a colour (or from it with <paramref name="FadeIn"/>). The colour stays when it is done.</summary>
    public void fade(uint Color = 0xff000000, double Duration = 1, AsFunction? OnComplete = null, bool Force = false, bool FadeIn = false)
    {
        if (!Force && _fading)
        {
            return;
        }

        _fadeColor = Color;
        _fadeDuration = Duration <= 0 ? 1.0 / 1000 : Duration;
        _fadeTime = 0;
        _fadeIn = FadeIn;
        _fading = true;
        _fadeDone = OnComplete;
    }

    /// <summary>Shakes the view. <paramref name="Intensity"/> is a fraction of the view size.</summary>
    public void shake(double Intensity = 0.05, double Duration = 0.5, AsFunction? OnComplete = null, bool Force = true, uint Direction = SHAKE_BOTH_AXES)
    {
        if (!Force && _shakeTime > 0)
        {
            return;
        }

        _shakeIntensity = Intensity;
        _shakeTime = Duration;
        _shakeDone = OnComplete;
        _shakeDirection = Direction;
        shakeOffset.make();
    }

    /// <summary>Stops every effect and puts the camera back at the origin.</summary>
    public void stopFX()
    {
        _flashTime = 0;
        _fading = false;
        _shakeTime = 0;
        shakeOffset.make();
    }

    /// <summary>Follows the target, keeps the view in its bounds and runs the effects. Called by the game each frame.</summary>
    public void update()
    {
        if (target is not null)
        {
            if (_deadzone is null)
            {
                scroll.x = target.x + (target.width / 2) - (width / 2.0);
                scroll.y = target.y + (target.height / 2) - (height / 2.0);
            }
            else
            {
                // Same order as Flixel: the top/left edge first, then the bottom/right edge wins.
                scroll.x = Math.Min(scroll.x, target.x - _deadzone.x);
                scroll.x = Math.Max(scroll.x, target.x + target.width - _deadzone.x - _deadzone.width);
                scroll.y = Math.Min(scroll.y, target.y - _deadzone.y);
                scroll.y = Math.Max(scroll.y, target.y + target.height - _deadzone.y - _deadzone.height);
            }
        }

        if (bounds is not null)
        {
            scroll.x = Math.Max(bounds.left, Math.Min(scroll.x, Math.Max(bounds.left, bounds.right - width)));
            scroll.y = Math.Max(bounds.top, Math.Min(scroll.y, Math.Max(bounds.top, bounds.bottom - height)));
        }

        if (_flashTime > 0)
        {
            _flashTime -= FlxG.elapsed;
            if (_flashTime <= 0)
            {
                _flashTime = 0;
                var done = _flashDone;
                _flashDone = null;
                done?.Call();
            }
        }

        if (_fading && _fadeTime < _fadeDuration)
        {
            _fadeTime += FlxG.elapsed;
            if (_fadeTime >= _fadeDuration)
            {
                _fadeTime = _fadeDuration;
                var done = _fadeDone;
                _fadeDone = null;
                done?.Call();
            }
        }

        if (_shakeTime > 0)
        {
            _shakeTime -= FlxG.elapsed;
            if (_shakeTime <= 0)
            {
                _shakeTime = 0;
                shakeOffset.make();
                var done = _shakeDone;
                _shakeDone = null;
                done?.Call();
            }
            else
            {
                shakeOffset.x = _shakeDirection == SHAKE_VERTICAL_ONLY ? 0 : Math.Round(((FlxG.random() * _shakeIntensity * width * 2) - (_shakeIntensity * width)));
                shakeOffset.y = _shakeDirection == SHAKE_HORIZONTAL_ONLY ? 0 : Math.Round(((FlxG.random() * _shakeIntensity * height * 2) - (_shakeIntensity * height)));
            }
        }
    }

    /// <summary>Draws the flash and fade colours over the view.</summary>
    internal void drawEffects(IFlxRenderer renderer)
    {
        if (_flashTime > 0)
        {
            DrawOverlay(renderer, _flashColor, _flashTime / _flashDuration);
        }

        if (_fading)
        {
            var progress = _fadeTime / _fadeDuration;
            DrawOverlay(renderer, _fadeColor, _fadeIn ? 1 - progress : progress);
        }
    }

    private void DrawOverlay(IFlxRenderer renderer, uint argb, double strength)
    {
        var alpha = Math.Clamp(strength, 0, 1) * ((argb >> 24) & 0xff);
        if (alpha > 0)
        {
            renderer.DrawQuad(0, 0, width, height, ((uint)Math.Round(alpha) << 24) | (argb & 0xffffff));
        }
    }
}
