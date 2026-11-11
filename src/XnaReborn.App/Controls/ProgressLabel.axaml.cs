using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace XnaReborn.App.Controls;

/// <summary>A progress bar with a text label and a percent. Shows a moving bar and no percent while <see cref="IsIndeterminate"/> is true.</summary>
public sealed partial class ProgressLabel : UserControl
{
    /// <summary>What is in progress, such as "Analyzing".</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<ProgressLabel, string?>(nameof(Label));

    /// <summary>The progress from 0 to 100. Values outside the range are clamped.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProgressLabel, double>(nameof(Value));

    /// <summary>True when the amount of work is not known.</summary>
    public static readonly StyledProperty<bool> IsIndeterminateProperty =
        AvaloniaProperty.Register<ProgressLabel, bool>(nameof(IsIndeterminate));

    static ProgressLabel()
    {
        LabelProperty.Changed.AddClassHandler<ProgressLabel>((p, _) => p.Refresh());
        ValueProperty.Changed.AddClassHandler<ProgressLabel>((p, _) => p.Refresh());
        IsIndeterminateProperty.Changed.AddClassHandler<ProgressLabel>((p, _) => p.Refresh());
    }

    /// <summary>Creates the control.</summary>
    public ProgressLabel()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>What is in progress, such as "Analyzing".</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>The progress from 0 to 100. Values outside the range are clamped.</summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>True when the amount of work is not known.</summary>
    public bool IsIndeterminate
    {
        get => GetValue(IsIndeterminateProperty);
        set => SetValue(IsIndeterminateProperty, value);
    }

    private void Refresh()
    {
        var value = Math.Clamp(double.IsNaN(Value) ? 0 : Value, 0, 100);
        LabelView.Text = Label;
        Bar.IsIndeterminate = IsIndeterminate;
        Bar.Value = value;
        PercentView.IsVisible = !IsIndeterminate;
        PercentView.Text = value.ToString("0", CultureInfo.InvariantCulture) + "%";
        AutomationProperties.SetName(this, IsIndeterminate ? $"{Label}, working" : $"{Label}, {PercentView.Text}");
    }
}
