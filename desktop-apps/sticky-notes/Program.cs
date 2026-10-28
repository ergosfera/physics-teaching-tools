// Sticky Notes: sticky notes on the Windows desktop that sync through a Google Drive folder.
// Each note is a small text file in "My Drive\StickyNotes". The Mac app (../sticky-notes-mac)
// reads and writes the same files, and Google Drive for desktop copies them between computers.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Sticky Notes")]
[assembly: AssemblyProduct("Sticky Notes")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.2.2.0")]
[assembly: AssemblyFileVersion("1.2.2.0")]

namespace StickyNotes
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--write-icon")
            {
                IconArt.WriteIco(args[1]);
                return;
            }
            if (args.Length == 1 && args[0] == "--selftest")
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }
            string folder = null;
            if (args.Length == 2 && args[0] == "--folder") folder = Path.GetFullPath(args[1]);

            bool first;
            using (var mutex = new Mutex(true, "Ergosfera.StickyNotes", out first))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Ergosfera.StickyNotes.Show"))
            {
                // Already running: ask that copy to bring its notes forward.
                if (!first) { showEvent.Set(); return; }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new NotesApp(folder, showEvent));
            }
        }
    }

    // MARK: Note files (the Mac app uses the same format; see SelfTest)

    class NoteData
    {
        public string Id = "";
        public string Color = "yellow";
        public string Title = "";
        public string Modified = "";
        public string By = "";
        public string Text = "";
    }

    // A note file is a short header, a "---" line, then the note text:
    //   color: yellow
    //   title: Groceries
    //   modified: 2026-10-18T14:03:22.123Z
    //   by: HOME-PC
    //   ---
    //   Buy milk
    // The title line is there only when the note has a title.
    // A plain .txt file without the header also works: all of it becomes the note text.
    static class NoteFile
    {
        static readonly Random Rng = new Random();

        public static string Now()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        public static string NewId()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Rng.Next(0x10000).ToString("x4");
        }

        public static string TrashName(string id)
        {
            return id + " deleted " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".txt";
        }

        public static string Normalize(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        // A title is one line, without spaces at either end
        public static string CleanTitle(string s)
        {
            return Normalize(s).Replace('\n', ' ').Trim(' ', '\t');
        }

        public static string Serialize(NoteData n)
        {
            var sb = new StringBuilder();
            sb.Append("color: ").Append(n.Color).Append('\n');
            string title = CleanTitle(n.Title);
            if (title.Length > 0) sb.Append("title: ").Append(title).Append('\n');
            sb.Append("modified: ").Append(n.Modified).Append('\n');
            sb.Append("by: ").Append(n.By.Replace('\n', ' ')).Append('\n');
            sb.Append("---\n");
            sb.Append(Normalize(n.Text));
            return sb.ToString();
        }

        public static NoteData Parse(string id, string content)
        {
            var n = new NoteData { Id = id };
            string s = Normalize(content);
            if (s.Length > 0 && s[0] == '﻿') s = s.Substring(1);
            string header, body;
            if (s.StartsWith("---\n", StringComparison.Ordinal))
            {
                header = "";
                body = s.Substring(4);
            }
            else
            {
                int sep = s.IndexOf("\n---\n", StringComparison.Ordinal);
                if (sep < 0) { n.Text = s; return n; }
                header = s.Substring(0, sep);
                body = s.Substring(sep + 5);
            }

            var fields = new Dictionary<string, string>();
            foreach (string line in header.Split('\n'))
            {
                if (line.Length == 0) continue;
                int colon = line.IndexOf(':');
                bool key = colon > 0;
                for (int i = 0; key && i < colon; i++) key = line[i] >= 'a' && line[i] <= 'z';
                // Not a header after all (for example a plain note with a "---" line in it).
                if (!key) { n.Text = s; return n; }
                fields[line.Substring(0, colon)] = line.Substring(colon + 1).Trim();
            }
            string v;
            if (fields.TryGetValue("color", out v) && v.Length > 0) n.Color = v;
            if (fields.TryGetValue("title", out v)) n.Title = v;
            if (fields.TryGetValue("modified", out v)) n.Modified = v;
            if (fields.TryGetValue("by", out v)) n.By = v;
            n.Text = body;
            return n;
        }
    }

    // A run of characters, counted in UTF-16 units like the Mac app
    struct TextSpan
    {
        public int Start, Length;
        public TextSpan(int start, int length) { Start = start; Length = length; }
    }

    // A run of characters in one text color (a name from Markup.Inks)
    struct InkSpan
    {
        public int Start, Length;
        public string Name;
        public InkSpan(int start, int length, string name) { Start = start; Length = length; Name = name; }
    }

    // Checkboxes, bold and text colors, kept in the note text as plain markers so the file stays readable anywhere:
    //   "[ ] milk" / "[x] milk" at the start of a line is a checkbox (shown as ☐ / ☑), "**word**" is bold,
    //   and "{red}word{/red}" is red text (the color names are Markup.Inks).
    // On screen the markers become a box character plus a space, bold text and colored text.
    static class Markup
    {
        public const char Unchecked = '\u2610', Checked = '\u2611';
        // Text colors besides the usual dark ink
        public static readonly string[] Inks = { "red", "orange", "green", "blue", "purple" };

        public static bool IsBox(char c) { return c == Unchecked || c == Checked; }

        // Is there a box (and its space) at the start of the line beginning at index i?
        public static bool HasBox(string s, int i) { return i >= 0 && i + 1 < s.Length && IsBox(s[i]) && s[i + 1] == ' '; }

        static void AddBold(List<TextSpan> bold, int i)
        {
            int n = bold.Count;
            if (n > 0 && bold[n - 1].Start + bold[n - 1].Length == i) bold[n - 1] = new TextSpan(bold[n - 1].Start, bold[n - 1].Length + 1);
            else bold.Add(new TextSpan(i, 1));
        }

        static void AddInk(List<InkSpan> ink, int i, string name)
        {
            int n = ink.Count;
            if (n > 0 && ink[n - 1].Name == name && ink[n - 1].Start + ink[n - 1].Length == i)
                ink[n - 1] = new InkSpan(ink[n - 1].Start, ink[n - 1].Length + 1, name);
            else ink.Add(new InkSpan(i, 1, name));
        }

        public static string Parse(string markup, List<TextSpan> bold) { return Parse(markup, bold, new List<InkSpan>()); }

        public static string Parse(string markup, List<TextSpan> bold, List<InkSpan> ink)
        {
            var o = new StringBuilder();
            string[] lines = markup.Split('\n');
            for (int n = 0; n < lines.Length; n++)
            {
                if (n > 0) o.Append('\n');
                string line = lines[n];
                if (line.StartsWith("[ ] ", StringComparison.Ordinal)) { o.Append(Unchecked).Append(' '); line = line.Substring(4); }
                else if (line.StartsWith("[x] ", StringComparison.Ordinal) || line.StartsWith("[X] ", StringComparison.Ordinal))
                { o.Append(Checked).Append(' '); line = line.Substring(4); }
                int len = line.Length;
                var drop = new bool[len];
                var isBold = new bool[len];
                var color = new string[len];

                // "{name}" and "{/name}" pair up within the line; a tag without its partner, or an empty pair, stays as typed
                for (int i = 0; i < len; )
                {
                    int next = i + 1;
                    if (line[i] == '{')
                        foreach (string name in Inks)
                        {
                            string open = "{" + name + "}", close = "{/" + name + "}";
                            if (i + open.Length > len || string.CompareOrdinal(line, i, open, 0, open.Length) != 0) continue;
                            int c = line.IndexOf(close, i + open.Length, StringComparison.Ordinal);
                            if (c <= i + open.Length) continue;
                            for (int k = i; k < i + open.Length; k++) drop[k] = true;
                            for (int k = c; k < c + close.Length; k++) drop[k] = true;
                            for (int k = i + open.Length; k < c; k++) color[k] = name;
                            next = c + close.Length;
                            break;
                        }
                    i = next;
                }

                // "**" marks pair up within the line; an odd one out, or an empty pair, stays as typed
                var marks = new List<int>();
                for (int j = 0; j + 1 < len; )
                {
                    if (line[j] == '*' && line[j + 1] == '*') { marks.Add(j); j += 2; } else j++;
                }
                if (marks.Count % 2 == 1) marks.RemoveAt(marks.Count - 1);
                for (int k = 0; k < marks.Count; k += 2)
                {
                    int a = marks[k], b = marks[k + 1];
                    if (b == a + 2) continue;
                    drop[a] = drop[a + 1] = drop[b] = drop[b + 1] = true;
                    for (int x = a + 2; x < b; x++) isBold[x] = true;
                }

                for (int x = 0; x < len; x++)
                {
                    if (drop[x]) continue;
                    int at = o.Length;
                    o.Append(line[x]);
                    if (isBold[x]) AddBold(bold, at);
                    if (color[x] != null) AddInk(ink, at, color[x]);
                }
            }
            return o.ToString();
        }

        public static string Serialize(string t, List<TextSpan> bold) { return Serialize(t, bold, new List<InkSpan>()); }

        public static string Serialize(string t, List<TextSpan> bold, List<InkSpan> ink)
        {
            var isBold = new bool[t.Length];
            foreach (var r in bold)
                for (int i = Math.Max(0, r.Start); i < Math.Min(t.Length, r.Start + r.Length); i++) isBold[i] = true;
            var color = new string[t.Length];
            foreach (var r in ink)
                if (Array.IndexOf(Inks, r.Name) >= 0)
                    for (int i = Math.Max(0, r.Start); i < Math.Min(t.Length, r.Start + r.Length); i++) color[i] = r.Name;
            var o = new StringBuilder();
            int start = 0;
            while (start <= t.Length)
            {
                int end = t.IndexOf('\n', start);
                if (end < 0) end = t.Length;
                int i = start;
                if (HasBox(t, i) && i + 1 < end)
                {
                    o.Append(t[i] == Unchecked ? "[ ] " : "[x] ");
                    i += 2;
                }
                // Colored runs get "{name}" and "{/name}" around them, outside any "**"; each line closes its own
                while (i < end)
                {
                    string c = color[i];
                    int j = i;
                    while (j < end && color[j] == c) j++;
                    if (c != null && t.Substring(i, j - i).Trim(' ', '\t').Length > 0)
                    {
                        o.Append('{').Append(c).Append('}');
                        WriteBold(o, t, isBold, i, j);
                        o.Append("{/").Append(c).Append('}');
                    }
                    else WriteBold(o, t, isBold, i, j);
                    i = j;
                }
                if (end < t.Length) o.Append('\n');
                start = end + 1;
            }
            return o.ToString();
        }

        // Bold runs get "**" around them
        static void WriteBold(StringBuilder o, string t, bool[] isBold, int from, int to)
        {
            for (int i = from; i < to; )
            {
                bool b = isBold[i];
                int j = i;
                while (j < to && isBold[j] == b) j++;
                string piece = t.Substring(i, j - i);
                if (b && piece.Trim(' ', '\t').Length > 0) o.Append("**").Append(piece).Append("**"); else o.Append(piece);
                i = j;
            }
        }
    }

    // StickyNotes.exe --selftest (run by build.ps1). The Mac app checks the same cases.
    static class SelfTest
    {
        public static int Run()
        {
            int fails = 0;
            var n = new NoteData { Id = "a", Color = "pink", Modified = "2026-10-18T10:00:00.000Z", By = "MAC", Text = "Hello\r\n---\nworld: yes" };
            string file = NoteFile.Serialize(n);
            if (file != "color: pink\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes") fails++;
            var back = NoteFile.Parse("a", file);
            if (back.Color != "pink" || back.Modified != n.Modified || back.By != "MAC" || back.Text != "Hello\n---\nworld: yes") fails++;
            var plain = NoteFile.Parse("b", "Just text\r\nline 2");
            if (plain.Text != "Just text\nline 2" || plain.Color != "yellow" || plain.Modified != "") fails++;
            var bom = NoteFile.Parse("c", "﻿color: blue\nmodified: x\nby: y\n---\n");
            if (bom.Color != "blue" || bom.Text != "" || bom.Modified != "x" || bom.By != "y") fails++;
            var list = NoteFile.Parse("d", "Shopping list\n---\nmilk");
            if (list.Text != "Shopping list\n---\nmilk" || list.Color != "yellow") fails++;
            var bodyOnly = NoteFile.Parse("e", "---\nonly body");
            if (bodyOnly.Text != "only body" || bodyOnly.Color != "yellow") fails++;

            // Checkboxes and bold
            var b0 = new List<TextSpan>();
            if (Markup.Parse("[ ] milk\n[x] eggs\nplain", b0) != "\u2610 milk\n\u2611 eggs\nplain" || b0.Count != 0) fails++;
            var b1 = new List<TextSpan>();
            if (Markup.Parse("a **b** c", b1) != "a b c" || b1.Count != 1 || b1[0].Start != 2 || b1[0].Length != 1) fails++;
            var b2 = new List<TextSpan>();
            if (Markup.Parse("x ** y", b2) != "x ** y" || b2.Count != 0) fails++;
            var b3 = new List<TextSpan>();
            if (Markup.Parse("****", b3) != "****" || b3.Count != 0) fails++;
            if (Markup.Serialize("\u2610 a\n\u2611 b", new List<TextSpan>()) != "[ ] a\n[x] b") fails++;
            if (Markup.Serialize("abc d", new List<TextSpan> { new TextSpan(0, 3) }) != "**abc** d") fails++;
            if (Markup.Serialize("ab\ncd", new List<TextSpan> { new TextSpan(1, 3) }) != "a**b**\n**c**d") fails++;
            if (Markup.Serialize("\u2610x", new List<TextSpan>()) != "\u2610x") fails++;
            string mixed = "[ ] **buy** milk\n[x] done\n**Title**\nend\n";
            var bm = new List<TextSpan>();
            string shown = Markup.Parse(mixed, bm);
            if (Markup.Serialize(shown, bm) != mixed) fails++;

            // Titles
            if (back.Title != "") fails++;
            var titled = new NoteData { Id = "a", Color = "pink", Title = " Groceries\nweek 2 ", Modified = n.Modified, By = "MAC", Text = n.Text };
            string titledFile = NoteFile.Serialize(titled);
            if (titledFile != "color: pink\ntitle: Groceries week 2\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes") fails++;
            if (NoteFile.Parse("a", titledFile).Title != "Groceries week 2") fails++;

            // Text colors
            var cb = new List<TextSpan>();
            var c1 = new List<InkSpan>();
            if (Markup.Parse("a {red}b{/red} c", cb, c1) != "a b c" || cb.Count != 0 || c1.Count != 1 || c1[0].Start != 2 || c1[0].Length != 1 || c1[0].Name != "red") fails++;
            var c0 = new List<InkSpan>();
            if (Markup.Parse("{red}x", new List<TextSpan>(), c0) != "{red}x" || c0.Count != 0) fails++;
            if (Markup.Parse("{pink}x{/pink}", new List<TextSpan>(), new List<InkSpan>()) != "{pink}x{/pink}") fails++;
            if (Markup.Parse("{red}{/red}", new List<TextSpan>(), new List<InkSpan>()) != "{red}{/red}") fails++;
            var c2b = new List<TextSpan>();
            var c2 = new List<InkSpan>();
            if (Markup.Parse("{blue}**b**{/blue}", c2b, c2) != "b" || c2b.Count != 1 || c2b[0].Start != 0 || c2b[0].Length != 1
                || c2.Count != 1 || c2[0].Start != 0 || c2[0].Length != 1 || c2[0].Name != "blue") fails++;
            if (Markup.Serialize("ab cd", new List<TextSpan>(), new List<InkSpan> { new InkSpan(0, 2, "green") }) != "{green}ab{/green} cd") fails++;
            if (Markup.Serialize("abc", new List<TextSpan> { new TextSpan(1, 2) }, new List<InkSpan> { new InkSpan(0, 2, "red") }) != "{red}a**b**{/red}**c**") fails++;
            if (Markup.Serialize("ab\ncd", new List<TextSpan>(), new List<InkSpan> { new InkSpan(1, 3, "purple") }) != "a{purple}b{/purple}\n{purple}c{/purple}d") fails++;
            if (Markup.Serialize("a b", new List<TextSpan>(), new List<InkSpan> { new InkSpan(1, 1, "red") }) != "a b") fails++;
            string colored = "[ ] {red}**buy** milk{/red} now\n[x] {orange}done{/orange}\n**Title** {blue}x{/blue}\nend\n";
            var qb = new List<TextSpan>();
            var qi = new List<InkSpan>();
            string qt = Markup.Parse(colored, qb, qi);
            if (qt != "\u2610 buy milk now\n\u2611 done\nTitle x\nend\n") fails++;
            if (Markup.Serialize(qt, qb, qi) != colored) fails++;
            return fails;
        }
    }

    // MARK: Look

    static class NoteColors
    {
        public static readonly string[] Names = { "yellow", "green", "pink", "purple", "blue", "gray" };
        public static readonly string[] Labels = { "Yellow", "Green", "Pink", "Purple", "Blue", "Gray" };
        static readonly int[] BackHex = { 0xFFF7AA, 0xD4F2C4, 0xFFD3E2, 0xE3D7FA, 0xCDE6FF, 0xE6E6E6 };
        static readonly int[] HeaderHex = { 0xF7E479, 0xB8E3A3, 0xF9B6CE, 0xCDBAF2, 0xA9D3FB, 0xD2D2D2 };
        public static readonly Color Ink = Color.FromArgb(34, 34, 34);
        public static readonly Color Glyph = Color.FromArgb(150, 40, 40, 40);
        // Text colors: the usual ink, then Markup.Inks. Dark enough to read on every note color.
        public static readonly string[] InkLabels = { "Black", "Red", "Orange", "Green", "Blue", "Purple" };
        static readonly int[] InkHex = { 0x222222, 0xB71C1C, 0xA03F00, 0x1B6E20, 0x0D47A1, 0x6A1B9A };

        public static int Index(string name)
        {
            int i = Array.IndexOf(Names, name);
            return i < 0 ? 0 : i;
        }

        public static Color Rgb(int hex) { return Color.FromArgb((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF); }
        public static Color Back(string name) { return Rgb(BackHex[Index(name)]); }
        public static Color Header(string name) { return Rgb(HeaderHex[Index(name)]); }

        public static Color InkColor(int k) { return Rgb(InkHex[k < 0 || k >= InkHex.Length ? 0 : k]); }

        // A checked item's text: its color, faded halfway to white
        public static Color DoneInkColor(int k)
        {
            Color c = InkColor(k);
            return Color.FromArgb((c.R + 255) / 2, (c.G + 255) / 2, (c.B + 255) / 2);
        }

        // Which text color a rich-edit color (0x00BBGGRR) is, plain or faded; 0 (the usual ink) for any other
        public static int InkIndexOf(int cr)
        {
            for (int k = 1; k < InkHex.Length; k++)
                foreach (Color c in new[] { InkColor(k), DoneInkColor(k) })
                    if ((c.R | (c.G << 8) | (c.B << 16)) == cr) return k;
            return 0;
        }

        public static Image InkSwatch(int k, float s)
        {
            int size = Math.Max(16, (int)(16 * s));
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var fill = new SolidBrush(InkColor(k)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(fill, 3 * s, 3 * s, size - 6 * s, size - 6 * s);
            }
            return bmp;
        }

        public static Image Swatch(int i, float s)
        {
            int size = Math.Max(16, (int)(16 * s));
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var fill = new SolidBrush(Rgb(BackHex[i])))
            using (var pen = new Pen(Rgb(HeaderHex[i]), Math.Max(1f, s)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(1.5f * s, 1.5f * s, size - 3 * s, size - 3 * s);
                g.FillRectangle(fill, r);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }
            return bmp;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // MARK: Settings kept on this computer only (sync folder, where each note sits on screen)

    class LocalState
    {
        readonly string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes");
        readonly Dictionary<string, string> place = new Dictionary<string, string>();
        public string Folder = "";
        public bool Dirty;

        string FilePath { get { return Path.Combine(dir, "settings.txt"); } }

        public LocalState()
        {
            try
            {
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                    if (k == "folder") Folder = v;
                    else if (k.StartsWith("note ", StringComparison.Ordinal)) place[k.Substring(5)] = v;
                }
            }
            catch { }
        }

        public bool TryGetPlacement(string id, out Rectangle r, out bool top)
        {
            r = Rectangle.Empty;
            top = false;
            string v;
            if (!place.TryGetValue(id, out v)) return false;
            string[] p = v.Split(',');
            int x, y, w, h, t;
            if (p.Length < 5 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w)
                || !int.TryParse(p[3], out h) || !int.TryParse(p[4], out t)) return false;
            r = new Rectangle(x, y, w, h);
            top = t != 0;
            return true;
        }

        public void SetPlacement(string id, Rectangle r, bool top)
        {
            string v = string.Join(",", new[] { r.X, r.Y, r.Width, r.Height, top ? 1 : 0 });
            string old;
            if (place.TryGetValue(id, out old) && old == v) return;
            place[id] = v;
            Dirty = true;
        }

        public void Forget(string id)
        {
            if (place.Remove(id)) Dirty = true;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(dir);
                var lines = new List<string> { "folder=" + Folder };
                foreach (var kv in place) lines.Add("note " + kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
                Dirty = false;
            }
            catch { }
        }
    }

    // MARK: Finding Google Drive for desktop

    static class DriveLocator
    {
        static readonly string[] MyDriveNames = { "My Drive", "Mi unidad", "Meine Ablage", "Mon Drive", "Il mio Drive", "Meu Drive", "O meu disco", "Mijn Drive" };
        static readonly string[] Skip = { "Shared drives", "Unidades compartidas", "Other computers", "Otros ordenadores", "System Volume Information" };

        // Each result is a "My Drive" folder; the notes go in its StickyNotes subfolder.
        public static List<string> Find()
        {
            var found = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType == DriveType.CDRom || d.DriveType == DriveType.Network || !d.IsReady) continue;
                    string hit = MyDrive(d.RootDirectory.FullName, d.VolumeLabel == "Google Drive");
                    if (hit != null && !found.Contains(hit)) found.Add(hit);
                }
                catch { }
            }
            // Older "mirror" setups keep the Drive folder in the user profile.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string sub in new[] { "My Drive", "Google Drive" })
            {
                string p = Path.Combine(home, sub);
                if (!Directory.Exists(p)) continue;
                string hit = MyDrive(p, false) ?? p;
                if (!found.Contains(hit)) found.Add(hit);
            }
            return found;
        }

        static string MyDrive(string root, bool anySubfolder)
        {
            foreach (string name in MyDriveNames)
            {
                string p = Path.Combine(root, name);
                if (Directory.Exists(p)) return p;
            }
            if (!anySubfolder) return null;
            foreach (string p in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(p);
                if (name.StartsWith(".") || name.StartsWith("$") || Array.IndexOf(Skip, name) >= 0) continue;
                return p;
            }
            return null;
        }
    }

    class FolderPicker : Form
    {
        readonly ListBox list = new ListBox();
        string chosen;

        public static string Pick(string intro, List<string> candidates, float s)
        {
            using (var f = new FolderPicker(intro, candidates, s))
                return f.ShowDialog() == DialogResult.OK ? f.chosen : null;
        }

        FolderPicker(string intro, List<string> candidates, float s)
        {
            Text = "Sticky Notes: sync folder";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = IconArt.SmallIcon(s);

            int W = (int)(500 * s), pad = (int)(14 * s), bh = (int)(30 * s), bw = (int)(96 * s);
            var label = new Label { Text = intro, AutoSize = true, MaximumSize = new Size(W - 2 * pad, 0), Location = new Point(pad, pad) };
            Controls.Add(label);
            int y = pad + label.GetPreferredSize(new Size(W - 2 * pad, 0)).Height + pad;

            list.IntegralHeight = false;
            list.SetBounds(pad, y, W - 2 * pad, (int)(100 * s));
            foreach (string c in candidates) list.Items.Add(c);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.DoubleClick += delegate { Accept(); };
            Controls.Add(list);
            y += list.Height + pad;

            var other = new Button { Text = "Other folder…" };
            other.SetBounds(pad, y, (int)(130 * s), bh);
            other.Click += delegate { Browse(); };
            var ok = new Button { Text = "OK" };
            ok.SetBounds(W - pad - 2 * bw - pad / 2, y, bw, bh);
            ok.Click += delegate { Accept(); };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(W - pad - bw, y, bw, bh);
            Controls.AddRange(new Control[] { other, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(W, y + bh + pad);
        }

        void Accept()
        {
            if (list.SelectedItem == null) { Browse(); return; }
            chosen = (string)list.SelectedItem;
            DialogResult = DialogResult.OK;
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Choose your Google Drive folder (My Drive). Notes are kept in a StickyNotes folder inside it.";
                d.ShowNewFolderButton = true;
                if (list.SelectedItem != null) d.SelectedPath = (string)list.SelectedItem;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                chosen = d.SelectedPath;
                DialogResult = DialogResult.OK;
            }
        }
    }

    // MARK: The app: tray icon, the notes folder and the open notes

    class NotesApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaStickyNotes";
        public static readonly string Machine = Environment.MachineName;
        const string Welcome = "Welcome to Sticky Notes!\n\nWrite anything here. Notes save as you type and sync through your Google Drive folder, so they also appear on your other computer.\n\n+  makes a new note.\n☐  makes a checklist: click a box to check it.\nB  makes the selected text bold.\nA  colors the selected text.\n⋯  changes the color or deletes the note.\nDouble-click the top bar to give a note a title.\nDrag the top bar to move a note, and an edge or the corner to resize it.";

        readonly LocalState local = new LocalState();
        readonly Dictionary<string, NoteForm> notes = new Dictionary<string, NoteForm>();
        // File stamp (time + size) of each note file as last read or written, to spot changes.
        readonly Dictionary<string, string> seen = new Dictionary<string, string>();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer poll = new Timer();
        readonly Control marshal = new Control();
        readonly ToolStripMenuItem startItem;
        readonly bool fixedFolder;
        public readonly float S;
        string folder;
        bool listedOnce, offerWelcome;
        int cascade;

        public NotesApp(string folderOverride, EventWaitHandle showEvent)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
            IntPtr unused = marshal.Handle; // lets the show event post to this thread
            ThreadPool.RegisterWaitForSingleObject(showEvent, delegate { marshal.BeginInvoke((MethodInvoker)ShowAll); }, null, -1, false);

            var menu = new ContextMenuStrip();
            menu.Items.Add("New note", null, delegate { NewNote(null); });
            menu.Items.Add("Show all notes", null, delegate { ShowAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Open notes folder", null, delegate { OpenFolder(); });
            menu.Items.Add("Change sync folder…", null, delegate { ChangeFolder(); });
            startItem = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            menu.Items.Add(startItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About Sticky Notes", null, delegate { About(); });
            menu.Items.Add("Quit", null, delegate { Quit(); });
            menu.Opening += delegate { startItem.Checked = StartsWithWindows(); };
            RefreshStartupCopy();
            tray.ContextMenuStrip = menu;
            tray.Icon = IconArt.SmallIcon(S);
            tray.Text = "Sticky Notes";
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowAll(); };
            tray.Visible = true;

            fixedFolder = folderOverride != null;
            if (fixedFolder) folder = folderOverride;
            else if (local.Folder != "") folder = local.Folder;
            else
            {
                offerWelcome = true;
                folder = FirstRunFolder();
                local.Folder = folder;
                local.Save();
            }

            poll.Interval = 2500;
            poll.Tick += delegate { Scan(); if (local.Dirty) local.Save(); };
            poll.Start();
            Scan();
            if (offerWelcome)
                tray.ShowBalloonTip(8000, "Sticky Notes", "Your notes sync through " + folder + ". Right-click this icon for options.", ToolTipIcon.Info);
            else if (notes.Count == 0)
                tray.ShowBalloonTip(5000, "Sticky Notes", "Sticky Notes is running. Click this icon to make a note.", ToolTipIcon.Info);
        }

        // MARK: Folder

        string FirstRunFolder()
        {
            var found = DriveLocator.Find();
            string root = found.Count == 1 ? found[0] : FolderPicker.Pick(found.Count == 0
                ? "Google Drive for desktop wasn't found. Choose your Google Drive folder (My Drive), or another folder that syncs between your computers."
                : "Choose the Google Drive folder where your notes should sync. Use the same Google account on your other computer.", found, S);
            if (root != null) return NotesFolderIn(root);
            MessageBox.Show("Notes will be kept in Documents\\StickyNotes on this computer only.\n\nTo sync them, choose a Google Drive folder later: right-click the Sticky Notes icon in the notification area, then Change sync folder.",
                "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return NotesFolderIn(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        static string NotesFolderIn(string chosen)
        {
            chosen = chosen.TrimEnd('\\');
            return string.Equals(Path.GetFileName(chosen), "StickyNotes", StringComparison.OrdinalIgnoreCase) ? chosen : Path.Combine(chosen, "StickyNotes");
        }

        // The folder is only created when its parent exists, so a Drive that hasn't started yet
        // is waited for instead of being replaced by an empty local folder.
        bool EnsureFolder()
        {
            try
            {
                if (Directory.Exists(folder)) return true;
                string parent = Path.GetDirectoryName(folder);
                if (parent == null || !Directory.Exists(parent)) return false;
                Directory.CreateDirectory(folder);
                return true;
            }
            catch { return false; }
        }

        string PathOf(string id) { return Path.Combine(folder, id + ".txt"); }

        static string Stamp(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return null;
                return fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + fi.Length.ToString(CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        public bool Write(NoteData d)
        {
            if (!EnsureFolder()) return false;
            string path = PathOf(d.Id);
            try { File.WriteAllText(path, NoteFile.Serialize(d), new UTF8Encoding(false)); }
            catch { return false; }
            string st = Stamp(path);
            if (st != null) seen[d.Id] = st;
            return true;
        }

        // Reads every note file that changed since the last look; opens new notes, updates
        // changed ones and closes notes whose file was deleted (on the other computer).
        void Scan()
        {
            if (!EnsureFolder()) { SetStatus("waiting for " + folder); return; }
            string[] files;
            try { files = Directory.GetFiles(folder, "*.txt"); }
            catch { SetStatus("can't read " + folder); return; }

            var present = new HashSet<string>();
            foreach (string f in files)
            {
                string id = Path.GetFileNameWithoutExtension(f);
                if (id.StartsWith(".")) continue;
                present.Add(id);
                string st = Stamp(f), old;
                if (st == null || (seen.TryGetValue(id, out old) && old == st)) continue;
                NoteData d;
                try { d = NoteFile.Parse(id, File.ReadAllText(f, Encoding.UTF8)); }
                catch { continue; } // still being written: try again next time
                seen[id] = st;
                NoteForm form;
                if (notes.TryGetValue(id, out form)) form.ApplyRemote(d);
                else Open(d, false, null);
            }
            foreach (string id in new List<string>(notes.Keys))
            {
                if (present.Contains(id)) continue;
                NoteForm form = notes[id];
                if (form.Dirty) { form.SaveNow(); continue; } // edited here after it was deleted elsewhere: keep it
                notes.Remove(id);
                seen.Remove(id);
                form.CloseQuietly();
            }

            if (!listedOnce)
            {
                listedOnce = true;
                if (offerWelcome && notes.Count == 0) NewNoteWith(Welcome, "yellow", null, true);
            }
            SetStatus(notes.Count == 1 ? "1 note" : notes.Count + " notes");
        }

        void SetStatus(string s)
        {
            string t = "Sticky Notes: " + s;
            tray.Text = t.Length > 63 ? t.Substring(0, 60) + "..." : t;
        }

        // MARK: Notes

        NoteForm Open(NoteData d, bool activate, NoteForm near)
        {
            var f = new NoteForm(this, d);
            Rectangle r;
            bool top;
            if (near == null && local.TryGetPlacement(d.Id, out r, out top) && OnScreen(r))
            {
                f.Bounds = r;
                f.TopMost = top;
            }
            else f.Bounds = DefaultBounds(near);
            notes[d.Id] = f;
            f.Show();
            if (activate) f.ActivateAndFocus();
            PlacementChanged(f);
            return f;
        }

        Rectangle DefaultBounds(NoteForm near)
        {
            var size = new Size((int)(260 * S), (int)(260 * S));
            if (near != null)
            {
                var r = new Rectangle(near.Left + (int)(30 * S), near.Top + (int)(30 * S), size.Width, size.Height);
                if (Screen.FromControl(near).WorkingArea.Contains(r)) return r;
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int step = (int)(30 * S) * (cascade++ % 8);
            return new Rectangle(wa.Right - size.Width - (int)(40 * S) - step, wa.Top + (int)(40 * S) + step, size.Width, size.Height);
        }

        static bool OnScreen(Rectangle r)
        {
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle i = Rectangle.Intersect(s.WorkingArea, r);
                if (i.Width >= 60 && i.Height >= 40) return true;
            }
            return false;
        }

        public void PlacementChanged(NoteForm f)
        {
            NoteForm known;
            if (notes.TryGetValue(f.Id, out known) && known == f) local.SetPlacement(f.Id, f.Bounds, f.TopMost);
        }

        public void NewNote(NoteForm near)
        {
            NewNoteWith("", near != null ? near.ColorName : "yellow", near, true);
        }

        // The note changed on the other computer while it was being edited here: keep the other
        // version as a new note next to it (this one is saved over the file a moment later).
        public void ConflictCopy(NoteData remote, NoteForm near)
        {
            NewNoteWith(remote.Text, remote.Color, near, false, remote.Title);
        }

        void NewNoteWith(string text, string color, NoteForm near, bool activate, string title = "")
        {
            var d = new NoteData { Id = NoteFile.NewId(), Color = color, Title = title, Text = text, Modified = NoteFile.Now(), By = Machine };
            bool saved = Write(d);
            NoteForm f = Open(d, activate, near);
            if (!saved) f.MarkDirty(); // keeps retrying until the folder is reachable
        }

        public void Delete(NoteForm f)
        {
            if (f.CurrentText.Trim().Length > 0 && MessageBox.Show(f, "Delete this note?\n\nIt moves to the Trash folder inside your notes folder.",
                "Sticky Notes", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            string src = PathOf(f.Id);
            try
            {
                if (File.Exists(src))
                {
                    string trash = Path.Combine(folder, "Trash");
                    Directory.CreateDirectory(trash);
                    File.Move(src, Path.Combine(trash, NoteFile.TrashName(f.Id)));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(f, "Couldn't delete the note.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            notes.Remove(f.Id);
            seen.Remove(f.Id);
            local.Forget(f.Id);
            f.CloseQuietly();
        }

        void ShowAll()
        {
            if (notes.Count == 0) { NewNote(null); return; }
            NoteForm last = null;
            foreach (NoteForm f in notes.Values)
            {
                // Toggling TopMost raises a window above other apps without activating it.
                bool top = f.TopMost;
                f.TopMost = true;
                f.TopMost = top;
                last = f;
            }
            last.ActivateAndFocus();
        }

        void FlushAll()
        {
            foreach (NoteForm f in notes.Values) if (f.Dirty) f.SaveNow();
        }

        // MARK: Menu commands

        void OpenFolder()
        {
            if (EnsureFolder()) Process.Start("explorer.exe", "\"" + folder + "\"");
            else MessageBox.Show("The notes folder isn't available right now:\n" + folder + "\n\nIs Google Drive running?", "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void ChangeFolder()
        {
            string root = FolderPicker.Pick("Notes are kept in a StickyNotes folder inside the folder you choose. Choose the same Google Drive folder on all your computers.\n\nNow: " + folder, DriveLocator.Find(), S);
            if (root == null) return;
            string next = NotesFolderIn(root);
            if (string.Equals(next, folder, StringComparison.OrdinalIgnoreCase)) return;
            FlushAll();
            foreach (NoteForm f in new List<NoteForm>(notes.Values)) f.CloseQuietly();
            notes.Clear();
            seen.Clear();
            string old = folder;
            folder = next;
            if (!fixedFolder) { local.Folder = next; local.Save(); }
            OfferCopy(old);
            Scan();
        }

        // Moving from a local folder to Drive (or between Drive accounts): bring the notes along.
        void OfferCopy(string from)
        {
            try
            {
                string[] old = Directory.GetFiles(from, "*.txt");
                if (old.Length == 0 || !EnsureFolder() || Directory.GetFiles(folder, "*.txt").Length > 0) return;
                if (MessageBox.Show("Copy your " + old.Length + " notes from\n" + from + "\nto the new folder?", "Sticky Notes",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                foreach (string f in old)
                {
                    string dst = Path.Combine(folder, Path.GetFileName(f));
                    if (!File.Exists(dst)) File.Copy(f, dst);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't copy the notes.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static bool StartsWithWindows()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunValue) != null;
            }
            catch { return false; }
        }

        // Windows starts the app from a copy on this computer: StickyNotes.exe is often kept in Google Drive,
        // and the G: drive isn't there yet when Windows runs its startup apps.
        static readonly string StartupExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                                         "Programs", "StickyNotes", "StickyNotes.exe");

        static bool RunningStartupCopy()
        {
            return string.Equals(Path.GetFullPath(Application.ExecutablePath), Path.GetFullPath(StartupExe), StringComparison.OrdinalIgnoreCase);
        }

        // Puts this version in the startup copy and points Windows at it
        static void InstallStartupCopy(RegistryKey k)
        {
            if (!RunningStartupCopy())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StartupExe));
                File.Copy(Application.ExecutablePath, StartupExe, true);
            }
            k.SetValue(RunValue, "\"" + StartupExe + "\"");
        }

        // When Start with Windows is on and this is a newer StickyNotes.exe opened from elsewhere, the copy gets
        // updated (this also moves the setting over from versions that pointed Windows at the original file).
        static void RefreshStartupCopy()
        {
            if (RunningStartupCopy()) return;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (k != null && k.GetValue(RunValue) != null) InstallStartupCopy(k);
            }
            catch { }
        }

        void ToggleStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k.GetValue(RunValue) != null)
                    {
                        k.DeleteValue(RunValue, false);
                        if (!RunningStartupCopy()) try { File.Delete(StartupExe); } catch { }
                    }
                    else InstallStartupCopy(k);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't change this setting.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void About()
        {
            MessageBox.Show("Sticky Notes 1.2.2\n\nNotes folder:\n" + folder +
                "\n\nEach note is a text file in this folder. Google Drive for desktop copies it to your other computers, where Sticky Notes for Windows or Mac shows it. Deleted notes go to the Trash folder inside it." +
                "\n\n© 2026 Mauricio Mendivelso. MIT License.\nProvided \"as is\", without warranty of any kind.",
                "About Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void Quit()
        {
            poll.Stop();
            FlushAll();
            local.Save();
            foreach (NoteForm f in new List<NoteForm>(notes.Values)) f.CloseQuietly();
            tray.Visible = false;
            ExitThread();
        }
    }

    // MARK: One note on the desktop

    // The note's text box: a click on a ☐ / ☑ checks it instead of moving the cursor
    class NoteBox : RichTextBox
    {
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        const int EM_GETEVENTMASK = 0x43B, EM_SETEVENTMASK = 0x445, ENM_LINK = 0x04000000;

        public Func<Point, bool> CheckboxClick;

        // Clicks on links (marked by NoteForm, not by the text box's own detection) are reported as LinkClicked
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int mask = (int)SendMessage(Handle, EM_GETEVENTMASK, IntPtr.Zero, IntPtr.Zero);
            SendMessage(Handle, EM_SETEVENTMASK, IntPtr.Zero, (IntPtr)(mask | ENM_LINK));
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_LBUTTONDOWN = 0x201;
            if (m.Msg == WM_LBUTTONDOWN && CheckboxClick != null)
            {
                long lp = m.LParam.ToInt64();
                if (CheckboxClick(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)))) return;
            }
            base.WndProc(ref m);
        }
    }

    class NoteForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref CHARFORMAT2 lParam);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct CHARFORMAT2
        {
            public int cbSize;
            public uint dwMask, dwEffects;
            public int yHeight, yOffset, crTextColor;
            public byte bCharSet, bPitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string szFaceName;
            public short wWeight, sSpacing;
            public int crBackColor, lcid, dwReserved;
            public short sStyle, wKerning;
            public byte bUnderlineType, bAnimation, bRevAuthor, bUnderlineColor;
        }

        const int EM_GETCHARFORMAT = 0x43A, EM_SETCHARFORMAT = 0x444, SCF_SELECTION = 1, WM_SETREDRAW = 0x0B;
        const int EM_GETSCROLLPOS = 0x4DD, EM_SETSCROLLPOS = 0x4DE;
        const uint CFM_BOLD = 0x1, CFM_STRIKEOUT = 0x8, CFM_LINK = 0x20, CFM_COLOR = 0x40000000;
        const uint CFE_BOLD = 0x1, CFE_STRIKEOUT = 0x8, CFE_LINK = 0x20, CFE_AUTOCOLOR = 0x40000000;
        // Web and mail addresses; the end of a sentence (".", ")" and the like) isn't part of the link
        static readonly Regex LinkPattern = new Regex(@"\b(?:(?:https?://|www\.)[^\s<>""]+|mailto:[^\s<>""]+|[\w.+-]+@[\w-]+(?:\.[\w-]+)+)",
                                                      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        readonly NotesApp app;
        readonly NoteBox box = new NoteBox();
        readonly Timer saveTimer = new Timer();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ContextMenuStrip inkMenu = new ContextMenuStrip();
        readonly ToolStripMenuItem topItem, titleItem;
        readonly Font titleFont;
        readonly float S;
        NoteData data; // the version last read from or written to the file
        bool loading, quiet, restyling;
        int hot = -1;
        int lastInk = 1; // shown under the A button
        TextBox titleBox; // while the title is being edited

        public bool Dirty { get; private set; }
        public string Id { get { return data.Id; } }
        public string ColorName { get { return data.Color; } }
        public string CurrentText { get { return box.Text; } }

        int HeaderH { get { return (int)(30 * S); } }
        int Edge { get { return Math.Max(4, (int)(6 * S)); } }
        int Pad { get { return (int)(10 * S); } }
        int ButtonSize { get { return HeaderH - (int)(6 * S); } }
        Rectangle PlusRect { get { return new Rectangle((int)(4 * S), (HeaderH - ButtonSize) / 2, ButtonSize, ButtonSize); } }
        Rectangle MenuRect { get { return new Rectangle(ClientSize.Width - (int)(4 * S) - ButtonSize, (HeaderH - ButtonSize) / 2, ButtonSize, ButtonSize); } }
        Rectangle BoldRect { get { var r = MenuRect; r.X -= ButtonSize + (int)(2 * S); return r; } }
        Rectangle InkRect { get { var r = BoldRect; r.X -= ButtonSize + (int)(2 * S); return r; } }
        Rectangle CheckRect { get { var r = InkRect; r.X -= ButtonSize + (int)(2 * S); return r; } }
        Rectangle TitleRect
        {
            get
            {
                int x = PlusRect.Right + (int)(4 * S);
                return new Rectangle(x, PlusRect.Y, Math.Max(0, CheckRect.Left - (int)(4 * S) - x), ButtonSize);
            }
        }

        public NoteForm(NotesApp app, NoteData d)
        {
            this.app = app;
            data = d;
            S = app.S;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            MinimumSize = new Size((int)(170 * S), (int)(120 * S));
            MaximizeBox = false; // a double-click on the top bar edits the title
            titleFont = new Font("Segoe UI", 12.5f * S, FontStyle.Bold, GraphicsUnit.Pixel);

            box.BorderStyle = BorderStyle.None;
            box.Font = new Font("Segoe UI", 11f);
            box.ForeColor = NoteColors.Ink;
            box.ScrollBars = RichTextBoxScrollBars.Vertical;
            box.DetectUrls = false; // RestyleLine marks the links, so they stay links through the formatting
            box.AcceptsTab = true;
            box.TextChanged += delegate
            {
                if (loading || restyling) return;
                RestyleLine(LineStart(box.SelectionStart));
                Changed();
            };
            box.LinkClicked += (s, e) => { try { Process.Start(LinkTarget(e.LinkText)); } catch { } };
            box.KeyDown += BoxKeyDown;
            box.KeyPress += BoxKeyPress;
            box.CheckboxClick = CheckboxClick;
            box.ContextMenuStrip = EditMenu();
            Controls.Add(box);

            saveTimer.Tick += delegate { SaveNow(); };

            for (int i = 0; i < NoteColors.Names.Length; i++)
            {
                string name = NoteColors.Names[i];
                menu.Items.Add(new ToolStripMenuItem(NoteColors.Labels[i], NoteColors.Swatch(i, S), delegate { SetColor(name); }));
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Checklist", null, delegate { ToggleChecklist(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+L" });
            menu.Items.Add(new ToolStripMenuItem("Bold", null, delegate { ToggleBold(); }) { ShortcutKeyDisplayString = "Ctrl+B" });
            menu.Items.Add(new ToolStripMenuItem("Text color", null, InkItems()));
            titleItem = new ToolStripMenuItem("Add title", null, delegate { BeginTitleEdit(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+T" };
            menu.Items.Add(titleItem);
            inkMenu.Items.AddRange(InkItems());
            menu.Items.Add(new ToolStripSeparator());
            topItem = new ToolStripMenuItem("Keep on top", null, delegate { TopMost = !TopMost; app.PlacementChanged(this); });
            menu.Items.Add(topItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("New note", null, delegate { app.NewNote(this); }) { ShortcutKeyDisplayString = "Ctrl+N" });
            menu.Items.Add("Delete note", null, delegate { app.Delete(this); });
            menu.Opening += delegate
            {
                topItem.Checked = TopMost;
                titleItem.Text = data.Title.Length == 0 ? "Add title" : "Edit title";
                for (int i = 0; i < NoteColors.Names.Length; i++)
                    ((ToolStripMenuItem)menu.Items[i]).Checked = NoteColors.Index(data.Color) == i;
            };

            ApplyColor();
            Deactivate += delegate
            {
                if (titleBox != null) BeginInvoke((Action)delegate { EndTitleEdit(true); });
                if (Dirty) SaveNow();
            };
            LocationChanged += delegate { app.PlacementChanged(this); };
            SizeChanged += delegate { app.PlacementChanged(this); LayoutBox(); Invalidate(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        // Notes arriving from the other computer appear without taking the keyboard focus.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // Windows 11: DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
            }
            catch { }
            LayoutBox();
        }

        // The text goes in once the text box exists, since bold and checkboxes are formats on it
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SetMarkup(data.Text);
            box.Select(box.TextLength, 0);
            UpdateTitle();
        }

        void LayoutBox()
        {
            int top = HeaderH + (int)(4 * S);
            box.SetBounds(Pad, top, Math.Max(10, ClientSize.Width - 2 * Pad), Math.Max(10, ClientSize.Height - top - Pad));
            if (titleBox != null)
            {
                Rectangle r = TitleRect;
                titleBox.SetBounds(r.X, r.Y + (r.Height - titleBox.PreferredHeight) / 2, r.Width, titleBox.PreferredHeight);
            }
        }

        // MARK: Title

        void BeginTitleEdit()
        {
            if (titleBox != null) return;
            var t = new TextBox { BorderStyle = BorderStyle.None, Font = titleFont, Text = data.Title,
                                  BackColor = NoteColors.Back(data.Color), ForeColor = NoteColors.Ink };
            bool cancelled = false;
            // Enter keeps the title and Esc puts the old one back; both go on to the note's text
            t.KeyDown += (s, e) =>
            {
                if (e.KeyCode != Keys.Enter && e.KeyCode != Keys.Escape) return;
                cancelled = e.KeyCode == Keys.Escape;
                e.Handled = e.SuppressKeyPress = true;
                box.Focus();
            };
            t.Leave += delegate { BeginInvoke((Action)delegate { EndTitleEdit(!cancelled); }); };
            titleBox = t;
            Controls.Add(t);
            LayoutBox();
            Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH));
            Activate();
            t.Focus();
            t.SelectAll();
        }

        void EndTitleEdit(bool keep)
        {
            TextBox t = titleBox;
            if (t == null) return;
            titleBox = null;
            string value = t.Text;
            Controls.Remove(t);
            t.Dispose();
            if (keep) SetTitle(value);
            Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH));
        }

        void SetTitle(string t)
        {
            string clean = NoteFile.CleanTitle(t);
            if (clean == data.Title) return;
            data.Title = clean;
            UpdateTitle();
            SaveNow();
        }

        void ApplyColor()
        {
            BackColor = NoteColors.Back(data.Color);
            box.BackColor = BackColor;
            Invalidate();
        }

        // The window's name (on the taskbar's Alt+Tab list): the title, or else the first line
        void UpdateTitle()
        {
            if (data.Title.Length > 0) { Text = data.Title.Length > 60 ? data.Title.Substring(0, 60) : data.Title; return; }
            string t = box.Text.Trim();
            int nl = t.IndexOf('\n');
            if (nl >= 0) t = t.Substring(0, nl);
            if (Markup.HasBox(t, 0)) t = t.Substring(2);
            if (t.Length > 60) t = t.Substring(0, 60);
            Text = t.Length == 0 ? "Sticky note" : t;
        }

        public void ActivateAndFocus()
        {
            Activate();
            box.Focus();
            box.Select(box.TextLength, 0);
        }

        void Changed()
        {
            Dirty = true;
            saveTimer.Stop();
            saveTimer.Interval = 700;
            saveTimer.Start();
            UpdateTitle();
        }

        // MARK: Checkboxes, bold and text colors (the text box keeps only these, so both apps show the same thing)

        // Runs an action that moves the selection around, then puts the selection, scroll and drawing back
        void Quietly(Action a)
        {
            if (!box.IsHandleCreated) { a(); return; }
            int ss = box.SelectionStart, sl = box.SelectionLength;
            var scroll = Point.Empty;
            SendMessage(box.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scroll);
            SendMessage(box.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            bool was = restyling;
            restyling = true;
            try { a(); }
            finally
            {
                box.Select(ss, sl);
                SendMessage(box.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scroll);
                restyling = was;
                SendMessage(box.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                box.Invalidate();
            }
        }

        CHARFORMAT2 GetFormat()
        {
            var cf = new CHARFORMAT2 { cbSize = Marshal.SizeOf(typeof(CHARFORMAT2)) };
            SendMessage(box.Handle, EM_GETCHARFORMAT, (IntPtr)SCF_SELECTION, ref cf);
            return cf;
        }

        void SetFormat(uint mask, uint effects, Color color)
        {
            var cf = new CHARFORMAT2 { cbSize = Marshal.SizeOf(typeof(CHARFORMAT2)), dwMask = mask, dwEffects = effects,
                                       crTextColor = color.R | (color.G << 8) | (color.B << 16) };
            SendMessage(box.Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, ref cf);
        }

        // The note as it is saved: plain text with "[ ] ", "[x] ", "**" and "{red}" markers
        string MarkupText
        {
            get
            {
                string t = box.Text;
                var bold = new List<TextSpan>();
                var ink = new List<InkSpan>();
                if (box.IsHandleCreated)
                    Quietly(delegate
                    {
                        int start = 0;
                        while (start <= t.Length)
                        {
                            int end = t.IndexOf('\n', start);
                            if (end < 0) end = t.Length;
                            BoldSpans(start, end - start, bold);
                            var runs = new List<int[]>();
                            InkRuns(start, end - start, runs);
                            foreach (int[] r in runs) if (r[2] > 0) ink.Add(new InkSpan(r[0], r[1], Markup.Inks[r[2] - 1]));
                            start = end + 1;
                        }
                    });
                return Markup.Serialize(t, bold, ink);
            }
        }

        // Finds the runs of one text color in a stretch of text, as { start, length, color number }
        void InkRuns(int start, int length, List<int[]> runs)
        {
            if (length <= 0) return;
            box.Select(start, length);
            var cf = GetFormat();
            if ((cf.dwMask & CFM_COLOR) != 0 || length == 1)
            {
                int k = (cf.dwMask & CFM_COLOR) != 0 && (cf.dwEffects & CFE_AUTOCOLOR) == 0 ? NoteColors.InkIndexOf(cf.crTextColor) : 0;
                int n = runs.Count;
                if (n > 0 && runs[n - 1][2] == k && runs[n - 1][0] + runs[n - 1][1] == start) runs[n - 1][1] += length;
                else runs.Add(new[] { start, length, k });
                return;
            }
            int half = length / 2;
            InkRuns(start, half, runs);
            InkRuns(start + half, length - half, runs);
        }

        // Crosses out and fades a checked item's text (or brings it back), keeping each run's color
        void Recolor(int start, int length, bool done)
        {
            var runs = new List<int[]>();
            InkRuns(start, length, runs);
            foreach (int[] r in runs)
            {
                box.Select(r[0], r[1]);
                SetFormat(CFM_STRIKEOUT | CFM_COLOR, done ? CFE_STRIKEOUT : 0, done ? NoteColors.DoneInkColor(r[2]) : NoteColors.InkColor(r[2]));
            }
        }

        // Applies parsed bold and colors to the text that starts at index "at"
        void ApplyMarks(int at, int length, List<TextSpan> bold, List<InkSpan> ink)
        {
            box.Select(at, length);
            SetFormat(CFM_BOLD | CFM_COLOR | CFM_STRIKEOUT, 0, NoteColors.Ink);
            foreach (var r in bold)
            {
                box.Select(at + r.Start, r.Length);
                SetFormat(CFM_BOLD, CFE_BOLD, NoteColors.Ink);
            }
            foreach (var r in ink)
            {
                box.Select(at + r.Start, r.Length);
                SetFormat(CFM_COLOR, 0, NoteColors.InkColor(Array.IndexOf(Markup.Inks, r.Name) + 1));
            }
        }

        // Colors the selected text, or what is typed next; 0 is the usual ink
        void SetInk(int k)
        {
            lastInk = k;
            Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH));
            int ss = box.SelectionStart, sl = box.SelectionLength;
            if (sl == 0)
            {
                SetFormat(CFM_COLOR, 0, NoteColors.InkColor(k));
                box.Focus();
                return;
            }
            string t = box.Text;
            Quietly(delegate
            {
                for (int start = LineStart(ss); start < ss + sl; )
                {
                    int end = LineEnd(start);
                    bool boxed = Markup.HasBox(t, start);
                    int from = Math.Max(ss, boxed ? start + 2 : start), to = Math.Min(ss + sl, end);
                    if (to > from)
                    {
                        box.Select(from, to - from);
                        SetFormat(CFM_COLOR, 0, boxed && t[start] == Markup.Checked ? NoteColors.DoneInkColor(k) : NoteColors.InkColor(k));
                    }
                    start = end + 1;
                }
            });
            Changed();
            box.Focus();
        }

        ToolStripItem[] InkItems()
        {
            var items = new ToolStripItem[NoteColors.InkLabels.Length];
            for (int i = 0; i < items.Length; i++)
            {
                int k = i;
                items[i] = new ToolStripMenuItem(NoteColors.InkLabels[i], NoteColors.InkSwatch(i, S), delegate { SetInk(k); });
            }
            return items;
        }

        // Finds the bold runs in a stretch of text, splitting it until each part is all bold or not bold
        void BoldSpans(int start, int length, List<TextSpan> bold)
        {
            if (length <= 0) return;
            box.Select(start, length);
            var cf = GetFormat();
            if ((cf.dwMask & CFM_BOLD) != 0)
            {
                if ((cf.dwEffects & CFE_BOLD) != 0) bold.Add(new TextSpan(start, length));
                return;
            }
            if (length == 1) return;
            int half = length / 2;
            BoldSpans(start, half, bold);
            BoldSpans(start + half, length - half, bold);
        }

        void SetMarkup(string m)
        {
            var bold = new List<TextSpan>();
            var ink = new List<InkSpan>();
            string t = Markup.Parse(m, bold, ink);
            loading = true;
            box.Text = t;
            Quietly(delegate { ApplyMarks(0, box.TextLength, bold, ink); });
            RestyleAll();
            box.ClearUndo();
            loading = false;
        }

        int LineStart(int i)
        {
            string t = box.Text;
            i = Math.Min(i, t.Length);
            return i <= 0 ? 0 : t.LastIndexOf('\n', i - 1) + 1;
        }

        int LineEnd(int start)
        {
            int end = box.Text.IndexOf('\n', start);
            return end < 0 ? box.TextLength : end;
        }

        void RestyleAll()
        {
            string t = box.Text;
            int start = 0;
            while (start <= t.Length)
            {
                RestyleLine(start);
                int end = t.IndexOf('\n', start);
                if (end < 0) break;
                start = end + 1;
            }
        }

        // Where the links are in a line of text
        static List<TextSpan> FindLinks(string line)
        {
            var links = new List<TextSpan>();
            foreach (Match m in LinkPattern.Matches(line))
            {
                string u = m.Value.TrimEnd('.', ',', ';', ':', '!', '?', ')', ']', '}', '\'', '"');
                if (u.Length > 4) links.Add(new TextSpan(m.Index, u.Length));
            }
            return links;
        }

        // What a click on a link opens
        static string LinkTarget(string link)
        {
            if (link.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) return "http://" + link;
            if (link.IndexOf("://", StringComparison.Ordinal) < 0 && !link.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) && link.Contains("@"))
                return "mailto:" + link;
            return link;
        }

        // Finds the runs of link text in a stretch of text, joining neighbors
        void LinkSpans(int start, int length, List<TextSpan> links)
        {
            if (length <= 0) return;
            box.Select(start, length);
            var cf = GetFormat();
            if ((cf.dwMask & CFM_LINK) != 0)
            {
                if ((cf.dwEffects & CFE_LINK) == 0) return;
                int n = links.Count;
                if (n > 0 && links[n - 1].Start + links[n - 1].Length == start) links[n - 1] = new TextSpan(links[n - 1].Start, links[n - 1].Length + length);
                else links.Add(new TextSpan(start, length));
                return;
            }
            if (length == 1) return;
            int half = length / 2;
            LinkSpans(start, half, links);
            LinkSpans(start + half, length - half, links);
        }

        // A checked item's text is faded and crossed out; everything else keeps its own color.
        // Web and mail addresses become links you can click.
        // The format only changes when it has to, so typing doesn't fill the undo list with format changes.
        void RestyleLine(int start)
        {
            if (!box.IsHandleCreated) return;
            string t = box.Text;
            int end = LineEnd(start);
            bool done = Markup.HasBox(t, start) && t[start] == Markup.Checked;
            Quietly(delegate
            {
                int from = done ? start + 2 : start;
                if (end > from)
                {
                    box.Select(from, end - from);
                    var cf = GetFormat();
                    bool struck = (cf.dwMask & CFM_STRIKEOUT) != 0 && (cf.dwEffects & CFE_STRIKEOUT) != 0;
                    bool plain = (cf.dwMask & CFM_STRIKEOUT) != 0 && (cf.dwEffects & CFE_STRIKEOUT) == 0;
                    if (done && !struck) Recolor(from, end - from, true);
                    else if (!done && !plain) Recolor(from, end - from, false);
                }
                if (done)
                {
                    box.Select(start, 2);
                    var cf = GetFormat();
                    if ((cf.dwMask & CFM_STRIKEOUT) == 0 || (cf.dwEffects & CFE_STRIKEOUT) != 0) SetFormat(CFM_STRIKEOUT | CFM_COLOR, 0, NoteColors.Ink);
                }
                var want = FindLinks(t.Substring(start, end - start));
                var have = new List<TextSpan>();
                LinkSpans(start, end - start, have);
                bool same = want.Count == have.Count;
                for (int i = 0; same && i < want.Count; i++) same = start + want[i].Start == have[i].Start && want[i].Length == have[i].Length;
                if (!same)
                {
                    box.Select(start, end - start);
                    SetFormat(CFM_LINK, 0, NoteColors.Ink);
                    foreach (var r in want)
                    {
                        box.Select(start + r.Start, r.Length);
                        SetFormat(CFM_LINK, CFE_LINK, NoteColors.Ink);
                    }
                }
            });
        }

        bool CheckboxClick(Point p)
        {
            int i = box.GetCharIndexFromPosition(p);
            string t = box.Text;
            if (!Markup.HasBox(t, i) || (i > 0 && t[i - 1] != '\n')) return false;
            Point a = box.GetPositionFromCharIndex(i), b = box.GetPositionFromCharIndex(i + 1);
            int lineH = box.Font.Height;
            if (p.X < a.X - 2 || p.X > Math.Max(b.X, a.X + lineH) + 2 || p.Y < a.Y - 2 || p.Y > a.Y + lineH + 2) return false;
            ToggleCheck(i);
            return true;
        }

        void ToggleCheck(int i)
        {
            string t = box.Text;
            if (!Markup.HasBox(t, i)) return;
            int ss = box.SelectionStart, sl = box.SelectionLength;
            restyling = true;
            box.Select(i, 1);
            box.SelectedText = (t[i] == Markup.Unchecked ? Markup.Checked : Markup.Unchecked).ToString();
            box.Select(ss, sl);
            restyling = false;
            RestyleLine(i);
            Changed();
        }

        // Adds a checkbox to each selected line, or takes them away if they all have one
        void ToggleChecklist()
        {
            string t = box.Text;
            int first = LineStart(box.SelectionStart), last = LineStart(box.SelectionStart + box.SelectionLength);
            var starts = new List<int>();
            for (int i = first; ; )
            {
                starts.Add(i);
                int end = t.IndexOf('\n', i);
                if (end < 0 || end + 1 > last) break;
                i = end + 1;
            }
            bool allBoxed = true;
            foreach (int st in starts) if (!Markup.HasBox(t, st)) allBoxed = false;
            restyling = true;
            for (int k = starts.Count - 1; k >= 0; k--)
            {
                int st = starts[k];
                if (allBoxed) { box.Select(st, 2); box.SelectedText = ""; }
                else if (!Markup.HasBox(t, st)) { box.Select(st, 0); box.SelectedText = Markup.Unchecked + " "; }
            }
            restyling = false;
            box.Select(LineEnd(LineStart(first)), 0);
            RestyleAll();
            Changed();
            box.Focus();
        }

        void ToggleBold()
        {
            var cf = GetFormat();
            bool allBold = (cf.dwMask & CFM_BOLD) != 0 && (cf.dwEffects & CFE_BOLD) != 0;
            SetFormat(CFM_BOLD, allBold ? 0 : CFE_BOLD, NoteColors.Ink);
            if (box.SelectionLength > 0) Changed();
            box.Focus();
        }

        // MARK: Saving and syncing

        public bool SaveNow()
        {
            saveTimer.Stop();
            var d = new NoteData { Id = data.Id, Color = data.Color, Title = data.Title, Text = MarkupText, Modified = NoteFile.Now(), By = NotesApp.Machine };
            if (app.Write(d))
            {
                data = d;
                Dirty = false;
                return true;
            }
            MarkDirty();
            return false;
        }

        public void MarkDirty()
        {
            Dirty = true;
            saveTimer.Stop();
            saveTimer.Interval = 3000;
            saveTimer.Start();
        }

        public void ApplyRemote(NoteData d)
        {
            if (d.Modified == data.Modified && d.Color == data.Color && d.Title == data.Title && d.Text == data.Text) return; // our own save
            if (Dirty) { app.ConflictCopy(d, this); return; }
            if (d.Text != MarkupText)
            {
                int caret = box.SelectionStart;
                SetMarkup(d.Text);
                box.Select(Math.Min(caret, box.TextLength), 0);
            }
            data = d;
            ApplyColor();
            UpdateTitle();
        }

        void SetColor(string name)
        {
            data.Color = name;
            ApplyColor();
            SaveNow();
        }

        public void CloseQuietly()
        {
            quiet = true;
            Dirty = false;
            saveTimer.Stop();
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Alt+F4 doesn't close a note; notes are deleted from the ⋯ menu.
            if (!quiet && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
            if (Dirty) SaveNow();
            base.OnFormClosing(e);
        }

        // MARK: Text editing

        void BoxKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert))
            {
                e.Handled = e.SuppressKeyPress = true;
                PasteMarkup();
                return;
            }
            if (e.Control && !e.Shift && e.KeyCode == Keys.B)
            {
                e.Handled = e.SuppressKeyPress = true;
                ToggleBold();
                return;
            }
            if (e.Control && e.Shift && e.KeyCode == Keys.L)
            {
                e.Handled = e.SuppressKeyPress = true;
                ToggleChecklist();
                return;
            }
            if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                e.Handled = e.SuppressKeyPress = true;
                BeginTitleEdit();
                return;
            }
            if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift && !e.Alt && ContinueChecklist())
            {
                e.Handled = e.SuppressKeyPress = true;
                return;
            }
            // Rich-edit shortcuts (alignment, line spacing, font size, italic, underline) would change only this screen.
            Keys k = e.KeyCode;
            if (e.Control && (k == Keys.L || k == Keys.E || k == Keys.R || k == Keys.J || k == Keys.D1 || k == Keys.D2 || k == Keys.D5
                || k == Keys.OemPeriod || k == Keys.Oemcomma || k == Keys.I || k == Keys.U))
                e.Handled = e.SuppressKeyPress = true;
        }

        // Enter on a checkbox line starts a new one; on an empty one it ends the list
        bool ContinueChecklist()
        {
            if (box.SelectionLength != 0) return false;
            string t = box.Text;
            int caret = box.SelectionStart, start = LineStart(caret), end = LineEnd(start);
            if (!Markup.HasBox(t, start) || caret < start + 2) return false;
            if (t.Substring(start + 2, end - start - 2).Trim().Length == 0)
            {
                box.Select(start, end - start);
                box.SelectedText = "";
            }
            else box.SelectedText = "\n" + Markup.Unchecked + " ";
            return true;
        }

        // "[ ]" or "[x]" then a space at the start of a line becomes a checkbox
        void BoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != ' ' || box.SelectionLength != 0) return;
            int caret = box.SelectionStart, start = LineStart(caret);
            if (caret - start != 3) return;
            string pre = box.Text.Substring(start, 3);
            if (pre != "[ ]" && pre != "[x]" && pre != "[X]") return;
            e.Handled = true;
            box.Select(start, 3);
            box.SelectedText = (pre == "[ ]" ? Markup.Unchecked : Markup.Checked) + " ";
        }

        // Pastes plain text; any checkbox, bold and color markers in it come in formatted
        void PasteMarkup()
        {
            try
            {
                if (!Clipboard.ContainsText()) return;
                var bold = new List<TextSpan>();
                var ink = new List<InkSpan>();
                string t = Markup.Parse(NoteFile.Normalize(Clipboard.GetText()), bold, ink);
                int at = box.SelectionStart;
                box.SelectedText = t;
                Quietly(delegate { ApplyMarks(at, t.Length, bold, ink); });
                RestyleAll();
            }
            catch { }
        }

        ContextMenuStrip EditMenu()
        {
            var m = new ContextMenuStrip();
            var undo = m.Items.Add("Undo", null, delegate { box.Undo(); });
            m.Items.Add(new ToolStripSeparator());
            var cut = m.Items.Add("Cut", null, delegate { box.Cut(); });
            var copy = m.Items.Add("Copy", null, delegate { box.Copy(); });
            var paste = m.Items.Add("Paste", null, delegate { PasteMarkup(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Bold", null, delegate { ToggleBold(); }) { ShortcutKeyDisplayString = "Ctrl+B" });
            m.Items.Add(new ToolStripMenuItem("Text color", null, InkItems()));
            m.Items.Add(new ToolStripMenuItem("Checklist", null, delegate { ToggleChecklist(); }) { ShortcutKeyDisplayString = "Ctrl+Shift+L" });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Select all", null, delegate { box.SelectAll(); });
            m.Opening += delegate
            {
                undo.Enabled = box.CanUndo;
                cut.Enabled = copy.Enabled = box.SelectionLength > 0;
                paste.Enabled = Clipboard.ContainsText();
            };
            return m;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N)
            {
                e.Handled = e.SuppressKeyPress = true;
                app.NewNote(this);
                return;
            }
            base.OnKeyDown(e);
        }

        // MARK: Top bar, moving and resizing

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(NoteColors.Header(data.Color)))
                g.FillRectangle(b, 0, 0, ClientSize.Width, HeaderH);
            DrawButton(g, PlusRect, 0);
            DrawButton(g, CheckRect, 2);
            DrawButton(g, InkRect, 4);
            DrawButton(g, BoldRect, 3);
            DrawButton(g, MenuRect, 1);
            if (titleBox == null && data.Title.Length > 0 && TitleRect.Width > 10)
                TextRenderer.DrawText(g, data.Title, titleFont, TitleRect, NoteColors.Ink,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            // Corner grip: every edge resizes the note, the corner too
            using (var pen = new Pen(Color.FromArgb(90, 0, 0, 0), Math.Max(1f, 1.2f * S)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                foreach (float k in new[] { 4f, 8f, 12f })
                {
                    float w = ClientSize.Width - 3 * S, h = ClientSize.Height - 3 * S;
                    g.DrawLine(pen, w, h - k * S, w - k * S, h);
                }
        }

        void DrawButton(Graphics g, Rectangle r, int which)
        {
            if (hot == which)
                using (var b = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                using (var p = NoteColors.Round(r, 4 * S))
                    g.FillPath(b, p);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, a = r.Width * 0.24f;
            using (var pen = new Pen(NoteColors.Glyph, Math.Max(1.5f, 1.6f * S)) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var b = new SolidBrush(NoteColors.Glyph))
            {
                switch (which)
                {
                    case 0:
                        g.DrawLine(pen, cx - a, cy, cx + a, cy);
                        g.DrawLine(pen, cx, cy - a, cx, cy + a);
                        break;
                    case 1:
                        float dot = Math.Max(2.5f, 3f * S);
                        for (int i = -1; i <= 1; i++) g.FillEllipse(b, cx + i * a - dot / 2, cy - dot / 2, dot, dot);
                        break;
                    case 2:
                        float h = 5.5f * S;
                        using (var thin = new Pen(NoteColors.Glyph, Math.Max(1.2f, 1.3f * S)))
                        using (var p = NoteColors.Round(new RectangleF(cx - h, cy - h, 2 * h, 2 * h), 2 * S))
                            g.DrawPath(thin, p);
                        g.DrawLines(pen, new[] { new PointF(cx - h + 2.5f * S, cy), new PointF(cx - h + 4.7f * S, cy + h - 2.5f * S),
                                                 new PointF(cx + h - 2.2f * S, cy - h + 2.5f * S) });
                        break;
                    case 3:
                        using (var f = new Font("Segoe UI", 10.5f * S, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("B", f, b, new RectangleF(r.X, r.Y, r.Width, r.Height), sf);
                        break;
                    case 4: // "A" over a bar in the text color picked last
                        using (var f = new Font("Segoe UI", 10.5f * S, FontStyle.Bold, GraphicsUnit.Pixel))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("A", f, b, new RectangleF(r.X, r.Y - 2 * S, r.Width, r.Height), sf);
                        using (var bar = new SolidBrush(NoteColors.InkColor(lastInk)))
                            g.FillRectangle(bar, cx - 6 * S, r.Bottom - 6 * S, 12 * S, 3 * S);
                        break;
                }
            }
        }

        int HitButton(Point p)
        {
            if (PlusRect.Contains(p)) return 0;
            if (MenuRect.Contains(p)) return 1;
            if (CheckRect.Contains(p)) return 2;
            if (BoldRect.Contains(p)) return 3;
            if (InkRect.Contains(p)) return 4;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitButton(e.Location);
            if (h != hot) { hot = h; Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH)); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hot != -1) { hot = -1; Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH)); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = HitButton(e.Location);
            if (e.Button == MouseButtons.Right) { menu.Show(this, e.Location); return; }
            if (e.Button != MouseButtons.Left) return;
            if (h == 0) app.NewNote(this);
            else if (h == 1) menu.Show(this, new Point(MenuRect.Left, MenuRect.Bottom));
            else if (h == 2) ToggleChecklist();
            else if (h == 3) ToggleBold();
            else if (h == 4) inkMenu.Show(this, new Point(InkRect.Left, InkRect.Bottom));
            else if (e.Y < HeaderH && e.Clicks == 2) BeginTitleEdit();
            else if (e.Y < HeaderH)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN on HTCAPTION: drag the window
            }
            else box.Focus();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x84) return; // WM_NCHITTEST: the margins around the text resize the note
            long lp = m.LParam.ToInt64();
            Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            int e = Edge, w = ClientSize.Width, h = ClientSize.Height;
            bool left = p.X < e, right = p.X >= w - e, top = p.Y < Math.Max(3, (int)(4 * S)), bottom = p.Y >= h - e;
            bool corner = p.X >= w - 3 * e && p.Y >= h - 3 * e && !box.Bounds.Contains(p);
            int ht = 0;
            if (corner || (bottom && right)) ht = 17;
            else if (top && left) ht = 13;
            else if (top && right) ht = 14;
            else if (bottom && left) ht = 16;
            else if (left) ht = 10;
            else if (right) ht = 11;
            else if (top) ht = 12;
            else if (bottom) ht = 15;
            if (ht != 0) m.Result = (IntPtr)ht;
        }
    }

    // MARK: Icon (used by build.ps1: StickyNotes --write-icon <file>, and for the tray)

    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.1f;
            var r = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            float fold = r.Width * 0.28f;
            using (var body = new GraphicsPath())
            {
                body.AddPolygon(new[] {
                    new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom - fold),
                    new PointF(r.Right - fold, r.Bottom), new PointF(r.Left, r.Bottom) });
                using (var b = new SolidBrush(NoteColors.Rgb(0xFFE25A))) g.FillPath(b, body);
                g.SetClip(body);
                using (var b = new SolidBrush(NoteColors.Rgb(0xF0C83C))) g.FillRectangle(b, r.Left, r.Top, r.Width, r.Height * 0.2f);
                g.ResetClip();
            }
            using (var b = new SolidBrush(NoteColors.Rgb(0xE6BE32)))
                g.FillPolygon(b, new[] { new PointF(r.Right, r.Bottom - fold), new PointF(r.Right - fold, r.Bottom - fold), new PointF(r.Right - fold, r.Bottom) });
            if (size >= 24)
                using (var pen = new Pen(NoteColors.Rgb(0x96781E), s * 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float[,] lines = { { 0.42f, 0.62f }, { 0.58f, 0.62f }, { 0.74f, 0.4f } };
                    for (int i = 0; i < 3; i++)
                    {
                        float y = r.Top + lines[i, 0] * r.Height;
                        g.DrawLine(pen, r.Left + 0.14f * r.Width, y, r.Left + (0.14f + lines[i, 1]) * r.Width, y);
                    }
                }
        }

        public static Icon SmallIcon(float s)
        {
            int size = Math.Max(16, (int)Math.Round(16 * s));
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) Draw(g, size);
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        public static void WriteIco(string path)
        {
            int[] sizes = { 256, 64, 48, 32, 24, 16 };
            var pngs = new List<byte[]>();
            foreach (int size in sizes)
                using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) Draw(g, size);
                    using (var ms = new MemoryStream())
                    {
                        bmp.Save(ms, ImageFormat.Png);
                        pngs.Add(ms.ToArray());
                    }
                }
            using (var fs = File.Create(path))
            using (var w = new BinaryWriter(fs))
            {
                w.Write((short)0);
                w.Write((short)1);
                w.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                    w.Write((short)1); w.Write((short)32);
                    w.Write(pngs[i].Length); w.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (var p in pngs) w.Write(p);
            }
        }
    }
}
