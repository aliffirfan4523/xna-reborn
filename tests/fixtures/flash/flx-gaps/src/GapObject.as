package
{
	public class GapObject
	{
		public function run():Object
		{
			var bag:Object = {name: "x", count: 2};
			bag["other"] = 5;
			return bag;
		}
	}
}
