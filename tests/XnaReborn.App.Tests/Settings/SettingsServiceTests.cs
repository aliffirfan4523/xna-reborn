using XnaReborn.App.Services;
using XnaReborn.Migrator;

namespace XnaReborn.App.Tests.Settings;

/// <summary>The JSON-file settings store: round trip, and a missing, corrupt or newer file never stops the app.</summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "xnareborn-settings-" + Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_folder, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void A_missing_file_gives_the_defaults_without_a_problem()
    {
        var service = new SettingsService(FilePath);

        Assert.Equal(new AppSettings(), service.Current);
        Assert.Null(service.LoadProblem);
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public void The_defaults_are_the_safe_choices()
    {
        var d = new AppSettings();

        Assert.Equal(AppSettings.CurrentVersion, d.Version);
        Assert.Equal(ThemeChoice.System, d.Theme);
        Assert.Equal(MotionChoice.System, d.ReduceMotion);
        Assert.Equal(MonoGameTarget.DesktopGL, d.DefaultTarget);
        Assert.False(d.EnableShims || d.SubstituteFonts || d.AllowWindowResize || d.FullscreenToggle || d.HighDpi);
        Assert.Null(d.OutputParentFolder);
        Assert.Equal("win-x64", d.PublishRid);
        Assert.True(d.KeepRecentProjects && d.ShowHints && d.AutoFollowLog && d.CheckSdkOnStart);
        Assert.Equal(10, d.MaxRecentProjects);
    }

    [Fact]
    public void Saved_settings_come_back_from_a_new_service()
    {
        var chosen = new AppSettings
        {
            Theme = ThemeChoice.Dark,
            ReduceMotion = MotionChoice.On,
            DefaultTarget = MonoGameTarget.WindowsDX,
            EnableShims = true,
            SubstituteFonts = true,
            AllowWindowResize = true,
            FullscreenToggle = true,
            HighDpi = true,
            OutputParentFolder = Path.Combine(_folder, "copies"),
            PublishRid = "linux-x64",
            KeepRecentProjects = false,
            MaxRecentProjects = 3,
            ShowHints = false,
            AutoFollowLog = false,
            CheckSdkOnStart = false,
        };

        Assert.True(new SettingsService(FilePath).Save(chosen));
        var again = new SettingsService(FilePath);

        Assert.Equal(chosen, again.Current);
        Assert.Null(again.LoadProblem);
        Assert.Contains("\"theme\": \"Dark\"", File.ReadAllText(FilePath), StringComparison.Ordinal);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("{ \"theme\": \"Purple\" }")]
    [InlineData("{ \"maxRecentProjects\": \"many\" }")]
    [InlineData("")]
    public void A_corrupt_file_gives_the_defaults_and_says_so(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, content);

        var service = new SettingsService(FilePath);

        Assert.Equal(new AppSettings(), service.Current);
        Assert.NotNull(service.LoadProblem);
    }

    [Fact]
    public void A_file_with_the_word_null_gives_the_defaults_and_says_so()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "null");

        var service = new SettingsService(FilePath);

        Assert.Equal(new AppSettings(), service.Current);
        Assert.NotNull(service.LoadProblem);
    }

    [Fact]
    public void A_file_from_a_newer_version_is_ignored()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ \"version\": 99, \"theme\": \"Dark\" }");

        var service = new SettingsService(FilePath);

        Assert.Equal(ThemeChoice.System, service.Current.Theme);
        Assert.NotNull(service.LoadProblem);
    }

    [Fact]
    public void Missing_properties_keep_their_defaults_and_unknown_ones_are_ignored()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ \"version\": 1, \"theme\": \"Light\", \"somethingNew\": true }");

        var service = new SettingsService(FilePath);

        Assert.Equal(ThemeChoice.Light, service.Current.Theme);
        Assert.True(service.Current.ShowHints);
        Assert.Null(service.LoadProblem);
    }

    [Fact]
    public void Values_out_of_range_are_brought_back_in_range()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ \"maxRecentProjects\": 500, \"publishRid\": \"linux-x64 --evil\", \"outputParentFolder\": \"   \" }");

        var service = new SettingsService(FilePath);

        Assert.Equal(AppSettings.MaxRecentLimit, service.Current.MaxRecentProjects);
        Assert.Equal(AppSettings.DefaultPublishRid, service.Current.PublishRid);
        Assert.Null(service.Current.OutputParentFolder);
        Assert.Equal(1, new AppSettings { MaxRecentProjects = -4 }.Normalize().MaxRecentProjects);
    }

    [Fact]
    public void Saving_raises_Changed_only_when_a_value_changed()
    {
        var service = new SettingsService(FilePath);
        var raised = 0;
        service.Changed += (_, _) => raised++;

        service.Save(service.Current with { Theme = ThemeChoice.Light });
        service.Save(service.Current with { Theme = ThemeChoice.Light });

        Assert.Equal(1, raised);
        Assert.Equal(ThemeChoice.Light, service.Current.Theme);
    }

    [Fact]
    public void A_save_that_fails_still_changes_the_settings_in_memory_and_reports_false()
    {
        // The file path is a folder, so writing there fails.
        Directory.CreateDirectory(FilePath);
        var service = new SettingsService(FilePath);

        var written = service.Save(service.Current with { Theme = ThemeChoice.Dark });

        Assert.False(written);
        Assert.Equal(ThemeChoice.Dark, service.Current.Theme);
    }

    [Fact]
    public void A_successful_save_clears_an_earlier_load_problem()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(FilePath, "{ nope");
        var service = new SettingsService(FilePath);
        Assert.NotNull(service.LoadProblem);

        Assert.True(service.Save(new AppSettings()));

        Assert.Null(service.LoadProblem);
        Assert.Equal(new AppSettings(), new SettingsService(FilePath).Current);
    }
}
