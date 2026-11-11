using Microsoft.Xna.Framework.Input;

namespace org.flixel;

/// <summary>Reports which keys are down. The game uses the MonoGame keyboard; tests use a fake.</summary>
public interface IFlxKeyState
{
    /// <summary>True when the key is down now.</summary>
    bool IsDown(Keys key);
}

/// <summary>Reports the mouse. The game uses the MonoGame mouse; tests use a fake. Positions are in game pixels, not window pixels.</summary>
public interface IFlxMouseState
{
    /// <summary>Where the cursor is in the game view.</summary>
    (double X, double Y) Position { get; }

    /// <summary>True while the left button is down.</summary>
    bool LeftDown { get; }

    /// <summary>Total wheel turn since the game started. The mouse class turns it into a change per frame.</summary>
    int WheelTotal { get; }
}

/// <summary>Keyboard state with Flixel's names: <c>FlxG.keys.LEFT</c>, <c>FlxG.keys.justPressed("X")</c>.</summary>
public sealed class FlxKeyboard
{
    private static readonly Dictionary<string, Keys[]> _map = BuildMap();
    private readonly HashSet<Keys> _down = new();
    private readonly HashSet<Keys> _previous = new();

    /// <summary>Every key name that <see cref="pressed"/> understands.</summary>
    public static IReadOnlyCollection<string> KeyNames => _map.Keys;

    /// <summary>Reads the new key state. Call once per frame.</summary>
    public void update(IFlxKeyState state)
    {
        _previous.Clear();
        _previous.UnionWith(_down);
        _down.Clear();
        foreach (var key in _map.Values.SelectMany(k => k).Distinct().Where(state.IsDown))
        {
            _down.Add(key);
        }
    }

    /// <summary>True while the key is held. Names are Flixel key names such as "LEFT", "SPACE" or "X".</summary>
    public bool pressed(string KeyName) => Any(KeyName, _down);

    /// <summary>True on the frame the key went down.</summary>
    public bool justPressed(string KeyName) => Any(KeyName, _down) && !Any(KeyName, _previous);

    /// <summary>True on the frame the key went up.</summary>
    public bool justReleased(string KeyName) => !Any(KeyName, _down) && Any(KeyName, _previous);

    /// <summary>True when any key is held.</summary>
    public bool any() => _down.Count > 0;

    /// <summary>Forgets every key, so nothing counts as pressed or just pressed.</summary>
    public void reset()
    {
        _down.Clear();
        _previous.Clear();
    }

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

    private static bool Any(string name, HashSet<Keys> set) =>
        _map.TryGetValue(name.ToUpperInvariant(), out var keys) && keys.Any(set.Contains);

    private static Dictionary<string, Keys[]> BuildMap()
    {
        var map = new Dictionary<string, Keys[]>(StringComparer.Ordinal)
        {
            ["LEFT"] = [Keys.Left],
            ["RIGHT"] = [Keys.Right],
            ["UP"] = [Keys.Up],
            ["DOWN"] = [Keys.Down],
            ["SPACE"] = [Keys.Space],
            ["ENTER"] = [Keys.Enter],
            ["ESCAPE"] = [Keys.Escape],
            ["SHIFT"] = [Keys.LeftShift, Keys.RightShift],
            ["CONTROL"] = [Keys.LeftControl, Keys.RightControl],
            ["ALT"] = [Keys.LeftAlt, Keys.RightAlt],
            ["TAB"] = [Keys.Tab],
            ["BACKSPACE"] = [Keys.Back],
            ["DELETE"] = [Keys.Delete],
            ["INSERT"] = [Keys.Insert],
            ["HOME"] = [Keys.Home],
            ["END"] = [Keys.End],
            ["PAGEUP"] = [Keys.PageUp],
            ["PAGEDOWN"] = [Keys.PageDown],
            ["CAPSLOCK"] = [Keys.CapsLock],
            ["MINUS"] = [Keys.OemMinus],
            ["PLUS"] = [Keys.OemPlus],
            ["COMMA"] = [Keys.OemComma],
            ["PERIOD"] = [Keys.OemPeriod],
            ["SLASH"] = [Keys.OemQuestion],
            ["SEMICOLON"] = [Keys.OemSemicolon],
            ["QUOTE"] = [Keys.OemQuotes],
            ["LBRACKET"] = [Keys.OemOpenBrackets],
            ["RBRACKET"] = [Keys.OemCloseBrackets],
            ["BACKSLASH"] = [Keys.OemPipe],
            ["GRAVEACCENT"] = [Keys.OemTilde],
        };
        for (var c = 'A'; c <= 'Z'; c++)
        {
            map[c.ToString()] = [Enum.Parse<Keys>(c.ToString())];
        }

        string[] digits = ["ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE"];
        for (var d = 0; d <= 9; d++)
        {
            map["D" + d] = [Enum.Parse<Keys>("D" + d)];
            map[digits[d]] = [Enum.Parse<Keys>("D" + d)];
            map["NUMPAD" + digits[d]] = [Enum.Parse<Keys>("NumPad" + d)];
        }

        for (var f = 1; f <= 12; f++)
        {
            map["F" + f] = [Enum.Parse<Keys>("F" + f)];
        }

        return map;
    }
}

/// <summary>
/// Mouse state, like Flixel's <c>FlxG.mouse</c>. <see cref="screenX"/> and <see cref="screenY"/> are in the view;
/// <see cref="x"/> and <see cref="y"/> add the camera scroll, so they are world positions.
/// </summary>
public sealed class FlxMouse
{
    private bool _down;
    private bool _previous;
    private int _lastWheel;
    private bool _wheelKnown;

    /// <summary>World position: view position plus camera scroll.</summary>
    public double x;

    /// <summary>World position: view position plus camera scroll.</summary>
    public double y;

    /// <summary>Position in the game view.</summary>
    public double screenX;

    /// <summary>Position in the game view.</summary>
    public double screenY;

    /// <summary>How far the wheel turned this frame.</summary>
    public int wheel;

    /// <summary>False when the game hides the cursor.</summary>
    public bool visible = true;

    /// <summary>Reads the new mouse state. Call once per frame.</summary>
    public void update(IFlxMouseState state)
    {
        _previous = _down;
        _down = state.LeftDown;
        (screenX, screenY) = state.Position;
        x = screenX + FlxG.camera.scroll.x;
        y = screenY + FlxG.camera.scroll.y;
        wheel = _wheelKnown ? state.WheelTotal - _lastWheel : 0;
        _lastWheel = state.WheelTotal;
        _wheelKnown = true;
    }

    /// <summary>True while the left button is held.</summary>
    public bool pressed() => _down;

    /// <summary>True on the frame the button went down.</summary>
    public bool justPressed() => _down && !_previous;

    /// <summary>True on the frame the button went up.</summary>
    public bool justReleased() => !_down && _previous;

    /// <summary>Shows the cursor.</summary>
    public void show() => visible = true;

    /// <summary>Hides the cursor.</summary>
    public void hide() => visible = false;

    /// <summary>Forgets the button state.</summary>
    public void reset() => _down = _previous = false;
}

/// <summary>
/// Gamepad stub. Flixel 2.x has no gamepad support, so converted games never read one; this keeps code that asks compiling,
/// and nothing is ever pressed.
/// </summary>
public sealed class FlxGamepad
{
    /// <summary>Always false.</summary>
    public bool pressed(string ButtonName) => false;

    /// <summary>Always false.</summary>
    public bool justPressed(string ButtonName) => false;

    /// <summary>Always false.</summary>
    public bool justReleased(string ButtonName) => false;
}
