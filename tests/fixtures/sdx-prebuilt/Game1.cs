using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace SdxPrebuilt
{
    /// <summary>
    /// Loads a texture that only exists as a compiled .tkb file.
    /// </summary>
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private Texture2D balls;

        public Game1()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            balls = Content.Load<Texture2D>("Balls");
            base.LoadContent();
        }
    }
}