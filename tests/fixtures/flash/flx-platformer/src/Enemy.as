package
{
	import org.flixel.*;

	public class Enemy extends FlxSprite
	{
		private var _speed:Number = 30;
		private var _target:FlxSprite;

		public function Enemy(X:Number, Y:Number, Target:FlxSprite)
		{
			super(X, Y);
			makeGraphic(12, 12, 0xffcc3030);
			_target = Target;
			acceleration.y = 420;
			health = 2;
		}

		override public function update():void
		{
			var dx:Number = _target.x - x;
			velocity.x = dx > 0 ? _speed : -_speed;
			super.update();
		}

		override public function hurt(Damage:Number):void
		{
			flicker(0.1);
			super.hurt(Damage);
		}
	}
}
