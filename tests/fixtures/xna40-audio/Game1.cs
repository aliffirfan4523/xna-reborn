using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Xna40Audio
{
    /// <summary>
    /// Plays a song on start and a sound effect when Space is pressed.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private SoundEffect beep;
        private SoundEffect quiet;
        private SoundEffect jingle;
        private Song theme;
        private Song legacy;
        private bool spaceWasDown;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            beep = Content.Load<SoundEffect>("beep");
            quiet = Content.Load<SoundEffect>("quiet");
            jingle = Content.Load<SoundEffect>("jingle");
            theme = Content.Load<Song>("theme");
            legacy = Content.Load<Song>("legacy");

            MediaPlayer.IsRepeating = true;
            MediaPlayer.Play(theme);
        }

        protected override void Update(GameTime gameTime)
        {
            KeyboardState keys = Keyboard.GetState();
            if (keys.IsKeyDown(Keys.Escape))
            {
                Exit();
            }

            bool spaceDown = keys.IsKeyDown(Keys.Space);
            if (spaceDown && !spaceWasDown)
            {
                beep.Play();
                quiet.Play(0.3f, 0f, 0f);
                jingle.Play();
            }

            if (keys.IsKeyDown(Keys.L))
            {
                MediaPlayer.Play(legacy);
            }

            spaceWasDown = spaceDown;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }
    }
}
