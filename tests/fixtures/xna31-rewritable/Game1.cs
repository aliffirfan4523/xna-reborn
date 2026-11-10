using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Xna31Rewritable
{
    /// <summary>
    /// XNA 3.1 style game whose only 3.1-specific code is covered by the
    /// automatic rewriters, so the migrated project builds without edits.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D sprite;
        private VertexPositionColor[] vertices;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            sprite = new Texture2D(GraphicsDevice, 1, 1);
            sprite.SetData(new[] { Color.White });

            vertices = new VertexPositionColor[]
            {
                new VertexPositionColor(new Vector3(0f, 1f, 0f), Color.Red),
                new VertexPositionColor(new Vector3(1f, -1f, 0f), Color.Green),
                new VertexPositionColor(new Vector3(-1f, -1f, 0f), Color.Blue),
            };
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.RenderState.AlphaBlendEnable = true;
            GraphicsDevice.RenderState.CullMode = CullMode.None;

            spriteBatch.Begin(SpriteBlendMode.AlphaBlend);
            spriteBatch.Draw(sprite, new Rectangle(32, 32, 64, 64), Color.White);
            spriteBatch.End();

            spriteBatch.Begin(SpriteBlendMode.Additive, SpriteSortMode.Deferred, SaveStateMode.None);
            spriteBatch.Draw(sprite, new Rectangle(96, 32, 64, 64), Color.Yellow);
            spriteBatch.End();

            base.Draw(gameTime);
        }

        /// <summary>
        /// Draws one triangle with the given effect. Never called by the demo.
        /// </summary>
        void DrawWithEffect(Effect effect)
        {
            effect.Begin();
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Begin();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, vertices, 0, 1);
                pass.End();
            }
            effect.End();
        }
    }
}
