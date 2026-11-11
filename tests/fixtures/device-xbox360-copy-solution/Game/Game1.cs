using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CopyGame
{
    /// <summary>A game that is shared by a Windows project and an Xbox 360 copy of it.</summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager graphics;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 1280;
            graphics.PreferredBackBufferHeight = 720;
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }
    }
}
