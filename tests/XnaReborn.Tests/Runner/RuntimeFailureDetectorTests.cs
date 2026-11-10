using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Run;

namespace XnaReborn.Tests.Runner;

[Trait("Category", "Runner")]
public class RuntimeFailureDetectorTests
{
    private const string _fileNotFound =
        """
        Unhandled exception. Microsoft.Xna.Framework.Content.ContentLoadException: The content file was not found. ---> System.IO.FileNotFoundException: Could not find file 'C:\Games\Hero\bin\Debug\net8.0\Content\hero.xnb'.
        File name: 'C:\Games\Hero\bin\Debug\net8.0\Content\hero.xnb'
           at System.IO.Strategy.FileStreamHelpers.ValidateHandle(SafeFileHandle handle)
           --- End of inner exception stack trace ---
           at Microsoft.Xna.Framework.Content.ContentManager.OpenStream(String assetName)
           at Hero.Game1.LoadContent() in C:\Games\Hero\Game1.cs:line 42
        """;

    private const string _typeMismatch =
        """
        Unhandled exception. System.InvalidCastException: Unable to cast object of type 'Microsoft.Xna.Framework.Graphics.Texture2D' to type 'Microsoft.Xna.Framework.Graphics.SpriteFont'.
           at Microsoft.Xna.Framework.Content.ContentManager.ReadAsset[T](String assetName, Action`1 recordDisposableObject)
           at Microsoft.Xna.Framework.Content.ContentManager.Load[T](String assetName)
           at Hero.Game1.LoadContent() in C:\Games\Hero\Game1.cs:line 44
        """;

    private const string _noReader =
        "Unhandled exception. Microsoft.Xna.Framework.Content.ContentLoadException: Could not load font as a managed object. Cannot find ContentTypeReader MyGame.FooReader, MyGame.";

    private const string _contentLoadType =
        "Unhandled exception. Microsoft.Xna.Framework.Content.ContentLoadException: Error loading \"hero\". Wrong Type requested for asset.";

    private const string _dllNotFound =
        """
        Unhandled exception. System.DllNotFoundException: Unable to load DLL 'SDL2' or one of its dependencies: The specified module could not be found.
           at Sdl.GetError()
           at Microsoft.Xna.Framework.Sdl..cctor()
        """;

    private const string _sharedLibrary =
        "Unhandled exception. System.DllNotFoundException: Unable to load shared library 'libopenal.so.1' or one of its dependencies.";

    private const string _noDevice =
        """
        Unhandled exception. Microsoft.Xna.Framework.Graphics.NoSuitableGraphicsDeviceException: Failed to create graphics device!
           at Microsoft.Xna.Framework.Graphics.GraphicsDevice.PlatformInitialize()
           at Microsoft.Xna.Framework.GraphicsDeviceManager.CreateDevice()
        """;

    private const string _noAudio =
        """
        Unhandled exception. Microsoft.Xna.Framework.Audio.NoAudioHardwareException: Audio has failed to initialize. Call SoundEffect.Initialize() to see the OpenAL error.
           at Microsoft.Xna.Framework.Audio.OpenALSoundController.CheckALError(String operation)
        """;

    private static readonly RuntimeFailureDetector _detector = new();

    private static IReadOnlyList<string> Ids(string stdout, string stderr) =>
        _detector.Detect(stdout, stderr).Select(h => h.Id).ToList();

    public static IEnumerable<object[]> PositiveRows() =>
    [
        ["R001", "", _fileNotFound],
        ["R001", _fileNotFound, ""],
        ["R002", "", _typeMismatch],
        ["R002", "", _noReader],
        ["R002", "", _contentLoadType],
        ["R003", "", _dllNotFound],
        ["R003", "", _sharedLibrary],
        ["R003", "SDL2.dll not found", ""],
        ["R004", "", _noDevice],
        ["R004", "", "Failed to create graphics device!"],
        ["R004", "NoSuitableGraphicsDeviceException", ""],
        ["R005", "", _noAudio],
        ["R005", "", "OpenAL device could not be opened"],
    ];

    public static IEnumerable<object[]> NearMissRows() =>
    [
        ["R001", "Could not find file 'C:\\Users\\me\\save.dat'."],
        ["R001", "Loaded hero.xnb in 3 ms"],
        ["R002", "InvalidCastException: Unable to cast object of type 'A' to type 'B'.\n   at Hero.Game1.Update()"],
        ["R002", "ContentLoadException: The content file was loaded"],
        ["R003", "SDL2 initialised"],
        ["R003", "FileNotFoundException: Could not find file 'x.dat'"],
        ["R004", "Graphics device created: 1920x1080"],
        ["R005", "Audio device opened with OpenAL Soft"],
        ["R005", "Playing hero.wav"],
    ];

    [Theory]
    [MemberData(nameof(PositiveRows))]
    public void Sample_output_matches_its_hint(string id, string stdout, string stderr)
    {
        Assert.Contains(id, Ids(stdout, stderr));
    }

    [Theory]
    [MemberData(nameof(NearMissRows))]
    public void Near_miss_output_does_not_match_the_hint(string id, string text)
    {
        Assert.DoesNotContain(id, Ids(text, ""));
        Assert.DoesNotContain(id, Ids("", text));
    }

    [Fact]
    public void Every_runtime_hint_in_the_catalog_has_a_positive_sample_and_a_near_miss()
    {
        var runtimeIds = HintCatalog.Default.Entries.Where(e => e.Match.IsRuntime).Select(e => e.Hint.Id).Order().ToList();

        Assert.Equal(runtimeIds, PositiveRows().Select(r => (string)r[0]).Distinct().Order());
        Assert.Equal(runtimeIds, NearMissRows().Select(r => (string)r[0]).Distinct().Order());
    }

    [Fact]
    public void Output_without_a_known_failure_gives_no_hints()
    {
        Assert.Empty(_detector.Detect("Game started\nFrame 1\nFrame 2", ""));
        Assert.Empty(_detector.Detect("", ""));
        Assert.Empty(_detector.Detect(null!, null!));
    }

    [Fact]
    public void Several_failures_give_several_hints_in_catalog_order_without_repeats()
    {
        var hints = _detector.Detect(_noAudio + "\n" + _fileNotFound, _fileNotFound + "\n" + _dllNotFound);

        Assert.Equal(["R001", "R003", "R005"], hints.Select(h => h.Id));
    }

    [Fact]
    public void Build_hints_are_never_returned_for_game_output()
    {
        var hints = _detector.Detect("The type or namespace name 'GamerServices' does not exist  CS0234 NU1101", "");

        Assert.Empty(hints);
    }

    [Fact]
    public void Detector_uses_the_matcher_it_is_given()
    {
        var catalog = HintCatalog.Parse("""{ "version": 1, "hints": [ { "id": "X1", "title": "T", "advice": "A", "match": { "runtimeContains": "boom" } } ] }""");
        var detector = new RuntimeFailureDetector(new HintMatcher(catalog));

        Assert.Equal(["X1"], detector.Detect("", "BOOM").Select(h => h.Id));
        Assert.Empty(detector.Detect(_fileNotFound, ""));
    }
}
