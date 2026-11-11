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
        AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFile = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
        SetDragOver(hasFile);
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => SetDragOver(false);

    // Only the visual drag state lives here; opening stays in the view model.
    private void SetDragOver(bool value)
    {
        if (DataContext is OpenProjectViewModel vm)
        {
            vm.IsDragOver = value;
        }
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;
        SetDragOver(false);
        var path = e.DataTransfer.TryGetFile()?.TryGetLocalPath();
        if (path is not null && DataContext is OpenProjectViewModel vm)
        {
            await vm.OpenPathAsync(path);
        }
    }
}
