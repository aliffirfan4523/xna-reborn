using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace XnaReborn.App.Controls;

/// <summary>A card that shows one value (a number or a short word) under an icon and above a label.</summary>
public sealed partial class StatTile : UserControl
{
    /// <summary>The big value, such as "12".</summary>
    public static readonly StyledProperty<string?> ValueProperty =
        AvaloniaProperty.Register<StatTile, string?>(nameof(Value));

    /// <summary>The label under the value, such as "Blockers".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<StatTile, string?>(nameof(Label));

    /// <summary>The meaning of the tile. Tints the icon.</summary>
    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<StatTile, StatusKind>(nameof(Kind));

    /// <summary>An icon from Styles/Icons.axaml. When null the icon of <see cref="Kind"/> is used.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<StatTile, Geometry?>(nameof(Icon));

    static StatTile()
    {
        ValueProperty.Changed.AddClassHandler<StatTile>((t, _) => t.Refresh());
        LabelProperty.Changed.AddClassHandler<StatTile>((t, _) => t.Refresh());
        KindProperty.Changed.AddClassHandler<StatTile>((t, _) => t.Refresh());
        IconProperty.Changed.AddClassHandler<StatTile>((t, _) => t.Refresh());
    }

    /// <summary>Creates the tile.</summary>
    public StatTile()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>The big value, such as "12".</summary>
    public string? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>The label under the value, such as "Blockers".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The meaning of the tile. Tints the icon.</summary>
    public StatusKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>An icon from Styles/Icons.axaml. When null the icon of <see cref="Kind"/> is used.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    private void Refresh()
    {
        StatusKinds.Apply(IconHost, Kind);
        IconView.Data = Icon ?? StatusKinds.DefaultIcon(Kind);
        ValueView.Text = Value;
        LabelView.Text = Label;
        AutomationProperties.SetName(this, $"{Label}: {Value}");
    }
}
