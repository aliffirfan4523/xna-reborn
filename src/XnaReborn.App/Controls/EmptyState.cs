using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace XnaReborn.App.Controls;

/// <summary>
/// What a list or page shows when there is nothing to show yet: a big icon, a title, a text and (as <c>Content</c>)
/// an optional action button. The look is in the control theme in Styles/Controls.axaml.
/// </summary>
public sealed class EmptyState : ContentControl
{
    /// <summary>The heading, such as "No recent projects".</summary>
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Title));

    /// <summary>The text under the heading that says what to do next.</summary>
    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<EmptyState, string?>(nameof(Message));

    /// <summary>An icon from Styles/Icons.axaml. When null the theme's folder icon is used.</summary>
    public static readonly StyledProperty<Geometry?> IconProperty =
        AvaloniaProperty.Register<EmptyState, Geometry?>(nameof(Icon));

    static EmptyState()
    {
        FocusableProperty.OverrideDefaultValue<EmptyState>(false);
    }

    /// <summary>The heading, such as "No recent projects".</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>The text under the heading that says what to do next.</summary>
    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>An icon from Styles/Icons.axaml. When null a folder icon is used.</summary>
    public Geometry? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
}
