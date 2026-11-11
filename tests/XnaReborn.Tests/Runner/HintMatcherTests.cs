using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
public class HintMatcherTests
{
    private static BuildDiagnostic Error(string code, string message) =>
        new(BuildDiagnosticSeverity.Error, code, message, "Game1.cs", 1, 1, "Game.csproj", null);

    /// <summary>Hint id, code, a message that must match it, and a near-miss that must not (same code).</summary>
    /// <remarks>Messages are the real compiler text from MonoGame.Framework.DesktopGL 3.8.6.1 on the .NET 10 SDK (C# 14).</remarks>
    public static readonly IReadOnlyList<(string Id, string Code, string Positive, string NearMiss)> BuildCases =
    [
        ("H001", "CS0234", "The type or namespace name 'GamerServices' does not exist in the namespace 'Microsoft.Xna.Framework' (are you missing an assembly reference?)", "The type or namespace name 'Audio' does not exist in the namespace 'Microsoft.Xna.Framework' (are you missing an assembly reference?)"),
        ("H002", "CS0103", "The name 'Guide' does not exist in the current context", "The name 'Guidelines' does not exist in the current context"),
        ("H003", "CS0234", "The type or namespace name 'Net' does not exist in the namespace 'Microsoft.Xna.Framework' (are you missing an assembly reference?)", "The type or namespace name 'Network' does not exist in the namespace 'Microsoft.Xna.Framework' (are you missing an assembly reference?)"),
        ("H004", "CS0103", "The name 'NetworkSession' does not exist in the current context", "The name 'NetworkTime' does not exist in the current context"),
        ("H005", "CS0234", "The type or namespace name 'Storage' does not exist in the namespace 'Microsoft.Xna.Framework' (are you missing an assembly reference?)", "The type or namespace name 'Media' does not exist in the namespace 'Microsoft.Xna.Framework' (are you missing an assembly reference?)"),
        ("H006", "CS0246", "The type or namespace name 'StorageDevice' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'StorageHelper' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H007", "CS0103", "The name 'SpriteBlendMode' does not exist in the current context", "The name 'SpriteSortMode' does not exist in the current context"),
        ("H008", "CS0103", "The name 'SaveStateMode' does not exist in the current context", "The name 'SaveData' does not exist in the current context"),
        ("H009", "CS1061", "'GraphicsDevice' does not contain a definition for 'RenderState' and no accessible extension method 'RenderState' accepting a first argument of type 'GraphicsDevice' could be found (are you missing a using directive or an assembly reference?)", "'GraphicsDevice' does not contain a definition for 'Viewport2' and no accessible extension method 'Viewport2' accepting a first argument of type 'GraphicsDevice' could be found (are you missing a using directive or an assembly reference?)"),
        ("H010", "CS0117", "'GraphicsDevice' does not contain a definition for 'RenderState'", "'GraphicsDevice' does not contain a definition for 'Adapter2'"),
        ("H011", "CS0246", "The type or namespace name 'ResolveTexture2D' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'RenderTarget3D' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H012", "CS1061", "'GraphicsDevice' does not contain a definition for 'ResolveBackBuffer' and no accessible extension method 'ResolveBackBuffer' accepting a first argument of type 'GraphicsDevice' could be found (are you missing a using directive or an assembly reference?)", "'GraphicsDevice' does not contain a definition for 'ResolveFront' and no accessible extension method 'ResolveFront' accepting a first argument of type 'GraphicsDevice' could be found (are you missing a using directive or an assembly reference?)"),
        ("H013", "CS1061", "'GraphicsDevice' does not contain a definition for 'VertexDeclaration' and no accessible extension method 'VertexDeclaration' accepting a first argument of type 'GraphicsDevice' could be found (are you missing a using directive or an assembly reference?)", "'GraphicsDevice' does not contain a definition for 'VertexBuffers' and no accessible extension method 'VertexBuffers' accepting a first argument of type 'GraphicsDevice' could be found (are you missing a using directive or an assembly reference?)"),
        ("H014", "CS0117", "'VertexPositionColor' does not contain a definition for 'VertexElements'", "'VertexPositionColor' does not contain a definition for 'Position2'"),
        ("H015", "CS1061", "'VertexPositionColor' does not contain a definition for 'VertexElements' and no accessible extension method 'VertexElements' accepting a first argument of type 'VertexPositionColor' could be found (are you missing a using directive or an assembly reference?)", "'VertexPositionColor' does not contain a definition for 'Color2' and no accessible extension method 'Color2' accepting a first argument of type 'VertexPositionColor' could be found (are you missing a using directive or an assembly reference?)"),
        ("H016", "CS1061", "'Effect' does not contain a definition for 'Begin' and no accessible extension method 'Begin' accepting a first argument of type 'Effect' could be found (are you missing a using directive or an assembly reference?)", "'Effect' does not contain a definition for 'Parameters2' and no accessible extension method 'Parameters2' accepting a first argument of type 'Effect' could be found (are you missing a using directive or an assembly reference?)"),
        ("H017", "CS1061", "'EffectPass' does not contain a definition for 'End' and no accessible extension method 'End' accepting a first argument of type 'EffectPass' could be found (are you missing a using directive or an assembly reference?)", "'EffectPass' does not contain a definition for 'Name2' and no accessible extension method 'Name2' accepting a first argument of type 'EffectPass' could be found (are you missing a using directive or an assembly reference?)"),
        ("H018", "MGCB", "Could not find \"Pescadero\" font file for Content\\Fonts\\Hud.spritefont", "Could not find file Content\\hero.png"),
        ("H019", "CS0246", "The type or namespace name 'AudioEngine' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'AudioFile' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H020", "MGCB", "Content\\Audio\\Game.xap: Unknown file type; no importer found", "Content\\Audio\\Game.wav: Unknown file type; no importer found"),
        ("H021", "NU1101", "Unable to find package Does.Not.Exist.Xna. No packages exist with this id in source(s): nuget.org", "Unable to find package Does.Not.Exist.Xna. No packages exist with this id in source(s): nuget.org"),
        ("H022", "MSB3073", "The command \"dotnet mgcb /@:Content.mgcb\" exited with code 1.", "The command \"dotnet mgcb /@:Content.mgcb\" exited with code 1."),
        ("H023", "CS5001", "Program does not contain a static 'Main' method suitable for an entry point", "Program does not contain a static 'Main' method suitable for an entry point"),
        ("H102", "CS0117", "'Color' does not contain a definition for 'TransparentBlack'", "'Color' does not contain a definition for 'TransparentWhite'"),
        ("H103", "CS0246", "The type or namespace name 'OpenTK' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'OpenTKish' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H104", "NETSDK1100", "To build a project targeting Windows on this operating system, set the EnableWindowsTargeting property to true.", "To build a project targeting Windows on this operating system, set the EnableWindowsTargeting property to true."),
        ("H105", "MGCB", "Failed to load assembly 'C:/game/Content/Pipeline/GlowPipeline.dll'", "Failed to build asset 'C:/game/Content/hero.png'"),
        ("H301", "MGFX0002", "Invalid profile ''. Pixel shader 'MainPS' must be SM 4.0 level 9.1 or higher!", "Invalid profile 'ps_4_0'. Pixel shader 'P' must be SM 3.0 or lower!"),
        ("H302", "MGFX0002", "Invalid profile 'ps_4_0'. Pixel shader 'P' must be SM 3.0 or lower!", "Invalid profile 'ps_2_0'. Pixel shader 'P' must be SM 4.0 level 9.1 or higher!"),
        ("H303", "X5608", "Compiled shader code uses too many arithmetic instruction slots (72). Max. allowed is 64.", "syntax error: unexpected token"),
        ("H304", "MGFX0001", "Unexpected token 'A' found. Expected CloseBracket", "Unexpected token 'A' found. Expected CloseBracket"),
        ("H305", "X4576", "Input signature mismatch", "Input signature mismatch"),
        ("H306", "MGCB", "Could not run ffprobe: no such file", "Could not find file hero.png"),
        ("H307", "X4502", "invalid ps_2_0 input semantic 'VPOS'", "invalid ps_2_0 input semantic 'COLOR3'"),
        ("H308", "X4505", "anything", "anything"),
        ("H309", "X5426", "anything", "anything"),
        ("H401", "CS0246", "The type or namespace name 'RectangleF' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'Rectangles' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H402", "CS0103", "The name 'Color4' does not exist in the current context", "The name 'Color' does not exist in the current context"),
        ("H403", "CS0246", "The type or namespace name 'GameSystem' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'GameSystems' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H404", "CS0246", "The type or namespace name 'GeometricPrimitive' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'GeometricPrimitives' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H405", "CS0246", "The type or namespace name 'SharpDX' could not be found (are you missing a using directive or an assembly reference?)", "The type or namespace name 'SharpDXtra' could not be found (are you missing a using directive or an assembly reference?)"),
        ("H406", "CS1061", "'EffectParameter' does not contain a definition for 'SetResource' and no accessible extension method 'SetResource' accepting a first argument of type 'EffectParameter' could be found (are you missing a using directive or an assembly reference?)", "'EffectParameter' does not contain a definition for 'SetValue2' and no accessible extension method 'SetValue2' accepting a first argument of type 'EffectParameter' could be found (are you missing a using directive or an assembly reference?)"),
        ("H407", "MGFX0001", "Tint.fx(17,9) : Unexpected token 'P' found. Expected Compile", "Tint.fx(17,9) : Unexpected token 'P' found. Expected Semicolon"),
        ("H408", "MGFX0001", "Tint.fx(5,14) : Unexpected token 'M' found. Expected None, Linear, Point, or Anisotropic.", "Tint.fx(5,14) : Unexpected token 'M' found. Expected Semicolon"),
    ];

