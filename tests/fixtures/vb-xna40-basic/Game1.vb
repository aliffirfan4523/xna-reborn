Public Class Game1
    Inherits Microsoft.Xna.Framework.Game

    Private graphics As GraphicsDeviceManager
    Private spriteBatch As SpriteBatch
    Private hero As Texture2D

    Public Sub New()
        graphics = New GraphicsDeviceManager(Me)
        Content.RootDirectory = "Content"
    End Sub

    Protected Overrides Sub LoadContent()
        spriteBatch = New SpriteBatch(GraphicsDevice)
        hero = Content.Load(Of Texture2D)("Textures/hero")
    End Sub

    Protected Overrides Sub Update(ByVal gameTime As GameTime)
        If Keyboard.GetState().IsKeyDown(Keys.Escape) Then
            [Exit]()
        End If

        MyBase.Update(gameTime)
    End Sub

    Protected Overrides Sub Draw(ByVal gameTime As GameTime)
        GraphicsDevice.Clear(Color.CornflowerBlue)
        spriteBatch.Begin()
        spriteBatch.Draw(hero, New Vector2(32.0F, 32.0F), Color.White)
        spriteBatch.End()
        MyBase.Draw(gameTime)
    End Sub
End Class
