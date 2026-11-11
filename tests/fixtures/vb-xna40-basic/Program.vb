#If WINDOWS OrElse XBOX Then
Friend Class Program
    ''' <summary>The main entry point for the application.</summary>
    Shared Sub Main(ByVal args As String())
        Using game As New Game1()
            game.Run()
        End Using
    End Sub
End Class
#End If
