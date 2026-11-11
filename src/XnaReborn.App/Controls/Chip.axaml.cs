using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace XnaReborn.App.Controls;

/// <summary>A small pill with an icon and a text, tinted by <see cref="Kind"/>. Use it for status, counts and tags.</summary>
public sealed partial class Chip : UserControl
{
    /// <summary>The text of the chip.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Chip, string?>(nameof(Text));

    /// <summary>The meaning of the chip. Sets the tint and the default icon.</summary>
    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<Chip, StatusKind>(nameof(Kind));

    /// <summary>An icon from Styles/Icons.axaml. When null the icon of <see cref="Kind"/> is used.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<Chip, Geometry?>(nameof(Icon));

    static Chip()
    {
        TextProperty.Changed.AddClassHandler<Chip>((c, _) => c.Refresh());
        KindProperty.Changed.AddClassHandler<Chip>((c, _) => c.Refresh());
        IconProperty.Changed.AddClassHandler<Chip>((c, _) => c.Refresh());
    }

    /// <summary>Creates the chip.</summary>
    public Chip()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>The text of the chip.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>The meaning of the chip. Sets the tint and the default icon.</summary>
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
        StatusKinds.Apply(Root, Kind);
        IconView.Data = Icon ?? StatusKinds.DefaultIcon(Kind);
        TextView.Text = Text;
        var word = StatusKinds.WordOf(Kind);
        AutomationProperties.SetName(this, word.Length == 0 ? Text ?? string.Empty : $"{word}: {Text}");
    }
}
