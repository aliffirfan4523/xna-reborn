Imports Microsoft.Xna.Framework
Imports Microsoft.Xna.Framework.Graphics

Public Class Game1
    Inherits Game

    Private graphics As GraphicsDeviceManager
    Private spriteBatch As SpriteBatch
    Private fx As BasicEffect
    Private mode As SpriteBlendMode = SpriteBlendMode.AlphaBlend

    Public Sub New()
        graphics = New GraphicsDeviceManager(Me)
    End Sub

    Protected Overrides Sub LoadContent()
        spriteBatch = New SpriteBatch(GraphicsDevice)
        fx = New BasicEffect(GraphicsDevice, Nothing)
    End Sub

    Protected Overrides Sub Draw(ByVal gameTime As GameTime)
        GraphicsDevice.Clear(Color.Black)

        With GraphicsDevice.RenderState
            .CullMode = CullMode.None
            .AlphaBlendEnable = True
        End With

        spriteBatch.Begin(SpriteBlendMode.AlphaBlend)
        spriteBatch.End()

        fx.Begin
        fx.End()

        Dim title As String = My.Application.Info.Title
        Window.Title = title
        MyBase.Draw(gameTime)
    End Sub
End Class
