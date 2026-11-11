package
{
	import org.flixel.*;

	public class EmbedGame extends FlxGame
	{
		[Embed(source="../data/picture.png")] private var ImgPicture:Class;
		[Embed(source="../data/sound.mp3")] private var SndSound:Class;
		[Embed(source="../data/map.csv", mimeType="application/octet-stream")] private var DataMap:Class;
		[Embed(source="../data/info.xml", mimeType="application/octet-stream")] private var DataInfo:Class;
		[Embed(source="../data/face.ttf", fontFamily="Face", embedAsCFF="false")] private var FontFace:Class;
		[Embed(source="library.swf", symbol="Hero")] private var SymbolHero:Class;

		public function EmbedGame()
		{
			super(320, 240, FlxState);
		}
	}
}
