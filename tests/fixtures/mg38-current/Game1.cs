using Microsoft.Xna.Framework;

namespace Mg38Current
{
    /// <summary>Clears the screen with a transparent color. Never built.</summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager graphics;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Transparent);
            base.Draw(gameTime);
        }
    }
}
