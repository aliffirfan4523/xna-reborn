using Avalonia.Controls;
using Avalonia.Controls.Templates;
using XnaReborn.App.ViewModels;

namespace XnaReborn.App;

/// <summary>Maps a view model to its view by name: <c>XnaReborn.App.ViewModels.XxxViewModel</c> becomes <c>XnaReborn.App.Views.XxxView</c>.</summary>
public sealed class ViewLocator : IDataTemplate
{
    /// <summary>Returns the type of the view for a view model type, or null when there is none.</summary>
    /// <param name="viewModelType">The view model type.</param>
    public static Type? ViewTypeFor(Type viewModelType)
    {
        ArgumentNullException.ThrowIfNull(viewModelType);
        var name = viewModelType.FullName?.Replace("ViewModels", "Views", StringComparison.Ordinal);
        name = name?.Replace("ViewModel", "View", StringComparison.Ordinal);
        return name is null ? null : typeof(ViewLocator).Assembly.GetType(name);
    }

    /// <inheritdoc />
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        var type = ViewTypeFor(param.GetType());
        return type is not null && Activator.CreateInstance(type) is Control control
            ? control
            : new TextBlock { Text = "View not found for " + param.GetType().Name };
    }

    /// <inheritdoc />
    public bool Match(object? data) => data is ViewModelBase;
}
