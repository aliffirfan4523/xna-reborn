using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MonoGameGame
{
    /// <summary>
    /// Uses the MonoGame names that look like Toolkit names. No SDX rule may fire here.
    /// </summary>
    public class Game1 : Game
    {
        private Matrix view;

        protected override void Update(GameTime gameTime)
        {
            var keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            view = Matrix.CreateLookAt(Vector3.Backward, Vector3.Zero, Vector3.Up);
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.BlendState = BlendState.Opaque;
            base.Draw(gameTime);
        }
    }
}