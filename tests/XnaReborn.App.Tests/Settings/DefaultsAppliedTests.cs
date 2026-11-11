using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.BuildRun;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.Tests.MigrationSetup;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;
using XnaReborn.Runner.Diagnostics;
using XnaReborn.Runner.Hints;
using XnaReborn.Runner.Publish;

namespace XnaReborn.App.Tests.Settings;

/// <summary>Migration setup, Build and run and Open read the saved defaults when a project is opened; per-run choices win.</summary>
public sealed class DefaultsAppliedTests
{
    private static (TestServices Services, SetupTree Tree) CreateSetup(AppSettings settings, params string[] ruleIds)
    {
        var tree = new SetupTree();
        var t = TestServices.Create(s => s.AddSingleton<IMigrationService>(new GuardingMigrationService()));
        t.Settings.Current = settings;
        t.Session.SourcePath = tree.Source;
        t.Session.Report = SetupReports.WithRules(ruleIds);
        return (t, tree);
    }

    [Fact]
    public async Task Migration_setup_starts_with_the_saved_target_and_options()
    {
        var (t, tree) = CreateSetup(new AppSettings
        {
            DefaultTarget = MonoGameTarget.WindowsDX,
            EnableShims = true,
            SubstituteFonts = true,
            AllowWindowResize = true,
            FullscreenToggle = true,
            HighDpi = true,
        });
        using var _ = t;
        using var __ = tree;

        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();

        Assert.Equal(MonoGameTarget.WindowsDX, vm.Target);
        Assert.True(vm.EnableShims && vm.SubstituteFonts && vm.AllowWindowResize && vm.FullscreenToggle && vm.HighDpi);
    }

    [Fact]
    public async Task The_saved_defaults_flow_into_the_options_of_the_run_and_a_per_run_choice_overrides_them()
    {
        var (t, tree) = CreateSetup(new AppSettings { EnableShims = true, HighDpi = true });
        using var _ = t;
        using var __ = tree;
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();

        vm.EnableShims = false;
        vm.FullscreenToggle = true;
        await vm.PreviewCommand.ExecuteAsync(null);

        var service = (GuardingMigrationService)t.Get<IMigrationService>();
        var options = Assert.Single(service.Calls).Options;
        Assert.False(options.EnableShims);
        Assert.True(options.HighDpi);
        Assert.True(options.FullscreenToggle);
    }

    [Fact]
    public async Task A_choice_made_for_one_run_is_not_replaced_when_the_page_is_opened_again()
    {
        var (t, tree) = CreateSetup(new AppSettings { EnableShims = true });
        using var _ = t;
        using var __ = tree;
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();
        vm.EnableShims = false;

        t.Settings.Current = t.Settings.Current with { HighDpi = true };
        await t.Main.GoToAsync(WorkflowStep.Report);
        await t.Main.GoToAsync(WorkflowStep.MigrationSetup);

        Assert.False(vm.EnableShims);
        Assert.False(vm.HighDpi);
    }

    [Fact]
    public async Task A_new_project_reads_the_defaults_again()
    {
        var (t, tree) = CreateSetup(new AppSettings());
        using var _ = t;
        using var __ = tree;
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();
        Assert.False(vm.EnableShims);

        t.Settings.Current = t.Settings.Current with { EnableShims = true };
        t.Session.Report = SetupReports.WithRules("XNA022");
        await t.Main.GoToAsync(WorkflowStep.Report);
        await t.Main.GoToAsync(WorkflowStep.MigrationSetup);

        Assert.True(vm.EnableShims);
    }

    [Fact]
    public async Task A_toolkit_project_still_starts_on_WindowsDX_when_the_default_is_DesktopGL()
    {
        var (t, tree) = CreateSetup(new AppSettings { DefaultTarget = MonoGameTarget.DesktopGL });
        using var _ = t;
        using var __ = tree;
        t.Session.Report = t.Session.Report! with { Projects = [.. t.Session.Report.Projects, SetupReports.ToolkitProject()] };

        await t.OpenPageAsync(WorkflowStep.MigrationSetup);

        Assert.Equal(MonoGameTarget.WindowsDX, t.Get<MigrationSetupViewModel>().Target);
    }

    [Fact]
    public async Task The_output_parent_folder_decides_where_the_copy_goes_and_the_folder_is_still_checked()
    {
        var (t, tree) = CreateSetup(new AppSettings());
        using var _ = t;
        using var __ = tree;
        var parent = Path.Combine(tree.Root, "all-copies");
        Directory.CreateDirectory(parent);
        t.Settings.Current = t.Settings.Current with { OutputParentFolder = parent };

        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();

        Assert.Equal(Path.Combine(parent, "MyGame-monogame"), vm.OutputFolder);
        Assert.True(vm.OutputIsOk);
    }

