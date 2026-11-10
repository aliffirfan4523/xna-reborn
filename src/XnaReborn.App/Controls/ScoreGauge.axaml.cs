using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;

namespace XnaReborn.App.Controls;

/// <summary>Shows a compatibility score from 0 to 100 as a ring with the number inside, and optionally the score after the automatic fixes.</summary>
public sealed partial class ScoreGauge : UserControl
{
    /// <summary>The score to show, from 0 to 100. Values outside the range are clamped.</summary>
    public static readonly StyledProperty<int> ScoreProperty =
        AvaloniaProperty.Register<ScoreGauge, int>(nameof(Score));

    /// <summary>The score after the automatic fixes, from 0 to 100, or null to hide it.</summary>
    public static readonly StyledProperty<int?> ProjectedScoreProperty =
        AvaloniaProperty.Register<ScoreGauge, int?>(nameof(ProjectedScore));

    static ScoreGauge()
    {
        ScoreProperty.Changed.AddClassHandler<ScoreGauge>((gauge, _) => gauge.Refresh());
        ProjectedScoreProperty.Changed.AddClassHandler<ScoreGauge>((gauge, _) => gauge.Refresh());
    }

    /// <summary>Creates the gauge.</summary>
    public ScoreGauge()
    {
        InitializeComponent();
        Refresh();
    }

    /// <summary>The score to show, from 0 to 100. Values outside the range are clamped.</summary>
    public int Score
    {
        get => GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    /// <summary>The score after the automatic fixes, from 0 to 100, or null to hide it.</summary>
    public int? ProjectedScore
    {
        get => GetValue(ProjectedScoreProperty);
        set => SetValue(ProjectedScoreProperty, value);
    }

    private void Refresh()
    {
        var score = Math.Clamp(Score, 0, 100);

        // An arc of a full 360 degrees draws nothing, so stop just short of it.
        ScoreArc.SweepAngle = Math.Min(score * 3.6, 359.9);
        ScoreText.Text = score.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var name = $"Compatibility score {score} out of 100";
        if (ProjectedScore is { } projected)
        {
            projected = Math.Clamp(projected, 0, 100);
            ProjectedText.Text = $"After automatic fixes: {projected}";
            ProjectedBar.Value = projected;
            name += $". After automatic fixes {projected} out of 100";
        }

        ProjectedPanel.IsVisible = ProjectedScore is not null;
        AutomationProperties.SetName(this, name);
    }
}
