using Avalonia.Controls;
using Avalonia.LogicalTree;
using XnaReborn.App.Controls;

namespace XnaReborn.App.Views;

/// <summary>The view of <see cref="ViewModels.SettingsViewModel"/>. It holds no logic.</summary>
public sealed partial class SettingsView : UserControl
{
    // Below this width a label and its drop-down no longer fit side by side, so the drop-down goes under the label.
    private const double _narrowRow = 520;

    /// <summary>Creates the view.</summary>
    public SettingsView()
    {
        InitializeComponent();
        foreach (var row in this.GetLogicalDescendants().OfType<Grid>().Where(g => g.Children is [StackPanel, ComboBox]))
        {
            NarrowLayout.Attach(row, _narrowRow, "*,Auto");
        }
    }
}
