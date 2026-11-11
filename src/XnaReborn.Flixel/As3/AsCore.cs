using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace XnaReborn.As3;

/// <summary>AS3 <c>Function</c> value: wraps a delegate and tolerates calls with too few or too many arguments, like AS3.</summary>
public sealed class AsFunction
{
    private readonly Delegate _target;

    /// <summary>Wraps a delegate.</summary>
    /// <param name="target">The delegate, for example <c>new Action&lt;int&gt;(Method)</c>.</param>
    public AsFunction(Delegate target) => _target = target ?? throw new ArgumentNullException(nameof(target));

    /// <summary>Wraps a delegate. Same as the constructor; reads better in converted code.</summary>
    /// <param name="target">The delegate.</param>
    public static AsFunction From(Delegate target) => new(target);

    /// <summary>Number of declared parameters.</summary>
    public int Arity => _target.Method.GetParameters().Length;

    /// <summary>Calls the function. Missing arguments become default values, extra arguments are dropped, numbers are converted.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The result, or null for a function that returns nothing.</returns>
    public object? Call(params object?[] args)
    {
        var parameters = _target.Method.GetParameters();
        // Closures compile to a method whose first parameter is the target only for open delegates; Method.GetParameters() on a bound delegate excludes it.
        var actual = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var type = parameters[i].ParameterType;
            if (i < args.Length)
            {
                actual[i] = Coerce(args[i], type);
            }
            else
            {
                actual[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
            }
        }

        try
        {
            return _target.DynamicInvoke(actual);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static object? Coerce(object? value, Type type)
    {
        if (value is null)
        {
            return type.IsValueType ? Activator.CreateInstance(type) : null;
        }

        if (type.IsInstanceOfType(value))
        {
            return value;
        }

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(type))
        {
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }

        return value;
    }
}

/// <summary>What kind of embedded file an <see cref="AssetRef"/> names.</summary>
public enum AssetKind
{
    /// <summary>A picture, loaded as a texture.</summary>
    Texture,

    /// <summary>A short sound.</summary>
    Sound,

    /// <summary>Music, loaded as a song.</summary>
    Song,

    /// <summary>A data file copied to the Content folder.</summary>
    Data,
}

/// <summary>A reference to an embedded asset: the content name and its kind. It stands in for AS3 embed classes.</summary>
/// <param name="Name">Content name without extension, such as "data/sprites". For data files, the file name with extension.</param>
/// <param name="Kind">The asset kind.</param>
public sealed record AssetRef(string Name, AssetKind Kind);

/// <summary>AS3 <c>Class</c> value: either a .NET type (for <c>new cls()</c>) or an embedded asset reference.</summary>
public sealed class AsClass
{
    private AsClass(Type? type, AssetRef? asset)
    {
        Type = type;
        Asset = asset;
    }

    /// <summary>The type, or null for an asset class.</summary>
    public Type? Type { get; }

    /// <summary>The asset, or null for a type class.</summary>
    public AssetRef? Asset { get; }

    /// <summary>The class value of <typeparamref name="T"/>.</summary>
    public static AsClass Of<T>() => new(typeof(T), null);

    /// <summary>The class value of a type.</summary>
    public static AsClass Of(Type type) => new(type ?? throw new ArgumentNullException(nameof(type)), null);

    /// <summary>An embedded asset class.</summary>
    public static AsClass FromAsset(string name, AssetKind kind) => new(null, new AssetRef(name, kind));

    /// <summary>Creates an instance of the type with the given constructor arguments.</summary>
    public object New(params object?[] args)
    {
        if (Type is null)
        {
            throw new InvalidOperationException("An embedded asset class cannot be constructed.");
        }

        // OptionalParamBinding lets AS3 constructors with default values (FlxSprite(X = 0, Y = 0)) be called with fewer arguments.
        return Activator.CreateInstance(Type, BindingFlags.Public | BindingFlags.Instance | BindingFlags.CreateInstance | BindingFlags.OptionalParamBinding, null, args, CultureInfo.InvariantCulture)!;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is AsClass c && c.Type == Type && Equals(c.Asset, Asset);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Type, Asset);
}

/// <summary>AS3 operators and conversions that C# does not have.</summary>
public static class AsOps
{
    /// <summary>The AS3 <c>Number()</c> conversion. Non-numbers become NaN.</summary>
    public static double ToNumber(object? value) => value switch
    {
        null => 0,
        double d => d,
        int i => i,
        uint u => u,
        float f => f,
        long l => l,
        bool b => b ? 1 : 0,
        string s => string.IsNullOrWhiteSpace(s) ? 0
            : s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h
            : double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN,
        IConvertible c => c.ToDouble(CultureInfo.InvariantCulture),
        _ => double.NaN,
    };

    /// <summary>The AS3 <c>int()</c> conversion: truncate, NaN becomes 0, wrap to 32 bits.</summary>
    public static int ToInt(object? value)
    {
        var d = ToNumber(value);
        return double.IsNaN(d) || double.IsInfinity(d) ? 0 : unchecked((int)(long)Math.Truncate(d));
    }

    /// <summary>The AS3 <c>uint()</c> conversion.</summary>
    public static uint ToUInt(object? value) => unchecked((uint)ToInt(value));

    /// <summary>The AS3 <c>Boolean()</c> conversion: 0, NaN, null and the empty string are false.</summary>
    public static bool ToBoolean(object? value) => value switch
    {
        null => false,
        bool b => b,
        string s => s.Length > 0,
        double d => d != 0 && !double.IsNaN(d),
        IConvertible c when value.GetType().IsPrimitive => c.ToDouble(CultureInfo.InvariantCulture) != 0,
        _ => true,
    };

