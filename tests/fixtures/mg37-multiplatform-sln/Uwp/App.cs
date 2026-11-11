namespace Mg37Uwp
{
    /// <summary>Stand-in for the platform application class. Never built.</summary>
    public class App
    {
        public void OnLaunched()
        {
            var game = new Mg37Multi.SharedGame();
            game.Run();
        }
    }
}
