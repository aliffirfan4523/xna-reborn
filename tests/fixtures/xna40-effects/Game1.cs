using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Xna40Effects
{
    /// <summary>
    /// Loads the sample effects. It never draws with them; the fixture exists for the effect rewriters.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private Dictionary<string, Effect> effects = new Dictionary<string, Effect>();

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            effects["Sm2"] = Content.Load<Effect>("Sm2");
            effects["Sm3"] = Content.Load<Effect>("Sm3");
            effects["Template"] = Content.Load<Effect>("Template");
            effects["Portable"] = Content.Load<Effect>("Portable");
            effects["MipBias"] = Content.Load<Effect>("MipBias");
            effects["Gaussian"] = Content.Load<Effect>("Gaussian");
            effects["AlphaTest"] = Content.Load<Effect>("AlphaTest");
            effects["Annotated"] = Content.Load<Effect>("Annotated");
            effects["Vpos"] = Content.Load<Effect>("Vpos");
            effects["Defaults"] = Content.Load<Effect>("Defaults");
            effects["WithInclude"] = Content.Load<Effect>("WithInclude");
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }
    }
}