    /// <summary>Codes whose near-miss differs only by code: the same message must not match under another code.</summary>
    private static readonly string[] _codeOnlyHints = ["H021", "H022", "H023", "H104", "H304", "H305", "H308", "H309"];

    public static IEnumerable<object[]> PositiveRows() => BuildCases.Select(c => new object[] { c.Id, c.Code, c.Positive });

    public static IEnumerable<object[]> NearMissRows() =>
        BuildCases.Where(c => !_codeOnlyHints.Contains(c.Id)).Select(c => new object[] { c.Id, c.Code, c.NearMiss });

    private static readonly HintMatcher _matcher = new();

    [Fact]
    public void Every_build_hint_in_the_catalog_has_a_test_case()
    {
        var buildIds = HintCatalog.Default.Entries.Where(e => !e.Match.IsRuntime).Select(e => e.Hint.Id).Order();

        Assert.Equal(buildIds, BuildCases.Select(c => c.Id).Order());
    }

    [Theory]
    [MemberData(nameof(PositiveRows))]
    public void Positive_sample_matches_its_hint(string id, string code, string message)
    {
        var hint = _matcher.Match(Error(code, message));

        Assert.NotNull(hint);
        Assert.Equal(id, hint.Id);
    }

    [Theory]
    [MemberData(nameof(NearMissRows))]
    public void Near_miss_does_not_match_the_hint(string id, string code, string message)
    {
        var hint = _matcher.Match(Error(code, message));

        Assert.NotEqual(id, hint?.Id);
    }

