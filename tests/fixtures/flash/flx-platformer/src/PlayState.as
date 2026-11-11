package
{
	import org.flixel.*;

	public class PlayState extends FlxState
	{
		[Embed(source="../data/sprites.png")] protected var ImgTiles:Class;

		private var _player:Player;
		private var _enemies:FlxGroup;
		private var _bullets:FlxGroup;
		private var _floor:FlxTileblock;
		private var _hud:FlxText;
		private var _score:Score;
		private var _spawnTimer:Number;
		private var _wave:int;
		private var _tracked:Vector.<FlxSprite>;

		override public function create():void
		{
			_score = new Score();
			_score.addListener(function(v:int):void
			{
				_hud.text = "SCORE " + v;
			});

			_bullets = new FlxGroup();
			for (var i:int = 0; i < 8; i++)
			{
				_bullets.add(new Bullet());
			}
			add(_bullets);

			_floor = new FlxTileblock(0, 216, 320, 24);
			_floor.loadTiles(ImgTiles, 16, 16, 0);
			add(_floor);

			_player = new Player(40, 100, _bullets);
			add(_player);

			_enemies = new FlxGroup();
			add(_enemies);

			_hud = new FlxText(4, 4, 200, "SCORE 0");
			add(_hud);

			_spawnTimer = 1;
			_tracked = new Vector.<FlxSprite>();
			_tracked.push(_player);
		}

		override public function update():void
		{
			_spawnTimer -= FlxG.elapsed;
			if (_spawnTimer <= 0)
			{
				_enemies.add(new Enemy(300, 100, _player));
				_spawnTimer = 3 - _wave * 0.1;
				_wave++;
			}

			super.update();

			FlxG.collide(_floor, _player);
			FlxG.collide(_floor, _enemies);
			FlxG.overlap(_bullets, _enemies, hitEnemy);
			FlxG.overlap(_player, _enemies, hitPlayer);
		}

		private function hitEnemy(B:FlxObject, E:FlxObject):void
		{
			B.kill();
			E.hurt(1);
			if (!E.alive)
			{
				_score.value += 10;
			}
		}

		private function hitPlayer(P:FlxObject, E:FlxObject):void
		{
			FlxG.switchState(new MenuState());
		}
	}
}
