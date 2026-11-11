package
{
	public class GapXml
	{
		public function run():void
		{
			var data:XML = <items><item id="1"/></items>;
			var first:XMLList = data.item.(@id == 1);
			trace(first);
		}
	}
}
