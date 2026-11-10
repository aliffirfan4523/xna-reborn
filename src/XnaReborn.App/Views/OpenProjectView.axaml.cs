using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App.Views;

/// <summary>The "Open a project" page. Only forwards a dropped path to <see cref="OpenProjectViewModel"/>.</summary>
public sealed partial class OpenProjectView : UserControl
{
    /// <summary>Creates the view.</summary>
    public OpenProjectView()
    {
        InitializeComponent();
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        var path = e.DataTransfer.TryGetFile()?.TryGetLocalPath();
        if (path is not null && DataContext is OpenProjectViewModel vm)
        {
            await vm.OpenPathAsync(path);
        }
    }
}
