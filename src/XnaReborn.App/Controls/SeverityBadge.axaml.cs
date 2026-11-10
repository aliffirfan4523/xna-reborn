using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using XnaReborn.Core.Models;

namespace XnaReborn.App.Controls;

/// <summary>Shows a <see cref="Core.Models.Severity"/> as an icon, a text label and a color, so color is never the only cue.</summary>
public sealed partial class SeverityBadge : UserControl
{
    /// <summary>The severity to show.</summary>
    public static readonly StyledProperty<Severity> SeverityProperty =
        AvaloniaProperty.Register<SeverityBadge, Severity>(nameof(Severity));

    static SeverityBadge()
    {
        SeverityProperty.Changed.AddClassHandler<SeverityBadge>((badge, _) => badge.Refresh());
    }

    /// <summary>Creates the badge.</summary>
    public SeverityBadge()
    {
        InitializeComponent();
        ActualThemeVariantChanged += (_, _) => Refresh();
        Refresh();
    }

    /// <summary>The severity to show.</summary>
    public Severity Severity
    {
        get => GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Resources are looked up through the visual parents, so they are only found once the badge is in a window.
        Refresh();
    }

    private void Refresh()
    {
        var name = Severity.ToString();
        var label = Find($"Severity{name}Label") as string ?? name;
        IconText.Text = Find($"Severity{name}Icon") as string ?? string.Empty;
        LabelText.Text = label;
        Foreground = Find($"Severity{name}Brush") as IBrush;
        AutomationProperties.SetName(this, "Severity: " + label);
    }

    private object? Find(string key)
    {
        if (this.TryFindResource(key, ActualThemeVariant, out var value))
        {
            return value;
        }

        return Application.Current is { } app && app.TryGetResource(key, ActualThemeVariant, out value) ? value : null;
    }
}
