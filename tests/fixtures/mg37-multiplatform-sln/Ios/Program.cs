namespace Mg37Ios
{
    /// <summary>Stand-in for the platform entry point. Never built.</summary>
    public static class Program
    {
        private static void Main()
        {
            var game = new Mg37Multi.SharedGame();
            game.Run();
        }
    }
}
