using XnaReborn.App.ViewModels;
using XnaReborn.Migrator.Output;
using XnaReborn.Migrator.Source;

namespace XnaReborn.App.Tests.Diff;

public sealed class DiffViewModelTests
{
    [Fact]
    public async Task The_file_list_has_every_change_ordered_by_kind_then_path()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.Equal(
            ["Game1.cs", "Program.cs", "Game.csproj", "Shim.cs", "Notes.txt", "Content/logo.png", "Old.txt"],
            vm.Files.Select(f => f.Path));
        Assert.Equal("7 files", vm.FilesSummary);
    }

    [Fact]
    public async Task Counts_are_given_per_kind_and_kinds_without_files_are_left_out()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.Equal(
            ["Modified: 2", "New: 2", "Manual work: 1", "Copied: 1", "Skipped: 1"],
            vm.KindCounts.Select(c => c.Text));

        var only = await DiffTestData.OpenMigrationAsync(t, [new FileChange("a.cs", ChangeKind.Copied, null, null, null)]);
        Assert.Equal(["Copied: 1"], only.KindCounts.Select(c => c.Text));
    }

    [Fact]
    public async Task Every_row_has_a_text_label_and_a_glyph_so_color_is_never_the_only_cue()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.All(vm.Files, f =>
        {
            Assert.False(string.IsNullOrWhiteSpace(f.KindLabel));
            Assert.False(string.IsNullOrWhiteSpace(f.KindGlyph));
            Assert.Contains(f.KindLabel, f.AccessibleName, StringComparison.Ordinal);
        });
        Assert.Equal(5, vm.Files.Select(f => f.KindGlyph).Distinct().Count());
    }

    [Fact]
    public async Task The_first_file_is_selected_and_its_rows_are_built()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.Equal("Game1.cs", vm.SelectedFile?.Path);
        Assert.NotNull(vm.CurrentDiff);
        Assert.True(vm.HasDiff);
        Assert.False(vm.HasPlaceholder);
        Assert.Equal(1, vm.CurrentDiff.Modified);
        Assert.Equal(1, vm.CurrentDiff.Added);
        Assert.Equal(vm.CurrentDiff.OldLines.Count, vm.CurrentDiff.NewLines.Count);
    }

    [Fact]
    public async Task Selecting_a_file_builds_the_rows_of_that_file()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);

        vm.SelectedFile = vm.Files.Single(f => f.Path == "Shim.cs");

        Assert.NotNull(vm.CurrentDiff);
        Assert.Equal("Shim.cs", vm.CurrentDiff.Path);
        Assert.Equal(1, vm.CurrentDiff.Added);
        Assert.All(vm.CurrentDiff.OldLines, l => Assert.Equal(DiffLineKind.Imaginary, l.Kind));
        Assert.Equal(["class Shim { }"], vm.CurrentDiff.NewLines.Select(l => l.Text));
    }

    [Fact]
    public async Task Files_with_markers_say_so_in_the_list()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.Equal([1, 2, 0, 0, 0, 0, 0], vm.Files.Select(f => f.MarkerCount));
        Assert.Equal("1 marker", vm.Files[0].MarkerText);
        Assert.Equal("2 markers", vm.Files[1].MarkerText);
        Assert.Contains("2 markers", vm.Files[1].AccessibleName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Markers_are_found_in_every_file_with_their_line_numbers_in_the_new_text()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.Equal(
            [("Game1.cs", 3), ("Program.cs", 2), ("Program.cs", 4)],
            vm.Markers.Select(m => (m.Path, m.NewLine)));
        Assert.Equal("// XNAREBORN: XNA039: first", vm.Markers[1].Text);
        Assert.Equal("3 manual-work markers", vm.MarkerStatus);
    }

    [Fact]
    public async Task Next_and_previous_move_through_the_markers_across_files_and_wrap_around()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);

        vm.NextMarkerCommand.Execute(null);
        Assert.Equal("Game1.cs", vm.SelectedFile?.Path);
        Assert.Equal(3, vm.Reveal?.NewLine);
        Assert.Equal("Marker 1 of 3", vm.MarkerStatus);

        vm.NextMarkerCommand.Execute(null);
        Assert.Equal("Program.cs", vm.SelectedFile?.Path);
        Assert.Equal(2, vm.Reveal?.NewLine);
        Assert.NotNull(vm.CurrentDiff);
        Assert.Equal("Program.cs", vm.CurrentDiff.Path);

        vm.NextMarkerCommand.Execute(null);
        Assert.Equal(4, vm.Reveal?.NewLine);
        Assert.Equal("Marker 3 of 3", vm.MarkerStatus);

        vm.NextMarkerCommand.Execute(null);
        Assert.Equal("Game1.cs", vm.SelectedFile?.Path);
        Assert.Equal("Marker 1 of 3", vm.MarkerStatus);

        vm.PreviousMarkerCommand.Execute(null);
        Assert.Equal("Program.cs", vm.SelectedFile?.Path);
        Assert.Equal(4, vm.Reveal?.NewLine);
        Assert.Equal("Marker 3 of 3", vm.MarkerStatus);

        vm.PreviousMarkerCommand.Execute(null);
        Assert.Equal(2, vm.Reveal?.NewLine);
    }

    [Fact]
    public async Task Previous_from_the_start_goes_to_the_last_marker()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);

        vm.PreviousMarkerCommand.Execute(null);

        Assert.Equal("Marker 3 of 3", vm.MarkerStatus);
        Assert.Equal("Program.cs", vm.SelectedFile?.Path);
        Assert.Equal(4, vm.Reveal?.NewLine);
    }

    [Fact]
    public async Task Asking_for_the_same_marker_again_makes_a_new_reveal_request()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t, [new FileChange("A.cs", ChangeKind.Modified, "x\n", "// XNAREBORN: R: y\n", null)]);

        vm.NextMarkerCommand.Execute(null);
        var first = vm.Reveal;
        vm.NextMarkerCommand.Execute(null);

        Assert.NotNull(first);
        Assert.NotSame(first, vm.Reveal);
        Assert.Equal(first.NewLine, vm.Reveal?.NewLine);
    }

    [Fact]
    public async Task Without_markers_the_navigation_commands_cannot_run()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t, [new FileChange("A.cs", ChangeKind.Modified, "x\n", "y\n", null)]);

        Assert.False(vm.HasMarkers);
        Assert.Equal("No manual-work markers", vm.MarkerStatus);
        Assert.False(vm.NextMarkerCommand.CanExecute(null));
        Assert.False(vm.PreviousMarkerCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_dry_run_shows_the_preview_banner_and_cannot_continue()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenDryRunAsync(t);

        Assert.True(vm.IsDryRun);
        Assert.Equal("Preview, nothing written yet", vm.PreviewBanner);
        Assert.False(vm.CanContinue);
        Assert.False(vm.ContinueCommand.CanExecute(null));
        Assert.Equal(7, vm.Files.Count);
    }

    [Fact]
    public async Task A_real_migration_has_no_banner_and_can_continue()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t);

        Assert.False(vm.IsDryRun);
        Assert.True(vm.CanContinue);
        Assert.True(vm.ContinueCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_real_migration_wins_over_an_older_dry_run()
    {
        using var t = TestServices.Create();
        t.Session.Report = Fakes.SampleData.Report();
        t.Session.DryRunResult = DiffTestData.Result([new FileChange("Dry.cs", ChangeKind.Created, null, "x\n", null)], completed: false);
        t.Session.MigrationResult = DiffTestData.Result([new FileChange("Real.cs", ChangeKind.Created, null, "x\n", null)], completed: true);

        await t.OpenPageAsync(WorkflowStep.Diff);

        var vm = t.Get<DiffViewModel>();
        Assert.False(vm.IsDryRun);
        Assert.Equal(["Real.cs"], vm.Files.Select(f => f.Path));
    }

    [Fact]
    public async Task A_migration_that_did_not_finish_cannot_continue_and_is_not_shown_as_a_preview()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t, completed: false);

        Assert.False(vm.IsDryRun);
        Assert.False(vm.CanContinue);
    }

    [Fact]
    public async Task Back_to_setup_goes_to_the_migration_setup_page_from_a_dry_run()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenDryRunAsync(t);

        vm.BackToSetupCommand.Execute(null);

        Assert.Equal(WorkflowStep.MigrationSetup, t.Main.CurrentStep);
    }

    [Fact]
    public async Task Continue_goes_to_build_and_run_after_a_real_migration()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);

        vm.ContinueCommand.Execute(null);

        Assert.Equal(WorkflowStep.BuildRun, t.Main.CurrentStep);
    }

    [Fact]
    public async Task A_binary_file_shows_a_placeholder_and_no_diff()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t, [new FileChange("data.bin", ChangeKind.Modified, "a\0b", "a\0c", null)]);

        Assert.False(vm.HasDiff);
        Assert.Null(vm.CurrentDiff);
        Assert.True(vm.HasPlaceholder);
        Assert.Contains("Binary", vm.Placeholder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_very_large_file_shows_a_placeholder_and_no_diff()
    {
        using var t = TestServices.Create();
        var big = new string('x', DiffViewModel.MaxDiffCharacters + 1);
        var vm = await DiffTestData.OpenMigrationAsync(t, [new FileChange("big.cs", ChangeKind.Modified, big, big + "y", null)]);

        Assert.False(vm.HasDiff);
        Assert.True(vm.HasPlaceholder);
        Assert.Contains("too large", vm.Placeholder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_exactly_at_the_size_limit_is_still_compared()
    {
        using var t = TestServices.Create();
        var text = new string('x', DiffViewModel.MaxDiffCharacters);
        var vm = await DiffTestData.OpenMigrationAsync(t, [new FileChange("edge.cs", ChangeKind.Modified, text, "y" + text[1..], null)]);

        Assert.True(vm.HasDiff);
        Assert.False(vm.HasPlaceholder);
    }

    [Theory]
    [InlineData("Notes.txt", "needs manual work")]
    [InlineData("Content/logo.png", "copied as it is")]
    [InlineData("Old.txt", "left out")]
    public async Task Files_without_text_show_a_placeholder_that_says_what_happened(string path, string expected)
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);

        vm.SelectedFile = vm.Files.Single(f => f.Path == path);

        Assert.Null(vm.CurrentDiff);
        Assert.Contains(expected, vm.Placeholder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_manual_work_placeholder_includes_the_reason()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);

        vm.SelectedFile = vm.Files.Single(f => f.Path == "Notes.txt");

        Assert.EndsWith("Needs a person", vm.Placeholder, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Selecting_a_text_file_after_a_placeholder_clears_the_placeholder()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenMigrationAsync(t);
        vm.SelectedFile = vm.Files.Single(f => f.Path == "Old.txt");
        Assert.True(vm.HasPlaceholder);

        vm.SelectedFile = vm.Files[0];

        Assert.False(vm.HasPlaceholder);
        Assert.True(vm.HasDiff);
    }

    [Fact]
    public async Task A_result_with_no_changes_shows_an_empty_page_without_errors()
    {
        using var t = TestServices.Create();

        var vm = await DiffTestData.OpenMigrationAsync(t, []);

        Assert.Empty(vm.Files);
        Assert.Null(vm.SelectedFile);
        Assert.False(vm.HasDiff);
        Assert.False(vm.HasPlaceholder);
        Assert.Equal("0 files", vm.FilesSummary);
    }

    [Fact]
    public async Task Opening_the_page_again_reads_the_new_result()
    {
        using var t = TestServices.Create();
        var vm = await DiffTestData.OpenDryRunAsync(t);
        Assert.True(vm.IsDryRun);

        t.Session.MigrationResult = DiffTestData.Result([new FileChange("Real.cs", ChangeKind.Created, null, "x\n", null)], completed: true);
        await vm.OnNavigatedToAsync();

        Assert.False(vm.IsDryRun);
        Assert.Equal(["Real.cs"], vm.Files.Select(f => f.Path));
        Assert.Equal("Real.cs", vm.CurrentDiff?.Path);
    }
}
