using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

public class As3RuntimeTests
{
    [Fact]
    public void Array_push_pop_shift_unshift_follow_as3()
    {
        var a = new AsArray();
        Assert.Equal(3, a.push(1, 2, 3));
        Assert.Equal(4, a.unshift(0));
        Assert.Equal(0, a.shift());
        Assert.Equal(3, a.pop());
        Assert.Equal(2, a.length);
        Assert.Null(new AsArray().pop());
    }

    [Fact]
    public void Array_sparse_assignment_pads_with_defaults_and_reads_out_of_range_as_null()
    {
        var a = new AsArray();
        a[3] = "x";
        Assert.Equal(4, a.length);
        Assert.Null(a[1]);
        Assert.Null(a[99]);
        a.length = 1;
        Assert.Single(a);
    }

    [Fact]
    public void Splice_and_slice_handle_negative_and_oversized_arguments()
    {
        var v = new AsVector<int>(new[] { 1, 2, 3, 4, 5 });
        var removed = v.splice(1, 2, 9, 9, 9);
        Assert.Equal("2,3", removed.join());
        Assert.Equal("1,9,9,9,4,5", v.join());
        Assert.Equal("4,5", v.slice(-2).join());
        Assert.Equal("1,9", v.slice(0, 2).join());
        Assert.Equal(string.Empty, v.slice(4, 2).join());
        Assert.Equal("5", v.splice(-1).join());
    }

    [Fact]
    public void Sort_uses_the_compare_function_and_is_stable()
    {
        var v = new AsVector<int>(new[] { 3, 1, 2 });
        v.sort(new AsFunction(new Func<int, int, double>((a, b) => b - a)));
        Assert.Equal("3,2,1", v.join());
        var a = new AsArray("b", "a", "c");
        Assert.Equal("a,b,c", a.sort().join());
    }

    [Fact]
    public void Iteration_helpers_pass_item_index_and_list_and_tolerate_short_callbacks()
    {
        var v = new AsVector<int>(new[] { 1, 2, 3, 4 });
        var seen = new List<int>();
        v.forEach(new AsFunction(new Action<int>(i => seen.Add(i))));
        Assert.Equal([1, 2, 3, 4], seen);
        Assert.Equal("2,4", v.filter(new AsFunction(new Func<int, bool>(i => i % 2 == 0))).join());
        Assert.True(v.some(new AsFunction(new Func<int, bool>(i => i > 3))));
        Assert.False(v.every(new AsFunction(new Func<int, bool>(i => i > 3))));
    }

    [Fact]
    public void Object_and_dictionary_behave_like_property_bags()
    {
        var o = new AsObject();
        o["a"] = 1;
        Assert.Null(o["missing"]);
        Assert.True(o.hasOwnProperty("a"));
        Assert.True(o.delete("a"));
        Assert.False(o.hasOwnProperty("a"));

        var key = new object();
        var d = new AsDictionary();
        d[key] = "v";
        d["s"] = "t";
        Assert.Equal("v", d[key]);
        Assert.Null(d[new object()]);
        Assert.Equal("t", d[new string("s".ToCharArray())]);
    }

    [Fact]
    public void Function_calls_pad_missing_arguments_drop_extras_and_convert_numbers()
    {
        var f = new AsFunction(new Func<int, double, string>((a, b) => $"{a}:{b}"));
        Assert.Equal("1:0", f.Call(1));
        Assert.Equal("2:3", f.Call(2.0, 3, "extra"));
        Assert.Equal(2, f.Arity);
        var boom = new AsFunction(new Action(() => throw new InvalidOperationException("x")));
        Assert.Throws<InvalidOperationException>(() => boom.Call());
    }

    [Fact]
    public void Class_values_create_instances_and_hold_assets()
    {
        var c = AsClass.Of<List<int>>();
        Assert.IsType<List<int>>(c.New());
        var asset = AsClass.FromAsset("data/a", AssetKind.Texture);
        Assert.Equal(new AssetRef("data/a", AssetKind.Texture), asset.Asset);
        Assert.Throws<InvalidOperationException>(() => asset.New());
    }

    [Theory]
    [InlineData("12", 12.0)]
    [InlineData("0x10", 16.0)]
    [InlineData("", 0.0)]
    [InlineData("abc", double.NaN)]
    public void Number_conversion_follows_as3(string text, double expected)
    {
        Assert.Equal(expected, AsOps.ToNumber(text));
    }

    [Fact]
    public void Int_conversion_truncates_wraps_and_turns_nan_into_zero()
    {
        Assert.Equal(-3, AsOps.ToInt(-3.9));
        Assert.Equal(0, AsOps.ToInt(double.NaN));
        Assert.Equal(int.MinValue, AsOps.ToInt(2147483648.0));
        Assert.Equal(4294967295u, AsOps.ToUInt(-1));
    }

    [Fact]
    public void Loose_equality_converts_between_strings_numbers_and_booleans()
    {
        Assert.True(AsOps.LooseEquals("1", 1));
        Assert.True(AsOps.LooseEquals(true, 1));
        Assert.True(AsOps.LooseEquals(null, null));
        Assert.False(AsOps.LooseEquals(null, 0));
        Assert.False(AsOps.LooseEquals("a", "b"));
    }

    [Fact]
    public void Boolean_and_string_conversions_follow_as3()
    {
        Assert.False(AsOps.ToBoolean(double.NaN));
        Assert.False(AsOps.ToBoolean(string.Empty));
        Assert.True(AsOps.ToBoolean("0"));
        Assert.Equal("NaN", AsOps.ToAsString(double.NaN));
        Assert.Equal("1.5", AsOps.ToAsString(1.5));
        Assert.Equal("true", AsOps.ToAsString(true));
    }

    [Fact]
    public void Unsigned_shift_treats_the_value_as_unsigned()
    {
        Assert.Equal(2147483647u, AsOps.UnsignedShiftRight(-1, 1));
        Assert.Equal(1u, AsOps.UnsignedShiftRight(8, 3));
    }

    [Fact]
    public void Globals_trace_parse_and_isnan()
    {
        var lines = new List<string>();
        AsGlobals.TraceOutput = lines.Add;
        try
        {
            AsGlobals.trace("a", 1, null, double.NaN);
        }
        finally
        {
            AsGlobals.TraceOutput = Console.WriteLine;
        }

        Assert.Equal(["a 1 null NaN"], lines);
        Assert.Equal(42, AsGlobals.parseInt("42px"));
        Assert.Equal(255, AsGlobals.parseInt("ff", 16));
        Assert.True(double.IsNaN(AsGlobals.parseInt("x")));
        Assert.True(AsGlobals.isNaN("abc"));
        Assert.Equal(7, AsCast.To<int>(7.0));
        Assert.Equal(0, AsCast.To<int>("nope"));
    }
}
