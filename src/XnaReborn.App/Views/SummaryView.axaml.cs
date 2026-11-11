using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using XnaReborn.App.Controls;

namespace XnaReborn.App.Views;

/// <summary>The Summary page view. The only code here is the short score reveal.</summary>
public sealed partial class SummaryView : UserControl
{
    private CancellationTokenSource? _reveal;

    /// <summary>Creates the view.</summary>
    public SummaryView()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _ = RevealAsync();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _reveal?.Cancel();
        base.OnDetachedFromVisualTree(e);
    }

    // The hero card fades in and the "after" number settles with a small scale. Skipped when the user asked for less motion.
    private async Task RevealAsync()
    {
        if (MotionPreference.Reduce() || this.FindControl<Border>("Hero") is not { } hero)
        {
            return;
        }

        _reveal?.Cancel();
        var cts = _reveal = new CancellationTokenSource();
        var animation = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(420),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0.0), new Setter(RenderTransformProperty, TransformOperations.Parse("scale(0.97)")) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1.0), new Setter(RenderTransformProperty, TransformOperations.Parse("scale(1)")) } },
            },
        };
        try
        {
            await animation.RunAsync(hero, cts.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The page was left; nothing to finish.
        }
    }
}
