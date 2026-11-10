using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;

namespace XnaReborn.App.Tests.MigrationSetup;

public sealed class MigrationSetupViewModelTests
{
    private static readonly string[] _excludable = ["XNA005", "XNA022", "XNA024", "XNA039", "XNA040", "XNA049"];

    private sealed class Setup : IDisposable
    {
        private readonly TestServices _t;

        public Setup(params string[] ruleIds)
        {
            Tree = new SetupTree();
            Service = new GuardingMigrationService();
            _t = TestServices.Create(s => s.AddSingleton<IMigrationService>(Service));
            _t.Session.SourcePath = Tree.Source;
            _t.Session.Report = SetupReports.WithRules(ruleIds);
        }

        public SetupTree Tree { get; }

        public GuardingMigrationService Service { get; }

        public WorkflowSession Session => _t.Session;

        public FakeDialogService Dialogs => _t.Dialogs;

        public MainViewModel Main => _t.Main;

        public MigrationSetupViewModel Vm => _t.Get<MigrationSetupViewModel>();

        public async Task<MigrationSetupViewModel> OpenAsync()
        {
            await _t.OpenPageAsync(WorkflowStep.MigrationSetup);
            return Vm;
        }

        public void Dispose()
        {
            _t.Dispose();
            Tree.Dispose();
        }
    }

    [Fact]
    public async Task Defaults_are_a_sibling_folder_DesktopGL_and_everything_optional_off()
    {
        using var s = new Setup("XNA022");
        var vm = await s.OpenAsync();

        Assert.Equal(s.Tree.Sibling, vm.OutputFolder);
        Assert.Equal(MonoGameTarget.DesktopGL, vm.Target);
        Assert.True(vm.IsDesktopGL);
        Assert.False(vm.EnableShims);
        Assert.False(vm.SubstituteFonts);
        Assert.False(vm.AllowWindowResize);
        Assert.False(vm.FullscreenToggle);
        Assert.False(vm.HighDpi);
        Assert.True(vm.OutputIsOk);
        Assert.True(vm.PreviewCommand.CanExecute(null));
    }

    [Fact]
    public async Task Migrating_without_touching_the_extras_sends_them_all_off()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();

        await vm.MigrateCommand.ExecuteAsync(null);

        var (options, _) = Assert.Single(s.Service.Calls);
        Assert.False(options.AllowWindowResize);
        Assert.False(options.FullscreenToggle);
        Assert.False(options.HighDpi);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public async Task Each_extras_toggle_reaches_only_its_own_migration_option(bool resize, bool fullscreen, bool dpi)
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        vm.AllowWindowResize = resize;
        vm.FullscreenToggle = fullscreen;
        vm.HighDpi = dpi;

        await vm.MigrateCommand.ExecuteAsync(null);

        var (options, _) = Assert.Single(s.Service.Calls);
        Assert.Equal((resize, fullscreen, dpi), (options.AllowWindowResize, options.FullscreenToggle, options.HighDpi));
        Assert.Equal((resize, fullscreen, dpi), (s.Session.MigrationOptions!.AllowWindowResize, s.Session.MigrationOptions.FullscreenToggle, s.Session.MigrationOptions.HighDpi));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task An_output_folder_inside_the_source_is_refused_and_no_run_starts()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();

        vm.OutputFolder = Path.Combine(s.Tree.Source, "out");

        Assert.True(vm.OutputIsRefused);
        Assert.Contains("inside the source folder", vm.OutputMessage);
        Assert.False(vm.MigrateCommand.CanExecute(null));
        Assert.False(vm.PreviewCommand.CanExecute(null));

        // Even when a command is forced, the page checks again before it runs.
        await vm.MigrateCommand.ExecuteAsync(null);
        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Empty(s.Service.Calls);
        Assert.Empty(s.Dialogs.Confirmations);
        Assert.Null(s.Session.MigrationResult);
        Assert.Null(s.Session.DryRunResult);
        Assert.False(Directory.Exists(vm.OutputFolder));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task The_source_folder_itself_is_refused()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();

        vm.OutputFolder = s.Tree.Source;

        Assert.True(vm.OutputIsRefused);
        Assert.False(vm.MigrateCommand.CanExecute(null));
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task A_non_empty_folder_shows_a_warning_and_is_not_used_without_a_yes()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        vm.OutputFolder = s.Tree.NonEmptyFolder();
        s.Dialogs.DefaultConfirmAnswer = false;

        Assert.True(vm.OutputNeedsConfirmation);
        Assert.Contains("already has content", vm.OutputMessage);

