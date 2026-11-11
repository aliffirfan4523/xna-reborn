using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Xna20Game
{
    /// <summary>XNA Game Studio 2.0 style game that uses APIs removed in 4.0.</summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D hero;
        private BasicEffect effect;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            hero = Content.Load<Texture2D>("Textures\\hero");
            effect = new BasicEffect(GraphicsDevice, null);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.RenderState.AlphaBlendEnable = true;

            effect.Begin();
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Begin();
                pass.End();
            }

            effect.End();

            spriteBatch.Begin(SpriteBlendMode.AlphaBlend);
            spriteBatch.Draw(hero, new Vector2(32f, 32f), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