    [Fact]
    public async Task A_parent_folder_inside_the_original_project_is_refused_like_any_other_folder()
    {
        var (t, tree) = CreateSetup(new AppSettings());
        using var _ = t;
        using var __ = tree;
        t.Settings.Current = t.Settings.Current with { OutputParentFolder = tree.Source };

        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        var vm = t.Get<MigrationSetupViewModel>();

        Assert.True(vm.OutputIsRefused);
        Assert.False(vm.MigrateCommand.CanExecute(null));
    }

    [Fact]
    public async Task Build_and_run_starts_with_the_saved_log_follow_and_publish_platform()
    {
        using var t = TestServices.Create();
        t.Settings.Current = new AppSettings { AutoFollowLog = false, PublishRid = "osx-arm64" };

        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.False(vm.AutoScroll);
        Assert.Equal(PublishTarget.MacArm64, vm.Publish.SelectedTarget);
    }

    [Fact]
    public async Task A_choice_on_the_build_page_survives_leaving_and_coming_back()
    {
        using var t = TestServices.Create();
        var vm = await BuildRunTestData.OpenAsync(t);
        Assert.True(vm.AutoScroll);

        vm.AutoScroll = false;
        vm.Publish.SelectedTarget = PublishTarget.LinuxX64;
        await t.Main.GoToAsync(WorkflowStep.Diff);
        await t.Main.GoToAsync(WorkflowStep.BuildRun);

        Assert.False(vm.AutoScroll);
        Assert.Equal(PublishTarget.LinuxX64, vm.Publish.SelectedTarget);
    }

    [Fact]
    public async Task With_the_SDK_check_off_the_page_does_not_check_but_can_still_build()
    {
        using var t = TestServices.Create();
        t.Settings.Current = new AppSettings { CheckSdkOnStart = false };

        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.Null(vm.EnvironmentStatus);
        Assert.Contains("Not checked", vm.SdkText, StringComparison.Ordinal);
        Assert.True(vm.CanBuild);
        await vm.RefreshEnvironmentCommand.ExecuteAsync(null);
        Assert.NotNull(vm.EnvironmentStatus);
    }

    [Fact]
    public async Task With_the_SDK_check_on_the_page_checks_when_it_opens()
    {
        using var t = TestServices.Create();

        var vm = await BuildRunTestData.OpenAsync(t);

        Assert.NotNull(vm.EnvironmentStatus);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Hints_are_shown_or_hidden_in_the_build_results(bool showHints)
    {
        using var t = TestServices.Create();
        t.Settings.Current = new AppSettings { ShowHints = showHints };
        var group = new DiagnosticGroup(BuildRunTestData.GamerServicesHint, [BuildRunTestData.Error("CS0234", "The type or namespace 'GamerServices' does not exist")]);
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(group), problem: "The build failed.");
        var vm = await BuildRunTestData.OpenAsync(t);

        await vm.BuildCommand.ExecuteAsync(null);

        var shown = Assert.Single(vm.Groups);
        Assert.Equal(showHints, shown.HasAdvice);
        Assert.Equal(showHints, shown.HasRuleId);
        Assert.Equal(showHints, shown.RuleText.Length > 0);
        Assert.Single(shown.Items);
    }

    [Fact]
    public async Task Open_remembers_a_project_only_when_the_setting_is_on()
    {
        using var t = TestServices.Create();
        var vm = t.Get<OpenProjectViewModel>();
        t.Settings.Current = new AppSettings { KeepRecentProjects = false };
        var path = RepoPaths.Fixture("xna40-basic");

        Assert.True(await vm.OpenPathAsync(path));
        Assert.Empty(t.Recent.Load());
        Assert.False(vm.HasRecentProjects);

        t.Settings.Current = new AppSettings();
        Assert.True(await vm.OpenPathAsync(path));
        Assert.Single(t.Recent.Load());
    }

    [Fact]
    public async Task Open_shows_and_keeps_no_more_recent_projects_than_the_saved_count()
    {
        using var t = TestServices.Create();
        for (var i = 0; i < 6; i++)
        {
            t.Recent.Add("C:/projects/P" + i);
        }

        t.Settings.Current = new AppSettings { MaxRecentProjects = 3 };
        var vm = t.Get<OpenProjectViewModel>();
        await vm.OnNavigatedToAsync();
        Assert.Equal(3, vm.RecentProjects.Count);

        Assert.True(await vm.OpenPathAsync(RepoPaths.Fixture("xna40-basic")));

        Assert.Equal(3, t.Recent.Load().Count);
        Assert.Equal(3, vm.RecentProjects.Count);
    }

    [Fact]
    public async Task Open_hides_the_list_when_recent_projects_are_switched_off()
    {
        using var t = TestServices.Create();
        t.Recent.Add("C:/projects/Old");
        t.Settings.Current = new AppSettings { KeepRecentProjects = false };
        var vm = t.Get<OpenProjectViewModel>();

        await vm.OnNavigatedToAsync();

        Assert.Empty(vm.RecentProjects);
    }
}
