using System;

namespace SdxBasic
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var program = new Game1())
            {
                program.Run();
            }
        }
    }
}