        await vm.MigrateCommand.ExecuteAsync(null);

        Assert.Single(s.Dialogs.Confirmations);
        Assert.Empty(s.Service.Calls);
        Assert.Null(s.Session.MigrationResult);
        Assert.True(vm.OutputNeedsConfirmation);
        Assert.False(vm.OutputIsOk);
        Assert.True(File.Exists(Path.Combine(vm.OutputFolder, "keep.txt")));
        Assert.NotEqual(WorkflowStep.Diff, s.Main.CurrentStep);
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Saying_no_again_still_keeps_it_refused_and_asks_again_each_time()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        vm.OutputFolder = s.Tree.NonEmptyFolder();

        await vm.MigrateCommand.ExecuteAsync(null);
        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Equal(2, s.Dialogs.Confirmations.Count);
        Assert.Empty(s.Service.Calls);
    }

    [Fact]
    [Trait("Category", "Safety")]
    public async Task Saying_yes_lets_the_run_use_the_non_empty_folder_and_passes_the_confirmation()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        vm.OutputFolder = s.Tree.NonEmptyFolder();
        s.Dialogs.ConfirmAnswers.Enqueue(true);

        await vm.MigrateCommand.ExecuteAsync(null);

        var call = Assert.Single(s.Service.Calls);
        Assert.True(call.ConfirmedNonEmpty);
        Assert.NotNull(s.Session.MigrationResult);
    }

    [Fact]
    public async Task An_empty_or_new_folder_is_used_without_a_question()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();

        await vm.MigrateCommand.ExecuteAsync(null);

        Assert.Empty(s.Dialogs.Confirmations);
        Assert.False(Assert.Single(s.Service.Calls).ConfirmedNonEmpty);
    }

    [Fact]
    public async Task Items_for_other_rules_are_always_applied_and_cannot_be_excluded()
    {
        using var s = new Setup("XNA001", "XNA002", "XNA022");
        var vm = await s.OpenAsync();

        var locked = vm.PlanItems.Where(i => i.Item.RuleId is "XNA001" or "XNA002").ToList();
        Assert.Equal(2, locked.Count);
        foreach (var item in locked)
        {
            Assert.False(item.CanExclude);
            Assert.True(item.IsIncluded);
            Assert.Contains("Always applied", item.Reason);
            item.IsIncluded = false;
            Assert.True(item.IsIncluded);
        }

        Assert.Empty(s.Session.ExcludedPlanItemIds);
    }

    [Fact]
    public async Task A_locked_id_already_in_the_session_is_still_applied_and_not_sent_as_excluded()
    {
        using var s = new Setup("XNA001");
        s.Session.SetExcluded("XNA001:Game.csproj", true);
        var vm = await s.OpenAsync();

        Assert.True(Assert.Single(vm.PlanItems).IsIncluded);
        await vm.PreviewCommand.ExecuteAsync(null);

        Assert.Empty(Assert.Single(s.Service.Calls).Options.ExcludedPlanItemIds);
    }

    [Fact]
    public async Task The_six_optional_rules_can_be_excluded_and_included_again_through_the_session()
    {
        using var s = new Setup(_excludable);
        var vm = await s.OpenAsync();

        Assert.Equal(6, vm.PlanItems.Count);
        Assert.All(vm.PlanItems, i => Assert.True(i.CanExclude));
        foreach (var item in vm.PlanItems)
        {
            item.IsIncluded = false;
            Assert.Contains(item.Id, s.Session.ExcludedPlanItemIds);
        }

        Assert.Equal("0 of 6 steps will run.", vm.PlanSummary);

        vm.PlanItems[0].IsIncluded = true;
        Assert.DoesNotContain(vm.PlanItems[0].Id, s.Session.ExcludedPlanItemIds);
        Assert.Equal(5, s.Session.ExcludedPlanItemIds.Count);
    }

    [Fact]
    public async Task Excluded_items_reach_the_run_and_stay_unchecked_when_the_page_is_shown_again()
    {
        using var s = new Setup("XNA022", "XNA001");
        var vm = await s.OpenAsync();
        var optional = vm.PlanItems.Single(i => i.CanExclude);
        optional.IsIncluded = false;

        await vm.PreviewCommand.ExecuteAsync(null);
        await vm.OnNavigatedToAsync();

        Assert.Equal([optional.Id], s.Service.Calls[0].Options.ExcludedPlanItemIds.ToArray());
        Assert.False(vm.PlanItems.Single(i => i.CanExclude).IsIncluded);
        Assert.True(vm.PlanItems.Single(i => !i.CanExclude).IsIncluded);
    }

    [Fact]
    public async Task Preview_is_a_dry_run_that_sets_only_the_dry_run_result_and_goes_to_Diff()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        vm.IsWindowsDx = true;
        vm.EnableShims = true;
        vm.SubstituteFonts = true;

        await vm.PreviewCommand.ExecuteAsync(null);

        var (options, _) = Assert.Single(s.Service.Calls);
        Assert.True(options.DryRun);
        Assert.Equal(MonoGameTarget.WindowsDX, options.Target);
        Assert.True(options.EnableShims);
        Assert.True(options.SubstituteFonts);
        Assert.Equal(s.Tree.Sibling, options.OutputFolder);
        Assert.NotNull(s.Session.DryRunResult);
        Assert.Null(s.Session.MigrationResult);
        Assert.False(s.Session.MigrationOptions!.DryRun);
        Assert.Equal(MonoGameTarget.WindowsDX, s.Session.MigrationOptions.Target);
        Assert.Equal(WorkflowStep.Diff, s.Main.CurrentStep);
        Assert.False(Directory.Exists(s.Tree.Sibling));
    }

    [Fact]
    public async Task Migrate_is_a_real_run_that_sets_only_the_migration_result_and_goes_to_Diff()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();

        await vm.MigrateCommand.ExecuteAsync(null);

        Assert.False(Assert.Single(s.Service.Calls).Options.DryRun);
        Assert.NotNull(s.Session.MigrationResult);
        Assert.True(s.Session.MigrationResult.Completed);
        Assert.Null(s.Session.DryRunResult);
        Assert.NotNull(s.Session.MigrationOptions);
        Assert.Equal(WorkflowStep.Diff, s.Main.CurrentStep);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Cancel_stops_a_running_migration_and_keeps_the_session_clean()
    {
        using var s = new Setup();
        s.Service.Gate = new TaskCompletionSource();
        var vm = await s.OpenAsync();

        var run = vm.MigrateCommand.ExecuteAsync(null);
        Assert.True(vm.IsBusy);
        Assert.True(vm.CanCancel);
        Assert.False(vm.MigrateCommand.CanExecute(null));

        vm.CancelRunCommand.Execute(null);
        await run;

        Assert.False(vm.IsBusy);
        Assert.Contains("cancelled", vm.StatusMessage);
        Assert.Null(s.Session.MigrationResult);
        Assert.Null(s.Session.MigrationOptions);
        Assert.False(s.Session.HasError);
        Assert.NotEqual(WorkflowStep.Diff, s.Main.CurrentStep);
    }

    [Fact]
    public async Task A_failed_run_shows_the_error_banner_and_stays_on_the_page()
    {
        using var s = new Setup();
        s.Service.Failure = new InvalidOperationException("disk full");
        var vm = await s.OpenAsync();

        await vm.MigrateCommand.ExecuteAsync(null);

        Assert.True(s.Session.HasError);
        Assert.Contains("disk full", s.Session.ErrorDetails);
        Assert.Null(s.Session.MigrationResult);
        Assert.False(vm.IsBusy);
        Assert.Equal(WorkflowStep.MigrationSetup, s.Main.CurrentStep);
    }

    [Fact]
    public async Task Browse_puts_the_picked_folder_into_the_box_and_cancel_keeps_it()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        var before = vm.OutputFolder;

        s.Dialogs.FolderToPick = null;
        await vm.BrowseCommand.ExecuteAsync(null);
        Assert.Equal(before, vm.OutputFolder);

        s.Dialogs.FolderToPick = Path.Combine(s.Tree.Root, "picked");
        await vm.BrowseCommand.ExecuteAsync(null);
        Assert.Equal(s.Dialogs.FolderToPick, vm.OutputFolder);
        Assert.True(vm.OutputIsOk);
    }

    [Fact]
    public async Task Opening_another_project_resets_the_choices_and_the_default_folder()
    {
        using var s = new Setup();
        var vm = await s.OpenAsync();
        vm.IsWindowsDx = true;

        var other = Path.Combine(s.Tree.Root, "Other");
        Directory.CreateDirectory(other);
        s.Session.SourcePath = other;
        s.Session.Report = SetupReports.WithRules();
        await vm.OnNavigatedToAsync();

        Assert.Equal(Path.Combine(s.Tree.Root, "Other-monogame"), vm.OutputFolder);
        Assert.True(vm.IsDesktopGL);
    }
}
