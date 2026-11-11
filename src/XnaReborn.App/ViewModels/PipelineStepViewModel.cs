using CommunityToolkit.Mvvm.ComponentModel;
using XnaReborn.App.Controls;

namespace XnaReborn.App.ViewModels;

/// <summary>Where one pipeline step stands.</summary>
public enum PipelineState
{
    /// <summary>Not started yet.</summary>
    Waiting,

    /// <summary>Working now, or waiting for the user's answer.</summary>
    Running,

    /// <summary>Finished well.</summary>
    Done,

    /// <summary>Finished badly.</summary>
    Failed,

    /// <summary>Not part of this run.</summary>
    Skipped,
}

/// <summary>One card of the Build and run pipeline: SDK check, Build or Launch.</summary>
public sealed partial class PipelineStepViewModel : ObservableObject
{
    /// <summary>The state of the step.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(Kind), nameof(AutomationName))]
    private PipelineState _state;

    /// <summary>A short line under the name, such as the SDK version.</summary>
    [ObservableProperty]
    private string _detail = string.Empty;

    /// <summary>Time spent in the step, such as "3.2 s", or empty.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasElapsed), nameof(AutomationName))]
    private string _elapsedText = string.Empty;

    /// <summary>Creates a step.</summary>
    /// <param name="name">The card title.</param>
    public PipelineStepViewModel(string name) => Name = name;

    /// <summary>The card title.</summary>
    public string Name { get; }

    /// <summary>True when <see cref="ElapsedText"/> has a value.</summary>
    public bool HasElapsed => ElapsedText.Length > 0;

    /// <summary>The state in a word, so color is never the only cue.</summary>
    public string StateText => State switch
    {
        PipelineState.Running => "Running",
        PipelineState.Done => "Done",
        PipelineState.Failed => "Failed",
        PipelineState.Skipped => "Skipped",
        _ => "Waiting",
    };

    /// <summary>The chip kind for the state.</summary>
    public StatusKind Kind => State switch
    {
        PipelineState.Running => StatusKind.Accent,
        PipelineState.Done => StatusKind.Success,
        PipelineState.Failed => StatusKind.Error,
        _ => StatusKind.Neutral,
    };

    /// <summary>The name a screen reader reads for the card.</summary>
    public string AutomationName => $"{Name}: {StateText}" + (HasElapsed ? $", {ElapsedText}" : string.Empty);

    /// <summary>Sets state, detail and elapsed text in one call.</summary>
    /// <param name="state">The new state.</param>
    /// <param name="detail">The new detail line.</param>
    /// <param name="elapsed">The elapsed text, or empty.</param>
    public void Set(PipelineState state, string detail, string elapsed = "")
    {
        State = state;
        Detail = detail;
        ElapsedText = elapsed;
    }
}
