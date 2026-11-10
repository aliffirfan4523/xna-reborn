using CommunityToolkit.Mvvm.ComponentModel;
using XnaReborn.Migrator.Planning;

namespace XnaReborn.App.ViewModels;

/// <summary>One line of the plan checklist on the migration setup page.</summary>
public sealed class PlanItemViewModel : ObservableObject
{
    private readonly Action<PlanItemViewModel, bool> _changed;
    private bool _isIncluded;

    /// <summary>Creates a checklist line.</summary>
    /// <param name="item">The plan item to show.</param>
    /// <param name="canExclude">True when the user may switch the item off.</param>
    /// <param name="isIncluded">True when the item is currently switched on.</param>
    /// <param name="changed">Called with this line and the new "included" value when the user switches an excludable item.</param>
    public PlanItemViewModel(PlanItem item, bool canExclude, bool isIncluded, Action<PlanItemViewModel, bool> changed)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(changed);
        Item = item;
        CanExclude = canExclude;
        _isIncluded = isIncluded || !canExclude;
        _changed = changed;
    }

    /// <summary>The plan item.</summary>
    public PlanItem Item { get; }

    /// <summary>The plan item id, used for <see cref="WorkflowSession.SetExcluded"/>.</summary>
    public string Id => Item.Id;

    /// <summary>What the step does, in plain words.</summary>
    public string Description => Item.Description;

    /// <summary>True when the user may switch this item off.</summary>
    public bool CanExclude { get; }

    /// <summary>A short reason shown under the description.</summary>
    public string Reason => CanExclude
        ? "Optional. Switch it off to skip this step."
        : "Always applied. The migrated project needs it to build on MonoGame.";

    /// <summary>The files the step changes, as one short line.</summary>
    public string FilesText
    {
        get
        {
            var files = Item.AffectedFiles;
            if (files.Count == 0)
            {
                return string.Empty;
            }

            var shown = string.Join(", ", files.Take(3));
            return files.Count > 3 ? $"Files: {shown} and {files.Count - 3} more" : $"Files: {shown}";
        }
    }

    /// <summary>True when the step will run. An item that cannot be excluded stays true whatever is assigned.</summary>
    public bool IsIncluded
    {
        get => _isIncluded;
        set
        {
            if (!CanExclude)
            {
                // Tell the check box to go back to checked.
                OnPropertyChanged();
                return;
            }

            if (SetProperty(ref _isIncluded, value))
            {
                _changed(this, value);
            }
        }
    }
}
