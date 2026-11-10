using Avalonia;
using Avalonia.Automation;
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

/// <summary>Helpers for the keyboard and accessible-name tests: walks the visual tree and the real Tab order of a headless window.</summary>
internal static class AccessibilityProbe
{
    /// <summary>The controls a user can operate. Template parts (a ComboBox's drop-down button, scroll bar arrows) are left out.</summary>
    public static List<Control> InteractiveControls(Visual root) =>
        root.GetVisualDescendants()
            .OfType<Control>()
            .Where(c => c is Button or ToggleButton or TextBox or ComboBox or ListBox or Expander or GridSplitter or TextEditor)
            .Where(c => c is not RepeatButton && c.TemplatedParent is null && !c.GetVisualAncestors().OfType<ScrollBar>().Any())
            .Where(c => c is not TextBox || c.GetVisualAncestors().OfType<TextEditor>().Any() is false)
            .ToList();

    /// <summary>Describes a control for failure messages.</summary>
    public static string Describe(Control c) =>
        $"{c.GetType().Name} id='{AutomationProperties.GetAutomationId(c)}' name='{AutomationProperties.GetName(c)}'";

    /// <summary>
    /// The controls that Tab visits, in order, starting from the top of the window. Uses Avalonia's own navigation, so it
    /// is what the keyboard does. Stops when the order wraps or after <paramref name="limit"/> stops.
    /// </summary>
    public static List<Control> TabOrder(MainWindow window, int limit = 200)
    {
        Dispatcher.UIThread.RunJobs();
        var order = new List<Control>();
        while (order.Count < limit)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            if (window.FocusManager?.GetFocusedElement() is not Control control || order.Contains(control))
            {
                break;
            }

            order.Add(control);
        }

        return order;
    }

    /// <summary>
    /// A short label for a tab stop: the automation id, else the name, else the type. A part inside a control template
    /// (the header button of an Expander, the text area of an editor) is labelled by the control that owns the template.
    /// </summary>
    public static string Label(Control c)
    {
        while (c.TemplatedParent is Control owner)
        {
            c = owner;
        }

        if (c is TextArea && c.FindAncestorOfType<TextEditor>() is { } editor)
        {
            c = editor;
        }

        return AutomationProperties.GetAutomationId(c) is { Length: > 0 } id ? id : AutomationProperties.GetName(c) ?? c.GetType().Name;
    }
}
