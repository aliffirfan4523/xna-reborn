package
{
	import org.flixel.*;

	public class HelloState extends FlxState
	{
		override public function create():void
		{
			var text:FlxText = new FlxText(0, 100, 320, "HELLO FLIXEL");
			text.alignment = "center";
			add(text);
		}
	}
}
