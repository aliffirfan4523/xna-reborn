using Microsoft.Xna.Framework;

namespace Mg37Multi
{
    /// <summary>Game code shared by every platform project. Never built.</summary>
    public class SharedGame : Game
    {
        private readonly GraphicsDeviceManager graphics;

        public SharedGame()
        {
            graphics = new GraphicsDeviceManager(this);
        }
    }
}
