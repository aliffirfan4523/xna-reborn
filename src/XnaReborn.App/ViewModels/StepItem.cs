using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace XnaReborn.App.ViewModels;

/// <summary>One button of the stepper at the top of the window.</summary>
public sealed class StepItem : ObservableObject
{
    private bool _isCurrent;
    private bool _isEnabled;
    private bool _isDone;

    /// <summary>Creates a step button.</summary>
    /// <param name="step">The screen the button opens.</param>
    /// <param name="label">Text of the button.</param>
    /// <param name="number">Position in the flow, starting at 1.</param>
    /// <param name="goTo">Called when the button is pressed.</param>
    public StepItem(WorkflowStep step, string label, int number, Action<WorkflowStep> goTo)
    {
        Step = step;
        Label = label;
        Number = number;
        GoCommand = new RelayCommand(() => goTo(step), () => IsEnabled);
    }

    /// <summary>The screen the button opens.</summary>
    public WorkflowStep Step { get; }

    /// <summary>Text of the button.</summary>
    public string Label { get; }

    /// <summary>Position in the flow, starting at 1.</summary>
    public int Number { get; }

    /// <summary>Opens the screen. Can run only while <see cref="IsEnabled"/> is true.</summary>
    public IRelayCommand GoCommand { get; }

    /// <summary>True for the screen that is shown now.</summary>
    public bool IsCurrent => _isCurrent;

    /// <summary>True when the screen can be opened now.</summary>
    public bool IsEnabled => _isEnabled;

    /// <summary>True for a screen before the current one.</summary>
    public bool IsDone => _isDone;

    /// <summary>Stable id for UI tests, such as "Step_Report".</summary>
    public string AutomationId => "Step_" + Step;

    /// <summary>Screen reader text: position, label and state. State is words, not color.</summary>
    public string AutomationName =>
        $"Step {Number}: {Label}" + (_isCurrent ? ", current" : _isDone ? ", done" : _isEnabled ? string.Empty : ", not available yet");

    /// <summary>A check mark for done steps, otherwise the step number.</summary>
    public string Marker => _isDone ? "✓" : Number.ToString(System.Globalization.CultureInfo.InvariantCulture);

    internal void Update(bool isCurrent, bool isEnabled, bool isDone)
    {
        _isCurrent = isCurrent;
        _isEnabled = isEnabled;
        _isDone = isDone;
        OnPropertyChanged(string.Empty);
        GoCommand.NotifyCanExecuteChanged();
    }
}
