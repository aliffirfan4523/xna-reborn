using Avalonia.Controls;

namespace XnaReborn.App.Views;

/// <summary>The only window: stepper, error banner and the current page.</summary>
public sealed partial class MainWindow : Window
{
    /// <summary>Creates the window. Set <c>DataContext</c> to a <c>MainViewModel</c>.</summary>
    public MainWindow()
    {
        InitializeComponent();
    }
}
