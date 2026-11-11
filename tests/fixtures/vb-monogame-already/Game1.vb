Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Public Class Game1
    Inherits Game

    Private graphics As GraphicsDeviceManager
    Private spriteBatch As SpriteBatch

    Public Sub New()
        graphics = New GraphicsDeviceManager(Me)
    End Sub

    Protected Overrides Sub LoadContent()
        spriteBatch = New SpriteBatch(GraphicsDevice)
    End Sub

    Protected Overrides Sub Draw(ByVal gameTime As GameTime)
        GraphicsDevice.Clear(Color.CornflowerBlue)
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend)
        spriteBatch.End()
        MyBase.Draw(gameTime)
    End Sub
End Class
