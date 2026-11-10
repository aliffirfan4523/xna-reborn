using System.ComponentModel;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Tests;

public sealed class StepperTests
{
    [Fact]
    public void A_fresh_session_has_Open_current_and_every_other_step_disabled()
    {
        using var t = TestServices.Create();

        Assert.Equal(6, t.Main.Steps.Count);
        Assert.Equal(Enum.GetValues<WorkflowStep>(), t.Main.Steps.Select(s => s.Step));
        Assert.True(t.Main.Steps[0].IsCurrent);
        Assert.True(t.Main.Steps[0].IsEnabled);
        Assert.All(t.Main.Steps.Skip(1), s =>
        {
            Assert.False(s.IsCurrent);
            Assert.False(s.IsEnabled);
            Assert.False(s.GoCommand.CanExecute(null));
        });
    }

    [Fact]
    public void Setting_the_report_enables_Report_and_MigrationSetup_and_raises_change_notifications()
    {
        using var t = TestServices.Create();
        var report = t.Main.Steps.Single(s => s.Step == WorkflowStep.Report);
        var raised = new List<string?>();
        ((INotifyPropertyChanged)report).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        t.Session.Report = SampleData.Report();

        Assert.True(report.IsEnabled);
        Assert.True(t.Main.Steps.Single(s => s.Step == WorkflowStep.MigrationSetup).IsEnabled);
        Assert.False(t.Main.Steps.Single(s => s.Step == WorkflowStep.Diff).IsEnabled);
        Assert.True(report.GoCommand.CanExecute(null));
        Assert.NotEmpty(raised);
    }

    [Fact]
    public async Task Moving_forward_marks_earlier_steps_done_and_the_new_step_current()
    {
        using var t = TestServices.Create();

        await t.OpenPageAsync(WorkflowStep.BuildRun);

        Assert.Equal(
            [true, true, true, true, false, false],
            t.Main.Steps.Select(s => s.IsDone));
        Assert.True(t.Main.Steps[4].IsCurrent);
        Assert.Equal(1, t.Main.Steps.Count(s => s.IsCurrent));
        Assert.Contains("current", t.Main.Steps[4].AutomationName, StringComparison.Ordinal);
        Assert.Equal("✓", t.Main.Steps[0].Marker);
        Assert.Equal("5", t.Main.Steps[4].Marker);
    }

    [Fact]
    public void A_step_button_navigates_when_pressed()
    {
        using var t = TestServices.Create();
        t.Session.Report = SampleData.Report();

        t.Main.Steps.Single(s => s.Step == WorkflowStep.Report).GoCommand.Execute(null);

        Assert.Equal(WorkflowStep.Report, t.Main.CurrentStep);
    }

    [Fact]
    public async Task Clearing_the_report_disables_the_forward_steps_again()
    {
        using var t = TestServices.Create();
        await t.OpenPageAsync(WorkflowStep.Summary);
        await t.Main.GoToAsync(WorkflowStep.Open);

        t.Session.Report = null;

        Assert.All(t.Main.Steps.Skip(1), s => Assert.False(s.IsEnabled, s.Step + " should be disabled"));
    }
}
