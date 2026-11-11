package
{
	public class Score
	{
		private var _value:int;
		private var _listeners:Vector.<Function>;

		public function Score()
		{
			_value = 0;
			_listeners = new Vector.<Function>();
		}

		public function get value():int
		{
			return _value;
		}

		public function set value(v:int):void
		{
			_value = v;
			for each (var f:Function in _listeners)
			{
				f(_value);
			}
		}

		public function addListener(f:Function):void
		{
			_listeners.push(f);
		}

		public static function ratio(a:int, b:int):Number
		{
			return a / b;
		}
	}
}
