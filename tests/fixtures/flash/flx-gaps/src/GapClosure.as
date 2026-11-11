package
{
	public class GapClosure
	{
		public function run():Function
		{
			var base:int = 3;
			return function(v:int):int
			{
				return v + base;
			};
		}
	}
}
