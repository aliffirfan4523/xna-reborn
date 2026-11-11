package
{
	import org.flixel.*;

	public class MissingState extends FlxState
	{
		[Embed(source="../data/does-not-exist.png")] private var ImgMissing:Class;

		override public function create():void
		{
			var s:FlxSprite = new FlxSprite(10, 10, ImgMissing);
			add(s);
		}
	}
}
