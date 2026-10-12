// Physics Calculator for Windows: the calculator panel of
// tools/teaching/notebook_calculator.html as a desktop app. Engine.cs holds the math.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: AssemblyTitle("Physics Calculator")]
[assembly: AssemblyProduct("Physics Calculator")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace PhysicsCalculator
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
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CalcForm());
        }
    }

    // ── Classic Windows 95 skin (same tokens as the web page) ──
    static class W95
    {
        public static float K = 1;
        public static readonly Color Face = Color.FromArgb(0xc0, 0xc0, 0xc0);
        public static readonly Color Hi = Color.White;
        public static readonly Color Lt = Color.FromArgb(0xdf, 0xdf, 0xdf);
        public static readonly Color Sh = Color.FromArgb(0x80, 0x80, 0x80);
        public static readonly Color Dk = Color.Black;
        public static readonly Color Text = Color.Black;
        public static readonly Color Dim = Color.FromArgb(0x40, 0x40, 0x40);
        public static readonly Color TitleFrom = Color.FromArgb(0x00, 0x00, 0x80);
        public static readonly Color Navy = Color.FromArgb(0x00, 0x33, 0x99);
        public static readonly Color Green = Color.FromArgb(0x00, 0x66, 0x22);
        public static readonly Color Amber = Color.FromArgb(0x7a, 0x4a, 0x00);
        public static readonly Color Red = Color.FromArgb(0xa0, 0x00, 0x00);
        public static readonly Color Go = Color.FromArgb(0x0a, 0x7a, 0x35);
        public static readonly Color EmptyText = Color.FromArgb(0xa8, 0xa8, 0xa8);
        public static readonly Color Placeholder = Color.FromArgb(0x8a, 0x8a, 0x8a);

        public static int S(float v) { return (int)Math.Round(v * K); }
        static int T { get { return Math.Max(1, (int)Math.Round(K)); } }

        static void Edge(Graphics g, Rectangle r, Color tl, Color br)
        {
            int t = T;
            using (var b = new SolidBrush(tl))
            {
                g.FillRectangle(b, r.X, r.Y, r.Width, t);
                g.FillRectangle(b, r.X, r.Y, t, r.Height);
            }
            using (var b = new SolidBrush(br))
            {
                g.FillRectangle(b, r.X, r.Bottom - t, r.Width, t);
                g.FillRectangle(b, r.Right - t, r.Y, t, r.Height);
            }
        }

        public static void Raised(Graphics g, Rectangle r)
        {
            Edge(g, r, Hi, Dk);
            Edge(g, Rectangle.Inflate(r, -T, -T), Lt, Sh);
        }

        public static void Sunken(Graphics g, Rectangle r)
        {
            Edge(g, r, Dk, Hi);
            Edge(g, Rectangle.Inflate(r, -T, -T), Sh, Lt);
        }

        public static int BevelWidth { get { return 2 * T; } }

        static readonly Dictionary<string, Font> fonts = new Dictionary<string, Font>();
        public static Font Font(string family, float px, bool bold)
        {
            string key = family + "|" + px + "|" + bold;
            Font f;
            if (!fonts.TryGetValue(key, out f))
            {
                f = new Font(family, Math.Max(1, px * K), bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
                fonts[key] = f;
            }
            return f;
        }

        public const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                              TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
    }

    // A Windows 95 push button that never takes the keyboard focus,
    // so typing always stays in the expression box.
    class W95Button : Control
    {
        public Color Face = W95.Face;
        public bool Active;
        bool pressed;

        public W95Button(string text)
        {
            Text = text;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.Hand;
            ForeColor = W95.Text;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new SolidBrush(Face)) g.FillRectangle(b, ClientRectangle);
            bool down = pressed || Active;
            if (down) W95.Sunken(g, ClientRectangle); else W95.Raised(g, ClientRectangle);
            var r = ClientRectangle;
            if (down) r.Offset(W95.S(1), W95.S(1));
            TextRenderer.DrawText(g, Text, Font, r, ForeColor, W95.Center);
        }
    }

    // "= 20 m/s" with the unit in navy, shrinking to fit like the web display
    class ResultView : Control
    {
        enum Mode { Empty, Value, Error }
        Mode mode = Mode.Empty;
        string num = "", unit = "", err = "";

        public ResultView()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            BackColor = Color.White;
        }

        public void ShowEmpty() { mode = Mode.Empty; Invalidate(); }
        public void ShowValue(string n, string u) { mode = Mode.Value; num = n; unit = u; Invalidate(); }
        public void ShowError(string m) { mode = Mode.Error; err = m; Invalidate(); }

        static int TextW(string s, Font f)
        {
            return s == "" ? 0 : TextRenderer.MeasureText(s, f, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            var flags = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            if (mode == Mode.Empty)
            {
                TextRenderer.DrawText(g, "—", W95.Font("Courier New", 40, true), ClientRectangle, W95.EmptyText, flags);
                return;
            }
            if (mode == Mode.Error)
            {
                var f = W95.Font("Courier New", 17, true);
                TextRenderer.DrawText(g, "⚠ " + err, f, ClientRectangle, W95.Red,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }
            string main = "= " + num;
            bool tight = unit == "°";
            Font font = null;
            int wMain = 0, wUnit = 0, gap = 0;
            for (float size = 40; size >= 16; size -= 1.5f)
            {
                font = W95.Font("Courier New", size, true);
                wMain = TextW(main, font);
                wUnit = TextW(unit, font);
                gap = unit == "" || tight ? 0 : (int)(0.3f * size * W95.K);
                if (wMain + gap + wUnit <= ClientSize.Width) break;
            }
            int x = ClientSize.Width - (wMain + gap + wUnit);
            var tf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            TextRenderer.DrawText(g, main, font, new Rectangle(x, 0, wMain + W95.S(4), ClientSize.Height), W95.Text, tf);
            if (unit != "")
                TextRenderer.DrawText(g, unit, font, new Rectangle(x + wMain + gap, 0, wUnit + W95.S(4), ClientSize.Height), W95.Navy, tf);
        }
    }

    // A white or grey box with its own scroll bar; subclasses paint the content.
    abstract class ScrollBox : Control
    {
        protected readonly VScrollBar Bar = new VScrollBar();
        protected int Offset;
        protected readonly bool Bevel;

        protected ScrollBox(bool bevel)
        {
            Bevel = bevel;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            Bar.Visible = false;
            Bar.ValueChanged += delegate { Offset = Bar.Value; Invalidate(); };
            Controls.Add(Bar);
        }

        protected Rectangle Outer
        {
            get { int b = Bevel ? W95.BevelWidth : 0; return Rectangle.Inflate(ClientRectangle, -b, -b); }
        }

        protected Rectangle Inner
        {
            get { var r = Outer; if (Bar.Visible) r.Width -= Bar.Width; return r; }
        }

        protected abstract int ContentHeight(int width);

        public void UpdateScroll()
        {
            var o = Outer;
            int h = ContentHeight(o.Width);
            bool need = h > o.Height;
            if (need)
            {
                h = ContentHeight(o.Width - SystemInformation.VerticalScrollBarWidth);
                Bar.SetBounds(o.Right - SystemInformation.VerticalScrollBarWidth, o.Y, SystemInformation.VerticalScrollBarWidth, o.Height);
                Bar.Minimum = 0;
                Bar.LargeChange = Math.Max(1, o.Height);
                Bar.SmallChange = W95.S(18);
                Bar.Maximum = Math.Max(0, h - 1);
                if (Bar.Value > h - o.Height) Bar.Value = Math.Max(0, h - o.Height);
            }
            else Bar.Value = 0;
            Bar.Visible = need;
            Offset = Bar.Value;
            Invalidate();
        }

        public void ScrollToTop() { Bar.Value = 0; Offset = 0; Invalidate(); }

        protected override void OnResize(EventArgs e) { base.OnResize(e); UpdateScroll(); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (!Bar.Visible) return;
            int max = Bar.Maximum - Bar.LargeChange + 1;
            Bar.Value = Math.Max(0, Math.Min(max, Bar.Value - e.Delta / 120 * W95.S(36)));
        }
    }

    class HistItem { public string Expr, Out, Insert; }

    // History tape, newest first; click a line to reuse its result
    class HistoryView : ScrollBox
    {
        public readonly List<HistItem> Items = new List<HistItem>();
        public event Action<HistItem> Picked;
        int hover = -1;

        public HistoryView() : base(true) { BackColor = Color.White; }

        int RowH { get { return W95.S(18); } }
        Font Mono { get { return W95.Font("Courier New", 13, false); } }
        Font MonoBold { get { return W95.Font("Courier New", 13, true); } }

        // Hint shown while the tape is empty: plain words and code samples
        static readonly string[][] HINT = {
            new[] { "Numbers can carry units: ", "`3 m/s × 2 s" },
            new[] { "Convert with “to”: ", "`72 km/h to m/s", ", ", "`25 °C to °F" },
            new[] { "Constants: ", "`G Mearth / Rearth^2", " · ½mv²: ", "`1/2 × 2 kg × (3 m/s)^2" } };

        struct Word { public string Text; public bool Code; public Rectangle R; }

        List<Word> LayoutHint(int width)
        {
            var words = new List<Word>();
            int pad = W95.S(5), x = pad, y = W95.S(2), lineH = W95.S(19);
            var plain = W95.Font("Tahoma", 12.5f, false);
            var code = W95.Font("Courier New", 13.5f, true);
            var flags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            foreach (var line in HINT)
            {
                foreach (var seg in line)
                {
                    bool isCode = seg.StartsWith("`");
                    string text = isCode ? seg.Substring(1) : seg;
                    var f = isCode ? code : plain;
                    foreach (var piece in Regex.Split(text, @"(?<= )"))
                    {
                        if (piece == "") continue;
                        // MeasureText pads every call, so take the advance by difference
                        int w = TextRenderer.MeasureText(piece + "|", f, Size.Empty, flags).Width
                              - TextRenderer.MeasureText("|", f, Size.Empty, flags).Width;
                        if (x + w > width - pad && x > pad) { x = pad; y += lineH; }
                        words.Add(new Word { Text = piece, Code = isCode, R = new Rectangle(x, y, w + W95.S(2), lineH) });
                        x += w;
                    }
                }
                x = pad;
                y += lineH;
            }
            return words;
        }

        protected override int ContentHeight(int width)
        {
            if (Items.Count == 0)
            {
                var w = LayoutHint(width);
                return w.Count == 0 ? 0 : w[w.Count - 1].R.Bottom + W95.S(4);
            }
            return Items.Count * RowH + W95.S(6);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Color.White);
            W95.Sunken(g, ClientRectangle);
            var inner = Inner;
            g.SetClip(inner);
            if (Items.Count == 0)
            {
                foreach (var w in LayoutHint(inner.Width))
                {
                    var r = w.R;
                    r.Offset(inner.X, inner.Y - Offset);
                    TextRenderer.DrawText(g, w.Text, w.Code ? W95.Font("Courier New", 13.5f, true) : W95.Font("Tahoma", 12.5f, false),
                        r, w.Code ? W95.Navy : W95.Dim, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                }
                return;
            }
            int pad = W95.S(5);
            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[Items.Count - 1 - i];
                var row = new Rectangle(inner.X + W95.S(3), inner.Y + W95.S(3) + i * RowH - Offset, inner.Width - W95.S(6), RowH);
                if (row.Bottom < inner.Y || row.Y > inner.Bottom) continue;
                bool hot = i == hover;
                if (hot) using (var b = new SolidBrush(W95.TitleFrom)) g.FillRectangle(b, row);
                string res = "= " + item.Out;
                int wRes = TextRenderer.MeasureText(res, MonoBold, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                var resRect = new Rectangle(row.Right - pad - wRes, row.Y, wRes + W95.S(2), row.Height);
                var exprRect = new Rectangle(row.X + pad, row.Y, Math.Max(0, resRect.X - row.X - pad - W95.S(10)), row.Height);
                var tf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
                TextRenderer.DrawText(g, item.Expr, Mono, exprRect, hot ? Color.White : W95.Dim, tf | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(g, res, MonoBold, resRect, hot ? Color.White : W95.Text, tf);
            }
        }

        int RowAt(Point p)
        {
            if (Items.Count == 0 || !Inner.Contains(p)) return -1;
            int i = (p.Y - Inner.Y - W95.S(3) + Offset) / RowH;
            return i >= 0 && i < Items.Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = RowAt(e.Location);
            if (h != hover) { hover = h; Cursor = h >= 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            int i = RowAt(e.Location);
            if (i >= 0 && Picked != null) Picked(Items[Items.Count - 1 - i]);
        }
    }

    // Constants pad: one raised button per constant, scrolling
    class ConstView : ScrollBox
    {
        public event Action<string> Picked;
        int pressed = -1;

        public ConstView() : base(false) { BackColor = W95.Face; }

        int Pad { get { return W95.S(8); } }
        int RowH { get { return W95.S(44); } }
        int Gap { get { return W95.S(4); } }

        protected override int ContentHeight(int width)
        {
            return 2 * Pad + Engine.CONSTANTS.Length * (RowH + Gap) - Gap;
        }

        Rectangle RowRect(int i)
        {
            var inner = Inner;
            return new Rectangle(inner.X + Pad, inner.Y + Pad + i * (RowH + Gap) - Offset, inner.Width - 2 * Pad, RowH);
        }

        static string Pretty(string val)
        {
            return Regex.Replace(val, @"e(-?\d+)", " ×10^$1");
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(W95.Face);
            g.SetClip(Inner);
            var tf = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis;
            var nameFont = W95.Font("Courier New", 16.5f, true);
            int nameW = TextRenderer.MeasureText("Mearth", nameFont, Size.Empty, TextFormatFlags.NoPadding).Width + W95.S(12);
            for (int i = 0; i < Engine.CONSTANTS.Length; i++)
            {
                var r = RowRect(i);
                if (r.Bottom < 0 || r.Y > ClientSize.Height) continue;
                var c = Engine.CONSTANTS[i];
                bool down = i == pressed;
                if (down) W95.Sunken(g, r); else W95.Raised(g, r);
                int dx = down ? W95.S(1) : 0;
                int x = r.X + W95.S(10) + dx;
                TextRenderer.DrawText(g, c.Name, nameFont, new Rectangle(x, r.Y + dx, nameW, r.Height), W95.Navy, tf);
                int x2 = x + nameW;
                int w2 = r.Right - x2 - W95.S(6);
                TextRenderer.DrawText(g, Pretty(c.Value) + " " + c.Unit, W95.Font("Courier New", 13.5f, true),
                    new Rectangle(x2, r.Y + W95.S(4) + dx, w2, r.Height / 2 - W95.S(2)), W95.Text, tf);
                TextRenderer.DrawText(g, c.Desc, W95.Font("Tahoma", 12, false),
                    new Rectangle(x2, r.Y + r.Height / 2 + dx, w2, r.Height / 2 - W95.S(4)), W95.Dim, tf);
            }
        }

        int RowAt(Point p)
        {
            for (int i = 0; i < Engine.CONSTANTS.Length; i++) if (RowRect(i).Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = RowAt(e.Location);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int hit = RowAt(e.Location);
            int was = pressed;
            pressed = -1;
            Invalidate();
            if (was >= 0 && hit == was && Picked != null) Picked(Engine.CONSTANTS[was].Name);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = RowAt(e.Location) >= 0 ? Cursors.Hand : Cursors.Default;
        }
    }

    // A 4 × 10 grid of keys
    class PadPanel : Control
    {
        public class Cell { public int Row, Col, Span = 1; }

        public PadPanel()
        {
            SetStyle(ControlStyles.Selectable, false);
            BackColor = W95.Face;
        }

        public void Place(W95Button b, int row, int col, int span = 1)
        {
            b.Tag = new Cell { Row = row, Col = col, Span = span };
            Controls.Add(b);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int pad = W95.S(8), gap = W95.S(4);
            float cw = (ClientSize.Width - 2 * pad - 3 * gap) / 4f;
            float ch = (ClientSize.Height - 2 * pad - 9 * gap) / 10f;
            foreach (Control c in Controls)
            {
                var cell = c.Tag as Cell;
                if (cell == null) continue;
                int x = (int)Math.Round(pad + cell.Col * (cw + gap));
                int y = (int)Math.Round(pad + cell.Row * (ch + gap));
                int x2 = (int)Math.Round(pad + (cell.Col + cell.Span) * (cw + gap) - gap);
                int y2 = (int)Math.Round(pad + (cell.Row + 1) * (ch + gap) - gap);
                c.SetBounds(x, y, x2 - x, y2 - y);
            }
        }
    }

    class Settings
    {
        public string AngleMode = "deg";
        public int? Dec, Sig;
        public bool Pow10, TopMost;
        public Rectangle Bounds = Rectangle.Empty;

        static string Dir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhysicsCalculator"); } }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                var map = new Dictionary<string, string>();
                foreach (var line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                string v;
                int n;
                if (map.TryGetValue("AngleMode", out v) && (v == "deg" || v == "rad")) s.AngleMode = v;
                if (map.TryGetValue("Dec", out v) && int.TryParse(v, out n) && n >= 0 && n <= 10) s.Dec = n;
                if (map.TryGetValue("Sig", out v) && int.TryParse(v, out n) && n >= 1 && n <= 8) s.Sig = n;
                if (s.Dec.HasValue && s.Sig.HasValue) s.Sig = null;
                if (map.TryGetValue("Pow10", out v)) s.Pow10 = v == "True";
                if (map.TryGetValue("TopMost", out v)) s.TopMost = v == "True";
                if (map.TryGetValue("Bounds", out v))
                {
                    var p = v.Split(',');
                    int x, y, w, h;
                    if (p.Length == 4 && int.TryParse(p[0], out x) && int.TryParse(p[1], out y) &&
                        int.TryParse(p[2], out w) && int.TryParse(p[3], out h))
                        s.Bounds = new Rectangle(x, y, w, h);
                }
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllLines(FilePath, new[] {
                    "AngleMode=" + AngleMode,
                    "Dec=" + (Dec.HasValue ? Dec.Value.ToString() : "auto"),
                    "Sig=" + (Sig.HasValue ? Sig.Value.ToString() : "auto"),
                    "Pow10=" + Pow10, "TopMost=" + TopMost,
                    "Bounds=" + Bounds.X + "," + Bounds.Y + "," + Bounds.Width + "," + Bounds.Height });
            }
            catch { }
        }
    }

    class CalcForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        readonly Engine engine = new Engine();
        readonly Settings cfg = Settings.Load();
        readonly ToolTip tip = new ToolTip();

        // Display
        readonly Control header = new Control();
        readonly Label title = new Label();
        readonly W95Button btnPin = new W95Button(""), btnAbout = new W95Button("?"), btnDeg = new W95Button("DEG"), btnRad = new W95Button("RAD");
        readonly Control display = new Control();
        readonly TextBox expr = new TextBox();
        readonly ResultView result = new ResultView();
        readonly Label ans = new Label();
        readonly Label lblDec = new Label(), lblSig = new Label();
        readonly ComboBox decSel = new ComboBox(), sigSel = new ComboBox();
        readonly W95Button btnPow10 = new W95Button("×10^");
        readonly HistoryView hist = new HistoryView();
        readonly W95Button[] tabs = { new W95Button("Keys"), new W95Button("Units"), new W95Button("Constants") };
        readonly PadPanel keysPad = new PadPanel(), unitsPad = new PadPanel();
        readonly ConstView constPad = new ConstView();

        bool resultShown;
        Result lastResult;
        int histIdx;
        static readonly Regex CONTINUES = new Regex(@"^\s*([-+−×÷*/^·]|to\b)");

        public CalcForm()
        {
            using (var g = CreateGraphics()) W95.K = g.DpiX / 96f;
            Text = "Physics Calculator";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = W95.Face;
            Font = W95.Font("Tahoma", 12, false);
            StartPosition = FormStartPosition.Manual;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            MinimumSize = SizeFromClientSize(new Size(W95.S(320), W95.S(600)));

            BuildHeader();
            BuildDisplay();
            BuildPads();

            Controls.AddRange(new Control[] { header, display, hist, keysPad, unitsPad, constPad });
            foreach (var t in tabs) Controls.Add(t);

            ApplySettings();
            PlaceWindow();
            RenderHistory();
            ShowAns();
            Shown += delegate { expr.Focus(); };
        }

        // ── Building the window ──

        void BuildHeader()
        {
            header.BackColor = W95.Face;
            header.Paint += (s, e) =>
            {
                using (var b = new SolidBrush(W95.Sh)) e.Graphics.FillRectangle(b, 0, header.Height - W95.S(2), header.Width, W95.S(1));
                using (var b = new SolidBrush(W95.Lt)) e.Graphics.FillRectangle(b, 0, header.Height - W95.S(1), header.Width, W95.S(1));
            };
            title.Text = "PHYSICS CALCULATOR";
            title.Font = W95.Font("Tahoma", 12, true);
            title.AutoSize = false;
            title.TextAlign = ContentAlignment.MiddleLeft;
            title.BackColor = W95.Face;
            header.Controls.Add(title);

            btnPin.Font = W95.Font("Segoe MDL2 Assets", 12, false);
            btnPin.Click += delegate { SetTopMost(!TopMost); };
            btnAbout.Font = W95.Font("Tahoma", 12.5f, true);
            btnAbout.Click += delegate { using (var a = new AboutForm()) a.ShowDialog(this); expr.Focus(); };
            tip.SetToolTip(btnAbout, "About Physics Calculator");
            foreach (var b in new[] { btnDeg, btnRad })
            {
                b.Font = W95.Font("Tahoma", 12.5f, true);
                var mode = b == btnDeg ? "deg" : "rad";
                b.Click += delegate { SetAngleMode(mode); };
            }
            tip.SetToolTip(btnDeg, "Plain numbers in sin/cos/tan are degrees");
            tip.SetToolTip(btnRad, "Plain numbers in sin/cos/tan are radians");
            header.Controls.AddRange(new Control[] { btnPin, btnAbout, btnDeg, btnRad });
        }

        void BuildDisplay()
        {
            display.BackColor = Color.White;
            display.Paint += (s, e) => W95.Sunken(e.Graphics, display.ClientRectangle);

            expr.BorderStyle = BorderStyle.None;
            expr.BackColor = Color.White;
            expr.ForeColor = W95.Text;
            expr.Font = W95.Font("Courier New", 26, true);
            expr.TextAlign = HorizontalAlignment.Right;
            expr.HandleCreated += delegate { SendMessage(expr.Handle, 0x1501 /* EM_SETCUEBANNER */, (IntPtr)1, "e.g. 72 km/h to m/s"); };
            tip.SetToolTip(expr, "Numbers can carry units: 3 m/s × 2 s. Convert with “to”: 72 km/h to m/s. ↑/↓ recall earlier lines.");
            expr.KeyDown += ExprKeyDown;
            expr.KeyPress += ExprKeyPress;

            ans.Font = W95.Font("Courier New", 14, false);
            ans.ForeColor = W95.Dim;
            ans.BackColor = Color.White;
            ans.TextAlign = ContentAlignment.MiddleRight;
            ans.AutoSize = false;

            foreach (var l in new[] { lblDec, lblSig })
            {
                l.Font = W95.Font("Tahoma", 12, false);
                l.ForeColor = W95.Dim;
                l.BackColor = Color.White;
                l.AutoSize = true;
            }
            lblDec.Text = "Dec";
            lblSig.Text = "Sig. fig.";

            decSel.Items.Add("auto");
            for (int i = 0; i <= 10; i++) decSel.Items.Add(i.ToString());
            sigSel.Items.Add("auto");
            for (int i = 1; i <= 8; i++) sigSel.Items.Add(i.ToString());
            foreach (var c in new[] { decSel, sigSel })
            {
                c.DropDownStyle = ComboBoxStyle.DropDownList;
                c.Font = W95.Font("Tahoma", 12, false);
                c.TabStop = false;
                c.DropDownClosed += delegate { BeginInvoke((Action)(() => expr.Focus())); };
            }
            decSel.SelectedIndexChanged += delegate
            {
                engine.DecPlaces = decSel.SelectedIndex <= 0 ? (int?)null : decSel.SelectedIndex - 1;
                if (engine.DecPlaces.HasValue && sigSel.SelectedIndex != 0) { engine.SigFigs = null; sigSel.SelectedIndex = 0; }
                cfg.Dec = engine.DecPlaces;
                cfg.Sig = engine.SigFigs;
                RefreshShown();
            };
            sigSel.SelectedIndexChanged += delegate
            {
                engine.SigFigs = sigSel.SelectedIndex <= 0 ? (int?)null : sigSel.SelectedIndex;
                if (engine.SigFigs.HasValue && decSel.SelectedIndex != 0) { engine.DecPlaces = null; decSel.SelectedIndex = 0; }
                cfg.Dec = engine.DecPlaces;
                cfg.Sig = engine.SigFigs;
                RefreshShown();
            };

            btnPow10.Font = W95.Font("Courier New", 12.5f, true);
            tip.SetToolTip(btnPow10, "Toggle ×10^ notation");
            btnPow10.Click += delegate
            {
                engine.Pow10 = !engine.Pow10;
                cfg.Pow10 = engine.Pow10;
                btnPow10.Active = engine.Pow10;
                btnPow10.ForeColor = engine.Pow10 ? W95.Navy : W95.Text;
                btnPow10.Invalidate();
                RefreshShown();
                expr.Focus();
            };

            display.Controls.AddRange(new Control[] { expr, result, ans, lblDec, decSel, lblSig, sigSel, btnPow10 });

            hist.Picked += item => { if (item.Insert != "") InsertAt(item.Insert); };
            tip.SetToolTip(hist, "Click a line to use its result");
        }

        W95Button Key(string text, Color face, Color fore, string family = "Tahoma", float size = 15)
        {
            var b = new W95Button(text) { Face = face, ForeColor = fore, Font = W95.Font(family, size, true) };
            return b;
        }

        void BuildPads()
        {
            // Keys: [text, insert or action, kind]
            string[,] keys = {
                { "sin", "sin(", "fn" }, { "cos", "cos(", "fn" }, { "tan", "tan(", "fn" }, { "⌫", "!back", "ctrl" },
                { "sin⁻¹", "asin(", "fn" }, { "cos⁻¹", "acos(", "fn" }, { "tan⁻¹", "atan(", "fn" }, { "AC", "!clear", "ctrl" },
                { "log", "log(", "fn" }, { "ln", "ln(", "fn" }, { "√", "sqrt(", "fn" }, { "x²", "^2", "fn" },
                { "π", "π", "spec" }, { "e", "e", "spec" }, { "×10^", "*10^", "spec" }, { "xʸ", "^", "op" },
                { "(", "(", "op" }, { ")", ")", "op" }, { "to", " to ", "spec" }, { "ANS", "ANS", "spec" },
                { "7", "7", "num" }, { "8", "8", "num" }, { "9", "9", "num" }, { "÷", "÷", "op" },
                { "4", "4", "num" }, { "5", "5", "num" }, { "6", "6", "num" }, { "×", "×", "op" },
                { "1", "1", "num" }, { "2", "2", "num" }, { "3", "3", "num" }, { "−", "−", "op" },
                { "±", "!negate", "op" }, { "0", "0", "num" }, { ".", ".", "num" }, { "+", "+", "op" } };
            for (int i = 0; i < keys.GetLength(0); i++)
            {
                var b = MakeKey(keys[i, 0], keys[i, 1], keys[i, 2]);
                keysPad.Place(b, i / 4, i % 4);
            }
            keysPad.Place(MakeKey("= (Enter)", "!eq", "eq"), 9, 0, 4);

            string[] units = { "m", "cm", "mm", "km", "s", "min", "h", "ms", "kg", "g", "N", "J", "W", "Pa", "Hz", "kWh",
                               "m/s", "km/h", "m/s²", "eV", "A", "V", "Ω", "C", "K", "°C", "°", "rad", "L", "atm", "bar", "mol",
                               "·", "/", "²", "³" };
            for (int i = 0; i < units.Length; i++)
            {
                bool op = "·/²³".Contains(units[i]) && units[i].Length == 1;
                unitsPad.Place(MakeKey(units[i], units[i], op ? "op" : "unit"), i / 4, i % 4);
            }
            unitsPad.Place(MakeKey("to", " to ", "spec"), 9, 0, 2);
            unitsPad.Place(MakeKey("⌫", "!back", "ctrl"), 9, 2);
            unitsPad.Place(MakeKey("=", "!eq", "eq"), 9, 3);

            constPad.Picked += name => InsertAt(name);

            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                tabs[i].Font = W95.Font("Tahoma", 12.5f, true);
                tabs[i].Click += delegate { ShowPad(idx); expr.Focus(); };
            }
            ShowPad(0);
        }

        W95Button MakeKey(string text, string what, string kind)
        {
            W95Button b;
            switch (kind)
            {
                case "fn": b = Key(text, W95.Green, Color.White); break;
                case "op": b = Key(text, W95.Navy, Color.White); break;
                case "spec": b = Key(text, W95.Amber, Color.White); break;
                case "ctrl": b = Key(text, W95.Red, Color.White); break;
                case "eq": b = Key(text, W95.Go, Color.White, "Tahoma", 17); break;
                case "unit": b = Key(text, Color.White, W95.Navy, "Courier New", 15); break;
                default: b = Key(text, Color.White, Color.Black); break;
            }
            if (what == " to ") tip.SetToolTip(b, "Convert the result, e.g. 72 km/h to m/s");
            b.Click += delegate
            {
                if (what.StartsWith("!")) DoAction(what.Substring(1));
                else InsertAt(what);
            };
            return b;
        }

        void ShowPad(int idx)
        {
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Active = i == idx;
                tabs[i].ForeColor = i == idx ? W95.Navy : W95.Text;
                tabs[i].Invalidate();
            }
            keysPad.Visible = idx == 0;
            unitsPad.Visible = idx == 1;
            constPad.Visible = idx == 2;
            if (idx == 2) constPad.UpdateScroll();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (header == null) return;
            int W = ClientSize.Width, H = ClientSize.Height;
            int m = W95.S(8);

            // Header: title on the left; pin, about, DEG/RAD on the right
            int hh = W95.S(40);
            header.SetBounds(0, 0, W, hh);
            int bh = W95.S(26), by = (hh - bh) / 2;
            int x = W - W95.S(10);
            btnRad.SetBounds(x - W95.S(44), by, W95.S(44), bh); x -= W95.S(44);
            btnDeg.SetBounds(x - W95.S(44), by, W95.S(44), bh); x -= W95.S(44) + W95.S(8);
            btnAbout.SetBounds(x - W95.S(26), by, W95.S(26), bh); x -= W95.S(26) + W95.S(3);
            btnPin.SetBounds(x - W95.S(26), by, W95.S(26), bh); x -= W95.S(26);
            title.SetBounds(W95.S(10), 0, Math.Max(0, x - W95.S(14)), hh - W95.S(2));

            // Display
            int y = hh + m;
            int dx = W95.S(12), dw = W - 2 * m;
            int exprH = expr.PreferredHeight;
            int resH = W95.S(50), ansH = W95.S(18), optH = W95.S(26);
            int dh = W95.S(10) + exprH + W95.S(3) + resH + W95.S(3) + ansH + W95.S(6) + optH + W95.S(8);
            display.SetBounds(m, y, dw, dh);
            int iy = W95.S(10);
            expr.SetBounds(dx, iy, dw - 2 * dx, exprH); iy += exprH + W95.S(3);
            result.SetBounds(dx, iy, dw - 2 * dx, resH); iy += resH + W95.S(3);
            ans.SetBounds(dx, iy, dw - 2 * dx, ansH); iy += ansH + W95.S(6);
            int ox = dx;
            int comboH = decSel.PreferredHeight;
            int cy = iy + (optH - comboH) / 2;
            lblDec.Location = new Point(ox, iy + (optH - lblDec.PreferredHeight) / 2); ox += lblDec.PreferredWidth + W95.S(6);
            decSel.SetBounds(ox, cy, W95.S(58), comboH); ox += W95.S(58) + W95.S(10);
            lblSig.Location = new Point(ox, iy + (optH - lblSig.PreferredHeight) / 2); ox += lblSig.PreferredWidth + W95.S(6);
            sigSel.SetBounds(ox, cy, W95.S(58), comboH); ox += W95.S(58) + W95.S(10);
            btnPow10.SetBounds(ox, iy, W95.S(52), optH);
            y += dh;

            // History tape
            y += W95.S(6);
            int histH = W95.S(92);
            hist.SetBounds(m, y, W - 2 * m, histH);
            y += histH;

            // Pad tabs and pads
            y += m;
            int tw = (W - 2 * m) / 3;
            for (int i = 0; i < 3; i++)
                tabs[i].SetBounds(m + i * tw, y, i == 2 ? W - 2 * m - 2 * tw : tw, W95.S(26));
            y += W95.S(26);
            foreach (Control pad in new Control[] { keysPad, unitsPad, constPad })
                pad.SetBounds(0, y, W, Math.Max(0, H - y));
            hist.UpdateScroll();
            constPad.UpdateScroll();
        }

        void ApplySettings()
        {
            engine.AngleMode = cfg.AngleMode;
            engine.DecPlaces = cfg.Dec;
            engine.SigFigs = cfg.Sig;
            engine.Pow10 = cfg.Pow10;
            decSel.SelectedIndex = cfg.Dec.HasValue ? cfg.Dec.Value + 1 : 0;
            sigSel.SelectedIndex = cfg.Sig.HasValue ? cfg.Sig.Value : 0;
            btnPow10.Active = cfg.Pow10;
            btnPow10.ForeColor = cfg.Pow10 ? W95.Navy : W95.Text;
            UpdateModeButtons();
            SetTopMost(cfg.TopMost);
        }

        void PlaceWindow()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            var size = SizeFromClientSize(new Size(W95.S(380), W95.S(720)));
            if (size.Height > wa.Height - W95.S(20)) size.Height = Math.Max(MinimumSize.Height, wa.Height - W95.S(20));
            var b = new Rectangle(wa.Right - size.Width - W95.S(24), wa.Top + (wa.Height - size.Height) / 2, size.Width, size.Height);
            if (cfg.Bounds.Width > 0)
            {
                foreach (var scr in Screen.AllScreens)
                    if (scr.WorkingArea.IntersectsWith(new Rectangle(cfg.Bounds.X + W95.S(40), cfg.Bounds.Y, cfg.Bounds.Width - W95.S(80), W95.S(30))))
                    {
                        b = cfg.Bounds;
                        break;
                    }
            }
            Bounds = b;
        }

        void SetTopMost(bool on)
        {
            TopMost = on;
            cfg.TopMost = on;
            btnPin.Text = on ? "" : "";
            btnPin.Active = on;
            btnPin.Invalidate();
            tip.SetToolTip(btnPin, on ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)");
            expr.Focus();
        }

        void SetAngleMode(string mode)
        {
            engine.AngleMode = mode;
            cfg.AngleMode = mode;
            UpdateModeButtons();
            RefreshShown();
            expr.Focus();
        }

        void UpdateModeButtons()
        {
            btnDeg.Active = engine.AngleMode == "deg";
            btnRad.Active = engine.AngleMode == "rad";
            btnDeg.Invalidate();
            btnRad.Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cfg.Bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            cfg.Save();
            base.OnFormClosing(e);
        }

        // ── Calculator behaviour (as in the web page) ──

        void ShowAns() { ans.Text = "ANS = " + Engine.JoinOut(engine.FormatQ(engine.LastAns)); }

        void ShowResult(string[] o) { result.ShowValue(o[0], o[1]); }

        void RenderHistory()
        {
            hist.UpdateScroll();
            hist.ScrollToTop();
        }

        void ResetCalcDisplay()
        {
            expr.Text = "";
            result.ShowEmpty();
            resultShown = false;
        }

        void Commit()
        {
            string raw = expr.Text;
            if (raw.Trim() == "") return;
            Result r;
            try { r = engine.Evaluate(raw); }
            catch (CalcError ex) { result.ShowError(ex.Message); return; }
            catch (Exception) { result.ShowError("syntax error"); return; }
            engine.LastAns = r.Q;
            lastResult = r;
            ShowAns();
            var o = engine.FormatResult(r);
            ShowResult(o);
            hist.Items.Add(new HistItem { Expr = raw.Trim(), Out = Engine.JoinOut(o), Insert = Engine.ReinsertText(r) });
            if (hist.Items.Count > 50) hist.Items.RemoveAt(0);
            histIdx = hist.Items.Count;
            RenderHistory();
            resultShown = true;
            // expression stays visible so the user can see what they calculated
        }

        // Re-render after a format or angle-mode change
        void RefreshShown()
        {
            ShowAns();
            if (resultShown && lastResult != null) ShowResult(engine.FormatResult(lastResult));
        }

        void ExprKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; Commit(); return; }
            if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; ResetCalcDisplay(); return; }
            if ((e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) && hist.Items.Count > 0)
            {
                e.SuppressKeyPress = true;
                e.Handled = true;
                histIdx = Math.Max(0, Math.Min(hist.Items.Count, histIdx + (e.KeyCode == Keys.Up ? -1 : 1)));
                expr.Text = histIdx < hist.Items.Count ? hist.Items[histIdx].Expr : "";
                expr.SelectionStart = expr.Text.Length;
                result.ShowEmpty();
                resultShown = false;
                return;
            }
            // Backspace/Delete after a result clear it, like any other content key
            if (resultShown && (e.KeyCode == Keys.Back || e.KeyCode == Keys.Delete) && !e.Control && !e.Alt)
            {
                expr.Text = "";
                result.ShowEmpty();
                resultShown = false;
            }
        }

        void ExprKeyPress(object sender, KeyPressEventArgs e)
        {
            if (!resultShown || char.IsControl(e.KeyChar)) return;
            if ((ModifierKeys & (Keys.Control | Keys.Alt)) != 0 && (ModifierKeys & (Keys.Control | Keys.Alt)) != (Keys.Control | Keys.Alt)) return;
            // After a result, a new key starts fresh; an operator carries on from ANS
            expr.Text = CONTINUES.IsMatch(e.KeyChar.ToString()) ? "ANS" : "";
            expr.SelectionStart = expr.Text.Length;
            result.ShowEmpty();
            resultShown = false;
        }

        void InsertAt(string text)
        {
            int s, f;
            if (resultShown)
            {
                ResetCalcDisplay();
                if (CONTINUES.IsMatch(text)) expr.Text = "ANS";
                s = f = expr.Text.Length;
            }
            else
            {
                s = expr.SelectionStart;
                f = s + expr.SelectionLength;
            }
            string before = expr.Text.Substring(0, s);
            // Keep units and constants apart from what comes before: "3 m s", not "3ms"
            if (Regex.IsMatch(text, @"^[\p{L}°]") && Regex.IsMatch(before, @"[\p{L}0-9)°⁰-⁹²³¹]$")) text = " " + text;
            expr.Text = before + text + expr.Text.Substring(f);
            expr.SelectionStart = s + text.Length;
            expr.SelectionLength = 0;
            expr.Focus();
        }

        void DoAction(string act)
        {
            switch (act)
            {
                case "back":
                    if (resultShown) { ResetCalcDisplay(); expr.Focus(); return; }
                    int s = expr.SelectionStart, f = s + expr.SelectionLength;
                    if (s != f) { expr.Text = expr.Text.Substring(0, s) + expr.Text.Substring(f); expr.SelectionStart = s; }
                    else if (s > 0) { expr.Text = expr.Text.Substring(0, s - 1) + expr.Text.Substring(s); expr.SelectionStart = s - 1; }
                    expr.Focus();
                    break;
                case "clear":
                    ResetCalcDisplay();
                    expr.Focus();
                    break;
                case "eq":
                    Commit();
                    expr.Focus();
                    break;
                case "negate":
                    if (resultShown) ResetCalcDisplay();
                    string v = expr.Text.Trim();
                    expr.Text = v.StartsWith("-") ? v.Substring(1) : (v != "" ? "-(" + v + ")" : "");
                    expr.SelectionStart = expr.Text.Length;
                    expr.Focus();
                    break;
            }
        }
    }

    class AboutForm : Form
    {
        const string License =
            "MIT License\r\n\r\n" +
            "Copyright (c) 2026 Mauricio Mendivelso\r\n\r\n" +
            "Permission is hereby granted, free of charge, to any person obtaining a copy " +
            "of this software and associated documentation files (the \"Software\"), to deal " +
            "in the Software without restriction, including without limitation the rights " +
            "to use, copy, modify, merge, publish, distribute, sublicense, and/or sell " +
            "copies of the Software, and to permit persons to whom the Software is " +
            "furnished to do so, subject to the following conditions:\r\n\r\n" +
            "The above copyright notice and this permission notice shall be included in all " +
            "copies or substantial portions of the Software.\r\n\r\n" +
            "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR " +
            "IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
            "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE " +
            "AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
            "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, " +
            "OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE " +
            "SOFTWARE.";

        public AboutForm()
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "About Physics Calculator";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 410);

            var icon = new PictureBox { Location = new Point(16, 16), Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom };
            var bmp = new Bitmap(96, 96, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) IconArt.Draw(g, 96);
            icon.Image = bmp;
            Controls.Add(icon);

            Controls.Add(new Label { Text = "Physics Calculator", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
            Controls.Add(new Label { Text = "Version 1.0  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Free software, provided \"as is\", without warranty of any kind. Check important results " +
                       "independently.\r\nThis app collects no personal data and never connects to the internet.",
                Location = new Point(16, 80), Size = new Size(390, 54)
            });

            Controls.Add(new TextBox
            {
                Text = License, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 142), Size = new Size(388, 214), TabStop = false,
                BackColor = SystemColors.Window
            });

            var ok = new Button { Text = "OK", Location = new Point(324, 368), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
            ResumeLayout(false);
        }
    }

    static class IconArt
    {
        static void Bevel(Graphics g, RectangleF r, float t, bool raised)
        {
            using (var hi = new SolidBrush(raised ? Color.White : Color.FromArgb(0x80, 0x80, 0x80)))
            using (var dk = new SolidBrush(raised ? Color.FromArgb(0x50, 0x50, 0x50) : Color.White))
            {
                g.FillRectangle(hi, r.X, r.Y, r.Width, t);
                g.FillRectangle(hi, r.X, r.Y, t, r.Height);
                g.FillRectangle(dk, r.X, r.Bottom - t, r.Width, t);
                g.FillRectangle(dk, r.Right - t, r.Y, t, r.Height);
            }
        }

        // A little Windows 95 calculator: grey body, white display, colored keys
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.None;
            g.Clear(Color.Transparent);
            float t = Math.Max(1, (float)Math.Round(s / 32));
            var body = new RectangleF((float)Math.Round(s * 0.12f), (float)Math.Round(s * 0.04f), (float)Math.Round(s * 0.76f), (float)Math.Round(s * 0.92f));
            using (var b = new SolidBrush(W95.Face)) g.FillRectangle(b, body);
            using (var b = new SolidBrush(Color.Black)) { g.FillRectangle(b, body.X, body.Bottom - t, body.Width, t); g.FillRectangle(b, body.Right - t, body.Y, t, body.Height); }
            Bevel(g, new RectangleF(body.X, body.Y, body.Width - t, body.Height - t), t, true);

            float pad = (float)Math.Round(s * 0.08f);
            var disp = new RectangleF(body.X + pad, body.Y + pad, body.Width - 2 * pad, (float)Math.Round(s * 0.2f));
            using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, disp);
            Bevel(g, disp, t, false);
            if (size >= 32)
                using (var b = new SolidBrush(W95.Navy))
                    g.FillRectangle(b, disp.Right - disp.Width * 0.45f, disp.Y + disp.Height * 0.38f, disp.Width * 0.3f, Math.Max(t, disp.Height * 0.22f));

            Color[,] keys = {
                { W95.Green, W95.Green, W95.Red },
                { Color.White, Color.White, W95.Navy },
                { Color.White, Color.White, W95.Navy } };
            float top = disp.Bottom + pad * 0.8f, gap = Math.Max(1, (float)Math.Round(s * 0.035f));
            float kw = (disp.Width - 2 * gap) / 3, kh = (body.Bottom - pad - top - 2 * gap) / 3;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    var k = new RectangleF(disp.X + c * (kw + gap), top + r * (kh + gap), kw, kh);
                    using (var b = new SolidBrush(keys[r, c])) g.FillRectangle(b, k);
                    if (size >= 32) Bevel(g, k, Math.Max(1, t / 2), true);
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
