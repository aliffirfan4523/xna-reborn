using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Xna40Basic
{
    /// <summary>
    /// Draws one texture and moves it with the arrow keys.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D hero;
        private Vector2 position;
        private const float Speed = 150f;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 480;
            Content.RootDirectory = "Content";
        }

        protected override void Initialize()
        {
            position = new Vector2(100f, 100f);
            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            hero = Content.Load<Texture2D>("hero");
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            float step = Speed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (keys.IsKeyDown(Keys.Left)) position.X -= step;
            if (keys.IsKeyDown(Keys.Right)) position.X += step;
            if (keys.IsKeyDown(Keys.Up)) position.Y -= step;
            if (keys.IsKeyDown(Keys.Down)) position.Y += step;

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();
            spriteBatch.Draw(hero, position, Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}