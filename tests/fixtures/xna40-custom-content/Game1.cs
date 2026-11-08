using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CustomContent
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D hero;
        private Texture2D ship;
        private SpriteFont hud;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            hero = Content.Load<Texture2D>("hero");
            ship = Content.Load<Texture2D>("Textures/ship");
            hud = Content.Load<SpriteFont>("Fonts/Hud");
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            spriteBatch.Begin();
            spriteBatch.Draw(hero, Vector2.Zero, Color.White);
            spriteBatch.Draw(ship, new Vector2(100, 100), Color.White);
            spriteBatch.DrawString(hud, "Score", new Vector2(10, 10), Color.Yellow);
            spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}
