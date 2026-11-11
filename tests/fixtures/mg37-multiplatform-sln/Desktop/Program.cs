namespace Mg37Desktop
{
    public static class Program
    {
        private static void Main()
        {
            using (var game = new Mg37Multi.SharedGame())
            {
                game.Run();
            }
        }
    }
}
