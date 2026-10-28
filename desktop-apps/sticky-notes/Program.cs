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
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]

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

    // Tags, kept in the note text as "[#Delayed]" (a tag from the tag list) and "[@2026-10-30]" (a date).
    // On screen each one is a small colored label. The token is what is between the brackets.
    struct TagSpan
    {
        public int Start, Length;
        public string Token;
        public TagSpan(int start, int length, string token) { Start = start; Length = length; Token = token; }
    }

    static class Tags
    {
        static readonly Regex Pattern = new Regex(@"\[(#[^\[\]{}*|:\n]{1,24}|@[0-9]{4}-[0-9]{2}-[0-9]{2})\]", RegexOptions.CultureInvariant);
        static readonly string[] Months = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

        public static List<TagSpan> Find(string text)
        {
            var tags = new List<TagSpan>();
            foreach (Match m in Pattern.Matches(text))
            {
                string token = m.Groups[1].Value;
                if (token[0] == '#' && CleanName(token.Substring(1)) != token.Substring(1)) continue;
                int y, mo, d;
                if (token[0] == '@' && !Date(token, out y, out mo, out d)) continue;
                tags.Add(new TagSpan(m.Index, m.Length, token));
            }
            return tags;
        }

        // "@2026-10-30" as year, month and day, if it is a real date
        public static bool Date(string token, out int y, out int mo, out int d)
        {
            y = mo = d = 0;
            string[] parts = token.Length == 11 && token[0] == '@' ? token.Substring(1).Split('-') : new string[0];
            if (parts.Length != 3 || !int.TryParse(parts[0], out y) || !int.TryParse(parts[1], out mo) || !int.TryParse(parts[2], out d)) return false;
            if (mo < 1 || mo > 12) return false;
            bool leap = y % 4 == 0 && (y % 100 != 0 || y % 400 == 0);
            int days = (new[] { 31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31 })[mo - 1];
            return d >= 1 && d <= days;
        }

        public static string DateToken(DateTime t)
        {
            return "@" + t.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        // What the label shows: the tag's name, or the date as "Oct 30" (and the year when it isn't this one)
        public static string Label(string token, int thisYear)
        {
            int y, mo, d;
            if (!Date(token, out y, out mo, out d)) return token.Substring(1);
            return Months[mo - 1] + " " + d + (y == thisYear ? "" : " " + y);
        }

        // A date label's two parts, as on a calendar page: "OCT" (and the year when it isn't this one) and "30"
        public static bool CalendarParts(string token, int thisYear, out string month, out string day)
        {
            int y, mo, d;
            month = day = null;
            if (!Date(token, out y, out mo, out d)) return false;
            month = Months[mo - 1].ToUpperInvariant() + (y == thisYear ? "" : " " + y);
            day = d.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        // A tag name is one short line, without the characters the note markers use
        public static string CleanName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) if ("[]{}*|:\n\r\t".IndexOf(c) < 0) sb.Append(c);
            string t = sb.ToString().Trim(' ');
            if (t.Length > 24) t = t.Substring(0, 24);
            return t.Trim(' ');
        }
    }

    // The tag list, shared by both computers: "tags.conf" in the notes folder, one "name: color" a line
    class TagDef
    {
        public string Name, Color;
        public TagDef(string name, string color) { Name = name; Color = color; }
    }

    static class TagList
    {
        public const string FileName = "tags.conf";
        public static readonly string[] Colors = { "red", "orange", "yellow", "green", "teal", "blue", "purple", "pink", "gray" };
        public static readonly string[] Labels = { "Red", "Orange", "Yellow", "Green", "Teal", "Blue", "Purple", "Pink", "Gray" };
        // Deep enough for white letters
        static readonly int[] HexOf = { 0xC62828, 0xD84315, 0xA16800, 0x2E7D32, 0x00796B, 0x1565C0, 0x6A1B9A, 0xAD1457, 0x616161 };
        public const int DateHex = 0xD32F2F, UnknownHex = 0x757575; // a date looks like a calendar page: the month on red, the day on white

        public static List<TagDef> Parse(string text)
        {
            var defs = new List<TagDef>();
            foreach (string line in NoteFile.Normalize(text).Split('\n'))
            {
                string t = line.Trim(' ', '\t');
                int colon = t.LastIndexOf(':');
                if (t.StartsWith("#", StringComparison.Ordinal) || colon < 0) continue;
                string name = Tags.CleanName(t.Substring(0, colon));
                string color = t.Substring(colon + 1).Trim(' ', '\t').ToLowerInvariant();
                if (name.Length == 0 || Find(defs, name) != null) continue;
                defs.Add(new TagDef(name, Array.IndexOf(Colors, color) >= 0 ? color : "gray"));
            }
            return defs;
        }

        public static string Serialize(List<TagDef> defs)
        {
            var sb = new StringBuilder("# Sticky Notes tags: one \"name: color\" a line. Colors: " + string.Join(", ", Colors) + "\n");
            foreach (TagDef d in defs) sb.Append(d.Name).Append(": ").Append(d.Color).Append('\n');
            return sb.ToString();
        }

        public static TagDef Find(List<TagDef> defs, string name)
        {
            foreach (TagDef d in defs) if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        public static int Hex(string color)
        {
            int i = Array.IndexOf(Colors, color);
            return i < 0 ? UnknownHex : HexOf[i];
        }

        public static bool Same(List<TagDef> a, List<TagDef> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (a[i].Name != b[i].Name || a[i].Color != b[i].Color) return false;
            return true;
        }

        public static Image Swatch(int hex, float s)
        {
            int size = Math.Max(16, (int)(16 * s));
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var fill = new SolidBrush(NoteColors.Rgb(hex)))
            using (var p = NoteColors.Round(new RectangleF(1.5f * s, 4 * s, size - 3 * s, size - 8 * s), 2 * s))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillPath(fill, p);
            }
            return bmp;
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

            // Tags
            var t1 = Tags.Find("[#On time] go [@2026-10-30]");
            if (t1.Count != 2 || t1[0].Start != 0 || t1[0].Length != 10 || t1[0].Token != "#On time"
                || t1[1].Start != 14 || t1[1].Length != 13 || t1[1].Token != "@2026-10-30") fails++;
            if (Tags.Find("[# x] [#a:b] [#] [@2026-13-01] [@2026-02-30] [x]").Count != 0) fails++;
            if (Tags.Find("[@2028-02-29]").Count != 1) fails++;
            if (Tags.Label("@2026-10-30", 2026) != "Oct 30" || Tags.Label("@2027-01-05", 2026) != "Jan 5 2027") fails++;
            if (Tags.Label("#Delayed", 2026) != "Delayed") fails++;
            string cm, cd;
            if (!Tags.CalendarParts("@2026-10-30", 2026, out cm, out cd) || cm != "OCT" || cd != "30") fails++;
            if (!Tags.CalendarParts("@2027-01-05", 2026, out cm, out cd) || cm != "JAN 2027" || cd != "5") fails++;
            if (Tags.CalendarParts("#Delayed", 2026, out cm, out cd)) fails++;
            if (Tags.CleanName("  On [time]: ok* ") != "On time ok") fails++;
            string tagged = "[ ] [#Delayed] **lab** report [@2026-10-30]\n[x] {red}[#Done]{/red}";
            var tb = new List<TextSpan>();
            var ti = new List<InkSpan>();
            string tt = Markup.Parse(tagged, tb, ti);
            if (Markup.Serialize(tt, tb, ti) != tagged) fails++;
            var defs = TagList.Parse("# comment\nOn time: green\nDelayed:RED\nbad line\nOdd: plaid\non time: blue\n");
            if (!TagList.Same(defs, new List<TagDef> { new TagDef("On time", "green"), new TagDef("Delayed", "red"), new TagDef("Odd", "gray") })) fails++;
            if (!TagList.Same(TagList.Parse(TagList.Serialize(defs)), defs)) fails++;
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

    // Edit tags window: a row per tag (name, color, remove), and Add tag
    class TagEditor : Form
    {
        class Row
        {
            public TextBox Name;
            public ComboBox Color;
            public Button Remove;
            public string Original;
        }

        readonly List<Row> rows = new List<Row>();
        readonly Panel list = new Panel();
        readonly float s;

        // The new list and the renamed tags ({ old name, new name }); false if cancelled
        public static bool Run(IWin32Window owner, List<TagDef> defs, float s, out List<TagDef> result, out List<string[]> renames)
        {
            result = null;
            renames = null;
            using (var f = new TagEditor(defs, s))
            {
                if (f.ShowDialog(owner) != DialogResult.OK) return false;
                result = new List<TagDef>();
                renames = new List<string[]>();
                foreach (Row r in f.rows)
                {
                    string name = Tags.CleanName(r.Name.Text);
                    if (name.Length == 0 || TagList.Find(result, name) != null) continue;
                    result.Add(new TagDef(name, TagList.Colors[Math.Max(0, r.Color.SelectedIndex)]));
                    if (r.Original.Length > 0 && r.Original != name) renames.Add(new[] { r.Original, name });
                }
                return true;
            }
        }

        TagEditor(List<TagDef> defs, float s)
        {
            this.s = s;
            Text = "Sticky Notes: tags";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            Icon = IconArt.SmallIcon(s);

            int W = (int)(420 * s), pad = (int)(14 * s), bh = (int)(30 * s), bw = (int)(96 * s);
            var label = new Label { Text = "Name each tag and pick its color. Both computers share this list. Dates come from the tag menu.",
                                    AutoSize = true, MaximumSize = new Size(W - 2 * pad, 0), Location = new Point(pad, pad) };
            Controls.Add(label);
            int y = pad + label.GetPreferredSize(new Size(W - 2 * pad, 0)).Height + pad;

            list.AutoScroll = true;
            list.BorderStyle = BorderStyle.FixedSingle;
            list.SetBounds(pad, y, W - 2 * pad, (int)(230 * s));
            Controls.Add(list);
            y += list.Height + pad;

            var add = new Button { Text = "Add tag" };
            add.SetBounds(pad, y, (int)(110 * s), bh);
            add.Click += delegate { AddRow("", TagList.Colors[rows.Count % TagList.Colors.Length], ""); rows[rows.Count - 1].Name.Focus(); };
            var ok = new Button { Text = "Save", DialogResult = DialogResult.OK };
            ok.SetBounds(W - pad - 2 * bw - pad / 2, y, bw, bh);
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(W - pad - bw, y, bw, bh);
            Controls.AddRange(new Control[] { add, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(W, y + bh + pad);

            foreach (TagDef d in defs) AddRow(d.Name, d.Color, d.Name);
            if (defs.Count == 0) AddRow("", "blue", "");
        }

        void AddRow(string name, string color, string original)
        {
            var r = new Row { Original = original };
            r.Name = new TextBox { Text = name, MaxLength = 24 };
            r.Color = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed };
            r.Color.Items.AddRange(TagList.Labels);
            r.Color.SelectedIndex = Math.Max(0, Array.IndexOf(TagList.Colors, color));
            r.Color.DrawItem += (sender, e) =>
            {
                e.DrawBackground();
                if (e.Index < 0) return;
                int sw = e.Bounds.Height - 4;
                using (var b = new SolidBrush(NoteColors.Rgb(TagList.Hex(TagList.Colors[e.Index]))))
                    e.Graphics.FillRectangle(b, e.Bounds.X + 3, e.Bounds.Y + 2, sw, sw);
                TextRenderer.DrawText(e.Graphics, TagList.Labels[e.Index], e.Font, new Point(e.Bounds.X + sw + 8, e.Bounds.Y + 1), e.ForeColor);
            };
            r.Remove = new Button { Text = "✕" };
            r.Remove.Click += delegate
            {
                rows.Remove(r);
                list.Controls.Remove(r.Name);
                list.Controls.Remove(r.Color);
                list.Controls.Remove(r.Remove);
                LayoutRows();
            };
            list.Controls.Add(r.Name);
            list.Controls.Add(r.Color);
            list.Controls.Add(r.Remove);
            rows.Add(r);
            LayoutRows();
        }

        void LayoutRows()
        {
            int rowH = (int)(34 * s), p = (int)(6 * s), w = list.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2 * p;
            int nameW = w * 55 / 100, colorW = w * 30 / 100, removeW = w - nameW - colorW - 2 * p;
            list.AutoScrollPosition = Point.Empty;
            for (int i = 0; i < rows.Count; i++)
            {
                int y = p + i * rowH;
                rows[i].Name.SetBounds(p, y, nameW, rows[i].Name.PreferredHeight);
                rows[i].Color.SetBounds(2 * p + nameW, y, colorW, rows[i].Name.PreferredHeight);
                rows[i].Remove.SetBounds(3 * p + nameW + colorW, y - 1, removeW, rows[i].Name.PreferredHeight + 2);
            }
        }
    }

    class NotesApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaStickyNotes";
        public static readonly string Machine = Environment.MachineName;
        const string Welcome = "Welcome to Sticky Notes!\n\nWrite anything here. Notes save as you type and sync through your Google Drive folder, so they also appear on your other computer.\n\n+  makes a new note.\n☐  makes a checklist: click a box to check it.\nB  makes the selected text bold.\nA  colors the selected text.\nThe tag button puts a tag or a date at the start or end of a line.\n⋯  changes the color or deletes the note.\nDouble-click the top bar to give a note a title.\nDrag the top bar to move a note, and an edge or the corner to resize it.";

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
        public List<TagDef> TagDefs = new List<TagDef>();
        string tagStamp; // of tags.conf as last read or written

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
            menu.Items.Add("Edit tags…", null, delegate { EditTags(null); });
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

        // MARK: Tags

        string TagsPath { get { return Path.Combine(folder, TagList.FileName); } }

        // Reads the tag list when it changed (here or on the other computer) and shows the notes' tags again
        void LoadTags()
        {
            string st = Stamp(TagsPath) ?? "none";
            if (st == tagStamp) return;
            string text = "";
            if (st != "none")
            {
                try { text = File.ReadAllText(TagsPath, Encoding.UTF8); }
                catch { return; } // still being written: try again next time
            }
            tagStamp = st;
            var defs = TagList.Parse(text);
            if (TagList.Same(defs, TagDefs)) return;
            TagDefs = defs;
            foreach (NoteForm f in notes.Values) f.RefreshTags();
        }

        public Color TagColor(string token)
        {
            if (token.StartsWith("@", StringComparison.Ordinal)) return NoteColors.Rgb(TagList.DateHex);
            TagDef d = TagList.Find(TagDefs, token.Substring(1));
            return NoteColors.Rgb(TagList.Hex(d == null ? "" : d.Color));
        }

        public void EditTags(IWin32Window owner)
        {
            List<TagDef> defs;
            List<string[]> renames;
            if (!TagEditor.Run(owner, TagDefs, S, out defs, out renames)) return;
            try
            {
                if (!EnsureFolder()) throw new IOException("The notes folder isn't available right now:\n" + folder);
                File.WriteAllText(TagsPath, TagList.Serialize(defs), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't save the tags.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            tagStamp = Stamp(TagsPath);
            TagDefs = defs;
            foreach (NoteForm f in notes.Values)
            {
                f.RenameTags(renames);
                f.RefreshTags();
            }
        }

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
            LoadTags();
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
            tagStamp = null;
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
            MessageBox.Show("Sticky Notes 1.3\n\nNotes folder:\n" + folder +
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
        const uint CFM_BOLD = 0x1, CFM_STRIKEOUT = 0x8, CFM_LINK = 0x20, CFM_BACKCOLOR = 0x04000000, CFM_COLOR = 0x40000000, CFM_SIZE = 0x80000000;
        const uint CFE_BOLD = 0x1, CFE_STRIKEOUT = 0x8, CFE_LINK = 0x20, CFE_AUTOBACKCOLOR = 0x04000000, CFE_AUTOCOLOR = 0x40000000;
        const int TextTwips = 220, TagTwips = 180, TinyTwips = 20; // 11 pt text, 9 pt tags, 1 pt for what a tag hides
        const string MarkupFormat = "StickyNotesMarkup";
        // Web and mail addresses; the end of a sentence (".", ")" and the like) isn't part of the link
        static readonly Regex LinkPattern = new Regex(@"\b(?:(?:https?://|www\.)[^\s<>""]+|mailto:[^\s<>""]+|[\w.+-]+@[\w-]+(?:\.[\w-]+)+)",
                                                      RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        readonly NotesApp app;
        readonly NoteBox box = new NoteBox();
        readonly Timer saveTimer = new Timer();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ContextMenuStrip inkMenu = new ContextMenuStrip();
        readonly ContextMenuStrip tagMenu = new ContextMenuStrip();
        readonly ToolStripMenuItem tagSub = new ToolStripMenuItem("Tag");
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
        Rectangle TagRect { get { var r = CheckRect; r.X -= ButtonSize + (int)(2 * S); return r; } }
        Rectangle TitleRect
        {
            get
            {
                int x = PlusRect.Right + (int)(4 * S);
                return new Rectangle(x, PlusRect.Y, Math.Max(0, TagRect.Left - (int)(4 * S) - x), ButtonSize);
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
            MinimumSize = new Size((int)(200 * S), (int)(120 * S));
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
            menu.Items.Add(tagSub);
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
                tagSub.DropDownItems.Clear();
                tagSub.DropDownItems.AddRange(TagItems());
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
            string t = PlainText(box.Text).Trim();
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
            SetFormat(mask, effects, color, Color.Empty, TextTwips);
        }

        void SetFormat(uint mask, uint effects, Color color, Color back, int twips)
        {
            var cf = new CHARFORMAT2 { cbSize = Marshal.SizeOf(typeof(CHARFORMAT2)), dwMask = mask, dwEffects = effects,
                                       crTextColor = color.R | (color.G << 8) | (color.B << 16),
                                       crBackColor = back.R | (back.G << 8) | (back.B << 16), yHeight = twips };
            SendMessage(box.Handle, EM_SETCHARFORMAT, (IntPtr)SCF_SELECTION, ref cf);
        }

        // MARK: Tags

        // In the text box a tag stays text: "[#Delayed]" shows as a colored block with white letters, its brackets
        // in the block's color. A date also carries its label, and the date itself is tiny and invisible:
        // "[@2026-10-30]" in the file is "[@2026-10-30|OCT 30]" here, shown as a calendar page
        // (the month in white on red, the day in red on white).
        struct BoxTag
        {
            public int Start, Length, LabelStart, LabelLength;
            public string Token;
        }

        static readonly Regex BoxTagPattern = new Regex(@"\[(#[^\[\]{}*|:\n]{1,24}|@[0-9]{4}-[0-9]{2}-[0-9]{2}\|[^\]\n]*)\]", RegexOptions.CultureInvariant);
        static readonly Regex BoxDatePattern = new Regex(@"\[@([0-9]{4}-[0-9]{2}-[0-9]{2})\|[^\]\n]*\]", RegexOptions.CultureInvariant);

        static List<BoxTag> BoxTags(string line)
        {
            var tags = new List<BoxTag>();
            foreach (Match m in BoxTagPattern.Matches(line))
            {
                string inner = m.Groups[1].Value;
                int bar = inner.IndexOf('|');
                string token = bar < 0 ? inner : inner.Substring(0, bar);
                if (Tags.Find("[" + token + "]").Count != 1) continue;
                var g = new BoxTag { Start = m.Index, Length = m.Length, Token = token };
                g.LabelStart = bar < 0 ? m.Index + 2 : m.Index + 2 + bar;
                g.LabelLength = m.Index + m.Length - 1 - g.LabelStart;
                tags.Add(g);
            }
            return tags;
        }

        // The text box's form of a tag
        static string BoxText(string token)
        {
            return "[" + token + DateLabel(token) + "]";
        }

        // "|OCT 30" for a date, nothing for a tag
        static string DateLabel(string token)
        {
            string month, day;
            return Tags.CalendarParts(token, DateTime.Now.Year, out month, out day) ? "|" + month + " " + day : "";
        }

        static string ToBox(string markup)
        {
            var tags = Tags.Find(markup);
            var sb = new StringBuilder(markup);
            for (int i = tags.Count - 1; i >= 0; i--)
                if (tags[i].Token[0] == '@') sb.Insert(tags[i].Start + tags[i].Length - 1, DateLabel(tags[i].Token));
            return sb.ToString();
        }

        static string FromBox(string text) { return BoxDatePattern.Replace(text, "[@$1]"); }

        // The text as other apps get it: each tag as "[Delayed]" or "[Oct 30]"
        static string PlainText(string text)
        {
            var sb = new StringBuilder();
            int at = 0;
            foreach (Match m in BoxTagPattern.Matches(text))
            {
                sb.Append(text, at, m.Index - at);
                string inner = m.Groups[1].Value;
                int bar = inner.IndexOf('|');
                sb.Append('[').Append(Tags.Label(bar < 0 ? inner : inner.Substring(0, bar), DateTime.Now.Year)).Append(']');
                at = m.Index + m.Length;
            }
            return sb.Append(text, at, text.Length - at).ToString();
        }

        // Finds the runs with a background color (tags) in a stretch of text
        void BackSpans(int start, int length, List<TextSpan> spans)
        {
            if (length <= 0) return;
            box.Select(start, length);
            var cf = GetFormat();
            if ((cf.dwMask & CFM_BACKCOLOR) != 0)
            {
                if ((cf.dwEffects & CFE_AUTOBACKCOLOR) != 0) return;
                int n = spans.Count;
                if (n > 0 && spans[n - 1].Start + spans[n - 1].Length == start) spans[n - 1] = new TextSpan(spans[n - 1].Start, spans[n - 1].Length + length);
                else spans.Add(new TextSpan(start, length));
                return;
            }
            if (length == 1) return;
            int half = length / 2;
            BackSpans(start, half, spans);
            BackSpans(start + half, length - half, spans);
        }

        // Shows the tags in a line, if they changed (or always, when force is set)
        void StyleTags(int start, int end, bool force)
        {
            string t = box.Text;
            var tags = BoxTags(t.Substring(start, end - start));
            var have = new List<TextSpan>();
            BackSpans(start, end - start, have);
            bool same = !force && tags.Count == have.Count;
            for (int i = 0; same && i < tags.Count; i++) same = start + tags[i].Start == have[i].Start && tags[i].Length == have[i].Length;
            if (same) return;
            foreach (var r in have)
            {
                box.Select(r.Start, r.Length);
                SetFormat(CFM_BACKCOLOR | CFM_BOLD | CFM_COLOR | CFM_SIZE, CFE_AUTOBACKCOLOR, NoteColors.Ink, Color.Empty, TextTwips);
            }
            foreach (var g in tags)
            {
                Color c = app.TagColor(g.Token);
                box.Select(start + g.Start, g.Length);
                SetFormat(CFM_BACKCOLOR | CFM_BOLD | CFM_COLOR | CFM_SIZE | CFM_LINK, CFE_BOLD, c, c, TagTwips);
                box.Select(start + g.LabelStart, g.LabelLength);
                SetFormat(CFM_COLOR, 0, Color.White);
                if (g.LabelStart - g.Start > 2)
                {
                    box.Select(start + g.Start + 2, g.LabelStart - g.Start - 2);
                    SetFormat(CFM_SIZE, 0, c, c, TinyTwips);
                }
                if (g.Token[0] == '@')
                {
                    // The day and the closing bracket on white, the day in the date's red
                    int dayAt = start + g.LabelStart + t.Substring(start + g.LabelStart, g.LabelLength).LastIndexOf(' ') + 1;
                    box.Select(dayAt, start + g.Start + g.Length - dayAt);
                    SetFormat(CFM_BACKCOLOR | CFM_COLOR, 0, Color.White, Color.White, TagTwips);
                    box.Select(dayAt, start + g.LabelStart + g.LabelLength - dayAt);
                    SetFormat(CFM_COLOR, 0, c);
                }
            }
        }

        // The tag the cursor is in or next to: before it (Backspace, Left) or after it (Delete, Right)
        int[] TagAt(int caret, bool before)
        {
            int start = LineStart(caret);
            foreach (var g in BoxTags(box.Text.Substring(start, LineEnd(start) - start)))
            {
                int a = start + g.Start, b = a + g.Length;
                if (before ? caret > a && caret <= b : caret >= a && caret < b) return new[] { a, b };
            }
            return null;
        }

        // Puts a tag at the start of the line the cursor is on (after its checkbox) or at its end
        void InsertTag(string token, bool atEnd)
        {
            string t = box.Text;
            int caret = box.SelectionStart, start = LineStart(caret), end = LineEnd(start);
            string tag = BoxText(token), ins;
            int at;
            if (atEnd)
            {
                at = end;
                ins = (end > start && t[end - 1] != ' ' ? " " : "") + tag;
            }
            else
            {
                at = start + (Markup.HasBox(t, start) ? 2 : 0);
                ins = tag + " ";
            }
            restyling = true;
            box.Select(at, 0);
            SetFormat(CFM_BOLD | CFM_COLOR | CFM_STRIKEOUT | CFM_LINK | CFM_BACKCOLOR | CFM_SIZE, CFE_AUTOBACKCOLOR, NoteColors.Ink, Color.Empty, TextTwips);
            box.SelectedText = ins;
            restyling = false;
            box.Select(atEnd ? at + ins.Length : (caret >= at ? caret + ins.Length : caret), 0);
            RestyleLine(start, true);
            Changed();
            box.Focus();
        }

        void PickDate(bool atEnd)
        {
            using (var f = new Form())
            {
                f.Text = "Date tag";
                f.Font = SystemFonts.MessageBoxFont;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = FormStartPosition.CenterParent;
                int pad = (int)(12 * S), bh = (int)(30 * S), bw = (int)(90 * S);
                var cal = new MonthCalendar { MaxSelectionCount = 1, Location = new Point(pad, pad) };
                f.Controls.Add(cal);
                var ok = new Button { Text = "Add", DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
                f.Controls.Add(ok);
                f.Controls.Add(cancel);
                f.AcceptButton = ok;
                f.CancelButton = cancel;
                f.Load += delegate
                {
                    int w = Math.Max(cal.Width, 2 * bw + pad / 2);
                    ok.SetBounds(pad + w - 2 * bw - pad / 2, cal.Bottom + pad, bw, bh);
                    cancel.SetBounds(pad + w - bw, cal.Bottom + pad, bw, bh);
                    f.ClientSize = new Size(w + 2 * pad, cal.Bottom + 2 * pad + bh);
                };
                if (f.ShowDialog(this) == DialogResult.OK) InsertTag(Tags.DateToken(cal.SelectionStart), atEnd);
            }
        }

        // The tag menu: each tag and Date…, at the start or the end of the line, then Edit tags…
        ToolStripItem[] TagItems()
        {
            var items = new List<ToolStripItem>();
            foreach (bool atEnd in new[] { false, true })
            {
                bool end = atEnd;
                items.Add(new ToolStripMenuItem(end ? "At the end of the line" : "At the start of the line") { Enabled = false });
                foreach (TagDef d in app.TagDefs)
                {
                    string token = "#" + d.Name;
                    items.Add(new ToolStripMenuItem("    " + d.Name, TagList.Swatch(TagList.Hex(d.Color), S), delegate { InsertTag(token, end); }));
                }
                items.Add(new ToolStripMenuItem("    Date…", TagList.Swatch(TagList.DateHex, S), delegate { PickDate(end); }));
                items.Add(new ToolStripSeparator());
            }
            items.Add(new ToolStripMenuItem("Edit tags…", null, delegate { app.EditTags(this); }));
            return items.ToArray();
        }

        // Shows the tags again with the tag list as it is now (new colors, renamed or removed tags)
        public void RefreshTags()
        {
            if (!box.IsHandleCreated) return;
            RestyleAll(true);
            UpdateTitle();
        }

        // Changes "[#old]" to "[#new]" for each renamed tag, and saves the note if it had any
        public void RenameTags(List<string[]> renames)
        {
            if (!box.IsHandleCreated || renames.Count == 0) return;
            string m = MarkupText, was = m;
            foreach (string[] r in renames) m = m.Replace("[#" + r[0] + "]", "[#" + r[1] + "]");
            if (m == was) return;
            int caret = box.SelectionStart;
            SetMarkup(m);
            box.Select(Math.Min(caret, box.TextLength), 0);
            SaveNow();
        }

        // Copies the selection with its markers for notes, and as plain text (tags as "[Delayed]") for other apps
        void CopySelection()
        {
            if (box.SelectionLength == 0) return;
            try
            {
                var d = new DataObject();
                d.SetData(DataFormats.UnicodeText, PlainText(box.SelectedText.Replace("\n", "\r\n")));
                d.SetData(MarkupFormat, MarkupOf(box.SelectionStart, box.SelectionLength));
                Clipboard.SetDataObject(d, true);
            }
            catch { }
        }

        // The note as it is saved: plain text with "[ ] ", "[x] ", "**", "{red}" and "[#tag]" markers
        string MarkupText { get { return MarkupOf(0, box.TextLength); } }

        // The markup of part of the text. Tags keep only their markers, never bold or color.
        string MarkupOf(int at, int length)
        {
            string t = box.Text;
            length = Math.Max(0, Math.Min(length, t.Length - at));
            var bold = new List<TextSpan>();
            var ink = new List<InkSpan>();
            var isTag = new bool[length];
            if (box.IsHandleCreated)
                Quietly(delegate
                {
                    for (int start = LineStart(at); start <= at + length; )
                    {
                        int end = t.IndexOf('\n', start);
                        if (end < 0) end = t.Length;
                        foreach (var g in BoxTags(t.Substring(start, end - start)))
                            for (int i = start + g.Start; i < start + g.Start + g.Length; i++) if (i >= at && i < at + length) isTag[i - at] = true;
                        int from = Math.Max(start, at), to = Math.Min(end, at + length);
                        if (to > from)
                        {
                            var b = new List<TextSpan>();
                            BoldSpans(from, to - from, b);
                            foreach (var r in b) bold.Add(new TextSpan(r.Start - at, r.Length));
                            var runs = new List<int[]>();
                            InkRuns(from, to - from, runs);
                            foreach (int[] r in runs) if (r[2] > 0) ink.Add(new InkSpan(r[0] - at, r[1], Markup.Inks[r[2] - 1]));
                        }
                        start = end + 1;
                    }
                });
            var plainBold = new List<TextSpan>();
            foreach (var r in bold) foreach (var p in Outside(r.Start, r.Length, isTag)) plainBold.Add(p);
            var plainInk = new List<InkSpan>();
            foreach (var r in ink) foreach (var p in Outside(r.Start, r.Length, isTag)) plainInk.Add(new InkSpan(p.Start, p.Length, r.Name));
            return FromBox(Markup.Serialize(t.Substring(at, length), plainBold, plainInk));
        }

        // The parts of a span that aren't tags
        static List<TextSpan> Outside(int start, int length, bool[] isTag)
        {
            var parts = new List<TextSpan>();
            for (int i = start; i < start + length && i < isTag.Length; )
            {
                if (isTag[i]) { i++; continue; }
                int j = i;
                while (j < start + length && j < isTag.Length && !isTag[j]) j++;
                parts.Add(new TextSpan(i, j - i));
                i = j;
            }
            return parts;
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
            SetFormat(CFM_BOLD | CFM_COLOR | CFM_STRIKEOUT | CFM_BACKCOLOR | CFM_SIZE, CFE_AUTOBACKCOLOR, NoteColors.Ink, Color.Empty, TextTwips);
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
            RestyleAll(true); // tags keep their own look
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
            string t = Markup.Parse(ToBox(m), bold, ink);
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

        void RestyleAll(bool force = false)
        {
            string t = box.Text;
            int start = 0;
            while (start <= t.Length)
            {
                RestyleLine(start, force);
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
        // Web and mail addresses become links you can click, and tags colored labels.
        // The format only changes when it has to, so typing doesn't fill the undo list with format changes.
        void RestyleLine(int start, bool force = false)
        {
            if (!box.IsHandleCreated) return;
            string t = box.Text;
            int end = LineEnd(start);
            bool done = Markup.HasBox(t, start) && t[start] == Markup.Checked;
            Quietly(delegate
            {
                bool recolored = false;
                int from = done ? start + 2 : start;
                if (end > from)
                {
                    box.Select(from, end - from);
                    var cf = GetFormat();
                    bool struck = (cf.dwMask & CFM_STRIKEOUT) != 0 && (cf.dwEffects & CFE_STRIKEOUT) != 0;
                    bool plain = (cf.dwMask & CFM_STRIKEOUT) != 0 && (cf.dwEffects & CFE_STRIKEOUT) == 0;
                    if (done && !struck) { Recolor(from, end - from, true); recolored = true; }
                    else if (!done && !plain) { Recolor(from, end - from, false); recolored = true; }
                }
                if (done)
                {
                    box.Select(start, 2);
                    var cf = GetFormat();
                    if ((cf.dwMask & CFM_STRIKEOUT) == 0 || (cf.dwEffects & CFE_STRIKEOUT) != 0) SetFormat(CFM_STRIKEOUT | CFM_COLOR, 0, NoteColors.Ink);
                }
                var want = FindLinks(t.Substring(start, end - start));
                var tags = BoxTags(t.Substring(start, end - start));
                want.RemoveAll(r => tags.Exists(g => r.Start < g.Start + g.Length && g.Start < r.Start + r.Length));
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
                StyleTags(start, end, force || recolored);
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
            if (box.SelectionLength > 0)
            {
                RestyleAll(true); // tags stay bold
                Changed();
            }
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
            if ((e.Control && !e.Shift && (e.KeyCode == Keys.C || e.KeyCode == Keys.Insert)) || (e.Control && e.KeyCode == Keys.X) || (e.Shift && e.KeyCode == Keys.Delete))
            {
                e.Handled = e.SuppressKeyPress = true;
                CopySelection();
                if (e.KeyCode == Keys.X || e.KeyCode == Keys.Delete) box.SelectedText = "";
                return;
            }
            // A tag goes as a whole: Backspace and Delete remove it, the arrows step over it
            if (!e.Control && !e.Alt && !e.Shift && box.SelectionLength == 0
                && (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete || e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
            {
                bool before = e.KeyCode == Keys.Back || e.KeyCode == Keys.Left;
                int[] g = TagAt(box.SelectionStart, before);
                if (g != null)
                {
                    e.Handled = e.SuppressKeyPress = true;
                    if (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete) { box.Select(g[0], g[1] - g[0]); box.SelectedText = ""; }
                    else box.Select(before ? g[0] : g[1], 0);
                    return;
                }
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

        // Pastes plain text; any checkbox, bold, color and tag markers in it come in formatted.
        // Text copied from a note brings its markers along.
        void PasteMarkup()
        {
            try
            {
                string text = Clipboard.ContainsData(MarkupFormat) ? Clipboard.GetData(MarkupFormat) as string : null;
                if (text == null && Clipboard.ContainsText()) text = Clipboard.GetText();
                if (text == null) return;
                var bold = new List<TextSpan>();
                var ink = new List<InkSpan>();
                string t = Markup.Parse(ToBox(NoteFile.Normalize(text)), bold, ink);
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
            var cut = m.Items.Add("Cut", null, delegate { CopySelection(); box.SelectedText = ""; });
            var copy = m.Items.Add("Copy", null, delegate { CopySelection(); });
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
                paste.Enabled = Clipboard.ContainsText() || Clipboard.ContainsData(MarkupFormat);
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
            DrawButton(g, TagRect, 5);
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
                    case 5: // a luggage tag
                        float tx0 = cx - 6.5f * S, tx1 = cx + 6.5f * S, ty0 = cy - 4.5f * S, ty1 = cy + 4.5f * S;
                        using (var thin = new Pen(NoteColors.Glyph, Math.Max(1.2f, 1.3f * S)) { LineJoin = LineJoin.Round })
                            g.DrawPolygon(thin, new[] { new PointF(tx0, cy), new PointF(tx0 + 4 * S, ty0), new PointF(tx1, ty0),
                                                        new PointF(tx1, ty1), new PointF(tx0 + 4 * S, ty1) });
                        g.FillEllipse(b, tx0 + 3.2f * S, cy - 1.2f * S, 2.4f * S, 2.4f * S);
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
            if (TagRect.Contains(p)) return 5;
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
            else if (h == 5)
            {
                tagMenu.Items.Clear();
                tagMenu.Items.AddRange(TagItems());
                tagMenu.Show(this, new Point(TagRect.Left, TagRect.Bottom));
            }
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
