using Microsoft.Xna.Framework;

namespace Mg37DesktopGl
{
    /// <summary>Clears the screen. Never built.</summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager graphics;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }
    }
}
