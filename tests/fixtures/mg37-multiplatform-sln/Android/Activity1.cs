namespace Mg37Android
{
    /// <summary>Stand-in for the platform activity. Never built.</summary>
    public class Activity1
    {
        public void OnCreate()
        {
            var game = new Mg37Multi.SharedGame();
            game.Run();
        }
    }
}
