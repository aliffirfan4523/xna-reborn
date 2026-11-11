using SharpDX;
using SharpDX.Toolkit;

namespace ToolkitGame
{
    /// <summary>
    /// The Toolkit half of a mixed solution.
    /// </summary>
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private Matrix view;

        public Game1()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
        }

        protected override void Update(GameTime gameTime)
        {
            view = Matrix.LookAtRH(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY);
            base.Update(gameTime);
        }
    }
}