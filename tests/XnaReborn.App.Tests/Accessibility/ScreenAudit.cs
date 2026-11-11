using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using XnaReborn.App.Views;

namespace XnaReborn.App.Tests.Accessibility;

/// <summary>
/// Walks the live visual tree of a shown window and lists what a keyboard or screen-reader user would trip over.
/// Each check returns plain strings so a failing test prints every problem on the page at once.
/// </summary>
internal static class ScreenAudit
{
    /// <summary>The smallest height of a button or input, in device-independent pixels (WCAG 2.5.8 asks 24; the design system asks 32).</summary>
    public const double MinTarget = 32;

    private const string _severityGlyphs = "\u25CF\u25B2\u25A0\u25C6";

    /// <summary>What a screen reader announces for a control: the explicit name, else the name the control's own peer builds from its content.</summary>
    public static string AccessibleName(Control c)
    {
        var explicitName = AutomationProperties.GetName(c);
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName;
        }

        return ControlAutomationPeer.CreatePeerForElement(c).GetName() ?? string.Empty;
    }

    /// <summary>
    /// Every control a person can reach with the keyboard or the mouse: anything that takes focus by Tab, plus the usual
    /// input types. Template parts (the drop-down button of a ComboBox, the scroll bar arrows) are left out. The text area
    /// inside a text editor is the editor itself to a person, so the editor is listed and its area is not.
    /// </summary>
    public static List<Control> Interactive(Visual root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && c.IsEffectivelyEnabled)
            .Where(c => c is Button or ToggleButton or TextBox or ComboBox or ListBox or Expander or GridSplitter or TextEditor || (c.Focusable && c.IsTabStop && c is not ScrollViewer))
            .Where(c => c is not RepeatButton && c is not TextArea && c.TemplatedParent is null && !c.GetVisualAncestors().OfType<ScrollBar>().Any())
            .Where(c => c is not TextBox || !c.GetVisualAncestors().OfType<TextEditor>().Any())
            .ToList();

    /// <summary>Interactive controls that have no accessible name.</summary>
    public static IEnumerable<string> Unnamed(Visual root) =>
        Interactive(root).Where(c => string.IsNullOrWhiteSpace(AccessibleName(c))).Select(AccessibilityProbe.Describe);

    /// <summary>Text areas of a text editor, which is where the keyboard focus really lands. Each needs the name of its editor.</summary>
    public static IEnumerable<string> UnnamedEditorAreas(Visual root) =>
        root.GetVisualDescendants().OfType<TextArea>()
            .Where(a => a.IsEffectivelyVisible && string.IsNullOrWhiteSpace(AutomationProperties.GetName(a)))
            .Select(a => "text area of '" + (a.FindAncestorOfType<TextEditor>() is { } e ? AutomationProperties.GetName(e) : "?") + "'");

    /// <summary>Buttons, toggles and inputs shorter than <see cref="MinTarget"/>. A splitter is a drag handle with keyboard arrows, so it is exempt.</summary>
    public static IEnumerable<string> SmallTargets(Visual root) =>
        Interactive(root)
            .Where(c => c is Button or ToggleButton or TextBox or ComboBox or Expander)
            .Where(c => c.Bounds.Height < MinTarget - 0.5)
            .Select(c => $"{AccessibilityProbe.Describe(c)} is {c.Bounds.Height:0.#} px high")
            .Concat(root.GetVisualDescendants().OfType<ToggleButton>()
                .Where(t => t.TemplatedParent is Expander && t.IsEffectivelyVisible && t.Bounds.Height < MinTarget - 0.5)
                .Select(t => $"Expander header '{AccessibleName((Control)t.TemplatedParent!)}' is {t.Bounds.Height:0.#} px high"));

    /// <summary>Severity glyphs (circle, triangle, square, diamond) that a screen reader would read out as shapes. The word beside them says the same thing.</summary>
    public static IEnumerable<string> ExposedGlyphs(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && t.Text is { Length: 1 } text && _severityGlyphs.Contains(text[0]))
            .Where(t => AutomationProperties.GetAccessibilityView(t) != AccessibilityView.Raw)
            .Select(t => $"glyph '{t.Text}' is in the accessibility tree");

    /// <summary>
    /// Icons (PathIcon) that sit in the accessibility tree outside a named control. An icon carries no meaning of its own,
    /// so it has to be hidden or live inside a button, list item, expander or other control that has a name.
    /// </summary>
    public static IEnumerable<string> LoneIcons(Visual root) =>
        root.GetVisualDescendants().OfType<PathIcon>()
            .Where(i => i.IsEffectivelyVisible && AutomationProperties.GetAccessibilityView(i) == AccessibilityView.Content && i.TemplatedParent is null)
            .Where(i => !i.GetVisualAncestors().OfType<Control>().Any(IsNamedContainer))
            .Select(i => $"icon without a named parent at {i.Bounds}");

    private static bool IsNamedContainer(Control c) =>
        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(c)) || c is Button or ToggleButton or ListBoxItem or Expander;

    /// <summary>
    /// Presses Tab through the window and, at every stop, checks that a focus adorner is on screen for the focused control.
    /// Returns the stops that show nothing.
    /// </summary>
    public static List<string> StopsWithoutFocusRing(MainWindow window, int limit = 200)
    {
        var problems = new List<string>();
        var seen = new List<Control>();
        Dispatcher.UIThread.RunJobs();
        while (seen.Count < limit)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            if (window.FocusManager?.GetFocusedElement() is not Control focused || seen.Contains(focused))
            {
                break;
            }

            seen.Add(focused);
            var shown = AdornerLayer.GetAdornerLayer(focused)?.Children.Any(a => AdornerLayer.GetAdornedElement(a) is { } target && (ReferenceEquals(target, focused) || target.GetVisualAncestors().Contains(focused) || focused.GetVisualAncestors().Contains(target))) ?? false;
            if (!shown)
            {
                problems.Add($"no focus ring on '{AccessibilityProbe.Label(focused)}' ({focused.GetType().Name})");
            }
        }

        return problems;
    }

    /// <summary>All the checks that need no keyboard, for one page.</summary>
    public static List<string> Everything(MainWindow window) =>
        Unnamed(window).Select(s => "unnamed: " + s)
            .Concat(UnnamedEditorAreas(window).Select(s => "unnamed: " + s))
            .Concat(SmallTargets(window).Select(s => "small target: " + s))
            .Concat(ExposedGlyphs(window).Select(s => "glyph: " + s))
            .Concat(LoneIcons(window).Select(s => "icon: " + s))
            .ToList();

    /// <summary>The visible headings, with their levels.</summary>
    public static List<(int Level, string Text)> Headings(Visual root) =>
        root.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && AutomationProperties.GetHeadingLevel(t) > 0)
            .Select(t => (AutomationProperties.GetHeadingLevel(t), t.Text ?? string.Empty))
            .ToList();
}
