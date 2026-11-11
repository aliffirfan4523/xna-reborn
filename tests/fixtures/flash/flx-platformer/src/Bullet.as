package
{
	import org.flixel.*;

	public class Bullet extends FlxSprite
	{
		public function Bullet()
		{
			super(0, 0);
			makeGraphic(4, 2, 0xffffffff);
			exists = false;
		}

		public function shoot(X:Number, Y:Number, VelocityX:Number):void
		{
			reset(X, Y);
			velocity.x = VelocityX;
		}

		override public function update():void
		{
			super.update();
			if (!onScreen())
			{
				kill();
			}
		}
	}
}
