using Avalonia.Controls;

namespace XnaReborn.App.Controls;

/// <summary>
/// Turns a row of columns into a stack when the grid is narrow, so panels keep a readable width in a small window or at a
/// large text size. The grid's children are the panels, in order. This is layout only; nothing else changes.
/// </summary>
internal static class NarrowLayout
{
    /// <summary>
    /// Keeps <paramref name="grid"/> in <paramref name="wideColumns"/> (for example <c>"3*,2*"</c>) while it is at least
    /// <paramref name="narrowBelow"/> wide, and in one column with one row per child when it is narrower.
    /// </summary>
    /// <param name="grid">The grid whose children are the panels.</param>
    /// <param name="narrowBelow">The width in pixels under which the panels stack.</param>
    /// <param name="wideColumns">The column definitions to use when wide.</param>
    public static void Attach(Grid grid, double narrowBelow, string wideColumns)
    {
        var stacked = false;
        grid.SizeChanged += (_, e) =>
        {
            var narrow = e.NewSize.Width < narrowBelow;
            if (narrow == stacked)
            {
                return;
            }

            stacked = narrow;
            grid.ColumnDefinitions = new ColumnDefinitions(stacked ? "*" : wideColumns);
            grid.RowDefinitions = stacked ? new RowDefinitions(string.Join(',', grid.Children.Select(_ => "Auto"))) : new RowDefinitions();
            for (var i = 0; i < grid.Children.Count; i++)
            {
                Grid.SetColumn(grid.Children[i], stacked ? 0 : i);
                Grid.SetRow(grid.Children[i], stacked ? i : 0);
            }
        };
    }
}
