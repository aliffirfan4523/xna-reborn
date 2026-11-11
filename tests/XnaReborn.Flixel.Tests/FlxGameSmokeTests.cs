using org.flixel;
using XnaReborn.As3;

namespace XnaReborn.Flixel.Tests;

/// <summary>
/// A <see cref="FactAttribute"/> for tests that open a real window. They are skipped unless <c>XNAREBORN_GRAPHICS</c> is <c>1</c>,
/// so a machine with no graphics device (or no one watching) never opens one.
/// </summary>
public sealed class GraphicsFactAttribute : FactAttribute
{
    public GraphicsFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("XNAREBORN_GRAPHICS") != "1")
        {
            Skip = "Opens a window: set XNAREBORN_GRAPHICS=1 to run it on a machine with a graphics device.";
        }
    }
}

public sealed class FlxGameSmokeTests
{
    private sealed class Smoke : FlxState
    {
        public static int Created;
        public static int Drawn;

        public override void create()
        {
            Created++;
            add(new FlxSprite(4, 4).makeGraphic(8, 8, 0xffff0000));
        }

        public override void draw()
        {
            Drawn++;
            base.draw();
        }
    }

    [GraphicsFact]
    public void The_game_opens_a_window_of_game_size_times_zoom_and_runs_a_frame()
    {
        Smoke.Created = 0;
        Smoke.Drawn = 0;
        using var game = new FlxGame(160, 120, AsClass.Of<Smoke>(), 3, 60, 30, false, "Smoke");
        game.RunOneFrame();
        Assert.Equal("Smoke", game.Window.Title);
        Assert.Equal((480, 360), (game.GraphicsDevice.PresentationParameters.BackBufferWidth, game.GraphicsDevice.PresentationParameters.BackBufferHeight));
        Assert.Equal((160, 120), (FlxG.width, FlxG.height));
        Assert.Equal(1, Smoke.Created);
        Assert.True(Smoke.Drawn >= 1);
    }
}
