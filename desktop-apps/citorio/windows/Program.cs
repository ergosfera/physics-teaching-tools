// Citorio for Windows: every so often a small window pops up with a quote, then hides again.
// It lives in the notification area (by the clock). The quotes are plain-text files in
// Documents\Citorio, one file per category, so new categories and quotes are added by adding
// or editing files.
// A tribute to Citorio 1.1 (1995-96) for Windows 3.1 by Javier Cámara Melgosa, whose
// collection of quotes it ships with. Twin of the Mac app in ../mac.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Citorio")]
[assembly: AssemblyProduct("Citorio")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Citorio
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        public const string ShowSignal = "ErgosferaCitorioShow";

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--write-icon")
            {
                IconArt.WriteIco(args[1]);
                return 0;
            }
            // --render out.png quotesDir: reads the lists, checks them and draws one quote
            if (args.Length == 3 && args[0] == "--render")
                return Art.Render(args[1], args[2]) ? 0 : 1;

            // One copy at a time: opening it again shows a quote in the running copy, like the original
            bool fresh;
            using (var single = new Mutex(true, "ErgosferaCitorio", out fresh))
            {
                if (!fresh)
                {
                    try { using (var ev = EventWaitHandle.OpenExisting(ShowSignal)) ev.Set(); } catch { }
                    return 0;
                }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new QuoteForm());
            }
            return 0;
        }
    }

    // ── Quote files ──

    class Quote
    {
        public string Text, Author;
        public int Cat;
        public Quote(string text, string author, int cat) { Text = text; Author = author; Cat = cat; }
        public string Key { get { return QuoteFile.Hash(Text); } }
    }

    class Category
    {
        public string Name, File;
        public string[] Icons;
        public int Count;
    }

    class Parsed
    {
        public string Name;
        public string[] Icons = new string[0];
        public List<string[]> Quotes = new List<string[]>();
    }

    static class QuoteFile
    {
        // UTF-8 (with or without BOM), UTF-16 with BOM, or else Windows-1252 (old Notepad "ANSI")
        public static string Decode(byte[] b)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return Encoding.UTF8.GetString(b, 3, b.Length - 3);
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return Encoding.Unicode.GetString(b, 2, b.Length - 2);
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return Encoding.BigEndianUnicode.GetString(b, 2, b.Length - 2);
            try { return new UTF8Encoding(false, true).GetString(b); }
            catch (DecoderFallbackException) { return Encoding.GetEncoding(1252).GetString(b); }
        }

        static bool IsAuthorLine(string s)
        {
            string t = s.Trim();
            return t.StartsWith("—") || t.StartsWith("--");
        }

        static string AuthorText(string s)
        {
            return s.Trim().TrimStart('—', '-').Trim();
        }

        static bool Blank(string s) { return s.Trim().Length == 0; }

        // Format (same rules as the Mac app):
        //   Lines at the top starting with # are comments; "# name: X" names the category (else the file name)
        //   and "# icon: 😄" gives its icon (several, separated by spaces, are used in turn).
        //   Quotes are separated by lines with only %. A last line starting with — or -- is the author.
        //   A file without any % line has one quote per line, with the author after " — " or " -- ".
        public static Parsed Parse(string raw, string fallbackName)
        {
            var p = new Parsed();
            p.Name = fallbackName;
            string[] lines = raw.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            int i = 0;
            while (i < lines.Length)
            {
                string t = lines[i].Trim();
                if (t.Length == 0) { i++; continue; }
                if (!t.StartsWith("#")) break;
                string body = t.Substring(1).Trim();
                int colon = body.IndexOf(':');
                if (colon > 0)
                {
                    string key = body.Substring(0, colon).Trim().ToLowerInvariant();
                    string val = body.Substring(colon + 1).Trim();
                    if (key == "name" && val.Length > 0) p.Name = val;
                    if (key == "icon") p.Icons = val.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                }
                i++;
            }
            var rest = new List<string>();
            for (int k = i; k < lines.Length; k++) rest.Add(lines[k]);
            if (rest.Any(l => l.Trim() == "%"))
            {
                var chunk = new List<string>();
                Action flush = () =>
                {
                    while (chunk.Count > 0 && Blank(chunk[0])) chunk.RemoveAt(0);
                    while (chunk.Count > 0 && Blank(chunk[chunk.Count - 1])) chunk.RemoveAt(chunk.Count - 1);
                    if (chunk.Count == 0) return;
                    string author = "";
                    if (chunk.Count > 1 && IsAuthorLine(chunk[chunk.Count - 1]))
                    {
                        author = AuthorText(chunk[chunk.Count - 1]);
                        chunk.RemoveAt(chunk.Count - 1);
                        while (chunk.Count > 0 && Blank(chunk[chunk.Count - 1])) chunk.RemoveAt(chunk.Count - 1);
                    }
                    p.Quotes.Add(new[] { string.Join("\n", chunk), author });
                    chunk.Clear();
                };
                foreach (string l in rest)
                {
                    if (l.Trim() == "%") flush(); else chunk.Add(l);
                }
                flush();
            }
            else
            {
                foreach (string l in rest)
                {
                    string t = l.Trim();
                    if (t.Length == 0) continue;
                    string q = t, a = "";
                    foreach (string sep in new[] { " — ", " -- " })
                    {
                        int at = t.LastIndexOf(sep, StringComparison.Ordinal);
                        if (at >= 0)
                        {
                            q = t.Substring(0, at).Trim();
                            a = t.Substring(at + sep.Length).Trim();
                            break;
                        }
                    }
                    if (q.Length > 0) p.Quotes.Add(new[] { q, a });
                }
            }
            return p;
        }

        // FNV-1a of the text: remembers which quotes were shown, even if files are edited around them
        public static string Hash(string s)
        {
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h = unchecked(h * 16777619); }
            return h.ToString("x8");
        }
    }

    class Store
    {
        public readonly string Folder;
        public List<Category> Cats = new List<Category>();
        public List<Quote> Quotes = new List<Quote>();

        public Store(string folder) { Folder = folder; }

        public static string UserFolder
        {
            get
            {
                string t = Environment.GetEnvironmentVariable("CITORIO_FOLDER");   // for testing
                if (!string.IsNullOrEmpty(t)) return t;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Citorio");
            }
        }

        public List<string> ListFiles()
        {
            var r = new List<string>();
            try
            {
                foreach (string f in Directory.GetFiles(Folder, "*.txt"))
                {
                    string n = Path.GetFileName(f);
                    if (!n.StartsWith("_") && !n.StartsWith(".")) r.Add(f);
                }
            }
            catch { }
            r.Sort(StringComparer.CurrentCultureIgnoreCase);
            return r;
        }

        public void Load()
        {
            var cs = new List<Category>();
            var qs = new List<Quote>();
            var seen = new HashSet<string>();
            foreach (string f in ListFiles())
            {
                byte[] data;
                try { data = File.ReadAllBytes(f); } catch { continue; }
                Parsed p = QuoteFile.Parse(QuoteFile.Decode(data), Path.GetFileNameWithoutExtension(f));
                string name = p.Name;
                while (seen.Contains(name.ToLowerInvariant())) name += " (2)";
                seen.Add(name.ToLowerInvariant());
                int ci = cs.Count;
                cs.Add(new Category { Name = name, File = f, Icons = p.Icons, Count = p.Quotes.Count });
                foreach (string[] q in p.Quotes) qs.Add(new Quote(q[0], q[1], ci));
            }
            Cats = cs;
            Quotes = qs;
        }

        // The lists that come with the app, embedded in the .exe as resources named Quotes.<file>.txt
        public static List<byte[]> BuiltIns()
        {
            var r = new List<byte[]>();
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string n in asm.GetManifestResourceNames())
            {
                if (!n.StartsWith("Quotes.") || !n.EndsWith(".txt")) continue;
                using (Stream s = asm.GetManifestResourceStream(n))
                using (var m = new MemoryStream())
                {
                    s.CopyTo(m);
                    r.Add(m.ToArray());
                }
            }
            return r;
        }

        // Copies the built-in lists, named after their categories ("Ley de Murphy.txt")
        public void InstallBuiltIns(bool overwrite)
        {
            try { Directory.CreateDirectory(Folder); } catch { return; }
            foreach (byte[] d in BuiltIns())
            {
                string name = QuoteFile.Parse(QuoteFile.Decode(d), "Quotes").Name;
                string dest = Path.Combine(Folder, SafeFileName(name) + ".txt");
                if (File.Exists(dest) && !overwrite) continue;
                try { File.WriteAllBytes(dest, d); } catch { }
            }
            string help = Path.Combine(Folder, "_How to add quotes.txt");
            if (!File.Exists(help))
                try { File.WriteAllText(help, HelpText.Replace("\n", "\r\n"), new UTF8Encoding(true)); } catch { }
        }

        public static string SafeFileName(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append("/\\:*?\"<>|".IndexOf(c) >= 0 ? '-' : c);
            string t = sb.ToString().Trim();
            return t.Length == 0 ? "Quotes" : t;
        }

        public static string Template(string name)
        {
            return "# name: " + name + "\r\n" +
                   "# icon: \U0001F4AC\r\n" +
                   "# Write one quote after another, separated by a line with only %.\r\n" +
                   "# A last line starting with — (or --) is the author. Lines starting with # here at the top are notes.\r\n" +
                   "\r\n" +
                   "The first quote goes here.\r\n" +
                   "-- Its author\r\n" +
                   "%\r\n" +
                   "A quote without an author.\r\n" +
                   "%\r\n" +
                   "A quote can take\r\n" +
                   "several lines.\r\n";
        }

        public const string HelpText =
