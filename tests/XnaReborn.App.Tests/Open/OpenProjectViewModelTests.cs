using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Tests.Open;

public sealed class OpenProjectViewModelTests
{
    private static readonly string _folder = RepoPaths.Fixture("xna40-basic");

    [Fact]
    public async Task Opening_a_folder_analyzes_it_sets_the_session_stores_it_and_shows_the_report()
    {
        using var t = TestServices.Create();
        var vm = t.Get<OpenProjectViewModel>();

        var opened = await vm.OpenPathAsync(_folder);

        Assert.True(opened);
        Assert.Equal([_folder], t.Analysis.AnalyzedPaths);
        Assert.Equal(_folder, t.Session.SourcePath);
        Assert.Same(t.Analysis.Report, t.Session.Report);
        Assert.Equal(_folder, Assert.Single(t.Recent.Items).Path);
        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
        Assert.False(t.Session.HasError);
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task Browse_folder_opens_the_picked_folder_and_a_cancelled_picker_does_nothing()
    {
        using var t = TestServices.Create();
        var vm = t.Get<OpenProjectViewModel>();

        await vm.BrowseFolderCommand.ExecuteAsync(null);
        Assert.Empty(t.Analysis.AnalyzedPaths);

        t.Dialogs.FolderToPick = _folder;
        await vm.BrowseFolderCommand.ExecuteAsync(null);
        Assert.Equal([_folder], t.Analysis.AnalyzedPaths);
    }

    [Fact]
    public async Task Browse_file_opens_the_picked_project_file()
    {
        using var t = TestServices.Create();
        var vm = t.Get<OpenProjectViewModel>();
        var csproj = Directory.EnumerateFiles(_folder, "*.csproj", SearchOption.AllDirectories).First();
        t.Dialogs.ProjectFileToPick = csproj;

        await vm.BrowseFileCommand.ExecuteAsync(null);

        Assert.Equal([csproj], t.Analysis.AnalyzedPaths);
        Assert.Equal(csproj, t.Session.SourcePath);
    }

    [Fact]
    public async Task Cancel_stops_the_analysis_and_leaves_the_session_alone()
    {
        using var t = TestServices.Create();
        t.Analysis.Gate = new TaskCompletionSource().Task;
        var vm = t.Get<OpenProjectViewModel>();

        var running = vm.OpenPathAsync(_folder);
        Assert.True(vm.IsBusy);
        vm.CancelCommand.Execute(null);
        var opened = await running;

        Assert.False(opened);
        Assert.False(vm.IsBusy);
        Assert.Null(t.Session.Report);
        Assert.Null(t.Session.SourcePath);
        Assert.Empty(t.Recent.Items);
        Assert.False(t.Session.HasError);
        Assert.Equal("Analysis canceled.", vm.Message);
        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
    }

    [Fact]
    public async Task A_report_without_a_project_shows_the_analyzer_explanation_and_stays_on_the_page()
    {
        using var t = TestServices.Create();
        t.Analysis.Report = SampleData.Report() with { NoProjectReason = "No .csproj or .sln was found in this folder." };
        var vm = t.Get<OpenProjectViewModel>();

        var opened = await vm.OpenPathAsync(_folder);

        Assert.False(opened);
        Assert.Contains("No .csproj or .sln was found in this folder.", t.Session.ErrorMessage);
        Assert.Equal(t.Session.ErrorMessage, vm.Message);
        Assert.Null(t.Session.Report);
        Assert.Empty(t.Recent.Items);
        Assert.Equal(WorkflowStep.Open, t.Main.CurrentStep);
    }

    [Fact]
    public async Task An_analyzer_failure_shows_a_banner_with_details_and_stores_nothing()
    {
        using var t = TestServices.Create();
        t.Analysis.Failure = new IOException("disk gone");
        var vm = t.Get<OpenProjectViewModel>();

        var opened = await vm.OpenPathAsync(_folder);

        Assert.False(opened);
        Assert.Contains("disk gone", t.Session.ErrorMessage);
        Assert.Contains("IOException", t.Session.ErrorDetails);
        Assert.Null(t.Session.Report);
        Assert.Empty(t.Recent.Items);
        Assert.False(vm.IsBusy);
    }

    [Theory]
    [InlineData("does-not-exist")]
    [InlineData("notes.txt")]
    public async Task A_missing_path_or_wrong_file_type_is_refused_before_analysis(string name)
    {
        using var t = TestServices.Create();
        var vm = t.Get<OpenProjectViewModel>();
        var path = Path.Combine(Path.GetTempPath(), "xr-open-" + Guid.NewGuid().ToString("N"), name);
        if (name == "notes.txt")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, "x", TestContext.Current.CancellationToken);
        }

        try
        {
            Assert.False(await vm.OpenPathAsync(path));
        }
        finally
        {
            var dir = Path.GetDirectoryName(path)!;
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }

        Assert.Empty(t.Analysis.AnalyzedPaths);
        Assert.True(t.Session.HasError);
    }

    [Fact]
    public async Task Recent_projects_are_listed_most_recent_first_and_clicking_one_opens_it()
    {
        using var t = TestServices.Create();
        var older = RepoPaths.Fixture("xna31-legacy");
        t.Recent.Add(older);
        t.Recent.Add(_folder);
        var vm = t.Get<OpenProjectViewModel>();

        await vm.OnNavigatedToAsync();
        Assert.Equal([_folder, older], vm.RecentProjects.Select(r => r.Path));
        Assert.True(vm.HasRecentProjects);

        await vm.OpenRecentCommand.ExecuteAsync(vm.RecentProjects[1]);

        Assert.Equal([older], t.Analysis.AnalyzedPaths);
        Assert.Equal([older, _folder], vm.RecentProjects.Select(r => r.Path));
    }

    [AvaloniaFact]
    public async Task Progress_text_is_shown_while_the_analysis_runs()
    {
        using var t = TestServices.Create();
        var gate = new TaskCompletionSource();
        t.Analysis.Gate = gate.Task;
        var vm = t.Get<OpenProjectViewModel>();

        var running = vm.OpenPathAsync(_folder);
        Dispatcher.UIThread.RunJobs();
        Assert.Contains("Reading projects", vm.ProgressText);

        gate.SetResult();
        Assert.True(await running);
    }
}
