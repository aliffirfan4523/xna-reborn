using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using XnaReborn.App.Services;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.MigrationSetup;

public sealed class MigrationSetupViewTests
{
    [AvaloniaFact]
    public async Task The_page_shows_the_default_folder_target_and_a_checklist_with_locked_and_optional_items()
    {
        using var tree = new SetupTree();
        using var t = TestServices.Create(s => s.AddSingleton<IMigrationService>(new GuardingMigrationService()));
        t.Session.SourcePath = tree.Source;
        t.Session.Report = SetupReports.WithRules("XNA001", "XNA022");
        var window = new MainWindow { DataContext = t.Main };
        window.Show();
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);
        Dispatcher.UIThread.RunJobs();

        var view = Assert.Single(window.GetVisualDescendants().OfType<MigrationSetupView>());
        var box = ById<TextBox>(view, "OutputFolderBox");
        Assert.Equal(tree.Sibling, box.Text);
        Assert.StartsWith("OK:", ById<TextBlock>(view, "OutputMessage").Text);
        Assert.True(ById<RadioButton>(view, "TargetDesktopGL").IsChecked);
        Assert.False(ById<CheckBox>(view, "ShimsToggle").IsChecked);
        Assert.True(ById<Button>(view, "PreviewButton").IsEffectivelyEnabled);
        Assert.True(ById<Button>(view, "MigrateButton").IsEffectivelyEnabled);

        var checks = view.GetVisualDescendants().OfType<CheckBox>()
            .Where(c => AutomationProperties.GetAutomationId(c) == "PlanItemCheck")
            .ToList();
        Assert.Equal(2, checks.Count);
        Assert.Single(checks, c => c.IsEnabled);
        var locked = Assert.Single(checks, c => !c.IsEnabled);
        Assert.True(locked.IsChecked);

        box.Text = Path.Combine(tree.Source, "inside");
        Dispatcher.UIThread.RunJobs();

        Assert.StartsWith("Problem:", ById<TextBlock>(view, "OutputMessage").Text);
        Assert.False(ById<Button>(view, "MigrateButton").IsEffectivelyEnabled);
    }

    private static T ById<T>(Control root, string id)
        where T : Control =>
        root.GetVisualDescendants().OfType<T>().Single(c => AutomationProperties.GetAutomationId(c) == id);
}
