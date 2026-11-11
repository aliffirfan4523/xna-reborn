namespace org.flixel;

/// <summary>A small built-in 5 by 7 pixel font so text needs no font file. Lower case letters draw as capitals.</summary>
public static class PixelFont
{
    /// <summary>Pixels from one text line to the next at scale 1.</summary>
    public const int LineHeight = 8;

    private const int _advance = 6;

    // Each glyph: 7 rows, each row 5 bits (bit 4 is the left column).
    private static readonly Dictionary<char, byte[]> _glyphs = Build();

    /// <summary>The whole-number scale for a text size in pixels (8 gives 1, 16 gives 2).</summary>
    public static int ScaleFor(double size) => Math.Max(1, (int)Math.Round(size / 8));

    /// <summary>Width of a line of text in pixels at the given scale.</summary>
    public static double Measure(string line, int scale) => line.Length == 0 ? 0 : (line.Length * _advance - 1) * scale;

    /// <summary>Draws a line of text as one rectangle per lit pixel.</summary>
    public static void Draw(IFlxRenderer renderer, string line, double x, double y, int scale, uint argb)
    {
        var cx = x;
        foreach (var raw in line)
        {
            var glyph = _glyphs.TryGetValue(char.ToUpperInvariant(raw), out var g) ? g : _glyphs['?'];
            for (var row = 0; row < 7; row++)
            {
                for (var col = 0; col < 5; col++)
                {
                    if ((glyph[row] & (1 << (4 - col))) != 0)
                    {
                        renderer.DrawQuad(cx + col * scale, y + row * scale, scale, scale, argb);
                    }
                }
            }

            cx += _advance * scale;
        }
    }

    private static Dictionary<char, byte[]> Build()
    {
        var d = new Dictionary<char, byte[]> { [' '] = new byte[7] };
        void Add(char c, string rows) => d[c] = rows.Split(' ').Select(h => Convert.ToByte(h, 16)).ToArray();

        Add('0', "0E 11 13 15 19 11 0E");
        Add('1', "04 0C 04 04 04 04 0E");
        Add('2', "0E 11 01 02 04 08 1F");
        Add('3', "1F 02 04 02 01 11 0E");
        Add('4', "02 06 0A 12 1F 02 02");
        Add('5', "1F 10 1E 01 01 11 0E");
        Add('6', "06 08 10 1E 11 11 0E");
        Add('7', "1F 01 02 04 08 08 08");
        Add('8', "0E 11 11 0E 11 11 0E");
        Add('9', "0E 11 11 0F 01 02 0C");
        Add('A', "0E 11 11 1F 11 11 11");
        Add('B', "1E 11 11 1E 11 11 1E");
        Add('C', "0E 11 10 10 10 11 0E");
        Add('D', "1E 11 11 11 11 11 1E");
        Add('E', "1F 10 10 1E 10 10 1F");
        Add('F', "1F 10 10 1E 10 10 10");
        Add('G', "0E 11 10 17 11 11 0F");
        Add('H', "11 11 11 1F 11 11 11");
        Add('I', "0E 04 04 04 04 04 0E");
        Add('J', "07 02 02 02 02 12 0C");
        Add('K', "11 12 14 18 14 12 11");
        Add('L', "10 10 10 10 10 10 1F");
        Add('M', "11 1B 15 15 11 11 11");
        Add('N', "11 11 19 15 13 11 11");
        Add('O', "0E 11 11 11 11 11 0E");
        Add('P', "1E 11 11 1E 10 10 10");
        Add('Q', "0E 11 11 11 15 12 0D");
        Add('R', "1E 11 11 1E 14 12 11");
        Add('S', "0F 10 10 0E 01 01 1E");
        Add('T', "1F 04 04 04 04 04 04");
        Add('U', "11 11 11 11 11 11 0E");
        Add('V', "11 11 11 11 11 0A 04");
        Add('W', "11 11 11 15 15 15 0A");
        Add('X', "11 11 0A 04 0A 11 11");
        Add('Y', "11 11 11 0A 04 04 04");
        Add('Z', "1F 01 02 04 08 10 1F");
        Add('!', "04 04 04 04 04 00 04");
        Add('?', "0E 11 01 02 04 00 04");
        Add('.', "00 00 00 00 00 0C 0C");
        Add(',', "00 00 00 00 0C 04 08");
        Add(':', "00 0C 0C 00 0C 0C 00");
        Add('-', "00 00 00 1F 00 00 00");
        Add('+', "00 04 04 1F 04 04 00");
        Add('=', "00 00 1F 00 1F 00 00");
        Add('/', "01 01 02 04 08 10 10");
        Add('(', "02 04 08 08 08 04 02");
        Add(')', "08 04 02 02 02 04 08");
        Add('\'', "0C 04 08 00 00 00 00");
        Add('"', "0A 0A 0A 00 00 00 00");
        Add('_', "00 00 00 00 00 00 1F");
        Add('%', "18 19 02 04 08 13 03");
        Add('*', "00 04 15 0E 15 04 00");
        Add('<', "02 04 08 10 08 04 02");
        Add('>', "08 04 02 01 02 04 08");
        Add('#', "0A 0A 1F 0A 1F 0A 0A");
        return d;
    }
}
