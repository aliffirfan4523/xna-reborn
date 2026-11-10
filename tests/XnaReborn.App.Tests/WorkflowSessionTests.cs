using System.ComponentModel;
using XnaReborn.App.Tests.Fakes;
using XnaReborn.App.ViewModels;
using XnaReborn.Migrator;

namespace XnaReborn.App.Tests;

public sealed class WorkflowSessionTests
{
    [Fact]
    public void A_new_report_clears_everything_that_depends_on_the_old_one()
    {
        var session = new WorkflowSession { Report = SampleData.Report() };
        session.SetExcluded("XNA001:Game.csproj", true);
        session.MigrationOptions = new MigrationOptions("out", MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        session.DryRunResult = SampleData.Migration(false);
        session.MigrationResult = SampleData.Migration(true);
        session.RunnerResult = SampleData.Runner();

        session.Report = SampleData.Report(score: 10);

        Assert.Empty(session.ExcludedPlanItemIds);
        Assert.Null(session.MigrationOptions);
        Assert.Null(session.DryRunResult);
        Assert.Null(session.MigrationResult);
        Assert.Null(session.RunnerResult);
    }

    [Fact]
    public void SetExcluded_replaces_the_set_and_raises_a_change_notification()
    {
        var session = new WorkflowSession();
        var before = session.ExcludedPlanItemIds;
        var raised = new List<string?>();
        ((INotifyPropertyChanged)session).PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        session.SetExcluded("a", true);
        session.SetExcluded("a", true);
        session.SetExcluded("b", true);
        session.SetExcluded("a", false);

        Assert.Empty(before);
        Assert.Equal(["b"], session.ExcludedPlanItemIds);
        Assert.Equal(3, raised.Count(n => n == nameof(WorkflowSession.ExcludedPlanItemIds)));
    }

    [Fact]
    public void ShowError_and_ClearError_toggle_HasError()
    {
        var session = new WorkflowSession();

        session.ShowError("x", "y");
        Assert.True(session.HasError);

        session.ClearError();
        Assert.False(session.HasError);
        Assert.Null(session.ErrorDetails);
    }

    [Fact]
    public void ShowError_refuses_an_empty_message()
    {
        Assert.Throws<ArgumentException>(() => new WorkflowSession().ShowError(" "));
    }
}
