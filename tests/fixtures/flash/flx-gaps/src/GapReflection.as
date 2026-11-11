package
{
	import flash.utils.getDefinitionByName;

	public class GapReflection
	{
		public function make(name:String):Object
		{
			var c:Class = getDefinitionByName(name) as Class;
			return new c();
		}
	}
}
