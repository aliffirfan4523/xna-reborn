using XnaReborn.Core.Toolkit;
using XnaReborn.Migrator.Source;
using XnaReborn.Migrator.Source.Rewriters;

namespace XnaReborn.Tests.Sdx;

/// <summary>Task 6.1: Matrix factories and MathUtil members are renamed, and nothing else is.</summary>
[Trait("Category", "Sdx")]
public sealed class ToolkitMathRewriterTests
{
    private static readonly string[] _symbols = ["DEBUG", "TRACE"];

    private static string Wrap(string body) =>
        "using Microsoft.Xna.Framework;\n\nclass C\n{\n    void M(Matrix matrix)\n    {\n        " + body + "\n    }\n}\n";

    private static SourceRewriteResult Matrix(string text) =>
        ToolkitMathRewriter.ForMatrix().Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));

    private static SourceRewriteResult MathUtil(string text) =>
        ToolkitMathRewriter.ForMathUtil().Rewrite(new SourceRewriteContext("Game1.cs", text, _symbols));

    public static IEnumerable<object[]> MatrixRenames() => ToolkitMap.Default.MatrixFactories.Select(r => new object[] { r.From, r.To });

    public static IEnumerable<object[]> MathUtilRenames() => ToolkitMap.Default.MathUtilMembers.Select(r => new object[] { r.From, r.To });

    [Theory]
    [MemberData(nameof(MatrixRenames))]
    public void Each_matrix_factory_is_renamed(string from, string to)
    {
        var result = Matrix(Wrap($"var m = Matrix.{from}(1f, 2f, 3f);"));

        Assert.Equal(Wrap($"var m = Matrix.{to}(1f, 2f, 3f);"), result.Text);
        Assert.False(Assert.Single(result.Notes).NeedsManualWork);
    }

    [Theory]
    [MemberData(nameof(MathUtilRenames))]
    public void Each_mathutil_member_becomes_a_mathhelper_member(string from, string to)
    {
        var result = MathUtil(Wrap($"var m = MathUtil.{from};"));

        Assert.Equal(Wrap($"var m = MathHelper.{to};"), result.Text);
    }

    [Fact]
    public void A_qualified_mathutil_becomes_the_full_monogame_name_when_the_namespace_is_not_imported()
    {
        var text = "class C\n{\n    float M() => SharpDX.MathUtil.Clamp(1f, 0f, 2f);\n}\n";

        Assert.Equal("class C\n{\n    float M() => Microsoft.Xna.Framework.MathHelper.Clamp(1f, 0f, 2f);\n}\n", MathUtil(text).Text);
    }

    [Fact]
    public void A_qualified_mathutil_becomes_the_short_name_when_the_namespace_is_imported()
    {
        var text = "using Microsoft.Xna.Framework;\nclass C\n{\n    float M() => SharpDX.MathUtil.Pi;\n}\n";

        Assert.Equal("using Microsoft.Xna.Framework;\nclass C\n{\n    float M() => MathHelper.Pi;\n}\n", MathUtil(text).Text);
    }

    [Fact]
    public void A_qualified_matrix_type_name_is_accepted()
    {
        var text = "class C\n{\n    object M() => Microsoft.Xna.Framework.Matrix.RotationX(1f);\n}\n";

        Assert.Equal("class C\n{\n    object M() => Microsoft.Xna.Framework.Matrix.CreateRotationX(1f);\n}\n", Matrix(text).Text);
    }

    [Theory]
    [InlineData("var t = matrix.Translation;")]
    [InlineData("var t = matrix.Scaling(2f);")]
    [InlineData("var t = Matrix.Translation;")]
    [InlineData("var t = this.Matrix.Translation(1f, 2f, 3f);")]
    [InlineData("var t = other.Matrix.RotationX(1f);")]
    [InlineData("var t = Matrix.CreateRotationX(1f);")]
    [InlineData("var t = RotationX(1f);")]
    public void Instances_properties_and_look_alikes_are_not_renamed(string body)
    {
        var text = Wrap(body);

        var result = Matrix(text);

        Assert.Equal(text, result.Text);
        Assert.Empty(result.Notes);
    }

    [Theory]
    [InlineData("var t = Matrix.LookAtLH(a, b, c);")]
    [InlineData("var t = Matrix.PerspectiveFovLH(1f, 1f, 1f, 2f);")]
    [InlineData("var t = Matrix.OrthoLH(1f, 1f, 1f, 2f);")]
    [InlineData("var t = Matrix.OrthoOffCenterLH(1f, 1f, 1f, 2f, 3f, 4f);")]
    public void Left_handed_factories_are_left_alone(string body)
    {
        var text = Wrap(body);

        Assert.Equal(text, Matrix(text).Text);
    }

    [Fact]
    public void Mathutil_members_that_are_not_in_the_map_are_left_alone()
    {
        var text = Wrap("var m = MathUtil.ZeroTolerance; var n = Mathutil.Pi; var o = other.MathUtil.Pi;");

        Assert.Equal(text, MathUtil(text).Text);
    }

    [Fact]
    public void A_file_with_no_framework_context_is_not_touched()
    {
        var text = "class C\n{\n    object M() => Matrix.RotationX(1f) + MathUtil.Pi;\n}\n";

        Assert.Equal(text, Matrix(text).Text);
        Assert.Equal(text, MathUtil(text).Text);
    }

    [Fact]
    public void Surrounding_text_is_kept_and_a_second_run_changes_nothing()
    {
        var text = "\uFEFFusing Microsoft.Xna.Framework;\r\n\r\nclass C\r\n{\r\n    // view\r\n    Matrix v = Matrix.LookAtRH(a, b, c); /* eye */\r\n}\r\n";

        var once = Matrix(text);
        var twice = Matrix(once.Text);

        Assert.Equal(text.Replace("LookAtRH", "CreateLookAt"), once.Text);
        Assert.Equal(once.Text, twice.Text);
        Assert.Empty(twice.Notes);
    }

    [Fact]
    public void Arguments_and_nested_calls_are_kept()
    {
        var text = Wrap("var m = Matrix.Translation(Matrix.RotationY(MathUtil.Pi).M11, 0f, 0f);");

        var afterMatrix = Matrix(text);
        var afterMathUtil = MathUtil(afterMatrix.Text);

        Assert.Equal(Wrap("var m = Matrix.CreateTranslation(Matrix.CreateRotationY(MathHelper.Pi).M11, 0f, 0f);"), afterMathUtil.Text);
    }
}
