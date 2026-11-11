using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace Xna40XactCompiled
{
    /// <summary>
    /// Plays music from precompiled XACT banks (no .xap in the project) and a hit cue when Space is pressed.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private AudioEngine engine;
        private WaveBank waveBank;
        private WaveBank musicBank;
        private SoundBank soundBank;
        private Cue theme;
        private bool spaceWasDown;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        protected override void LoadContent()
        {
            engine = new AudioEngine("Content\\Sounds.xgs");
            waveBank = new WaveBank(engine, "Content\\Wave Bank.xwb");
            musicBank = new WaveBank(engine, "Content\\Music Bank.xwb", 0, 16);
            soundBank = new SoundBank(engine, "Content\\Sound Bank.xsb");

            theme = soundBank.GetCue("Theme");
            theme.Play();
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
                soundBank.PlayCue("Hit");
            }

            spaceWasDown = spaceDown;
            engine.Update();
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
        }
    }
}