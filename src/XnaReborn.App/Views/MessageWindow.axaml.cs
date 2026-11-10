using Avalonia.Controls;
using Avalonia.Interactivity;

namespace XnaReborn.App.Views;

/// <summary>A small modal window for confirmations and errors. Closes with true for the confirm button, otherwise false.</summary>
public sealed partial class MessageWindow : Window
{
    /// <summary>Used by the XAML loader. Do not call.</summary>
    public MessageWindow()
        : this(string.Empty, string.Empty, null, "OK", null)
    {
    }

    /// <summary>Creates the window.</summary>
    /// <param name="title">Window title.</param>
    /// <param name="message">The text shown.</param>
    /// <param name="details">Expandable technical details, or null for none.</param>
    /// <param name="confirmText">Text of the default button.</param>
    /// <param name="cancelText">Text of the cancel button, or null to show only the confirm button.</param>
    public MessageWindow(string title, string message, string? details, string confirmText, string? cancelText)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        DetailsExpander.IsVisible = !string.IsNullOrWhiteSpace(details);
        DetailsText.Text = details;
        ConfirmButton.Content = confirmText;
        CancelButton.IsVisible = cancelText is not null;
        CancelButton.Content = cancelText;
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
