using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace SdxEffects
{
    /// <summary>
    /// Loads a Toolkit effect and draws a full-screen sprite with it.
    /// </summary>
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private SpriteBatch spriteBatch;
        private Effect tint;
        private Texture2D pixel;

        public Game1()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            tint = Content.Load<Effect>("Tint");
            pixel = Texture2D.New(GraphicsDevice, 1, 1, PixelFormat.R8G8B8A8.UNorm);
            base.LoadContent();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            tint.Parameters["Texture"].SetResource(pixel);
            spriteBatch.Begin(SpriteSortMode.Deferred, tint);
            spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}