package
{
	import org.flixel.*;

	[SWF(width="640", height="480", backgroundColor="#000000")]
	public class HelloGame extends FlxGame
	{
		public function HelloGame()
		{
			super(320, 240, HelloState, 2);
		}
	}
}
