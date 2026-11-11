package game
{
	public class Counter
	{
		private var _count:int = 0;

		public function Counter()
		{
		}

		public function get count():int
		{
			return _count;
		}

		public function increment():void
		{
			_count++;
		}
	}
}
