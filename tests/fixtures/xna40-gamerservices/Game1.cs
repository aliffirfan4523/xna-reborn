using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Storage;

namespace Xna40GamerServices
{
    /// <summary>
    /// Uses sign-in and storage services that have no direct MonoGame equivalent.
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private GraphicsDeviceManager graphics;
        private StorageDevice storageDevice;
        private bool signInRequested;
        private string playerName = "(nobody)";

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            Components.Add(new GamerServicesComponent(this));
        }

        protected override void Update(GameTime gameTime)
        {
            if (!signInRequested && !Guide.IsVisible)
            {
                signInRequested = true;
                Guide.ShowSignIn(1, false);
            }

            if (Gamer.SignedInGamers.Count > 0)
            {
                playerName = Gamer.SignedInGamers[0].Gamertag;
                if (storageDevice == null && !Guide.IsVisible)
                {
                    StorageDevice.BeginShowSelector(OnSelectorDone, null);
                }
            }

            base.Update(gameTime);
        }

        private void OnSelectorDone(IAsyncResult result)
        {
            storageDevice = StorageDevice.EndShowSelector(result);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateGray);
            Window.Title = "Player: " + playerName;
            base.Draw(gameTime);
        }
    }
}