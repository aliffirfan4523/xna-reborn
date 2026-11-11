using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace WindowsPlain
{
    /// <summary>A Windows game with look-alike names: its own GamePad class and its own Accelerometer type.</summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private readonly Accelerometer tilt = new Accelerometer();

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
#if WINDOWS
            graphics.PreferredBackBufferWidth = 1024;
            graphics.PreferredBackBufferHeight = 768;
#endif
        }

        protected override void Update(GameTime gameTime)
        {
            if (Input.GamePad.GetState(0))
            {
                tilt.X += 1f;
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }
    }
}
