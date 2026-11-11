package
{
	public class GapUntyped
	{
		public var anything:*;

		public function run(value):void
		{
			var other = value;
			anything = other;
		}
	}
}
