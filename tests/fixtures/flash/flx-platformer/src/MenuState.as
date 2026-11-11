package
{
	import org.flixel.*;

	public class MenuState extends FlxState
	{
		override public function create():void
		{
			var title:FlxText = new FlxText(0, FlxG.height / 2 - 10, FlxG.width, "XNA REBORN PLATFORMER");
			title.alignment = "center";
			add(title);
			var prompt:FlxText = new FlxText(0, FlxG.height / 2 + 10, FlxG.width, "PRESS X TO START");
			prompt.alignment = "center";
			add(prompt);
		}

		override public function update():void
		{
			super.update();
			if (FlxG.keys.justPressed("X"))
			{
				FlxG.switchState(new PlayState());
			}
		}
	}
}
