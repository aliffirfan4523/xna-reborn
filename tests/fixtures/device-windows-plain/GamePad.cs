namespace WindowsPlain.Input
{
    /// <summary>The game's own pad wrapper. It is not the XNA type of the same name.</summary>
    public static class GamePad
    {
        public static bool GetState(int player)
        {
            return player == 0;
        }
    }
}
