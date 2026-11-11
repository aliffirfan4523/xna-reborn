using System.Collections;

namespace XnaReborn.As3;

/// <summary>Shared list behaviour of AS3 <c>Array</c> and <c>Vector</c>: AS3 method names, tolerant indexing.</summary>
/// <typeparam name="T">Element type.</typeparam>
/// <typeparam name="TSelf">The concrete list type, so that methods such as <c>slice</c> return it.</typeparam>
public abstract class AsList<T, TSelf> : IEnumerable<T>
    where TSelf : AsList<T, TSelf>, new()
{
    /// <summary>The items.</summary>
    protected readonly List<T> items = new();

    /// <summary>Number of items. Setting it shorter cuts the list; longer pads with default values.</summary>
    public int length
    {
        get => items.Count;
        set
        {
            if (value < items.Count)
            {
                items.RemoveRange(value, items.Count - value);
            }
            else
            {
                while (items.Count < value)
                {
                    items.Add(default!);
                }
            }
        }
    }

    /// <summary>Reads an item (default value when out of range) or writes one (growing the list when needed).</summary>
    /// <param name="index">The index.</param>
    public T this[int index]
    {
        get => index >= 0 && index < items.Count ? items[index] : default!;
        set
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (index >= items.Count)
            {
                length = index + 1;
            }

            items[index] = value;
        }
    }

    /// <summary>Adds items at the end and returns the new length.</summary>
    public int push(params T[] values)
    {
        items.AddRange(values);
        return items.Count;
    }

    /// <summary>Removes and returns the last item (default value when empty).</summary>
    public T pop()
    {
        if (items.Count == 0)
        {
            return default!;
        }

        var v = items[^1];
        items.RemoveAt(items.Count - 1);
        return v;
    }

    /// <summary>Removes and returns the first item (default value when empty).</summary>
    public T shift()
    {
        if (items.Count == 0)
        {
            return default!;
        }

        var v = items[0];
        items.RemoveAt(0);
        return v;
    }

    /// <summary>Adds items at the start and returns the new length.</summary>
    public int unshift(params T[] values)
    {
        items.InsertRange(0, values);
        return items.Count;
    }

    /// <summary>Removes <paramref name="deleteCount"/> items at <paramref name="startIndex"/>, inserts <paramref name="values"/>, and returns the removed items.</summary>
    public TSelf splice(int startIndex, int deleteCount = int.MaxValue, params T[] values)
    {
        var start = Clamp(startIndex);
        var count = Math.Max(0, Math.Min(deleteCount, items.Count - start));
        var removed = new TSelf();
        removed.items.AddRange(items.GetRange(start, count));
        items.RemoveRange(start, count);
        items.InsertRange(start, values);
        return removed;
    }

    /// <summary>Copies a range. Negative indexes count from the end.</summary>
    public TSelf slice(int startIndex = 0, int endIndex = 16777215)
    {
        var start = Clamp(startIndex);
        var end = endIndex < 0 ? Math.Max(0, items.Count + endIndex) : Math.Min(endIndex, items.Count);
        var result = new TSelf();
        if (end > start)
        {
            result.items.AddRange(items.GetRange(start, end - start));
        }

        return result;
    }

    /// <summary>Returns a new list with the items of this list followed by those of <paramref name="other"/>.</summary>
    public TSelf concat(TSelf? other = null)
    {
        var result = new TSelf();
        result.items.AddRange(items);
        if (other is not null)
        {
            result.items.AddRange(other.items);
        }

        return result;
    }

    /// <summary>Returns the first index of <paramref name="item"/> at or after <paramref name="fromIndex"/>, or -1.</summary>
    public int indexOf(T item, int fromIndex = 0) => items.IndexOf(item, Clamp(fromIndex));

    /// <summary>Returns the last index of <paramref name="item"/>, or -1.</summary>
    public int lastIndexOf(T item) => items.LastIndexOf(item);

    /// <summary>Joins the items with a separator.</summary>
    public string join(string sep = ",") => string.Join(sep, items.Select(i => i is null ? string.Empty : i.ToString()));

    /// <summary>Reverses the list in place and returns it.</summary>
    public TSelf reverse()
    {
        items.Reverse();
        return (TSelf)this;
    }

    /// <summary>Sorts in place with an AS3 compare function (negative, zero, positive), or by string value when none is given.</summary>
    public TSelf sort(AsFunction? compare = null)
    {
        var copy = items.ToArray();
        var comparer = compare is null
            ? Comparer<T>.Create((a, b) => string.CompareOrdinal(a?.ToString(), b?.ToString()))
            : Comparer<T>.Create((a, b) => (int)Math.Sign(AsOps.ToNumber(compare.Call(a, b))));
        var sorted = copy.Select((v, i) => (v, i)).OrderBy(p => p.v, comparer).ThenBy(p => p.i).Select(p => p.v).ToList();
        items.Clear();
        items.AddRange(sorted);
        return (TSelf)this;
    }

    /// <summary>Calls <paramref name="callback"/> with (item, index, list) for each item.</summary>
    public void forEach(AsFunction callback)
    {
        for (var i = 0; i < items.Count; i++)
        {
            callback.Call(items[i], i, this);
        }
    }

    /// <summary>Returns the items for which <paramref name="callback"/> returns true.</summary>
    public TSelf filter(AsFunction callback)
    {
        var result = new TSelf();
        for (var i = 0; i < items.Count; i++)
        {
            if (AsOps.ToBoolean(callback.Call(items[i], i, this)))
            {
                result.items.Add(items[i]);
            }
        }

        return result;
    }

    /// <summary>True when <paramref name="callback"/> returns true for at least one item.</summary>
    public bool some(AsFunction callback)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (AsOps.ToBoolean(callback.Call(items[i], i, this)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>True when <paramref name="callback"/> returns true for every item.</summary>
    public bool every(AsFunction callback)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (!AsOps.ToBoolean(callback.Call(items[i], i, this)))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => items.ToList().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <inheritdoc />
    public override string ToString() => join();

    private int Clamp(int index) => index < 0 ? Math.Max(0, items.Count + index) : Math.Min(index, items.Count);
}

/// <summary>AS3 <c>Array</c>: a list of any values.</summary>
public sealed class AsArray : AsList<object?, AsArray>
{
    /// <summary>Creates an empty array.</summary>
    public AsArray()
    {
    }

    /// <summary>Creates an array holding <paramref name="values"/>.</summary>
    public AsArray(params object?[] values) => items.AddRange(values);
}

/// <summary>AS3 <c>Vector.&lt;T&gt;</c>.</summary>
/// <typeparam name="T">Element type.</typeparam>
public sealed class AsVector<T> : AsList<T, AsVector<T>>
{
    /// <summary>Creates a vector of <paramref name="length"/> default values.</summary>
    public AsVector(int length) => this.length = length;

    /// <summary>Creates an empty vector.</summary>
    public AsVector()
    {
    }

    /// <summary>Creates a vector holding <paramref name="values"/>.</summary>
    public AsVector(IEnumerable<T> values) => items.AddRange(values);
}

/// <summary>AS3 <c>Object</c> used as a property bag: string keys, any values, missing keys read as null.</summary>
public class AsObject : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    /// <summary>Reads or writes a property.</summary>
    /// <param name="key">The property name.</param>
    public object? this[string key]
    {
        get => _values.GetValueOrDefault(key);
        set => _values[key] = value;
    }

    /// <summary>The property names, in insertion order for new keys.</summary>
    public IEnumerable<string> Keys => _values.Keys.ToList();

    /// <summary>True when the property exists.</summary>
    public bool hasOwnProperty(string key) => _values.ContainsKey(key);

    /// <summary>Removes a property and returns true when it existed.</summary>
    public bool delete(string key) => _values.Remove(key);

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _values.ToList().GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

/// <summary>AS3 <c>flash.utils.Dictionary</c>: object keys compared by reference (strings by value).</summary>
public sealed class AsDictionary
{
    private sealed class KeyComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) => x is string a && y is string b ? a == b : ReferenceEquals(x, y) || (x is not null && x.GetType().IsValueType && x.Equals(y));

        public int GetHashCode(object obj) => obj is string s ? s.GetHashCode() : obj.GetType().IsValueType ? obj.GetHashCode() : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }

    private readonly Dictionary<object, object?> _values = new(new KeyComparer());

    /// <summary>Reads or writes an entry. Missing keys read as null.</summary>
    /// <param name="key">The key object.</param>
    public object? this[object key]
    {
        get => _values.GetValueOrDefault(key);
        set => _values[key] = value;
    }

    /// <summary>The keys.</summary>
    public IEnumerable<object> Keys => _values.Keys.ToList();

    /// <summary>Removes an entry and returns true when it existed.</summary>
    public bool delete(object key) => _values.Remove(key);
}
