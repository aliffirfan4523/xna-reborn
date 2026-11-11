package
{
	import org.flixel.*;

	[SWF(width="640", height="480", backgroundColor="#000000")]
	public class PlatformerGame extends FlxGame
	{
		public function PlatformerGame()
		{
			super(320, 240, MenuState, 2);
		}
	}
}