    [Theory]
    [InlineData("NU1101", "CS0246")]
    [InlineData("MSB3073", "MSB3074")]
    [InlineData("CS5001", "CS5002")]
    public void Code_only_hint_needs_the_exact_code(string code, string otherCode)
    {
        Assert.NotNull(_matcher.Match(Error(code, "anything")));
        Assert.Null(_matcher.Match(Error(otherCode, "anything")));
    }

    [Fact]
    public void Code_match_is_case_insensitive_and_contains_is_case_insensitive()
    {
        var hint = _matcher.Match(Error("cs0234", "the type or namespace name 'gamerservices' does not exist"));

        Assert.Equal("H001", hint?.Id);
    }

    [Fact]
    public void Unknown_error_has_no_known_fix()
    {
        Assert.Null(_matcher.Match(Error("CS1002", "; expected")));
        Assert.Null(_matcher.Match(Error("CS0246", "The type or namespace name 'Foo' could not be found")));
    }

    [Fact]
    public void Runtime_only_hints_are_never_returned_for_a_build_diagnostic()
    {
        var hint = _matcher.Match(Error("CS0103", "DllNotFoundException NoSuitableGraphicsDeviceException"));

        Assert.Null(hint);
    }

    private static HintMatcher Matcher(string hintsJson) =>
        new(HintCatalog.Parse("{ \"version\": 1, \"hints\": [" + hintsJson + "] }"));

    private static string H(string id, string match) =>
        $$"""{ "id": "{{id}}", "title": "T", "advice": "A", "match": {{match}} }""";

    [Fact]
    public void Code_with_message_contains_beats_code_only_even_when_listed_later()
    {
        var matcher = Matcher(H("CODE", """{ "code": "CS1" }""") + "," + H("BOTH", """{ "code": "CS1", "messageContains": "foo" }"""));

        Assert.Equal("BOTH", matcher.Match(Error("CS1", "a Foo b"))?.Id);
        Assert.Equal("CODE", matcher.Match(Error("CS1", "bar"))?.Id);
    }

    [Fact]
    public void Code_only_beats_message_regex_even_when_listed_later()
    {
        var matcher = Matcher(H("RX", """{ "messageRegex": "fo+" }""") + "," + H("CODE", """{ "code": "CS1" }"""));

        Assert.Equal("CODE", matcher.Match(Error("CS1", "foo"))?.Id);
        Assert.Equal("RX", matcher.Match(Error("CS2", "foo"))?.Id);
    }

    [Fact]
    public void First_match_in_catalog_order_wins_within_a_step()
    {
        var matcher = Matcher(H("A", """{ "messageRegex": "foo" }""") + "," + H("B", """{ "messageRegex": "fo+" }"""));

        Assert.Equal("A", matcher.Match(Error("CS1", "foo"))?.Id);
    }

    [Fact]
    public void Message_regex_with_a_code_needs_both()
    {
        var matcher = Matcher(H("A", """{ "code": "CS1", "messageRegex": "fo+" }"""));

        Assert.Equal("A", matcher.Match(Error("CS1", "foo"))?.Id);
        Assert.Null(matcher.Match(Error("CS2", "foo")));
        Assert.Null(matcher.Match(Error("CS1", "bar")));
    }

    [Fact]
    public void Slow_regex_that_times_out_is_treated_as_no_match()
    {
        var matcher = Matcher(H("A", """{ "messageRegex": "^(a+)+$" }"""));
        var text = new string('a', 60) + "!";

        Assert.Null(matcher.Match(Error("CS1", text)));
    }

    [Fact]
    public void Match_rejects_null()
    {
        Assert.Throws<ArgumentNullException>(() => _matcher.Match(null!));
    }
}
