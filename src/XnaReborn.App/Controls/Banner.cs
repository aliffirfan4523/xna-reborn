using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;

namespace XnaReborn.App.Controls;

/// <summary>
/// An info, success, warning or error message with an icon, a title and a text. Its <c>Content</c> is shown under the
/// message and is meant for action buttons. The look is in the control theme in Styles/Controls.axaml.
/// </summary>
public sealed class Banner : ContentControl
{
    /// <summary>The bold first line.</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<Banner, string?>(nameof(Title));

    /// <summary>The text under the title.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<Banner, string?>(nameof(Message));

    /// <summary>The meaning of the banner. Sets the tint and the default icon.</summary>
    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<Banner, StatusKind>(nameof(Kind), StatusKind.Info);

    /// <summary>An icon from Styles/Icons.axaml. When null the icon of <see cref="Kind"/> is used.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<Banner, Geometry?>(nameof(Icon));

    /// <summary>The icon that is drawn: <see cref="Icon"/> or the default of <see cref="Kind"/>.</summary>
    public static readonly StyledProperty<Geometry?> IconDataProperty =
        AvaloniaProperty.Register<Banner, Geometry?>(nameof(IconData));

    /// <summary>True when <see cref="Title"/> has text. Used by the template.</summary>
    public static readonly StyledProperty<bool> HasTitleProperty =
        AvaloniaProperty.Register<Banner, bool>(nameof(HasTitle));

    /// <summary>True when <see cref="Message"/> has text. Used by the template.</summary>
    public static readonly StyledProperty<bool> HasMessageProperty =
        AvaloniaProperty.Register<Banner, bool>(nameof(HasMessage));

    static Banner()
    {
        TitleProperty.Changed.AddClassHandler<Banner>((b, _) => b.Refresh());
        MessageProperty.Changed.AddClassHandler<Banner>((b, _) => b.Refresh());
        KindProperty.Changed.AddClassHandler<Banner>((b, _) => b.Refresh());
        IconProperty.Changed.AddClassHandler<Banner>((b, _) => b.Refresh());
        FocusableProperty.OverrideDefaultValue<Banner>(false);
    }

    /// <summary>Creates the banner.</summary>
    public Banner() => Refresh();

    /// <summary>The bold first line.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The text under the title.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>The meaning of the banner. Sets the tint and the default icon.</summary>
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

    /// <summary>The icon that is drawn: <see cref="Icon"/> or the default of <see cref="Kind"/>. Used by the template.</summary>
    public Geometry? IconData
    {
        get => GetValue(IconDataProperty);
        private set => SetValue(IconDataProperty, value);
    }

    /// <summary>True when <see cref="Title"/> has text. Used by the template.</summary>
    public bool HasTitle
    {
        get => GetValue(HasTitleProperty);
        private set => SetValue(HasTitleProperty, value);
    }

    /// <summary>True when <see cref="Message"/> has text. Used by the template.</summary>
    public bool HasMessage
    {
        get => GetValue(HasMessageProperty);
        private set => SetValue(HasMessageProperty, value);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Refresh();
    }

    private void Refresh()
    {
        StatusKinds.Apply(this, Kind);
        HasTitle = !string.IsNullOrEmpty(Title);
        HasMessage = !string.IsNullOrEmpty(Message);
        IconData = Icon ?? StatusKinds.DefaultIcon(Kind);
        var word = StatusKinds.WordOf(Kind);
        AutomationProperties.SetName(this, string.Join(". ", new[] { word, Title, Message }.Where(s => !string.IsNullOrWhiteSpace(s))));

        // A failure interrupts the screen reader; the other kinds wait for it to finish speaking.
        AutomationProperties.SetLiveSetting(this, Kind == StatusKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
    }
}
