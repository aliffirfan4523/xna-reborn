using System;
using Microsoft.Devices.Sensors;
using Microsoft.Phone.Shell;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace Wp7Xna
{
    /// <summary>
    /// A tiny Windows Phone XNA game: a square that follows taps and drags, drifts with the tilt of the phone,
    /// and keeps its score when the game is deactivated.
    /// </summary>
    public class Game1 : Game
    {
        private readonly GraphicsDeviceManager graphics;
        private SpriteBatch spriteBatch;
        private Texture2D pixel;
        private Accelerometer accelerometer;
        private Vector3 tilt;
        private Vector2 position = new Vector2(240, 400);
        private int score;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            graphics.PreferredBackBufferWidth = 480;
            graphics.PreferredBackBufferHeight = 800;
#if WINDOWS_PHONE
            graphics.IsFullScreen = true;
            TargetElapsedTime = TimeSpan.FromTicks(333333);
#else
            graphics.IsFullScreen = false;
#endif
            TouchPanel.EnabledGestures = GestureType.Tap | GestureType.FreeDrag;
            Exiting += OnGameExiting;
        }

        protected override void Initialize()
        {
            accelerometer = new Accelerometer();
            accelerometer.ReadingChanged += OnReadingChanged;
            try
            {
                accelerometer.Start();
            }
            catch (InvalidOperationException)
            {
                // No motion sensor: the game still works with touch.
            }

            if (PhoneApplicationService.Current.State.ContainsKey("score"))
            {
                score = (int)PhoneApplicationService.Current.State["score"];
            }

            base.Initialize();
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            pixel = new Texture2D(GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
        }

        private void OnGameExiting(object sender, EventArgs args)
        {
            PhoneApplicationService.Current.State["score"] = score;
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
            {
                Exit();
            }

            while (TouchPanel.IsGestureAvailable)
            {
                GestureSample gesture = TouchPanel.ReadGesture();
                if (gesture.GestureType == GestureType.Tap)
                {
                    position = gesture.Position;
                    score++;
                }
                else if (gesture.GestureType == GestureType.FreeDrag)
                {
                    position += gesture.Delta;
                }
            }

            position += new Vector2(tilt.X, -tilt.Y) * 4f;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Guide.IsTrialMode ? Color.DarkGray : Color.CornflowerBlue);
            spriteBatch.Begin();
            spriteBatch.Draw(pixel, new Rectangle((int)position.X - 16, (int)position.Y - 16, 32, 32), Color.Yellow);
            spriteBatch.End();
            base.Draw(gameTime);
        }

        private void OnReadingChanged(object sender, AccelerometerReadingEventArgs e)
        {
            tilt = new Vector3((float)e.X, (float)e.Y, (float)e.Z);
        }
    }
}
