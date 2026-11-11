using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using XnaReborn.App.Services;
using XnaReborn.App.Tests.BuildRun;
using XnaReborn.App.Tests.Diff;
using XnaReborn.App.Tests.Report;
using XnaReborn.App.ViewModels;
using XnaReborn.App.Views;
using XnaReborn.Migrator;
using XnaReborn.Runner.Hints;

namespace XnaReborn.App.Tests.Accessibility;

/// <summary>
/// Layout at a 150% text size in the smallest window the app allows to be 800x600. Avalonia has no system text scale, so
/// the test multiplies the font size of every text element after the page is laid out, then lays it out again and looks for
/// text that is squeezed (smaller than it wants to be) or that sticks out of the window where nothing can scroll to it.
/// </summary>
public sealed class TextScaleTests
{
    private const double _scale = 1.5;

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Open_screen_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        t.Recent.Items.Add(new RecentProject("C:/projects/Old", DateTimeOffset.UtcNow));
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);

        AssertFits(window, "Open", scale);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Report_screen_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Report);
        var vm = t.Get<ReportViewModel>();
        vm.SelectedRow = vm.Rows[0];
        Dispatcher.UIThread.RunJobs();

        AssertFits(window, "Report", scale);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Migration_setup_screen_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        t.Session.Report = ReportTestData.Small();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.MigrationSetup);

        AssertFits(window, "Migration setup", scale);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Diff_screen_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        var window = Show(t);
        var vm = await DiffTestData.OpenMigrationAsync(t);
        vm.SelectedFile = vm.Files[0];
        Dispatcher.UIThread.RunJobs();

        AssertFits(window, "Diff", scale);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Build_and_run_screen_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        var group = new DiagnosticGroup(
            BuildRunTestData.GamerServicesHint,
            [BuildRunTestData.Error("CS0234", "The type or namespace 'GamerServices' does not exist")]);
        t.Runner.Result = BuildRunTestData.Result(BuildRunTestData.FailedBuild(group), problem: "The build failed.");
        var window = Show(t);
        var vm = await BuildRunTestData.OpenAsync(t);
        await vm.BuildCommand.ExecuteAsync(null);
        Dispatcher.UIThread.RunJobs();

        AssertFits(window, "Build and run", scale);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Summary_screen_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        t.Session.Report = Fakes.SampleData.Report();
        t.Session.MigrationOptions = new MigrationOptions(BuildRunTestData.OutputFolder, MonoGameTarget.DesktopGL, false, false, new HashSet<string>());
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Summary);

        AssertFits(window, "Summary", scale);
    }

    [AvaloniaTheory]
    [InlineData(1.0)]
    [InlineData(_scale)]
    public async Task Error_banner_fits_at_800x600(double scale)
    {
        using var t = TestServices.Create();
        var window = Show(t);
        await t.OpenPageAsync(WorkflowStep.Open);
        t.Session.ShowError("Could not read the project because the folder has no solution file or project file in it.", "details");
        Dispatcher.UIThread.RunJobs();

        AssertFits(window, "Shell with error", scale);
    }

    private static MainWindow Show(TestServices t)
    {
        var window = new MainWindow { DataContext = t.Main, Width = 800, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static void AssertFits(MainWindow window, string page, double scale)
    {
        Dispatcher.UIThread.RunJobs();
        if (scale > 1)
        {
            ScaleText(window, scale);
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }

        var problems = Squeezed(window).Concat(OutsideWindow(window)).Concat(Overlapping(window)).Concat(StickingOutOfCards(window)).Concat(CutOffText(window)).Distinct().ToList();
        Capture(window, page, scale);
        Assert.True(problems.Count == 0, $"{page} at {scale * 100:0}% text:{Environment.NewLine}{string.Join(Environment.NewLine, problems)}");
    }

    /// <summary>Multiplies the font size of every text element by <paramref name="scale"/>. Sizes are read first, so a size that was inherited is scaled once.</summary>
    private static void ScaleText(Visual root, double scale)
    {
        var sizes = root.GetVisualDescendants()
            .Where(v => v is TextBlock or TextBox or TextEditor)
            .Cast<Control>()
            .Select(c => (Control: c, Size: c switch { TextBlock b => b.FontSize, TextBox x => x.FontSize, TextEditor e => e.FontSize, _ => 0 }))
            .ToList();
        foreach (var (control, size) in sizes)
        {
            switch (control)
            {
                case TextBlock b:
                    b.FontSize = size * scale;
                    break;
                case TextBox x:
                    x.FontSize = size * scale;
                    break;
                case TextEditor e:
                    e.FontSize = size * scale;
                    break;
            }
        }
    }

    /// <summary>Text or controls that were given less room than they asked for, so part of them is cut off.</summary>
    private static IEnumerable<string> Squeezed(Visual root)
    {
        foreach (var c in root.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.TemplatedParent is null))
        {
            if (c is not (TextBlock or Button or CheckBox or RadioButton or TextBox or ComboBox))
            {
                continue;
            }

            if (c is TextBlock { MaxLines: > 0 } || c is TextBlock trimmed && trimmed.TextTrimming != Avalonia.Media.TextTrimming.None)
            {
                continue;
            }

            // The words of a button are checked once, as the button.
            if (c is TextBlock && c.GetVisualAncestors().Any(a => a is Button or ToggleButton))
            {
                continue;
            }

            if (c.GetVisualAncestors().OfType<TextBox>().Any() || c.GetVisualAncestors().OfType<TextEditor>().Any())
            {
                continue;
            }

            // DesiredSize includes the margin; Bounds does not.
            var wants = c.DesiredSize.Deflate(c.Margin);
            if (wants.Width > c.Bounds.Width + 1 || wants.Height > c.Bounds.Height + 1)
            {
                yield return $"squeezed: {Describe(c)} wants {wants.Width:0}x{wants.Height:0} but got {c.Bounds.Width:0}x{c.Bounds.Height:0}";
            }
        }
    }

    /// <summary>Controls whose edge is beyond the window where no scroll viewer can bring them into view.</summary>
    private static IEnumerable<string> OutsideWindow(MainWindow window)
    {
        var size = window.ClientSize;
        foreach (var c in window.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.TemplatedParent is null && c.Bounds.Width > 0))
        {
            if (c.GetVisualAncestors().OfType<ScrollViewer>().Any() || c.GetVisualAncestors().OfType<ScrollBar>().Any())
            {
                // Content of a scroll viewer can be reached by scrolling; its own width is checked below.
                var scroll = c.GetVisualAncestors().OfType<ScrollViewer>().First();
                var hidden = scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Disabled;
                if (!hidden)
                {
                    continue;
                }
            }

            if (c is TextBlock && c.GetVisualAncestors().Any(a => a is Button or ToggleButton))
            {
                continue;
            }

            if (c.TranslatePoint(default, window) is not { } origin)
            {
                continue;
            }

            if (origin.X + c.Bounds.Width > size.Width + 1 && c is TextBlock or Button or CheckBox or RadioButton or TextBox or ComboBox)
            {
                yield return $"outside window (right): {Describe(c)} ends at {origin.X + c.Bounds.Width:0} of {size.Width:0}";
            }
        }
    }

    /// <summary>
    /// One-line text that is wider than the box it was given. A text block reports a desired width capped at what it was
    /// offered, so the real width of the string is measured here.
    /// </summary>
    private static IEnumerable<string> CutOffText(Visual root)
    {
        foreach (var b in root.GetVisualDescendants().OfType<TextBlock>().Where(b => b.IsEffectivelyVisible && b.Text is { Length: > 0 } && b.TemplatedParent is null))
        {
            if (b.TextWrapping != Avalonia.Media.TextWrapping.NoWrap || b.TextTrimming != Avalonia.Media.TextTrimming.None || b.GetVisualAncestors().Any(a => a is TextBox or TextEditor))
            {
                continue;
            }

            var typeface = new Avalonia.Media.Typeface(b.FontFamily, b.FontStyle, b.FontWeight);
            var layout = new Avalonia.Media.FormattedText(b.Text!, System.Globalization.CultureInfo.CurrentUICulture, Avalonia.Media.FlowDirection.LeftToRight, typeface, b.FontSize, null);
            if (layout.Width > b.Bounds.Width + 1)
            {
                yield return $"cut off text: {Describe(b)} needs {layout.Width:0} px, has {b.Bounds.Width:0}";
            }
        }
    }

    /// <summary>Text, buttons and inputs that stick out of the card (a bordered panel) they sit in, so the card cuts or misses them.</summary>
    private static IEnumerable<string> StickingOutOfCards(MainWindow window)
    {
        foreach (var c in window.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0 && c.TemplatedParent is null))
        {
            if (c is not (TextBlock or Button or CheckBox or RadioButton or TextBox or ComboBox) || c.GetVisualAncestors().Any(a => a is Button or ToggleButton or TextBox or TextEditor))
            {
                continue;
            }

            if (c.TranslatePoint(default, window) is not { } origin)
            {
                continue;
            }

            var right = origin.X + c.Bounds.Width;
            foreach (var card in c.GetVisualAncestors().OfType<Border>().Where(b => b.Classes.Contains("card") && b.TemplatedParent is null))
            {
                if (card.TranslatePoint(default, window) is { } cardOrigin && right > cardOrigin.X + card.Bounds.Width + 1)
                {
                    yield return $"sticks out of card: {Describe(c)} ends at {right:0}, its card at {cardOrigin.X + card.Bounds.Width:0}";
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Buttons and inputs that cover each other. The part of a control that is scrolled out of a scroll viewer does not
    /// count, because it is not on screen.
    /// </summary>
    private static IEnumerable<string> Overlapping(MainWindow window)
    {
        var shown = new List<(Control Control, Rect Rect)>();
        foreach (var c in ScreenAudit.Interactive(window).Where(c => c is Button or ToggleButton or TextBox or ComboBox or Expander))
        {
            if (c.TranslatePoint(default, window) is not { } origin)
            {
                continue;
            }

            var rect = new Rect(origin, c.Bounds.Size);
            foreach (var scroll in c.GetVisualAncestors().OfType<ScrollViewer>())
            {
                if (scroll.TranslatePoint(default, window) is { } top)
                {
                    rect = rect.Intersect(new Rect(top, scroll.Bounds.Size));
                }
            }

            if (rect.Width > 0 && rect.Height > 0)
            {
                shown.Add((c, rect));
            }
        }

        for (var i = 0; i < shown.Count; i++)
        {
            for (var j = i + 1; j < shown.Count; j++)
            {
                var (a, b) = (shown[i], shown[j]);
                if (a.Control.GetVisualAncestors().Contains(b.Control) || b.Control.GetVisualAncestors().Contains(a.Control))
                {
                    continue;
                }

                var overlap = a.Rect.Intersect(b.Rect);
                if (overlap.Width > 2 && overlap.Height > 2)
                {
                    yield return $"overlap: {Describe(a.Control)} and {Describe(b.Control)} share {overlap.Width:0}x{overlap.Height:0} px";
                }
            }
        }
    }

    private static string Describe(Control c)
    {
        var text = c switch { TextBlock b => b.Text, ContentControl cc => cc.Content as string, _ => null };
        return $"{c.GetType().Name} '{AccessibilityProbe.Label(c)}'{(text is null ? "" : " \"" + (text.Length > 40 ? text[..40] : text) + "\"")}";
    }

    /// <summary>Writes a PNG of the page when <c>XNAREBORN_SCREENSHOTS</c> names a folder, to look at the result by eye.</summary>
    private static void Capture(MainWindow window, string page, double scale)
    {
        if (Environment.GetEnvironmentVariable("XNAREBORN_SCREENSHOTS") is not { Length: > 0 } folder)
        {
            return;
        }

        Directory.CreateDirectory(folder);
        window.CaptureRenderedFrame()?.Save(Path.Combine(folder, $"scale-{scale * 100:0}-{page.Replace(' ', '-')}.png"), new PngBitmapEncoderOptions());
    }
}
