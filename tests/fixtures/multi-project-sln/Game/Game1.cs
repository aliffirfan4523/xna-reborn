using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MultiProject.Library;

namespace MultiProject.Game
{
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D icon;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            icon = Content.Load<Texture2D>("Textures/icon");
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            Vector2 position = Helpers.Clamp(new Vector2(500, 300), 200f);
            spriteBatch.Begin();
            spriteBatch.Draw(icon, position, Color.White);
            spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
