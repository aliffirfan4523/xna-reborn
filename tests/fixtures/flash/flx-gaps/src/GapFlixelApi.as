package
{
	import org.flixel.*;
	import org.flixel.plugin.TimerManager;

	[Frame(factoryClass="Preloader")]
	public class GapFlixelApi extends FlxState
	{
		override public function create():void
		{
			var s:FlxSprite = new FlxSprite(0, 0);
			s.blendMode = "add";
			var px:uint = s.getPixel(1, 1);
			FlxG.stage.focus = null;
			FlxG.debug = true;
		}
	}
}