    /// <summary>The AS3 <c>String()</c> conversion.</summary>
    public static string ToAsString(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        double d => double.IsNaN(d) ? "NaN" : d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>The AS3 <c>==</c> operator: values of different types are converted to numbers before comparing.</summary>
    public static bool LooseEquals(object? a, object? b)
    {
        if (a is null || b is null)
        {
            return a is null && b is null;
        }

        if (a is string sa && b is string sb)
        {
            return sa == sb;
        }

        var aNum = a is string || a.GetType().IsPrimitive;
        var bNum = b is string || b.GetType().IsPrimitive;
        if (aNum && bNum)
        {
            return ToNumber(a) == ToNumber(b);
        }

        return ReferenceEquals(a, b) || a.Equals(b);
    }

    /// <summary>The AS3 <c>Math.round</c>: halves round up (towards positive infinity).</summary>
    public static double Round(double value) => Math.Floor(value + 0.5);

    /// <summary>The AS3 <c>Math.random</c>: a number from 0 up to but not including 1.</summary>
    public static double Random() => System.Random.Shared.NextDouble();

    /// <summary>The AS3 <c>typeof</c> operator.</summary>
    public static string TypeOf(object? value) => value switch
    {
        null => "object",
        string => "string",
        bool => "boolean",
        AsFunction => "function",
        _ when value.GetType().IsPrimitive => "number",
        _ => "object",
    };

    /// <summary>The AS3 <c>in</c> operator: true when the object has the property (or the array has the index).</summary>
    public static bool In(object? key, object? container) => container switch
    {
        AsObject o => o.hasOwnProperty(ToAsString(key)),
        AsDictionary d => d.Keys.Any(k => Equals(k, key)),
        AsArray a => ToInt(key) >= 0 && ToInt(key) < a.length,
        _ => false,
    };

    /// <summary>The property names a <c>for (var k in obj)</c> loop visits: object keys, or array indexes as strings.</summary>
    public static IEnumerable<string> KeysOf(object? container) => container switch
    {
        AsObject o => o.Keys,
        AsDictionary d => d.Keys.Select(k => ToAsString(k)).ToList(),
        AsArray a => Enumerable.Range(0, a.length).Select(i => i.ToString(CultureInfo.InvariantCulture)).ToList(),
        _ => Enumerable.Empty<string>(),
    };

    /// <summary>The values a <c>for each (var v in obj)</c> loop visits.</summary>
    public static IEnumerable<dynamic?> ValuesOf(object? container) => container switch
    {
        AsObject o => o.Select(p => (dynamic?)p.Value).ToList(),
        AsDictionary d => d.Keys.Select(k => (dynamic?)d[k]).ToList(),
        System.Collections.IEnumerable e and not string => e.Cast<object?>().Select(x => (dynamic?)x).ToList(),
        _ => Enumerable.Empty<dynamic?>(),
    };

    /// <summary>The exception a <c>throw</c> raises: the value itself when it is an exception, otherwise a wrapper that keeps the value.</summary>
    public static Exception ToException(object? value) => value as Exception ?? new AsError(value);

    /// <summary>The AS3 <c>&gt;&gt;&gt;</c> operator on a number: convert to int, then shift as unsigned.</summary>
    public static uint UnsignedShiftRight(double value, int count) => unchecked((uint)ToInt(value)) >> (count & 31);
}

/// <summary>AS3 top-level functions.</summary>
public static class AsGlobals
{
    private static readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>Where <see cref="trace"/> writes. Tests replace it.</summary>
    public static Action<string> TraceOutput { get; set; } = Console.WriteLine;

    /// <summary>AS3 <c>trace</c>: writes the arguments separated by spaces.</summary>
    public static void trace(params object?[] values) => TraceOutput(string.Join(" ", values.Select(AsOps.ToAsString)));

    /// <summary>AS3 <c>getTimer</c>: milliseconds since the program started.</summary>
    public static int getTimer() => (int)_clock.ElapsedMilliseconds;

    /// <summary>AS3 <c>isNaN</c>.</summary>
    public static bool isNaN(object? value) => double.IsNaN(AsOps.ToNumber(value));

    /// <summary>AS3 <c>parseInt</c>. Returns NaN when no digits are found.</summary>
    public static double parseInt(string text, int radix = 10)
    {
        var s = text.Trim();
        if (radix == 16 || (radix == 10 && s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)))
        {
            s = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s[2..] : s;
            return long.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h) ? h : double.NaN;
        }

        var end = 0;
        while (end < s.Length && (char.IsAsciiDigit(s[end]) || (end == 0 && s[end] is '-' or '+')))
        {
            end++;
        }

        return long.TryParse(s[..end], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
    }

    /// <summary>AS3 <c>parseFloat</c>.</summary>
    public static double parseFloat(string text) => AsOps.ToNumber(text);
}

/// <summary>AS3 <c>as</c> for value types.</summary>
public static class AsCast
{
    /// <summary>Converts <paramref name="value"/> to <typeparamref name="T"/>, or returns the default value when it is not convertible.</summary>
    public static T? To<T>(object? value)
    {
        if (value is T t)
        {
            return t;
        }

        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(typeof(T)))
        {
            try
            {
                return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                return default;
            }
        }

        return default;
    }
}

/// <summary>An exception that carries any thrown AS3 value.</summary>
public sealed class AsError : Exception
{
    /// <summary>Creates the exception.</summary>
    public AsError(object? value)
        : base(AsOps.ToAsString(value)) => Value = value;

    /// <summary>The thrown value.</summary>
    public object? Value { get; }
}
