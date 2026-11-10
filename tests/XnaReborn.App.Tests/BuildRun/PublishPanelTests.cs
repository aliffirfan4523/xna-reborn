using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Runner.Processes;
using XnaReborn.Runner.Publish;

namespace XnaReborn.App.Tests.BuildRun;

/// <summary>The Publish section of the Build and run page: picker, publish, confirmation, cancel and open folder. Nothing is published.</summary>
public sealed class PublishPanelTests
{
    private static readonly string _published = Path.Combine(Path.GetTempPath(), "xnareborn-ui-test-publish", "win-x64");

    private static PublishResult Ok(string rid = "win-x64") =>
        new(PublishStatus.Published, "Published.", _published, rid, Path.Combine(_published, "Game.exe"), ["Game.exe"], TimeSpan.FromSeconds(4));

    [Fact]
    public async Task The_picker_lists_windows_linux_and_both_mac_platforms_and_starts_on_windows()
    {
        using var t = TestServices.Create();

        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;

        Assert.Equal(["win-x64", "linux-x64", "osx-x64", "osx-arm64"], panel.Targets.Select(x => x.Rid));
        Assert.Equal("win-x64", panel.SelectedTarget.Rid);
        Assert.Contains("cannot be started, packed as a macOS app or signed from Windows", panel.Notice, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Publish_is_off_without_a_migrated_copy()
    {
        using var t = TestServices.Create();
        var vm = await BuildRunTestData.OpenAsync(t);
        t.Session.MigrationResult = null;
        await vm.OnNavigatedToAsync();

        Assert.False(vm.Publish.PublishCommand.CanExecute(null));
        Assert.False(vm.Publish.CanOpenFolder);
    }

    [Fact]
    public async Task Publish_sends_the_chosen_platform_for_the_migrated_copy_and_shows_the_result()
    {
        using var t = TestServices.Create();
        t.Runner.PublishResults.Add(Ok("linux-x64"));
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;
        panel.SelectedTarget = PublishTarget.LinuxX64;

        await panel.PublishCommand.ExecuteAsync(null);

        Assert.Equal([(BuildRunTestData.OutputFolder, "linux-x64", false)], t.Runner.PublishCalls);
        Assert.Equal("Published.", panel.StatusText);
        Assert.False(panel.IsStatusError);
        Assert.Equal(_published, panel.ResultFolder);
        Assert.True(panel.OpenFolderCommand.CanExecute(null));
        Assert.False(panel.IsPublishing);
    }

    [Fact]
    public async Task Open_folder_opens_the_published_folder()
    {
        using var t = TestServices.Create();
        t.Runner.PublishResults.Add(Ok());
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;
        await panel.PublishCommand.ExecuteAsync(null);

        await panel.OpenFolderCommand.ExecuteAsync(null);

        Assert.Equal([_published], t.Shell.OpenedFolders);
    }

    [Fact]
    public async Task A_refused_publish_shows_the_reason_as_an_error_and_offers_no_folder()
    {
        using var t = TestServices.Create();
        t.Runner.PublishResults.Add(new PublishResult(PublishStatus.Refused, "This copy was migrated for WindowsDX.", null, "linux-x64", null, [], TimeSpan.Zero));
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;

        await panel.PublishCommand.ExecuteAsync(null);

        Assert.True(panel.IsStatusError);
        Assert.Equal("This copy was migrated for WindowsDX.", panel.StatusText);
        Assert.Null(panel.ResultFolder);
        Assert.False(panel.OpenFolderCommand.CanExecute(null));
    }

    [Fact]
    public async Task An_existing_folder_asks_first_and_a_no_publishes_nothing()
    {
        using var t = TestServices.Create();
        t.Runner.PublishResults.Add(new PublishResult(PublishStatus.NeedsConfirmation, "The folder already has content.", _published, "win-x64", null, [], TimeSpan.Zero));
        t.Dialogs.DefaultConfirmAnswer = false;
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;

        await panel.PublishCommand.ExecuteAsync(null);

        var question = Assert.Single(t.Dialogs.Confirmations);
        Assert.Equal("The folder already has content.", question.Message);
        Assert.Single(t.Runner.PublishCalls);
        Assert.False(t.Runner.PublishCalls[0].Confirmed);
        Assert.False(panel.IsStatusError);
        Assert.Contains("not changed", panel.StatusText, StringComparison.Ordinal);
        Assert.Null(panel.ResultFolder);
    }

    [Fact]
    public async Task An_existing_folder_asks_first_and_a_yes_publishes_with_confirmation()
    {
        using var t = TestServices.Create();
        t.Runner.PublishResults.Add(new PublishResult(PublishStatus.NeedsConfirmation, "The folder already has content.", _published, "win-x64", null, [], TimeSpan.Zero));
        t.Runner.PublishResults.Add(Ok());
        t.Dialogs.DefaultConfirmAnswer = true;
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;

        await panel.PublishCommand.ExecuteAsync(null);

        Assert.Equal([false, true], t.Runner.PublishCalls.Select(c => c.Confirmed));
        Assert.Equal(_published, panel.ResultFolder);
    }

    [Fact]
    public async Task Output_lines_show_as_progress_and_cancel_stops_the_publish()
    {
        using var t = TestServices.Create();
        var started = new TaskCompletionSource();
        t.Runner.OnPublish = async (output, token) =>
        {
            output?.Report(new OutputLine("Restoring packages", false));
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Ok();
        };
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;

        var running = panel.PublishCommand.ExecuteAsync(null);
        await started.Task;

        Assert.True(panel.IsPublishing);
        Assert.Equal("Restoring packages", panel.StatusText);
        Assert.True(panel.CancelCommand.CanExecute(null));
        Assert.False(panel.PublishCommand.CanExecute(null));

        panel.CancelCommand.Execute(null);
        await running;

        Assert.False(panel.IsPublishing);
        Assert.Equal("The publish was cancelled.", panel.StatusText);
        Assert.Null(panel.ResultFolder);
        Assert.True(panel.PublishCommand.CanExecute(null));
    }

    [Fact]
    public async Task Build_is_off_while_a_publish_runs()
    {
        using var t = TestServices.Create();
        var started = new TaskCompletionSource();
        t.Runner.OnPublish = async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            return Ok();
        };
        var vm = await BuildRunTestData.OpenAsync(t);

        var running = vm.Publish.PublishCommand.ExecuteAsync(null);
        await started.Task;

        Assert.False(vm.CanBuild);
        Assert.False(vm.BuildCommand.CanExecute(null));

        await vm.ShutdownAsync();
        await running;
        Assert.False(vm.Publish.IsPublishing);
    }

    [Fact]
    public async Task An_unexpected_error_is_shown_and_the_panel_is_usable_again()
    {
        using var t = TestServices.Create();
        t.Runner.OnPublish = (_, _) => throw new InvalidOperationException("boom");
        var panel = (await BuildRunTestData.OpenAsync(t)).Publish;

        await panel.PublishCommand.ExecuteAsync(null);

        Assert.True(panel.IsStatusError);
        Assert.False(panel.IsPublishing);
        Assert.NotNull(t.Session.ErrorMessage);
        Assert.True(panel.PublishCommand.CanExecute(null));
    }
}
