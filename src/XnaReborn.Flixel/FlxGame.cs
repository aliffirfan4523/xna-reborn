using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using XnaReborn.As3;

namespace org.flixel;

/// <summary>
/// The game window and loop. It draws the state into a small render target of the game size and scales that to the window
/// with point sampling, like Flash's zoom.
/// </summary>
public class FlxGame : Game
{
    private readonly GraphicsDeviceManager _graphics;
    private readonly int _gameWidth;
    private readonly int _gameHeight;
    private readonly int _zoom;
    private readonly double _step;
    private readonly AsClass _initialState;
    private Adapter? _adapter;
    private SpriteBatch? _batch;
    private RenderTarget2D? _target;

    /// <summary>Creates the game. Nothing opens until <c>Run()</c>.</summary>
    /// <param name="GameSizeX">Width of the game view in game pixels.</param>
    /// <param name="GameSizeY">Height of the game view in game pixels.</param>
    /// <param name="InitialState">The class of the first <see cref="FlxState"/>.</param>
    /// <param name="Zoom">Window pixels per game pixel.</param>
    /// <param name="GameFramerate">Updates per second.</param>
    /// <param name="FlashFramerate">Accepted and ignored: drawing happens once per frame.</param>
    /// <param name="UseSystemCursor">True shows the system mouse cursor.</param>
    public FlxGame(int GameSizeX, int GameSizeY, AsClass InitialState, int Zoom = 2, int GameFramerate = 60, int FlashFramerate = 30, bool UseSystemCursor = false)
    {
        _gameWidth = GameSizeX;
        _gameHeight = GameSizeY;
        _zoom = Math.Max(1, Zoom);
        _initialState = InitialState;
        _step = 1.0 / Math.Max(1, GameFramerate);
        _graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = _gameWidth * _zoom,
            PreferredBackBufferHeight = _gameHeight * _zoom,
        };
        Content.RootDirectory = "Content";
        IsFixedTimeStep = true;
        TargetElapsedTime = TimeSpan.FromSeconds(_step);
        IsMouseVisible = UseSystemCursor;
        Window.Title = GetType().Name;
    }

    /// <inheritdoc />
    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _target = new RenderTarget2D(GraphicsDevice, _gameWidth, _gameHeight);
        _adapter = new Adapter(this, _batch);
        FlxG.init(_gameWidth, _gameHeight);
        FlxG.renderer = _adapter;
        FlxG.assets = _adapter;
        FlxG.switchState((FlxState)_initialState.New());
        base.LoadContent();
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        FlxG.step(_step, _adapter, _adapter);
        base.Update(gameTime);
    }

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        if (_batch is null || _target is null)
        {
            return;
        }

        GraphicsDevice.SetRenderTarget(_target);
        _batch.Begin(samplerState: SamplerState.PointClamp);
        FlxG.drawFrame();
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin(samplerState: SamplerState.PointClamp);
        _batch.Draw(_target, new Rectangle(0, 0, _gameWidth * _zoom, _gameHeight * _zoom), Color.White);
        _batch.End();
        base.Draw(gameTime);
    }

    private sealed class Adapter : IFlxRenderer, IFlxAssets, IFlxKeyState, IFlxMouseState
    {
        private readonly FlxGame _game;
        private readonly SpriteBatch _batch;
        private readonly Texture2D _pixel;
        private readonly Dictionary<string, Texture2D> _textures = new(StringComparer.Ordinal);
        private readonly Dictionary<string, SoundEffect> _sounds = new(StringComparer.Ordinal);

        public Adapter(FlxGame game, SpriteBatch batch)
        {
            _game = game;
            _batch = batch;
            _pixel = new Texture2D(game.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public bool IsDown(Keys key) => Keyboard.GetState().IsKeyDown(key);

        public (double X, double Y) Position
        {
            get
            {
                var m = Mouse.GetState();
                return ((double)m.X / _game._zoom, (double)m.Y / _game._zoom);
            }
        }

        public bool LeftDown => Mouse.GetState().LeftButton == ButtonState.Pressed;

        public int WheelTotal => Mouse.GetState().ScrollWheelValue;

        public void Clear(uint argb) => _game.GraphicsDevice.Clear(ToColor(argb));

        public void DrawQuad(double x, double y, double width, double height, uint argb) =>
            _batch.Draw(_pixel, new Rectangle((int)Math.Round(x), (int)Math.Round(y), Math.Max(1, (int)Math.Round(width)), Math.Max(1, (int)Math.Round(height))), ToColor(argb));

        public void DrawFrame(AssetRef texture, int srcX, int srcY, int srcWidth, int srcHeight, double x, double y, double width, double height, uint tintRgb, double alpha, bool flipHorizontal)
        {
            var tint = ToColor(((uint)Math.Round(255 * Math.Clamp(alpha, 0, 1)) << 24) | (tintRgb & 0xffffff));
            _batch.Draw(
                Load(texture),
                new Rectangle((int)Math.Round(x), (int)Math.Round(y), (int)Math.Round(width), (int)Math.Round(height)),
                new Rectangle(srcX, srcY, srcWidth, srcHeight),
                tint,
                0,
                Vector2.Zero,
                flipHorizontal ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0);
        }

        public (int Width, int Height)? GetTextureSize(AssetRef texture)
        {
            try
            {
                var t = Load(texture);
                return (t.Width, t.Height);
            }
            catch (ContentLoadException)
            {
                return null;
            }
        }

        public void PlaySound(AssetRef sound, double volume, bool looped)
        {
            if (!_sounds.TryGetValue(sound.Name, out var effect))
            {
                _sounds[sound.Name] = effect = _game.Content.Load<SoundEffect>(sound.Name);
            }

            if (looped)
            {
                var instance = effect.CreateInstance();
                instance.IsLooped = true;
                instance.Volume = (float)Math.Clamp(volume, 0, 1);
                instance.Play();
            }
            else
            {
                effect.Play((float)Math.Clamp(volume, 0, 1), 0, 0);
            }
        }

        public byte[] ReadData(AssetRef data)
        {
            using var stream = TitleContainer.OpenStream(Path.Combine(_game.Content.RootDirectory, data.Name.Replace('/', Path.DirectorySeparatorChar)));
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        private static Color ToColor(uint argb) =>
            Color.FromNonPremultiplied((int)((argb >> 16) & 0xff), (int)((argb >> 8) & 0xff), (int)(argb & 0xff), (int)((argb >> 24) & 0xff));

        private Texture2D Load(AssetRef texture)
        {
            if (!_textures.TryGetValue(texture.Name, out var t))
            {
                _textures[texture.Name] = t = _game.Content.Load<Texture2D>(texture.Name);
            }

            return t;
        }
    }
}
