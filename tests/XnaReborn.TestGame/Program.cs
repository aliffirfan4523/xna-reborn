// A stand-in for a game. Each argument is an action, run in order:
//   --stdout <text>   write a line to standard output
//   --stderr <text>   write a line to standard error
//   --cwd             write the working directory to standard output
//   --sleep <ms>      wait
//   --exit <code>     exit with this code
// With no --exit, the program waits until it is killed (like a game loop).
var exitCode = (int?)null;
for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--stdout":
            Console.Out.WriteLine(args[++i]);
            Console.Out.Flush();
            break;
        case "--stderr":
            Console.Error.WriteLine(args[++i]);
            Console.Error.Flush();
            break;
        case "--cwd":
            Console.Out.WriteLine(Environment.CurrentDirectory);
            Console.Out.Flush();
            break;
        case "--sleep":
            Thread.Sleep(int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture));
            break;
        case "--exit":
            exitCode = int.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            break;
        default:
            Console.Error.WriteLine("Unknown argument: " + args[i]);
            return 2;
    }

    if (exitCode is not null)
    {
        return exitCode.Value;
    }
}

Thread.Sleep(Timeout.Infinite);
return 0;
