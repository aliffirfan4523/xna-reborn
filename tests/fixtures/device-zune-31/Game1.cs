using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Zune31
{
    /// <summary>A Zune game: the D-pad changes the track and the back button leaves.</summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private MediaLibrary library;
        private int track;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
#if ZUNE
            graphics.PreferredBackBufferWidth = 240;
            graphics.PreferredBackBufferHeight = 320;
#endif
        }

        protected override void Initialize()
        {
            library = new MediaLibrary();
            base.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            GamePadState pad = GamePad.GetState(PlayerIndex.One);
            if (pad.Buttons.Back == ButtonState.Pressed)
            {
                Exit();
            }

            if (pad.DPad.Right == ButtonState.Pressed && track < library.Songs.Count - 1)
            {
                track++;
                MediaPlayer.Play(library.Songs[track]);
            }

            base.Update(gameTime);
        }
    }
}
