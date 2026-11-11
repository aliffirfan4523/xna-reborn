using SharpDX;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using SharpDX.Toolkit;
using SharpDX.Toolkit.Graphics;

namespace SdxLowLevel
{
    /// <summary>
    /// Mixes the Toolkit with raw Direct3D 11 and types that MonoGame does not have.
    /// </summary>
    public class Game1 : Game
    {
        private GraphicsDeviceManager graphicsDeviceManager;
        private GeometricPrimitive cube;
        private RectangleF bounds;
        private Color4 tint;
        private RenderTarget2D target;
        private DepthStencilBuffer depth;

        public Game1()
        {
            graphicsDeviceManager = new GraphicsDeviceManager(this);
        }

        protected override void LoadContent()
        {
            cube = GeometricPrimitive.Cube.New(GraphicsDevice);
            bounds = new RectangleF(0f, 0f, 10f, 10f);
            tint = new Color4(1f, 1f, 1f, 1f);
            target = RenderTarget2D.New(GraphicsDevice, 256, 256, PixelFormat.R8G8B8A8.UNorm);
            depth = DepthStencilBuffer.New(GraphicsDevice, 256, 256, DepthFormat.Depth32);
            var raw = (Device)GraphicsDevice;
            base.LoadContent();
        }

        protected override void Draw(GameTime gameTime)
        {
            var view = Matrix.LookAtLH(new Vector3(0f, 0f, -5f), Vector3.Zero, Vector3.UnitY);
            var point = Vector3.TransformCoordinate(Vector3.One, view);
            GraphicsDevice.SetRenderTargets(depth, target);
            GraphicsDevice.DrawQuad(null);
            base.Draw(gameTime);
        }
    }
}