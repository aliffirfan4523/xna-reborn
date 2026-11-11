package
{
	import org.flixel.*;

	public class Player extends FlxSprite
	{
		[Embed(source="../data/sprites.png")] private var ImgPlayer:Class;
		[Embed(source="../data/jump.wav")] private var SndJump:Class;

		public var runSpeed:Number;
		private var _shotTimer:Number = 0;
		private var _bullets:FlxGroup;

		public function Player(X:Number, Y:Number, Bullets:FlxGroup)
		{
			super(X, Y);
			loadGraphic(ImgPlayer, true, true, 16, 16);
			width = 10;
			height = 14;
			offset.x = 3;
			offset.y = 2;
			runSpeed = 80;
			drag.x = runSpeed * 8;
			acceleration.y = 420;
			maxVelocity.x = runSpeed;
			maxVelocity.y = 200;
			addAnimation("idle", [0]);
			addAnimation("walk", [1, 2, 3, 2], 10, true);
			_bullets = Bullets;
		}

		override public function update():void
		{
			acceleration.x = 0;
			if (FlxG.keys.LEFT)
			{
				facing = LEFT;
				acceleration.x = -drag.x;
			}
			else if (FlxG.keys.RIGHT)
			{
				facing = RIGHT;
				acceleration.x = drag.x;
			}

			if (FlxG.keys.justPressed("UP") && isTouching(FLOOR))
			{
				velocity.y = -200;
				FlxG.play(SndJump);
			}

			_shotTimer -= FlxG.elapsed;
			if (FlxG.keys.X && _shotTimer <= 0)
			{
				shoot();
			}

			if (velocity.x != 0)
			{
				play("walk");
			}
			else
			{
				play("idle");
			}

			super.update();
		}

		private function shoot():void
		{
			var b:Bullet = _bullets.recycle(Bullet) as Bullet;
			b.shoot(x + width / 2, y + 4, facing == LEFT ? -160 : 160);
			_shotTimer = 0.25;
		}
	}
}