@"CITORIO — HOW TO ADD QUOTES
===========================

Every .txt file in this folder is a category of quotes. To add a category, add a file;
to add quotes, edit a file in any text editor (Notepad, TextEdit…). Citorio reads the
files again before each quote, so changes show up without restarting it.
Files whose name starts with _ (like this one) are ignored.

Inside a file:

  # name: Física
  # icon: 🔭
  Lines starting with # at the top are notes. ""# name:"" names the category (otherwise the
  file name is used) and ""# icon:"" chooses its icon. Several icons separated by spaces
  are used in turn.

  Then the quotes, separated by a line with only %:

    Si he visto más lejos es porque estoy sentado sobre los hombros de gigantes.
    — Isaac Newton
    %
    La imaginación es más importante que el conocimiento.
    — Albert Einstein
    %
    A quote can take
    several lines.

  A last line that starts with — (or --) is the author.

A simple list also works: a file without any % line has one quote per line, with
the author after "" — "" or "" -- "", like

    La imaginación es más importante que el conocimiento. -- Albert Einstein

Save files as UTF-8 (the default in Notepad on Windows 10 and 11, and in TextEdit).
Turn categories on and off in Settings. ""Restore built-in lists"" in Settings puts back
the lists Citorio came with.
";
    }

    // ── Saved settings, in HKCU\Software\Ergosfera\Citorio ──
    static class Settings
    {
        const string Key = @"Software\Ergosfera\Citorio";

        public static string Get(string name)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(Key))
                {
                    object v = key == null ? null : key.GetValue(name);
                    return v == null ? null : v.ToString();
                }
            }
            catch { return null; }
        }

        public static void Set(string name, object value)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(Key))
                    key.SetValue(name, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            catch { }
        }

        public static void Remove(string name)
        {
            try { using (var key = Registry.CurrentUser.CreateSubKey(Key)) key.DeleteValue(name, false); } catch { }
        }

        public static int GetInt(string name, int fallback)
        {
            int v;
            string s = Get(name);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public static bool GetBool(string name, bool fallback)
        {
            string s = Get(name);
            return s == null ? fallback : s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        // Shown quotes, kept in a file because the list is long
        static string HistoryFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ergosfera", "Citorio", "history.txt"); }
        }

        public static List<string> LoadHistory()
        {
            try { return File.ReadAllText(HistoryFile).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList(); }
            catch { return new List<string>(); }
        }

        public static void SaveHistory(List<string> h)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(HistoryFile));
                File.WriteAllText(HistoryFile, string.Join(",", h.Skip(Math.Max(0, h.Count - 3000))));
            }
            catch { }
        }
    }

    // ── Drawing the quote (sizes in pixels at 96 dpi, times the screen scale) ──
    static class Art
    {
        public const int Pad = 16, IconBox = 46, IconGap = 14, AuthorGap = 10;
        const TextFormatFlags Flow = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding;

        public static Font QuoteFont(string family, float size)
        {
            try
            {
                var f = new Font(family, size, FontStyle.Regular, GraphicsUnit.Point);
                if (f.Name.Equals(family, StringComparison.OrdinalIgnoreCase)) return f;
                f.Dispose();
            }
            catch { }
            return new Font("Georgia", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font AuthorFont(string family, float size)
        {
            using (Font q = QuoteFont(family, size))
            {
                try { return new Font(q.FontFamily, size * 0.78f, FontStyle.Italic, GraphicsUnit.Point); }
                catch { return new Font(q.FontFamily, size * 0.78f, FontStyle.Regular, GraphicsUnit.Point); }
            }
        }

        static int TextHeight(Graphics g, string s, Font f, int w)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            return TextRenderer.MeasureText(g, s, f, new Size(w, int.MaxValue), Flow).Height;
        }

        public static int Height(Graphics g, Quote q, string family, float size, int width, bool icon, float k)
        {
            int pad = (int)(Pad * k), box = (int)(IconBox * k), gap = (int)(IconGap * k);
            int tw = width - 2 * pad - (icon ? box + gap : 0);
            using (Font qf = QuoteFont(family, size))
            using (Font af = AuthorFont(family, size))
            {
                int h = TextHeight(g, q.Text, qf, tw);
                if (q.Author.Length > 0) h += (int)(AuthorGap * k) + TextHeight(g, "— " + q.Author, af, tw);
                return Math.Max(h, icon ? box : 0) + 2 * pad;
            }
        }

        // Height the quote needs at this width; shrinks the letters if it would be taller than maxH
        public static float FitSize(Graphics g, Quote q, string family, float size, int width, bool icon, float k, int maxH)
        {
            float s = size;
            while (s > 10 && Height(g, q, family, s, width, icon, k) > maxH) s -= 1;
            return s;
        }

        public static void Draw(Graphics g, Quote q, string iconText, string family, float size, Rectangle r, bool icon, float k)
        {
            int pad = (int)(Pad * k), box = (int)(IconBox * k), gap = (int)(IconGap * k);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int x = r.Left + pad;
            if (icon)
            {
                var b = new Rectangle(x, r.Top + pad, box, box);
                using (var path = Rounded(b, 9 * k))
                using (var fill = new SolidBrush(Color.FromArgb(14, 0, 0, 0)))
                    g.FillPath(fill, path);
                using (var ef = new Font("Segoe UI Emoji", 21f * k, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, iconText, ef, b, Color.Black,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                x += box + gap;
            }
            int tw = r.Right - pad - x;
            using (Font qf = QuoteFont(family, size))
            using (Font af = AuthorFont(family, size))
            {
                int qh = TextHeight(g, q.Text, qf, tw);
                TextRenderer.DrawText(g, q.Text, qf, new Rectangle(x, r.Top + pad, tw, qh + 2), Color.FromArgb(20, 20, 20), Flow);
                if (q.Author.Length > 0)
                {
                    string a = "— " + q.Author;
                    int ah = TextHeight(g, a, af, tw);
                    TextRenderer.DrawText(g, a, af, new Rectangle(x, r.Top + pad + qh + (int)(AuthorGap * k), tw, ah + 2),
                        Color.FromArgb(0, 0, 128), Flow | TextFormatFlags.Right);
                }
            }
        }

        public static GraphicsPath Rounded(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static bool Render(string path, string folder)
        {
            var store = new Store(folder);
            store.Load();
            Console.WriteLine("Categories: " + store.Cats.Count + ", quotes: " + store.Quotes.Count);
            foreach (Category c in store.Cats) Console.WriteLine("  " + c.Name + ": " + c.Count);
            if (store.Quotes.Count == 0) return false;
            Quote q = store.Quotes.FirstOrDefault(x => x.Author.Length > 0 && x.Text.Length > 120) ?? store.Quotes[0];
            const float k = 2;
            int w = (int)(460 * k), h;
            using (var tmp = new Bitmap(1, 1))
            using (Graphics tg = Graphics.FromImage(tmp)) h = Height(tg, q, "Georgia", 14 * k, w, true, k);
            using (var bmp = new Bitmap(w, h))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.White);
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    Category c = store.Cats[q.Cat];
                    Draw(g, q, c.Icons.Length > 0 ? c.Icons[0] : "\U0001F4DC", "Georgia", 14 * k, new Rectangle(0, 0, w, h), true, k);
                }
                bmp.Save(path, ImageFormat.Png);
            }
            return true;
        }
    }

    // ── App icon: a white speech bubble with a big serif quotation mark, on a navy rounded square ──
    static class IconArt
    {
        public static Bitmap Make(int s)
        {
            var bmp = new Bitmap(s, s, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;
                float inset = s * 0.098f;
                var body = new RectangleF(inset, inset, s - 2 * inset, s - 2 * inset);
                Color navy = Color.FromArgb(0, 0, 128);
                using (var p = Art.Rounded(body, body.Width * 0.225f))
                using (var b = new SolidBrush(navy)) g.FillPath(b, p);
                // y grows downward here; same shapes as the Mac icon
                var bub = new RectangleF(s * 0.2f, s * 0.24f, s * 0.6f, s * 0.42f);
                using (var p = Art.Rounded(bub, s * 0.1f))
                {
                    p.AddPolygon(new[] { new PointF(s * 0.32f, bub.Bottom - 1), new PointF(s * 0.28f, s * 0.78f), new PointF(s * 0.46f, bub.Bottom - 1) });
                    using (var b = new SolidBrush(Color.White)) g.FillPath(b, p);
                }
                using (var f = new Font("Georgia", s * 0.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var gp = new GraphicsPath())
                {
                    gp.AddString("“", f.FontFamily, (int)FontStyle.Bold, s * 0.5f, PointF.Empty, StringFormat.GenericTypographic);
                    RectangleF gb = gp.GetBounds();
                    using (var m = new Matrix())
                    {
                        m.Translate(bub.Left + bub.Width / 2 - (gb.Left + gb.Width / 2), bub.Top + bub.Height / 2 - (gb.Top + gb.Height / 2));
                        gp.Transform(m);
                    }
                    using (var b = new SolidBrush(navy)) g.FillPath(b, gp);
                }
            }
            return bmp;
        }

        // Icon file with PNG images (16 to 256 px), for the .exe
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };
            var pngs = new List<byte[]>();
            foreach (int s in sizes)
                using (Bitmap b = Make(s))
                using (var m = new MemoryStream()) { b.Save(m, ImageFormat.Png); pngs.Add(m.ToArray()); }
            using (var f = new BinaryWriter(File.Create(path)))
            {
                f.Write((short)0); f.Write((short)1); f.Write((short)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    f.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i])); f.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
                    f.Write((byte)0); f.Write((byte)0); f.Write((short)1); f.Write((short)32);
                    f.Write(pngs[i].Length); f.Write(offset);
                    offset += pngs[i].Length;
                }
                foreach (byte[] p in pngs) f.Write(p);
            }
        }

        public static Icon MakeIcon(int s)
        {
            using (Bitmap b = Make(s)) return Icon.FromHandle(b.GetHicon());
        }
    }

    // ── The quote window ──
    class QuoteView : Control
    {
        public QuoteForm App;
        public QuoteView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Color.White;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.White);
            if (App == null) return;
            Quote q = App.Current ?? App.EmptyQuote();
            Art.Draw(e.Graphics, q, App.CurrentIcon, App.FontFamilyName, App.ShownSize, ClientRectangle,
                     App.ShowIcons && App.Current != null, App.K);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e) { if (App != null) App.NextQuote(); }
    }

    class QuoteForm : Form
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "Citorio";

        public readonly Store Store = new Store(Store.UserFolder);
        public Quote Current;
        public string CurrentIcon = "\U0001F4DC";
        public float ShownSize = 14;
        public float K = 1;

        readonly QuoteView view = new QuoteView();
        readonly Panel bar = new Panel();
        readonly Button pauseButton;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly ToolStripMenuItem pauseItem, startItem;
        readonly Timer timer = new Timer();
        readonly Random rng = new Random();
        readonly Dictionary<string, int> iconTurn = new Dictionary<string, int>();
        List<string> history;
        SettingsForm settings;
        EventWaitHandle showSignal;

        bool visible, paused, resizing, quitting;
        DateTime phaseEnd = DateTime.Now;
        double phaseLen = 1;
        int lastWidth;

        // Settings
        public int MinShow { get { return Settings.GetInt("minShow", 10); } set { Settings.Set("minShow", value); } }
        public int MaxShow { get { return Settings.GetInt("maxShow", 40); } set { Settings.Set("maxShow", value); } }
        public int HideFor { get { return Settings.GetInt("hideFor", 60); } set { Settings.Set("hideFor", value); } }
        public string FontFamilyName { get { return Settings.Get("font") ?? "Georgia"; } set { Settings.Set("font", value); } }
        public float FontSizePt { get { return Settings.GetInt("size", 14); } set { Settings.Set("size", (int)value); } }
        public bool ShowIcons { get { return Settings.GetBool("icons", true); } set { Settings.Set("icons", value ? 1 : 0); } }
        public bool Sound { get { return Settings.GetBool("sound", true); } set { Settings.Set("sound", value ? 1 : 0); } }
        public bool StayVisible { get { return Settings.GetBool("stayVisible", false); } set { Settings.Set("stayVisible", value ? 1 : 0); } }
        public HashSet<string> Off
        {
            get
            {
                string s = Settings.Get("off");
                if (s == null) return new HashSet<string>();
                return new HashSet<string>(s.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries));
            }
            set { Settings.Set("off", string.Join("|", value.OrderBy(x => x))); }
        }

        public QuoteForm()
        {
            Text = "Citorio";
            FormBorderStyle = FormBorderStyle.SizableToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = SystemColors.Control;
            using (Graphics g = CreateGraphics()) K = g.DpiX / 96f;
            Icon = IconArt.MakeIcon(32);

            int barH = (int)(38 * K);
            bar.Dock = DockStyle.Bottom;
            bar.Height = barH;
            int x = (int)(8 * K);
            foreach (var spec in new[] { new object[] { "Next", "Show another quote", (EventHandler)((s, e) => NextQuote()) },
                                         new object[] { "Later", "Hide; show this same quote next time", (EventHandler)((s, e) => Later()) },
                                         new object[] { "Pause", "Stop or restart the timer", (EventHandler)((s, e) => TogglePause()) },
                                         new object[] { "Copy", "Copy the quote", (EventHandler)((s, e) => CopyQuote()) } })
            {
                var b = new Button { Text = (string)spec[0], FlatStyle = FlatStyle.System, Width = (int)(70 * K), Height = (int)(26 * K), Left = x, Top = (int)(6 * K), TabStop = false };
                new ToolTip().SetToolTip(b, (string)spec[1]);
                b.Click += (EventHandler)spec[2];
                bar.Controls.Add(b);
                x += b.Width + (int)(4 * K);
                if ((string)spec[0] == "Pause") pauseButton = b;
            }
            var gear = new Button { Text = "Settings…", FlatStyle = FlatStyle.System, Width = (int)(84 * K), Height = (int)(26 * K), Top = (int)(6 * K), TabStop = false };
            bar.Resize += (s, e) => gear.Left = bar.ClientSize.Width - gear.Width - (int)(8 * K);
            gear.Click += (s, e) => OpenSettings();
            bar.Controls.Add(gear);
            view.App = this;
            view.Dock = DockStyle.Fill;
            Controls.Add(view);
            Controls.Add(bar);
            MinimumSize = new Size(x + gear.Width + (int)(30 * K), (int)(120 * K));

            int w = Settings.GetInt("width", (int)(480 * K));
            Width = w;
            lastWidth = w;

            // Notification-area icon and its menu
            var menu = new ContextMenuStrip();
            menu.Items.Add("Show a quote now", null, (s, e) => ShowNow());
            menu.Items.Add("Next quote", null, (s, e) => NextQuote());
            pauseItem = new ToolStripMenuItem("Pause", null, (s, e) => TogglePause());
            menu.Items.Add(pauseItem);
            menu.Items.Add("Copy quote", null, (s, e) => CopyQuote());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Settings…", null, (s, e) => OpenSettings());
            menu.Items.Add("Open quotes folder", null, (s, e) => OpenFolder());
            startItem = new ToolStripMenuItem("Start with Windows", null, (s, e) => ToggleStartup());
            menu.Items.Add(startItem);
            menu.Items.Add("About Citorio", null, (s, e) => About());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Citorio", null, (s, e) => Quit());
            menu.Opening += (s, e) => startItem.Checked = StartsWithWindows();
            tray.Icon = IconArt.MakeIcon(16);
            tray.Text = "Citorio";
            tray.ContextMenuStrip = menu;
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowNow(); };
            tray.Visible = true;
            view.ContextMenuStrip = menu;

            if (!Directory.Exists(Store.Folder)) Store.InstallBuiltIns(false);
            Store.Load();
            history = Settings.LoadHistory();

            // A second launch asks this copy to show a quote
            try
            {
                showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowSignal);
                var t = new Thread(() =>
                {
                    while (true)
                    {
                        showSignal.WaitOne();
                        try { BeginInvoke((Action)ShowNow); } catch { return; }
                    }
                });
                t.IsBackground = true;
                t.Start();
            }
            catch { }

            timer.Interval = 250;
            timer.Tick += (s, e) => Tick();
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x00000008;   // WS_EX_NOACTIVATE | WS_EX_TOPMOST: shows without taking the keyboard
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ShowQuote(Pick(), false);
            timer.Start();
        }

        // ── Picking ──

        List<int> Pool()
        {
            HashSet<string> off = Off;
            var r = new List<int>();
            for (int i = 0; i < Store.Quotes.Count; i++)
                if (!off.Contains(Store.Cats[Store.Quotes[i].Cat].Name)) r.Add(i);
            return r;
        }

        // Not one of the last 60% shown, like the original
        Quote Pick()
        {
            Store.Load();
            string pending = Settings.Get("pending");
            if (pending != null)
            {
                Settings.Remove("pending");
                Quote q = Store.Quotes.FirstOrDefault(x => x.Key == pending);
                if (q != null) return q;
            }
            List<int> p = Pool();
            if (p.Count == 0) return null;
            int mem = (int)(p.Count * 0.6);
            var recent = new HashSet<string>(history.Skip(Math.Max(0, history.Count - mem)));
            string cur = Current == null ? null : Current.Key;
            var fresh = p.Where(i => { string k = Store.Quotes[i].Key; return !recent.Contains(k) && k != cur; }).ToList();
            List<int> from = fresh.Count > 0 ? fresh : p;
            return Store.Quotes[from[rng.Next(from.Count)]];
        }

        double ShowDuration(Quote q)
        {
            if (q == null) return MaxShow;
            double len = q.Text.Length + q.Author.Length;
            double f = Math.Min(1, Math.Max(0, (len - 40) / 560));
            double lo = Math.Min(MinShow, MaxShow), hi = Math.Max(MinShow, MaxShow);
            return lo + (hi - lo) * f;
        }

        public Quote EmptyQuote()
        {
            return new Quote(Store.Quotes.Count == 0
                ? "There are no quotes in Documents\\Citorio. Open Settings to add a category or restore the built-in lists."
                : "All categories are turned off. Turn some on in Settings.", "", -1);
        }

        // ── Showing and hiding ──

        void ShowQuote(Quote q, bool playSound)
        {
            Current = q;
            if (q != null)
            {
                Category c = Store.Cats[q.Cat];
                if (c.Icons.Length == 0) CurrentIcon = "\U0001F4DC";
                else
                {
                    int n;
                    iconTurn.TryGetValue(c.Name, out n);
                    CurrentIcon = c.Icons[n % c.Icons.Length];
                    iconTurn[c.Name] = n + 1;
                }
                history.Add(q.Key);
                Settings.SaveHistory(history);
            }
            Relayout(true);
            view.Invalidate();
            if (!visible)
            {
                Show();
                visible = true;
            }
            if (playSound && Sound) SystemSounds.Asterisk.Play();
            StartPhase(ShowDuration(q));
            UpdateTitle();
        }

        void Relayout(bool usePlace)
        {
            Screen scr = Screen.FromPoint(new Point(Left + 20, Top + 10));
            Rectangle wa = scr.WorkingArea;
            int w = Width;
            int chrome = Height - ClientSize.Height;
            int cw = w - (Width - ClientSize.Width);
            Quote q = Current ?? EmptyQuote();
            bool icon = ShowIcons && Current != null;
            int maxH = (int)(wa.Height * 0.8) - bar.Height - chrome;
            using (Graphics g = view.CreateGraphics())
            {
                ShownSize = Art.FitSize(g, q, FontFamilyName, FontSizePt, cw, icon, K, maxH);
                int h = Math.Min(maxH, Art.Height(g, q, FontFamilyName, ShownSize, cw, icon, K)) + bar.Height + chrome;
                int x, top;
                if (usePlace && Settings.Get("x") != null) { x = Settings.GetInt("x", 0); top = Settings.GetInt("top", 0); }
                else if (usePlace) { x = wa.Right - w - (int)(16 * K); top = wa.Top + (int)(12 * K); }
                else { x = Left; top = Top; }
                // Keep it on a screen
                Rectangle home = Screen.AllScreens.Select(s => s.WorkingArea).FirstOrDefault(a => a.Contains(x + 20, top + 10));
                if (home.IsEmpty) { home = Screen.PrimaryScreen.WorkingArea; x = home.Right - w - (int)(16 * K); top = home.Top + (int)(12 * K); }
                if (top + h > home.Bottom) top = Math.Max(home.Top, home.Bottom - h);
                resizing = true;
                MinimumSize = new Size(MinimumSize.Width, Math.Min(h, MinimumSize.Height));
                SetBounds(x, top, w, h);
                resizing = false;
            }
        }

        void HideQuote()
        {
            if (StayVisible) { ShowQuote(Pick(), true); return; }
            Hide();
            visible = false;
            StartPhase(HideFor);
            UpdateTitle();
        }

        void StartPhase(double s) { phaseLen = Math.Max(1, s); phaseEnd = DateTime.Now.AddSeconds(phaseLen); }

        void Tick()
        {
            bool hovering = visible && Bounds.Contains(Cursor.Position);
            bool holding = paused || (visible && (hovering || (settings != null && settings.Visible)));
            if (holding) { phaseEnd = phaseEnd.AddMilliseconds(timer.Interval); return; }
            if (DateTime.Now >= phaseEnd)
            {
                if (visible) HideQuote(); else ShowQuote(Pick(), true);
            }
        }

        void UpdateTitle()
        {
            Text = paused ? "[Citorio]" : "Citorio";
            pauseButton.Text = paused ? "Resume" : "Pause";
            pauseItem.Text = paused ? "Resume" : "Pause";
            tray.Text = paused ? "Citorio (paused)" : "Citorio";
        }

        // ── Actions ──

        public void NextQuote() { ShowQuote(Pick(), false); }

        void ShowNow()
        {
            if (visible) { Show(); StartPhase(ShowDuration(Current)); }
            else ShowQuote(Pick(), false);
        }

        void Later()
        {
            if (Current != null) Settings.Set("pending", Current.Key);
            Hide();
            visible = false;
            StartPhase(HideFor);
            UpdateTitle();
        }

        void TogglePause()
        {
            paused = !paused;
            if (!paused) StartPhase(visible ? ShowDuration(Current) : HideFor);
            UpdateTitle();
        }

        void CopyQuote()
        {
            if (Current == null) return;
            try { Clipboard.SetText(Current.Text.Replace("\n", "\r\n") + (Current.Author.Length > 0 ? "\r\n— " + Current.Author : "")); } catch { }
        }

        void OpenSettings()
        {
            if (settings == null || settings.IsDisposed) settings = new SettingsForm(this);
            settings.RefreshAll();
            settings.Show();
            settings.Activate();
        }

        public void OpenFolder()
        {
            if (!Directory.Exists(Store.Folder)) Store.InstallBuiltIns(false);
            try { Process.Start("explorer.exe", "\"" + Store.Folder + "\""); } catch { }
        }

        void About()
        {
            MessageBox.Show(
                "Every so often a small window shows a quote, then hides again.\n\n" +
                "A tribute to Citorio 1.1 (1995–96) for Windows 3.1 by Javier Cámara Melgosa, itself based on " +
                "Pundit (1990) by Jeff Bienstadt. The " + Store.Quotes.Count + " quotes that come with it are the ones " +
                "he collected and translated; this program is new.\n\n" +
                "Your quotes are in Documents\\Citorio: one .txt file per category.\n\n" +
                "© 2026 Mauricio Mendivelso. MIT License.", "About Citorio 1.0");
        }

        static bool StartsWithWindows()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                    return key != null && key.GetValue(RunValue) != null;
            }
            catch { return false; }
        }

        void ToggleStartup()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (StartsWithWindows()) key.DeleteValue(RunValue, false);
                    else key.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't change Start with Windows.\n\n" + ex.Message, "Citorio");
            }
        }

        void Quit()
        {
            quitting = true;
            timer.Stop();
            tray.Visible = false;
            Close();
            Application.Exit();
        }

        public void SettingsChanged()
        {
            if (Current != null && Store.Cats.Count > Current.Cat && Off.Contains(Store.Cats[Current.Cat].Name)) { ShowQuote(Pick(), false); return; }
            if (visible) { Relayout(false); view.Invalidate(); }
        }

        // ── Window ──

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Closing only hides it until the next quote; quit from the icon by the clock
            if (!quitting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                visible = false;
                StartPhase(HideFor);
                UpdateTitle();
                return;
            }
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (resizing) return;
            if (Width != lastWidth)
            {
                lastWidth = Width;
                Settings.Set("width", Width);
                Relayout(false);
                view.Invalidate();
            }
            SavePosition();
        }

        void SavePosition()
        {
            Settings.Set("x", Left);
            Settings.Set("top", Top);
        }
    }

    // ── Settings window ──
    class SettingsForm : Form
    {
        readonly QuoteForm app;
        readonly NumericUpDown minBox = new NumericUpDown(), maxBox = new NumericUpDown(), hideBox = new NumericUpDown(), sizeBox = new NumericUpDown();
        readonly ComboBox fontBox = new ComboBox();
        readonly CheckBox iconsBox = new CheckBox(), soundBox = new CheckBox(), stayBox = new CheckBox();
        readonly CheckedListBox list = new CheckedListBox();
        readonly Label countLabel = new Label();
        bool loading;

        public SettingsForm(QuoteForm app)
        {
            this.app = app;
            Text = "Citorio Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Icon = IconArt.MakeIcon(32);
            // Laid out in pixels at 96 dpi; Windows scales it up on bigger settings
            AutoScaleDimensions = new SizeF(96f, 96f);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9f);
            ClientSize = new Size(560, 620);

            int y = 14;
            Heading("Timing", y);
            y += 28; Row("Show each quote at least", minBox, 2, 3600, y);
            y += 30; Row("and at most", maxBox, 2, 3600, y);
            y += 30; Row("Hide between quotes for", hideBox, 0, 86400, y);
            y += 28; Note("Longer quotes stay longer. Moving the mouse over a quote holds it.", y);

            y += 30; Heading("Appearance", y);
            y += 28;
            Add(new Label { Text = "Font", Left = 30, Top = y + 3, AutoSize = true });
            fontBox.DropDownStyle = ComboBoxStyle.DropDownList;
            fontBox.SetBounds(80, y, 240, 24);
            var installed = new HashSet<string>(FontFamily.Families.Select(f => f.Name));
            var serifs = new[] { "Georgia", "Times New Roman", "Palatino Linotype", "Book Antiqua", "Cambria", "Constantia", "Garamond", "Baskerville Old Face", "Century Schoolbook", "Sylfaen" }
                .Where(n => installed.Contains(n)).ToList();
            foreach (string f in serifs) fontBox.Items.Add(f);
            foreach (string f in installed.OrderBy(n => n)) if (!serifs.Contains(f)) fontBox.Items.Add(f);
            fontBox.SelectedIndexChanged += (s, e) => SaveLook();
            Add(fontBox);
            Add(new Label { Text = "Size", Left = 340, Top = y + 3, AutoSize = true });
            sizeBox.SetBounds(378, y, 56, 24);
            sizeBox.Minimum = 9; sizeBox.Maximum = 72;
            sizeBox.ValueChanged += (s, e) => SaveLook();
            Add(sizeBox);
            Add(new Label { Text = "pt", Left = 440, Top = y + 3, AutoSize = true });
            y += 32; Check(iconsBox, "Show the category icon", y);
            y += 26; Check(soundBox, "Play a sound when a quote appears", y);
            y += 26; Check(stayBox, "Never hide: go straight to the next quote (for a projector)", y);

            y += 36; Heading("Categories", y);
            countLabel.SetBounds(120, y + 2, 300, 20);
            countLabel.ForeColor = SystemColors.GrayText;
            Add(countLabel);
            y += 26;
            list.SetBounds(30, y, 400, 620 - y - 100);
            list.CheckOnClick = true;
            list.IntegralHeight = false;
            list.ItemCheck += (s, e) => { if (!loading) BeginInvoke((Action)SaveCategories); };
            list.DoubleClick += (s, e) => EditSelected();
            Add(list);
            int bx = 440, by = y;
            foreach (var spec in new[] { new object[] { "New category…", (EventHandler)((s, e) => NewCategory()) },
                                         new object[] { "Import list…", (EventHandler)((s, e) => ImportList()) },
                                         new object[] { "Edit", (EventHandler)((s, e) => EditSelected()) },
                                         new object[] { "Open folder", (EventHandler)((s, e) => app.OpenFolder()) },
                                         new object[] { "Restore built-in…", (EventHandler)((s, e) => Restore()) } })
            {
                var b = new Button { Text = (string)spec[0], Left = bx, Top = by, Width = 104, Height = 28 };
                b.Click += (EventHandler)spec[1];
                Add(b);
                by += 34;
            }
            Note("Each category is a .txt file in Documents\\Citorio (see \"_How to add quotes.txt\" there).", 620 - 66);
            var done = new Button { Text = "Done", Left = 560 - 104 - 16, Top = 620 - 40, Width = 104, Height = 28 };
            done.Click += (s, e) => Close();
            Add(done);
            AcceptButton = done;
            CancelButton = done;
        }

        void Add(Control c) { Controls.Add(c); }

        void Heading(string t, int y)
        {
            Add(new Label { Text = t, Left = 14, Top = y, AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) });
        }

        void Note(string t, int y)
        {
            Add(new Label { Text = t, Left = 30, Top = y, AutoSize = true, ForeColor = SystemColors.GrayText });
        }

        void Row(string label, NumericUpDown box, int min, int max, int y)
        {
            Add(new Label { Text = label, Left = 30, Top = y + 3, AutoSize = true });
            box.SetBounds(230, y, 70, 24);
            box.Minimum = min; box.Maximum = max;
            box.TextAlign = HorizontalAlignment.Right;
            box.ValueChanged += (s, e) => SaveTimes();
            Add(box);
            Add(new Label { Text = "seconds", Left = 306, Top = y + 3, AutoSize = true });
        }

        void Check(CheckBox b, string t, int y)
        {
            b.Text = t;
            b.SetBounds(30, y, 480, 22);
            b.CheckedChanged += (s, e) => SaveLook();
            Add(b);
        }

        static decimal Clamp(NumericUpDown b, int v) { return Math.Min(b.Maximum, Math.Max(b.Minimum, v)); }

        public void RefreshAll()
        {
            loading = true;
            minBox.Value = Clamp(minBox, app.MinShow);
            maxBox.Value = Clamp(maxBox, app.MaxShow);
            hideBox.Value = Clamp(hideBox, app.HideFor);
            sizeBox.Value = Clamp(sizeBox, (int)app.FontSizePt);
            if (!fontBox.Items.Contains(app.FontFamilyName)) fontBox.Items.Insert(0, app.FontFamilyName);
            fontBox.SelectedItem = app.FontFamilyName;
            iconsBox.Checked = app.ShowIcons;
            soundBox.Checked = app.Sound;
            stayBox.Checked = app.StayVisible;
            app.Store.Load();
            list.Items.Clear();
            HashSet<string> off = app.Off;
            foreach (Category c in app.Store.Cats)
                list.Items.Add((c.Icons.Length > 0 ? c.Icons[0] + "  " : "") + c.Name + "  (" + c.Count + ")", !off.Contains(c.Name));
            loading = false;
            UpdateCount();
        }

        void UpdateCount()
        {
            HashSet<string> off = app.Off;
            int on = app.Store.Cats.Where(c => !off.Contains(c.Name)).Sum(c => c.Count);
            countLabel.Text = on + " of " + app.Store.Quotes.Count + " quotes turned on";
        }

        void SaveTimes()
        {
            if (loading) return;
            app.MinShow = (int)minBox.Value;
            app.MaxShow = (int)maxBox.Value;
            app.HideFor = (int)hideBox.Value;
        }

        void SaveLook()
        {
            if (loading) return;
            if (fontBox.SelectedItem != null) app.FontFamilyName = (string)fontBox.SelectedItem;
            app.FontSizePt = (float)sizeBox.Value;
            app.ShowIcons = iconsBox.Checked;
            app.Sound = soundBox.Checked;
            app.StayVisible = stayBox.Checked;
            app.SettingsChanged();
        }

        void SaveCategories()
        {
            var off = app.Off;
            for (int i = 0; i < list.Items.Count && i < app.Store.Cats.Count; i++)
            {
                string n = app.Store.Cats[i].Name;
                if (list.GetItemChecked(i)) off.Remove(n); else off.Add(n);
            }
            app.Off = off;
            UpdateCount();
            app.SettingsChanged();
        }

        void EditSelected()
        {
            int i = list.SelectedIndex;
            if (i < 0 || i >= app.Store.Cats.Count) { MessageBox.Show(this, "Select a category in the list first.", "Citorio"); return; }
            OpenInEditor(app.Store.Cats[i].File);
        }

        static void OpenInEditor(string file)
        {
            try { Process.Start(file); }
            catch { try { Process.Start("notepad.exe", "\"" + file + "\""); } catch { } }
        }

        void NewCategory()
        {
            string name = Prompt("New category", "Name of the new category. Citorio creates a file for it and opens it so you can write the quotes.");
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            try { Directory.CreateDirectory(app.Store.Folder); } catch { }
            string file = Path.Combine(app.Store.Folder, Store.SafeFileName(name) + ".txt");
            if (!File.Exists(file))
                try { File.WriteAllText(file, Store.Template(name), new UTF8Encoding(true)); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Citorio"); return; }
            RefreshAll();
            OpenInEditor(file);
        }

        string Prompt(string title, string text)
        {
            using (var f = new Form())
            {
                f.Text = title;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = false; f.MinimizeBox = false;
                f.StartPosition = FormStartPosition.CenterParent;
                f.AutoScaleDimensions = new SizeF(96f, 96f);
                f.AutoScaleMode = AutoScaleMode.Dpi;
                f.Font = new Font("Segoe UI", 9f);
                f.ClientSize = new Size(380, 130);
                f.Controls.Add(new Label { Text = text, Left = 12, Top = 10, Width = 356, Height = 36 });
                var tb = new TextBox { Left = 12, Top = 52, Width = 356 };
                f.Controls.Add(tb);
                var ok = new Button { Text = "Create", Left = 196, Top = 90, Width = 82, DialogResult = DialogResult.OK };
                var cancel = new Button { Text = "Cancel", Left = 286, Top = 90, Width = 82, DialogResult = DialogResult.Cancel };
                f.Controls.Add(ok); f.Controls.Add(cancel);
                f.AcceptButton = ok; f.CancelButton = cancel;
                return f.ShowDialog(this) == DialogResult.OK ? tb.Text : null;
            }
        }

        void ImportList()
        {
            using (var d = new OpenFileDialog())
            {
                d.Title = "Choose text files with quotes. Each file becomes a category.";
                d.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                d.Multiselect = true;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try { Directory.CreateDirectory(app.Store.Folder); } catch { }
                var added = new List<string>();
                foreach (string src in d.FileNames)
                {
                    byte[] data;
                    try { data = File.ReadAllBytes(src); } catch { continue; }
                    string text = QuoteFile.Decode(data);
                    Parsed p = QuoteFile.Parse(text, Path.GetFileNameWithoutExtension(src));
                    string baseName = Store.SafeFileName(Path.GetFileNameWithoutExtension(src));
                    string dest = Path.Combine(app.Store.Folder, baseName + ".txt");
                    if (File.Exists(dest))
                    {
                        var r = MessageBox.Show(this, "\"" + Path.GetFileName(dest) + "\" already exists.\n\nYes: replace it.  No: keep both.  Cancel: skip it.",
                                                "Citorio", MessageBoxButtons.YesNoCancel);
                        if (r == DialogResult.Cancel) continue;
                        if (r == DialogResult.No)
                        {
                            int n = 2;
                            while (File.Exists(dest)) { dest = Path.Combine(app.Store.Folder, baseName + " " + n + ".txt"); n++; }
                        }
                    }
                    // Saved as UTF-8 so it reads the same everywhere
                    try { File.WriteAllText(dest, text, new UTF8Encoding(true)); } catch { continue; }
                    added.Add(p.Name + ": " + p.Quotes.Count + " quotes");
                }
                RefreshAll();
                if (added.Count > 0) MessageBox.Show(this, string.Join("\n", added), "Imported");
            }
        }

        void Restore()
        {
            if (MessageBox.Show(this, "This puts back the lists Citorio came with. Changes you made to those files are replaced; your own categories are kept.",
                                "Restore the built-in lists?", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            app.Store.InstallBuiltIns(true);
            RefreshAll();
            app.SettingsChanged();
        }
    }
}
