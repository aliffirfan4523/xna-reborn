using System;
using SharpDX;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;
using SharpDX.Toolkit.Input;

namespace SdxBasic
{
    /// <summary>
    /// Draws a texture and a line of text, and spins a camera matrix with the keyboard.
    /// </summary>
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private SpriteBatch spriteBatch;
        private SpriteFont arial16;
        private Texture2D balls;
        private KeyboardManager keyboard;
        private Matrix view;
        private Matrix projection;
        private Matrix world;
        private float angle;

        public Game1()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
            graphicsDeviceManager.PreferredBackBufferWidth = 800;
            graphicsDeviceManager.PreferredBackBufferHeight = 480;
            keyboard = new KeyboardManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            spriteBatch = ToDisposeContent(new SpriteBatch(GraphicsDevice));
            arial16 = Content.Load<SpriteFont>("Arial16");
            balls = Content.Load<Texture2D>("Balls");
            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            var state = keyboard.GetState();
            if (state.IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            angle = MathUtil.Clamp(angle + 0.01f, 0f, MathUtil.TwoPi);
            view = Matrix.LookAtRH(new Vector3(0f, 0f, 5f), Vector3.Zero, Vector3.UnitY);
            projection = Matrix.PerspectiveFovRH(MathUtil.PiOverFour, 1.6f, 0.1f, 100f);
            world = Matrix.RotationY(angle) * Matrix.Scaling(2f) * Matrix.Translation(1f, 0f, 0f);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.SetBlendState(GraphicsDevice.BlendStates.AlphaBlend);

            spriteBatch.Begin(SpriteSortMode.Deferred, GraphicsDevice.BlendStates.Opaque, GraphicsDevice.SamplerStates.PointClamp, null, null);
            spriteBatch.Draw(balls, new Vector2(100f, 100f), Color.White);
            spriteBatch.DrawString(arial16, "Hello", new Vector2(10f, 10f), Color.White);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}