using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Mg37WindowsDx
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

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            base.Draw(gameTime);
        }
    }
}
