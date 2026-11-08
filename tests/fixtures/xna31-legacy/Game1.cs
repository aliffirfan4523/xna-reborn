using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Xna31Legacy
{
    /// <summary>
    /// XNA 3.1 style game that uses several APIs removed or changed in 4.0.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D sprite;
        private ResolveTexture2D snapshot;
        private VertexDeclaration declaration;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            sprite = Texture2D.FromFile(GraphicsDevice, "sprite.png");
            snapshot = new ResolveTexture2D(
                GraphicsDevice,
                GraphicsDevice.PresentationParameters.BackBufferWidth,
                GraphicsDevice.PresentationParameters.BackBufferHeight,
                1,
                SurfaceFormat.Color);
            declaration = new VertexDeclaration(
                GraphicsDevice,
                VertexPositionColor.VertexElements);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.VertexDeclaration = declaration;
            GraphicsDevice.RenderState.AlphaBlendEnable = true;

            spriteBatch.Begin(SpriteBlendMode.AlphaBlend);
            spriteBatch.Draw(sprite, new Vector2(64f, 64f), Color.White);
            spriteBatch.End();

            spriteBatch.Begin(
                SpriteBlendMode.Additive,
                SpriteSortMode.Deferred,
                SaveStateMode.SaveState);
            spriteBatch.Draw(sprite, new Vector2(128f, 64f), Color.White);
            spriteBatch.End();

            GraphicsDevice.ResolveBackBuffer(snapshot);

            base.Draw(gameTime);
        }
    }
}