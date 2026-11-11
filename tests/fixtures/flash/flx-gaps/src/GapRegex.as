package
{
	public class GapRegex
	{
		public function run(s:String):Boolean
		{
			return /^[a-z]+$/i.test(s);
		}
	}
}
