using System.Windows.Media;

namespace PD2Launcherv2.Helpers
{
    /// <summary>A piece of item text in one colour.</summary>
    public record ColoredRun(string Text, Brush Brush);

    /// <summary>
    /// Splits Diablo II text with ÿc colour codes into coloured runs, line by line, in the
    /// order the game draws them (the game draws the last line of an item name on top).
    /// </summary>
    public static class D2ColorText
    {
        private const char ColorMarker = 'ÿ'; // 'ÿ'

        private static readonly Dictionary<char, Brush> Colors = new()
        {
            ['0'] = Freeze(Color.FromRgb(0xC4, 0xC4, 0xC4)), // white
            ['1'] = Freeze(Color.FromRgb(0xD4, 0x4A, 0x4A)), // red
            ['2'] = Freeze(Color.FromRgb(0x18, 0xFC, 0x00)), // set green
            ['3'] = Freeze(Color.FromRgb(0x68, 0x68, 0xF8)), // magic blue
            ['4'] = Freeze(Color.FromRgb(0xC7, 0xB3, 0x77)), // unique gold
            ['5'] = Freeze(Color.FromRgb(0x70, 0x70, 0x70)), // grey
            ['6'] = Freeze(Color.FromRgb(0x30, 0x30, 0x30)), // black (lifted so it stays visible)
            ['7'] = Freeze(Color.FromRgb(0xD0, 0xC2, 0x7D)), // tan
            ['8'] = Freeze(Color.FromRgb(0xFF, 0xA8, 0x00)), // crafted orange
            ['9'] = Freeze(Color.FromRgb(0xFF, 0xFF, 0x64)), // rare yellow
            [':'] = Freeze(Color.FromRgb(0x00, 0x80, 0x00)), // dark green
            [';'] = Freeze(Color.FromRgb(0xAE, 0x00, 0xFF)), // purple
            ['\x02'] = Freeze(Color.FromRgb(0x30, 0x30, 0x30)), // glide black
            ['\x06'] = Freeze(Color.FromRgb(0xFF, 0x80, 0x80)), // glide coral
            ['\x07'] = Freeze(Color.FromRgb(0x8F, 0xBC, 0x8F)), // glide sage
            ['\x09'] = Freeze(Color.FromRgb(0x00, 0x80, 0x80)), // glide teal
            ['\x0C'] = Freeze(Color.FromRgb(0xD3, 0xD3, 0xD3)), // glide light grey
        };

        public static Brush BrushFor(int qualityColor) =>
            Colors.TryGetValue(qualityColor.ToString()[0], out var b) ? b : Colors['0'];

        /// <summary>
        /// Parses <paramref name="text"/> starting in <paramref name="startColor"/> and returns its
        /// lines top to bottom as displayed (reversing the game's bottom-first line order when
        /// <paramref name="bottomLineFirst"/> is set, as it is for item names).
        /// </summary>
        public static List<List<ColoredRun>> Parse(string text, int startColor, bool bottomLineFirst)
        {
            var lines = new List<List<ColoredRun>>();
            var brush = BrushFor(startColor);
            foreach (var rawLine in text.Replace("\r", "").Split('\n'))
            {
                var runs = new List<ColoredRun>();
                var current = new System.Text.StringBuilder();
                for (int i = 0; i < rawLine.Length; i++)
                {
                    if (rawLine[i] == ColorMarker && i + 1 < rawLine.Length && rawLine[i + 1] == 'c')
                    {
                        if (current.Length > 0) { runs.Add(new ColoredRun(current.ToString(), brush)); current.Clear(); }
                        if (i + 2 < rawLine.Length)
                        {
                            // Unknown codes (e.g. transparency) keep the current colour.
                            if (Colors.TryGetValue(rawLine[i + 2], out var next)) brush = next;
                            i += 2;
                        }
                        else
                        {
                            i += 1;
                        }
                        continue;
                    }
                    current.Append(rawLine[i]);
                }
                if (current.Length > 0) runs.Add(new ColoredRun(current.ToString(), brush));
                lines.Add(runs);
            }
            if (bottomLineFirst) lines.Reverse();
            return lines;
        }

        /// <summary>The text with colour codes removed.</summary>
        public static string Strip(string text)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == ColorMarker && i + 1 < text.Length && text[i + 1] == 'c') { i += 2; continue; }
                sb.Append(text[i]);
            }
            return sb.ToString();
        }

        private static Brush Freeze(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }
    }
}
