// Screen Shapes for Windows: lines, rectangles, circles and guide lines that float over everything on screen
// and measure what's under them (twin of ../mac/main.swift). Builds with the C# compiler that ships with
// Windows (.NET Framework 4.x); see build.ps1. The shapes come from the Image Measure web tool.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

[assembly: AssemblyTitle("Screen Shapes")]
[assembly: AssemblyProduct("Screen Shapes")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ScreenShapes
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("shcore.dll")] static extern int SetProcessDpiAwareness(int value);

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--write-icon")
            {
                IconArt.WriteIco(args[1]);
                return 0;
            }
            if (args.Length == 1 && args[0] == "--selftest")
                return Measure.SelfTest();

            // Per-monitor DPI awareness, so one screen pixel is one pixel on every display
            bool aware = false;
            try { aware = SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { }
            if (!aware) try { aware = SetProcessDpiAwareness(2) == 0; } catch { }
            if (!aware) try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PanelForm());
            return 0;
        }
    }

    // ── Dark Windows 95 skin (same tokens as the other Ergosfera apps) ──
    static class Skin
    {
        public static readonly Color Face = Color.FromArgb(0x3b, 0x3b, 0x3b);
        public static readonly Color Hi = Color.FromArgb(0x6e, 0x6e, 0x6e);
        public static readonly Color Lt = Color.FromArgb(0x54, 0x54, 0x54);
        public static readonly Color Sh = Color.FromArgb(0x1c, 0x1c, 0x1c);
        public static readonly Color Dk = Color.Black;
        public static readonly Color Text = Color.FromArgb(0xe8, 0xe8, 0xe8);
        public static readonly Color Muted = Color.FromArgb(0x9a, 0x9a, 0x9a);
        public static readonly Color Lcd = Color.FromArgb(0x7f, 0xae, 0x86);
        public static readonly Color LcdDigit = Color.FromArgb(0x16, 0x32, 0x1c);
        public static readonly Color LcdDim = Color.FromArgb(0x3e, 0x62, 0x45);
        public static readonly Color Ink = Color.FromArgb(0x1e, 0x1e, 0x22);
        public static readonly Color Marker = Color.FromArgb(0xd0, 0x1c, 0x1c);

        // Shape colors: the Image Measure web tool's, then a few more
        public static readonly string[,] Palette = {
            { "Sky", "38bdf8" }, { "Violet", "a78bfa" }, { "Green", "34d399" }, { "Pink", "f472b6" }, { "Orange", "fb923c" },
            { "Yellow", "facc15" }, { "Red", "ef4444" }, { "Blue", "3b82f6" }, { "Black", "1e1e22" }, { "White", "ffffff" } };

        public static Color A(Color c, double alpha) { return Color.FromArgb((int)Math.Round(alpha * 255), c); }

        public static bool TryHex(string s, out Color c)
        {
            int v;
            c = Color.Black;
            if (s == null || s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return false;
            c = Color.FromArgb(v >> 16 & 255, v >> 8 & 255, v & 255);
            return true;
        }

        public static Color Hex(string s) { Color c; TryHex(s, out c); return c; }
        public static string HexString(Color c) { return (c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2")); }

        static void Edge(Graphics g, Rectangle r, int t, Color tl, Color br)
        {
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

        public static void Raised(Graphics g, Rectangle r, int t)
        {
            Edge(g, r, t, Hi, Dk);
            Edge(g, Rectangle.Inflate(r, -t, -t), t, Lt, Sh);
        }

        public static void Sunken(Graphics g, Rectangle r, int t)
        {
            Edge(g, r, t, Dk, Hi);
            Edge(g, Rectangle.Inflate(r, -t, -t), t, Sh, Lt);
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, RectangleF r, float radius, Color c)
        {
            using (var p = Round(r, radius))
            using (var b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static Bitmap Swatch(Color c)
        {
            var bmp = new Bitmap(12, 12);
            using (var g = Graphics.FromImage(bmp))
            {
                using (var b = new SolidBrush(c)) g.FillRectangle(b, 0, 0, 11, 11);
                using (var p = new Pen(A(Color.Black, 0.4))) g.DrawRectangle(p, 0, 0, 11, 11);
            }
            return bmp;
        }
    }

    // ── Units (same rules as the Mac app and Screen Ruler; checked by --selftest) ──

    // Sm, Skm, Scm: scaled (calibrated) m, km and cm
    enum Unit { Cm, In, Px, Sm, Skm, Scm }

    static class Units
    {
        public static readonly Unit[] All = { Unit.Cm, Unit.In, Unit.Px, Unit.Sm, Unit.Skm, Unit.Scm };

        public static bool Scaled(Unit u) { return u == Unit.Sm || u == Unit.Skm || u == Unit.Scm; }

        public static string Symbol(Unit u)
        {
            switch (u)
            {
                case Unit.Cm: case Unit.Scm: return "cm";
                case Unit.In: return "in";
                case Unit.Px: return "px";
                case Unit.Sm: return "m";
                default: return "km";
            }
        }

        public static string MenuTitle(Unit u)
        {
            switch (u)
            {
                case Unit.Cm: return "Centimeters (cm)";
                case Unit.In: return "Inches (in)";
                case Unit.Px: return "Pixels (px)";
                case Unit.Sm: return "Meters, scaled (m)";
                case Unit.Skm: return "Kilometers, scaled (km)";
                default: return "Centimeters, scaled (cm)";
            }
        }

        public static double Meters(Unit u)
        {
            switch (u)
            {
                case Unit.Sm: return 1;
                case Unit.Skm: return 1000;
                case Unit.Scm: return 0.01;
                default: return 0;
            }
        }

        // Saved names, the same as on the Mac
        public static string Key(Unit u)
        {
            switch (u)
            {
                case Unit.Cm: return "cm";
                case Unit.In: return "in";
                case Unit.Px: return "px";
                case Unit.Sm: return "sm";
                case Unit.Skm: return "skm";
                default: return "scm";
            }
        }

        public static bool Parse(string s, out Unit u)
        {
            foreach (Unit x in All)
                if (Key(x) == s) { u = x; return true; }
            u = Unit.Cm;
            return false;
        }
    }

    static class Measure
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Fixed(double v, int decimals) { return v.ToString("F" + decimals, Inv); }

        // Decimals for a reading: px whole, cm 2, in 3, scaled units 4 significant figures
        public static int Decimals(double v, Unit unit)
        {
            switch (unit)
            {
                case Unit.Px: return 0;
                case Unit.Cm: return 2;
                case Unit.In: return 3;
                default: return v == 0 ? 2 : Math.Min(6, Math.Max(0, 3 - (int)Math.Floor(Math.Log10(Math.Abs(v)))));
            }
        }

        public static string Number(double v, Unit unit) { return Fixed(v, Decimals(v, unit)); }
        public static string Format(double v, Unit unit) { return Number(v, unit) + " " + Units.Symbol(unit); }
        public static string FormatArea(double v, Unit unit) { return Number(v, unit) + " " + Units.Symbol(unit) + "²"; }
        public static string Angle(double a) { return Fixed(a, 1) + "°"; }

        public static double Norm180(double a)
        {
            double r = a % 360;
            if (r <= -180) r += 360;
            if (r > 180) r -= 360;
            return r;
        }

        // Distance from p to the segment ab
        public static float SegmentDistance(PointF p, PointF a, PointF b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y, len2 = dx * dx + dy * dy;
            double t = len2 == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2));
            double x = a.X + t * dx - p.X, y = a.Y + t * dy - p.Y;
            return (float)Math.Sqrt(x * x + y * y);
        }

        public static string Num(double v) { return v.ToString("R", Inv); }
        public static bool TryNum(string s, out double v) { return double.TryParse(s, NumberStyles.Float, Inv, out v); }

        public static int SelfTest()
        {
            int fails = 0;
            Action<bool, string> check = delegate(bool ok, string what)
            {
                if (!ok) { fails++; Console.WriteLine("FAIL: " + what); }
            };
            Func<double, double, bool> near = delegate(double a, double b) { return Math.Abs(a - b) < 1e-6; };
            check(Format(12.3456, Unit.Cm) == "12.35 cm", "format cm");
            check(Format(3.14159, Unit.In) == "3.142 in", "format in");
            check(Format(399.6, Unit.Px) == "400 px", "format px");
            check(Format(1234.56, Unit.Skm) == "1235 km", "format km");
            check(Format(0.012346, Unit.Sm) == "0.01235 m", "format m");
            check(Format(5.4321, Unit.Scm) == "5.432 cm", "format scaled cm");
            check(Format(0, Unit.Sm) == "0.00 m", "format zero");
            check(FormatArea(12.3456, Unit.Cm) == "12.35 cm²", "area cm");
            check(FormatArea(1234.6, Unit.Px) == "1235 px²", "area px");
            check(FormatArea(0.5, Unit.Skm) == "0.5000 km²", "area km");
            check(Angle(23.44) == "23.4°", "angle");
            check(near(Norm180(270), -90), "norm 270");
            check(near(Norm180(-180), 180), "norm -180");
            check(near(SegmentDistance(new PointF(5, 3), new PointF(0, 0), new PointF(10, 0)), 3), "segment middle");
            check(near(SegmentDistance(new PointF(-3, 4), new PointF(0, 0), new PointF(10, 0)), 5), "segment end");
            check(near(SegmentDistance(new PointF(1, 1), new PointF(2, 2), new PointF(2, 2)), Math.Sqrt(2)), "segment point");
            Console.WriteLine(fails == 0 ? "Self-test passed" : "Self-test: " + fails + " failed");
            return fails;
        }
    }

    // ── Settings: %APPDATA%\ScreenShapes\settings.ini ──
    static class Settings
    {
        static readonly Dictionary<string, string> values = new Dictionary<string, string>();

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenShapes"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) values[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
            }
            catch { }
        }

        public static string Get(string key) { string v; return values.TryGetValue(key, out v) ? v : null; }
        public static void Set(string key, string value) { if (value == null) values.Remove(key); else values[key] = value; }

        public static void RemovePrefix(string prefix)
        {
            var keys = new List<string>(values.Keys);
            foreach (var k in keys) if (k.StartsWith(prefix, StringComparison.Ordinal)) values.Remove(k);
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new List<string>();
                foreach (var kv in values) lines.Add(kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }
    }

    // ── Displays: real size (cm, in) and scale ──
    static class Screens
    {
        [DllImport("gdi32.dll")] static extern IntPtr CreateDC(string driver, string device, string output, IntPtr init);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern int GetDeviceCaps(IntPtr hdc, int index);
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt, int flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
        const int HORZSIZE = 4, HORZRES = 8;

        public static Screen At(PointF p) { return Screen.FromPoint(Point.Round(p)); }

        // Screen pixels per 96-dpi pixel (the Windows display scale)
        public static float Scale(PointF p)
        {
            try
            {
                uint x, y;
                IntPtr mon = MonitorFromPoint(Point.Round(p), 2);
                if (GetDpiForMonitor(mon, 0, out x, out y) == 0 && x > 0) return x / 96f;
            }
            catch { }
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }

        // Identifies a display (and its resolution) for the screen-size correction
        public static string Key(Screen s)
        {
            return s.DeviceName.Replace("\\", "").Replace(".", "") + "-" + s.Bounds.Width + "x" + s.Bounds.Height;
        }

        // Pixels per millimeter as the display reports it, or 0 if it doesn't (some projectors)
        public static double Reported(Screen s)
        {
            try
            {
                IntPtr dc = CreateDC("DISPLAY", s.DeviceName, null, IntPtr.Zero);
                if (dc == IntPtr.Zero) return 0;
                int mm = GetDeviceCaps(dc, HORZSIZE), px = GetDeviceCaps(dc, HORZRES);
                DeleteDC(dc);
                if (mm <= 20 || px <= 0) return 0;
                double v = (double)s.Bounds.Width / mm;
                return v > 0.5 && v < 40 ? v : 0;
            }
            catch { return 0; }
        }

        public static string FixKey(Screen s) { return "SizeFix." + Key(s); }

        public static double Fixed(Screen s)
        {
            double v;
            string raw = Settings.Get(FixKey(s));
            return raw != null && Measure.TryNum(raw, out v) && v > 0 ? v : 0;
        }

        public static double PixelsPerMM(Screen s, PointF at)
        {
            double v = Fixed(s);
            if (v > 0) return v;
            v = Reported(s);
            if (v > 0) return v;
            return 96 * Scale(at) / 25.4;
        }
    }

    enum Kind { Line, Rect, Circle, HLine, VLine }

    static class Kinds
    {
        public static string Title(Kind k)
        {
            switch (k)
            {
                case Kind.Line: return "Line";
                case Kind.Rect: return "Rect";
                case Kind.Circle: return "Circle";
                case Kind.HLine: return "H-line";
                default: return "V-line";
            }
        }

        public static string DefaultColor(Kind k)
        {
            switch (k)
            {
                case Kind.Line: return "38bdf8";
                case Kind.Rect: return "a78bfa";
                case Kind.Circle: return "34d399";
                default: return "f472b6";
            }
        }

        public static bool Fillable(Kind k) { return k == Kind.Rect || k == Kind.Circle; }

        // Saved names, the same as on the Mac
        public static string Key(Kind k)
        {
            switch (k)
            {
                case Kind.Line: return "line";
                case Kind.Rect: return "rect";
                case Kind.Circle: return "circle";
                case Kind.HLine: return "hline";
                default: return "vline";
            }
        }

        public static bool Parse(string s, out Kind k)
        {
            foreach (Kind x in new[] { Kind.Line, Kind.Rect, Kind.Circle, Kind.HLine, Kind.VLine })
                if (Key(x) == s) { k = x; return true; }
            k = Kind.Line;
            return false;
        }
    }

    // A part of a shape under the mouse: Handle >= 0 is a handle, Body moves the whole shape
    struct Part
    {
        public static readonly Part None = new Part(-2), Body = new Part(-1);
        public readonly int Code;
        public Part(int code) { Code = code; }
        public bool IsNone { get { return Code == -2; } }
        public bool IsBody { get { return Code == -1; } }
        public int Handle { get { return Code; } }
    }

    enum Dim { Length, Area, Angle }

    struct Quantity
    {
        public string Name;
        public double Value;      // screen pixels (lengths), pixels² (areas) or degrees
        public Dim Dim;
        public Quantity(string name, double value, Dim dim) { Name = name; Value = value; Dim = dim; }
    }

    // One shape in its own layered window. Points are screen pixels (y down):
    //   line: A, B = ends · rect: A, B = opposite corners · circle: A = center, B = a point on the rim
    //   hline: A = handle (A.Y is the line) · vline: A = handle (A.X is the line)
    // Fully transparent pixels let clicks through; so does the inside of a filled shape when another
    // shape's outline, handle or label is at the same point.
    class Shape : Form
    {
        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize,
            IntPtr hdcSrc, ref Point pprSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        public readonly Kind Kind;
        public readonly int Number;
        public PointF A, B;
        public Color Stroke;
        public bool Fill = true;
        public double Gap = -1;        // H/V lines: distance to the nearest line of the same kind above / to the left
        public float K = 1;            // screen pixels per 96-dpi pixel where the shape is
        public Part Grabbed = Part.None;
        Point grabMouse;
        PointF grabA, grabB, grabFixed;
        readonly PanelForm app;
        Rectangle frame;

        const float HandleR = 5, Near = 6;

        public Shape(PanelForm app, Kind kind, int number)
        {
            this.app = app;
            Kind = kind;
            Number = number;
            Stroke = Skin.Hex(Kinds.DefaultColor(kind));
            Text = Title;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000080;     // WS_EX_LAYERED | WS_EX_TOOLWINDOW
                return cp;
            }
        }

        public string Title { get { return Kinds.Title(Kind) + " " + Number; } }
        Screen Display { get { return Screens.At(A); } }
        float S(float v) { return v * K; }
        static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }
        float RadiusPx { get { return Len(B.X - A.X, B.Y - A.Y); } }
        RectangleF Box { get { return RectangleF.FromLTRB(Math.Min(A.X, B.X), Math.Min(A.Y, B.Y), Math.Max(A.X, B.X), Math.Max(A.Y, B.Y)); } }

        static PointF[] CornersOf(RectangleF r)
        {
            return new[] { new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom), new PointF(r.Left, r.Bottom) };
        }

        PointF[] Handles
        {
            get
            {
                switch (Kind)
                {
                    case Kind.Rect: return CornersOf(Box);
                    case Kind.HLine: case Kind.VLine: return new[] { A };
                    default: return new[] { A, B };
                }
            }
        }

        // The area the shape itself covers (not its label)
        RectangleF Outline
        {
            get
            {
                var d = Display.Bounds;
                switch (Kind)
                {
                    case Kind.Circle: float r = RadiusPx; return new RectangleF(A.X - r, A.Y - r, 2 * r, 2 * r);
                    case Kind.HLine: return new RectangleF(d.Left, A.Y, d.Width, 0);
                    case Kind.VLine: return new RectangleF(A.X, d.Top, 0, d.Height);
                    default: return Box;
                }
            }
        }

        // ── Measurements ──

        public List<Quantity> Quantities()
        {
            var q = new List<Quantity>();
            switch (Kind)
            {
                case Kind.Line:
                    q.Add(new Quantity("length", Len(B.X - A.X, B.Y - A.Y), Dim.Length));
                    q.Add(new Quantity("angle", Measure.Norm180(Math.Atan2(-(B.Y - A.Y), B.X - A.X) * 180 / Math.PI), Dim.Angle));
                    break;
                case Kind.Rect:
                    var r = Box;
                    q.Add(new Quantity("width", r.Width, Dim.Length));
                    q.Add(new Quantity("height", r.Height, Dim.Length));
                    q.Add(new Quantity("area", (double)r.Width * r.Height, Dim.Area));
                    break;
                case Kind.Circle:
                    double rad = RadiusPx;
                    q.Add(new Quantity("radius", rad, Dim.Length));
                    q.Add(new Quantity("area", Math.PI * rad * rad, Dim.Area));
                    break;
                case Kind.HLine:
                    q.Add(new Quantity("y", A.Y - Display.Bounds.Top, Dim.Length));
                    if (Gap >= 0) q.Add(new Quantity("gap", Gap, Dim.Length));
                    break;
                case Kind.VLine:
                    q.Add(new Quantity("x", A.X - Display.Bounds.Left, Dim.Length));
                    if (Gap >= 0) q.Add(new Quantity("gap", Gap, Dim.Length));
                    break;
            }
            return q;
        }

        // Short readings for the label and the panel
        public string[] Readouts()
        {
            double u = app.UnitPixels(A);
            Unit unit = app.CurrentUnit;
            var q = Quantities();
            Func<double, string> len = delegate(double v) { return Measure.Format(v / u, unit); };
            Func<double, string> area = delegate(double v) { return Measure.FormatArea(v / (u * u), unit); };
            switch (Kind)
            {
                case Kind.Line: return new[] { len(q[0].Value), "∠ " + Measure.Angle(q[1].Value) };
                case Kind.Rect: return new[] { Measure.Number(q[0].Value / u, unit) + " × " + len(q[1].Value), "A " + area(q[2].Value) };
                case Kind.Circle: return new[] { "r " + len(q[0].Value), "A " + area(q[1].Value) };
                default:
                    var s = new List<string> { (Kind == Kind.HLine ? "y " : "x ") + len(q[0].Value) };
                    if (q.Count > 1) s.Add("Δ " + len(q[1].Value));
                    return s.ToArray();
            }
        }

        string LabelText { get { return Title + "   " + string.Join("   ", Readouts()); } }

        Font labelFont;
        float labelFontK;
        Font LabelFont
        {
            get
            {
                if (labelFont == null || labelFontK != K)
                {
                    if (labelFont != null) labelFont.Dispose();
                    // Segoe UI Symbol has ∠, Δ and ²
                    labelFont = new Font("Segoe UI Symbol", 11 * K, FontStyle.Bold, GraphicsUnit.Pixel);
                    labelFontK = K;
                }
                return labelFont;
            }
        }

        RectangleF LabelRect
        {
            get
            {
                Size sz = TextRenderer.MeasureText(LabelText, LabelFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                float w = sz.Width + S(12), h = S(18);
                PointF c;
                switch (Kind)
                {
                    case Kind.Line: c = new PointF((A.X + B.X) / 2, Math.Min(A.Y, B.Y) - S(16)); break;
                    case Kind.Rect: c = new PointF(Box.Left + Box.Width / 2, Box.Top - S(16)); break;
                    case Kind.Circle: c = new PointF(A.X, A.Y - RadiusPx - S(16)); break;
                    case Kind.HLine: c = new PointF(A.X + S(14) + w / 2, A.Y - S(15)); break;
                    default: c = new PointF(A.X + S(12) + w / 2, A.Y - S(16)); break;
                }
                return new RectangleF(c.X - w / 2, c.Y - h / 2, w, h);
            }
        }

        // ── Window ──

        public void Relayout()
        {
            K = Screens.Scale(A);
            var o = Outline;
            o.Inflate(S(12), S(12));
            var l = LabelRect;
            l.Inflate(S(2), S(2));
            var u = RectangleF.Union(o, l);
            frame = Rectangle.FromLTRB((int)Math.Floor(u.Left), (int)Math.Floor(u.Top), (int)Math.Ceiling(u.Right), (int)Math.Ceiling(u.Bottom));
            if (!IsHandleCreated) Bounds = frame;
            Render();
        }

        public void Render()
        {
            if (!IsHandleCreated || frame.Width <= 0 || frame.Height <= 0) return;
            using (var bmp = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.TranslateTransform(-frame.X, -frame.Y);
                    DrawShape(g);
                }
                IntPtr screenDc = GetDC(IntPtr.Zero), memDc = CreateCompatibleDC(screenDc);
                IntPtr hBmp = bmp.GetHbitmap(Color.FromArgb(0)), old = SelectObject(memDc, hBmp);
                try
                {
                    var size = frame.Size;
                    var pos = frame.Location;
                    var src = Point.Empty;
                    var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                    UpdateLayeredWindow(Handle, screenDc, ref pos, ref size, memDc, ref src, 0, ref blend, 2);
                }
                finally
                {
                    SelectObject(memDc, old);
                    DeleteObject(hBmp);
                    DeleteDC(memDc);
                    ReleaseDC(IntPtr.Zero, screenDc);
                }
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Relayout();
        }

        // ── Hit testing (screen pixels) ──

        public Part Hit(PointF p) { bool weak; return HitTest(p, out weak); }

        // The part under p; weak means it's only the inside of a filled shape
        public Part HitTest(PointF p, out bool weak)
        {
            weak = false;
            var part = StrongHit(p);
            if (!part.IsNone || !Fill || !Kinds.Fillable(Kind)) return part;
            bool inside = Kind == Kind.Rect ? Box.Contains(p) : Len(p.X - A.X, p.Y - A.Y) < RadiusPx;
            weak = inside;
            return inside ? Part.Body : Part.None;
        }

        Part StrongHit(PointF p)
        {
            var hs = Handles;
            for (int i = 0; i < hs.Length; i++)
                if (Len(p.X - hs[i].X, p.Y - hs[i].Y) <= S(HandleR + 4))
                    return (Kind == Kind.Circle && i == 0) || Kind == Kind.HLine || Kind == Kind.VLine ? Part.Body : new Part(i);
            if (LabelRect.Contains(p)) return Part.Body;
            float n = S(Near);
            var d = Display.Bounds;
            switch (Kind)
            {
                case Kind.Line:
                    return Measure.SegmentDistance(p, A, B) <= n ? Part.Body : Part.None;
                case Kind.Rect:
                    var outer = Box; outer.Inflate(n, n);
                    var inner = Box; inner.Inflate(-n, -n);
                    return outer.Contains(p) && !inner.Contains(p) ? Part.Body : Part.None;
                case Kind.Circle:
                    return Math.Abs(Len(p.X - A.X, p.Y - A.Y) - RadiusPx) <= n ? Part.Body : Part.None;
                case Kind.HLine:
                    return Math.Abs(p.Y - A.Y) <= n && p.X >= d.Left && p.X < d.Right ? Part.Body : Part.None;
                default:
                    return Math.Abs(p.X - A.X) <= n && p.Y >= d.Top && p.Y < d.Bottom ? Part.Body : Part.None;
            }
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84, HTTRANSPARENT = -1;
            if (m.Msg == WM_NCHITTEST && Grabbed.IsNone)
            {
                long lp = m.LParam.ToInt64();
                var p = new PointF((short)(lp & 0xffff), (short)((lp >> 16) & 0xffff));
                bool weak;
                var part = HitTest(p, out weak);
                if (part.IsNone || (weak && app.StrongHitAt(p, this)))
                {
                    m.Result = new IntPtr(HTTRANSPARENT);
                    return;
                }
            }
            base.WndProc(ref m);
        }

        // ── Dragging ──

        static Point Mouse { get { return Control.MousePosition; } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Activate();
            if (e.Button != MouseButtons.Left) return;
            var g = Mouse;
            var part = Hit(g);
            if (part.IsNone) return;
            Grabbed = part;
            grabMouse = g;
            grabA = A;
            grabB = B;
            if (Kind == Kind.Rect && !part.IsBody) grabFixed = CornersOf(Box)[(part.Handle + 2) % 4];
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var g = Mouse;
            if (Grabbed.IsNone) { Cursor = CursorFor(Hit(g)); return; }
            Drag(g, (ModifierKeys & Keys.Shift) != 0);
            app.Changed(false);
        }

        Cursor CursorFor(Part p)
        {
            if (p.IsNone) return Cursors.Default;
            if (!p.IsBody) return Cursors.Cross;
            if (Kind == Kind.HLine) return Cursors.SizeNS;
            if (Kind == Kind.VLine) return Cursors.SizeWE;
            return Cursors.SizeAll;
        }

        void Drag(Point g, bool shift)
        {
            float dx = g.X - grabMouse.X, dy = g.Y - grabMouse.Y;
            if (Grabbed.IsBody)
            {
                A = new PointF(grabA.X + dx, grabA.Y + dy);
                B = new PointF(grabB.X + dx, grabB.Y + dy);
            }
            else
            {
                int i = Grabbed.Handle;
                switch (Kind)
                {
                    case Kind.Line:
                        var start = i == 0 ? grabA : grabB;
                        var q = new PointF(start.X + dx, start.Y + dy);
                        if (shift)      // 15° steps about the other end, keeping the length
                        {
                            var o = i == 0 ? B : A;
                            float len = Len(q.X - o.X, q.Y - o.Y);
                            double ang = Math.Round(Math.Atan2(q.Y - o.Y, q.X - o.X) * 180 / Math.PI / 15) * 15 * Math.PI / 180;
                            q = new PointF(o.X + len * (float)Math.Cos(ang), o.Y + len * (float)Math.Sin(ang));
                        }
                        if (i == 0) A = q; else B = q;
                        break;
                    case Kind.Rect:
                        var c = CornersOf(RectangleF.FromLTRB(Math.Min(grabA.X, grabB.X), Math.Min(grabA.Y, grabB.Y),
                                                              Math.Max(grabA.X, grabB.X), Math.Max(grabA.Y, grabB.Y)))[i];
                        var to = new PointF(c.X + dx, c.Y + dy);
                        if (shift)      // a square
                        {
                            float s = Math.Max(Math.Abs(to.X - grabFixed.X), Math.Abs(to.Y - grabFixed.Y));
                            to = new PointF(grabFixed.X + (to.X < grabFixed.X ? -s : s), grabFixed.Y + (to.Y < grabFixed.Y ? -s : s));
                        }
                        A = grabFixed;
                        B = to;
                        break;
                    case Kind.Circle:
                        B = new PointF(grabB.X + dx, grabB.Y + dy);
                        break;
                }
            }
            Relayout();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                if (!Hit(Mouse).IsNone) BuildMenu().Show(Mouse);
                return;
            }
            if (Grabbed.IsNone) return;
            Grabbed = Part.None;
            Capture = false;
            app.Changed(true);
        }

        protected override bool IsInputKey(Keys k)
        {
            switch (k & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(k);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            float step = e.Shift ? 10 : 1;
            switch (e.KeyCode)
            {
                case Keys.Left: Nudge(-step, 0); break;
                case Keys.Right: Nudge(step, 0); break;
                case Keys.Up: Nudge(0, -step); break;
                case Keys.Down: Nudge(0, step); break;
                case Keys.Delete: app.Remove(this); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        void Nudge(float dx, float dy)
        {
            A = new PointF(A.X + dx, A.Y + dy);
            B = new PointF(B.X + dx, B.Y + dy);
            Relayout();
            app.Changed(true);
        }

        // ── Drawing (screen pixels; Render shifts the origin) ──

        void StrokePath(Graphics g, GraphicsPath p)
        {
            // A dark edge, so any color shows on any background
            using (var dark = new Pen(Skin.A(Color.Black, 0.35), S(4)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawPath(dark, p);
            using (var pen = new Pen(Stroke, S(2)) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawPath(pen, p);
        }

        void DrawShape(Graphics g)
        {
            var d = Display.Bounds;
            using (var p = new GraphicsPath())
            {
                switch (Kind)
                {
                    case Kind.Line: p.AddLine(A, B); break;
                    case Kind.Rect: p.AddRectangle(Box); break;
                    case Kind.Circle: p.AddEllipse(Outline); break;
                    case Kind.HLine: p.AddLine(d.Left, A.Y, d.Right, A.Y); break;
                    case Kind.VLine: p.AddLine(A.X, d.Top, A.X, d.Bottom); break;
                }
                if (Fill && Kinds.Fillable(Kind))
                    using (var b = new SolidBrush(Skin.A(Stroke, 0.14))) g.FillPath(b, p);
                StrokePath(g, p);
            }
            if (Kind == Kind.Circle)
                using (var pen = new Pen(Stroke, S(1)) { DashPattern = new[] { 5f, 4f } })
                    g.DrawLine(pen, A, B);

            float r = S(HandleR);
            using (var b = new SolidBrush(Skin.A(Color.White, 0.95)))
            using (var pen = new Pen(Stroke, S(2)))
                foreach (var h in Handles)
                {
                    g.FillEllipse(b, h.X - r, h.Y - r, 2 * r, 2 * r);
                    g.DrawEllipse(pen, h.X - r, h.Y - r, 2 * r, 2 * r);
                }

            var lr = LabelRect;
            Skin.FillRound(g, lr, S(4), Skin.A(Color.White, 0.9));
            using (var path = Skin.Round(lr, S(4)))
            using (var pen = new Pen(Stroke, S(1.5f)))
                g.DrawPath(pen, path);
            using (var b = new SolidBrush(Skin.Ink))
            using (var sf = new StringFormat(StringFormat.GenericTypographic) { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(LabelText, LabelFont, b, lr, sf);
        }

        // ── Menu ──

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            var colors = new ToolStripMenuItem("Color");
            string current = Skin.HexString(Stroke);
            for (int i = 0; i < Skin.Palette.GetLength(0); i++)
            {
                string hex = Skin.Palette[i, 1];
                colors.DropDownItems.Add(new ToolStripMenuItem(Skin.Palette[i, 0], Skin.Swatch(Skin.Hex(hex)), delegate
                {
                    Stroke = Skin.Hex(hex);
                    Render();
                    app.Changed(true);
                }) { Checked = hex == current });
            }
            colors.DropDownItems.Add(new ToolStripSeparator());
            colors.DropDownItems.Add(new ToolStripMenuItem("Other color…", null, delegate { OtherColor(); }));
            m.Items.Add(colors);
            if (Kinds.Fillable(Kind))
                m.Items.Add(new ToolStripMenuItem("Fill", null, delegate { Fill = !Fill; Render(); app.Changed(true); }) { Checked = Fill });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(app.UnitsItem());
            if (Kind == Kind.Line)
            {
                m.Items.Add(new ToolStripMenuItem("Set scale from this line…", null, delegate { SetScale(); }));
                m.Items.Add(new ToolStripMenuItem("Correct screen size from this line…", null, delegate { CorrectSize(); }));
            }
            if (Screens.Fixed(Display) > 0)
                m.Items.Add(new ToolStripMenuItem("Use the display's reported size", null, delegate
                {
                    Settings.Set(Screens.FixKey(Display), null);
                    app.Changed(true);
                }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Duplicate " + Title, null, delegate { app.Duplicate(this); }) { Enabled = app.CanAdd });
            m.Items.Add(new ToolStripMenuItem("Remove " + Title, null, delegate { app.Remove(this); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Show panel", null, delegate { app.ShowPanel(); }));
            m.Items.Add(new ToolStripMenuItem("About Screen Shapes", null, delegate { app.ShowAbout(); }));
            m.Items.Add(new ToolStripMenuItem("Exit", null, delegate { app.Close(); }));
            return m;
        }

        void OtherColor()
        {
            using (var dlg = new ColorDialog { Color = Stroke, FullOpen = true })
                if (dlg.ShowDialog(app) == DialogResult.OK)
                {
                    Stroke = Color.FromArgb(dlg.Color.R, dlg.Color.G, dlg.Color.B);
                    Render();
                    app.Changed(true);
                }
        }

        void SetScale()
        {
            double len = Len(B.X - A.X, B.Y - A.Y);
            if (len < S(10)) return;
            var units = new[] { Unit.Sm, Unit.Skm, Unit.Scm };
            double v;
            int i;
            if (!app.AskLength("Set the scale",
                    "Lay " + Title + " along a length you know (a map's scale bar, or an object of known size in a photo). " +
                    "It is " + Measure.Format(len, Unit.Px) + " long on the screen. How long is it in reality?",
                    new[] { "m", "km", "cm" }, Math.Max(0, Array.IndexOf(units, app.CurrentUnit)), out v, out i)) return;
            app.MetersPerPixel = v * Units.Meters(units[i]) / len;
            app.CurrentUnit = units[i];
            app.Changed(true);
        }

        void CorrectSize()
        {
            double len = Len(B.X - A.X, B.Y - A.Y);
            if (len < S(10)) return;
            double v;
            int i;
            if (!app.AskLength("Correct the screen size",
                    "Hold a real ruler (or a bank card, 8.56 cm wide) against the screen along " + Title + ". It is " +
                    Measure.Format(len, Unit.Px) + " long on the screen. How long is it on your real ruler?",
                    new[] { "cm", "in" }, app.CurrentUnit == Unit.In ? 1 : 0, out v, out i)) return;
            Settings.Set(Screens.FixKey(Display), Measure.Num(len / (v * (i == 0 ? 10 : 25.4))));
            app.Changed(true);
        }

        // ── Saving ──

        public string Serialize()
        {
            return string.Join(",", new[] { Kinds.Key(Kind), Number.ToString(CultureInfo.InvariantCulture),
                Measure.Num(A.X), Measure.Num(A.Y), Measure.Num(B.X), Measure.Num(B.Y), Skin.HexString(Stroke), Fill ? "1" : "0" });
        }

        public static Shape Parse(PanelForm app, string s)
        {
            if (s == null) return null;
            var f = s.Split(',');
            Kind k;
            int n;
            double ax, ay, bx, by;
            Color c;
            if (f.Length != 8 || !Kinds.Parse(f[0], out k) || !int.TryParse(f[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ||
                !Measure.TryNum(f[2], out ax) || !Measure.TryNum(f[3], out ay) || !Measure.TryNum(f[4], out bx) ||
                !Measure.TryNum(f[5], out by) || !Skin.TryHex(f[6], out c)) return null;
            var sh = new Shape(app, k, n);
            sh.A = new PointF((float)ax, (float)ay);
            sh.B = new PointF((float)bx, (float)by);
            sh.Stroke = c;
            sh.Fill = f[7] == "1";
            return sh;
        }
    }

    // ── The panel: add shapes, choose units and read every measurement. Closing it quits. ──
    class PanelForm : Form
    {
        enum HitKind { None, Add, About, Units, Copy, Clear, Row, Close }

        public const int MaxShapes = 20;
        public readonly List<Shape> Shapes = new List<Shape>();
        readonly float k;
        HitKind pressed = HitKind.None;
        int pressedIndex = -1;
        readonly Font fButton, fName, fValue, fClose;

        const float Width_ = 460, RowH = 22, TopPad = 78;
        static readonly Kind[] AddKinds = { Kind.Line, Kind.Rect, Kind.Circle, Kind.HLine, Kind.VLine };
        static readonly string[] AddLabels = { "+ Line", "+ Rect", "+ Circle", "+ H-line", "+ V-line" };
        static readonly float[] AddWidths = { 70, 70, 80, 80, 80 };

        public PanelForm()
        {
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Text = "Screen Shapes";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Skin.Face;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            fButton = Px("Tahoma", 12, true);
            fName = Px("Tahoma", 12, true);
            fValue = Px("Consolas", 13, true);
            fClose = Px("Tahoma", 15, true);

            Settings.Load();
            ClientSize = new Size(S(Width_), S(TopPad + RowH + 16));
            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - S(40), wa.Top + S(40));
            double px, py;
            if (Measure.TryNum(Settings.Get("PanelX"), out px) && Measure.TryNum(Settings.Get("PanelY"), out py))
            {
                var at = new Point((int)px, (int)py);
                foreach (var scr in Screen.AllScreens)
                    if (scr.WorkingArea.Contains(new Point(at.X + S(40), at.Y + S(10)))) { Location = at; break; }
            }
        }

        int S(float v) { return (int)Math.Round(v * k); }
        Font Px(string family, float px, bool bold)
        {
            return new Font(family, px * k, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            for (int i = 0; i < MaxShapes; i++)
            {
                var sh = Shape.Parse(this, Settings.Get("Shape." + i));
                if (sh == null) continue;
                bool seen = false;
                foreach (var scr in Screen.AllScreens) if (scr.Bounds.Contains(Point.Round(sh.A))) seen = true;
                if (!seen) Place(sh);
                Attach(sh);
            }
            Changed(false);
        }

        // ── One unit and one scale for every shape ──

        public Unit CurrentUnit
        {
            get
            {
                Unit u;
                if (!Units.Parse(Settings.Get("Unit"), out u)) u = Unit.Cm;
                return Units.Scaled(u) && MetersPerPixel <= 0 ? Unit.Cm : u;
            }
            set { Settings.Set("Unit", Units.Key(value)); }
        }

        public double MetersPerPixel
        {
            get { double v; return Measure.TryNum(Settings.Get("MetersPerPixel"), out v) && v > 0 ? v : 0; }
            set { Settings.Set("MetersPerPixel", Measure.Num(value)); }
        }

        // Screen pixels for one unit near p
        public double UnitPixels(PointF p)
        {
            var s = Screens.At(p);
            switch (CurrentUnit)
            {
                case Unit.Cm: return Screens.PixelsPerMM(s, p) * 10;
                case Unit.In: return Screens.PixelsPerMM(s, p) * 25.4;
                case Unit.Px: return 1;
                default: return Units.Meters(CurrentUnit) / MetersPerPixel;
            }
        }

        public ToolStripMenuItem UnitsItem()
        {
            var it = new ToolStripMenuItem("Units");
            foreach (var x in UnitItems()) it.DropDownItems.Add(x);
            return it;
        }

        List<ToolStripItem> UnitItems()
        {
            var list = new List<ToolStripItem>();
            foreach (Unit u in Units.All)
            {
                if (u == Unit.Sm) list.Add(new ToolStripSeparator());
                Unit pick = u;
                list.Add(new ToolStripMenuItem(Units.MenuTitle(u), null, delegate { PickUnit(pick); }) { Checked = u == CurrentUnit });
            }
            return list;
        }

        void PickUnit(Unit u)
        {
            if (Units.Scaled(u) && MetersPerPixel <= 0)
            {
                Message("Set a scale first",
                    "Add a line (+ Line) and lay it along a length you know, such as a map's scale bar or an object of known " +
                    "size in a photo. Then right-click the line and choose Set scale from this line….");
                return;
            }
            CurrentUnit = u;
            Changed(true);
        }

        // ── Shapes ──

        public bool CanAdd { get { return Shapes.Count < MaxShapes; } }

        // True if some other shape's outline, handle or label is at p (so a fill under it gives way)
        public bool StrongHitAt(PointF p, Shape except)
        {
            foreach (var s in Shapes)
            {
                if (s == except) continue;
                bool weak;
                var part = s.HitTest(p, out weak);
                if (!part.IsNone && !weak) return true;
            }
            return false;
        }

        void Attach(Shape s)
        {
            Shapes.Add(s);
            s.Show();
        }

        int NextNumber(Kind kind)
        {
            int n = 0;
            foreach (var s in Shapes) if (s.Kind == kind) n = Math.Max(n, s.Number);
            return n + 1;
        }

        // A new shape on the screen with the mouse: each kind has its own spot, and another
        // of the same kind goes a little further along, so new shapes don't pile up
        void Place(Shape s)
        {
            var wa = Screen.FromPoint(Control.MousePosition).WorkingArea;
            float sk = Screens.Scale(new PointF(wa.X + wa.Width / 2, wa.Y + wa.Height / 2));
            int same = 0;
            foreach (var o in Shapes) if (o.Kind == s.Kind && o != s) same++;
            float n = same % 5;
            float cx = wa.X + wa.Width / 2f, cy = wa.Y + wa.Height / 2f;
            switch (s.Kind)
            {
                case Kind.Line:
                    s.A = new PointF(cx - 150 * sk, cy - (170 - 40 * n) * sk); s.B = new PointF(cx + 150 * sk, s.A.Y); break;
                case Kind.Rect:
                    s.A = new PointF(cx - (330 - 30 * n) * sk, cy - (60 - 30 * n) * sk); s.B = new PointF(s.A.X + 220 * sk, s.A.Y + 150 * sk); break;
                case Kind.Circle:
                    s.A = new PointF(cx + (220 + 30 * n) * sk, cy + (15 + 30 * n) * sk); s.B = new PointF(s.A.X + 90 * sk, s.A.Y); break;
                case Kind.HLine:
                    s.A = new PointF(cx - 40 * sk, cy + (170 + 40 * n) * sk); s.B = s.A; break;
                case Kind.VLine:
                    s.A = new PointF(cx - (420 + 40 * n) * sk, cy + (230 - 26 * n) * sk); s.B = s.A; break;
            }
            s.Relayout();
        }

        public void Add(Kind kind)
        {
            if (!CanAdd) return;
            var s = new Shape(this, kind, NextNumber(kind));
            Place(s);
            Attach(s);
            Changed(true);
        }

        public void Duplicate(Shape o)
        {
            if (!CanAdd) return;
            var s = new Shape(this, o.Kind, NextNumber(o.Kind));
            s.Stroke = o.Stroke;
            s.Fill = o.Fill;
            float d = 24 * o.K;
            s.A = new PointF(o.A.X + d, o.A.Y + d);
            s.B = new PointF(o.B.X + d, o.B.Y + d);
            s.Relayout();
            Attach(s);
            Changed(true);
        }

        public void Remove(Shape s)
        {
            Shapes.Remove(s);
            s.Close();
            Changed(true);
        }

        void ClearAll()
        {
            if (Shapes.Count == 0) return;
            if (MessageBox.Show(this, "Remove all " + Shapes.Count + " shapes?", "Screen Shapes",
                                MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            foreach (var s in Shapes.ToArray()) s.Close();
            Shapes.Clear();
            Changed(true);
        }

        public void ShowPanel()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        // Something moved or changed: update the gaps and the panel, and remember it
        public void Changed(bool save)
        {
            UpdateGaps();
            foreach (var s in Shapes) s.Relayout();
            var want = new Size(S(Width_), S(TopPad + Math.Max(1, Shapes.Count) * RowH + 16));
            if (ClientSize != want) ClientSize = want;
            Invalidate();
            if (!save) return;
            Settings.RemovePrefix("Shape.");
            for (int i = 0; i < Shapes.Count; i++) Settings.Set("Shape." + i, Shapes[i].Serialize());
            Settings.Save();
        }

        // Each H-line's gap to the nearest one above it, each V-line's to the nearest one on its left
        void UpdateGaps()
        {
            foreach (var s in Shapes)
            {
                if (s.Kind != Kind.HLine && s.Kind != Kind.VLine) continue;
                double best = -1;
                foreach (var o in Shapes)
                {
                    if (o == s || o.Kind != s.Kind) continue;
                    double d = s.Kind == Kind.HLine ? s.A.Y - o.A.Y : s.A.X - o.A.X;
                    if (d > 0 && (best < 0 || d < best)) best = d;
                }
                s.Gap = best;
            }
        }

        // The table, tab-separated, ready to paste into a spreadsheet
        void CopyTable()
        {
            var sb = new StringBuilder("Shape\tQuantity\tValue\tUnit\r\n");
            Unit unit = CurrentUnit;
            foreach (var s in Shapes)
            {
                double u = UnitPixels(s.A);
                foreach (var q in s.Quantities())
                {
                    switch (q.Dim)
                    {
                        case Dim.Length: sb.Append(s.Title + "\t" + q.Name + "\t" + Measure.Number(q.Value / u, unit) + "\t" + Units.Symbol(unit) + "\r\n"); break;
                        case Dim.Area: sb.Append(s.Title + "\t" + q.Name + "\t" + Measure.Number(q.Value / (u * u), unit) + "\t" + Units.Symbol(unit) + "²\r\n"); break;
                        case Dim.Angle: sb.Append(s.Title + "\t" + q.Name + "\t" + Measure.Fixed(q.Value, 1) + "\t°\r\n"); break;
                    }
                }
            }
            try { Clipboard.SetText(sb.ToString()); } catch { }
        }

        // ── Layout and painting ──

        Rectangle RcAdd(int i)
        {
            float x = 8;
            for (int j = 0; j < i; j++) x += AddWidths[j] + 6;
            return new Rectangle(S(x), S(8), S(AddWidths[i]), S(26));
        }
        Rectangle RcAbout { get { return new Rectangle(ClientSize.Width - S(34), S(8), S(26), S(26)); } }
        Rectangle RcUnits { get { return new Rectangle(S(8), S(40), S(190), S(26)); } }
        Rectangle RcCopy { get { return new Rectangle(ClientSize.Width - S(8 + 84 + 6 + 70), S(40), S(70), S(26)); } }
        Rectangle RcClear { get { return new Rectangle(ClientSize.Width - S(8 + 84), S(40), S(84), S(26)); } }
        Rectangle RcLcd { get { return new Rectangle(S(8), S(TopPad), ClientSize.Width - S(16), S(Math.Max(1, Shapes.Count) * RowH + 8)); } }
        Rectangle RcRow(int i) { var l = RcLcd; return new Rectangle(l.X + S(4), l.Y + S(4 + i * RowH), l.Width - S(8), S(RowH)); }
        Rectangle RcClose(int i) { var r = RcRow(i); return new Rectangle(r.Right - S(20), r.Y + S(2), S(18), S(18)); }

        HitKind HitTest(Point p, out int index)
        {
            index = -1;
            for (int i = 0; i < AddKinds.Length; i++)
                if (RcAdd(i).Contains(p)) { index = i; return CanAdd ? HitKind.Add : HitKind.None; }
            if (RcAbout.Contains(p)) return HitKind.About;
            if (RcUnits.Contains(p)) return HitKind.Units;
            if (RcCopy.Contains(p)) return Shapes.Count > 0 ? HitKind.Copy : HitKind.None;
            if (RcClear.Contains(p)) return Shapes.Count > 0 ? HitKind.Clear : HitKind.None;
            for (int i = 0; i < Shapes.Count; i++)
                if (RcRow(i).Contains(p)) { index = i; return RcClose(i).Contains(p) ? HitKind.Close : HitKind.Row; }
            return HitKind.None;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = HitTest(e.Location, out pressedIndex);
            Invalidate();
            if (pressed == HitKind.Units)       // a pop-up menu under the button
            {
                var m = new ContextMenuStrip();
                foreach (var x in UnitItems()) m.Items.Add(x);
                var r = RcUnits;
                m.Show(this, new Point(r.Left, r.Bottom + S(2)));
                pressed = HitKind.None;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int index;
            var h = HitTest(e.Location, out index);
            var was = pressed;
            int wasIndex = pressedIndex;
            pressed = HitKind.None;
            Invalidate();
            if (h != was || index != wasIndex) return;
            switch (h)
            {
                case HitKind.Add: Add(AddKinds[index]); break;
                case HitKind.About: ShowAbout(); break;
                case HitKind.Copy: CopyTable(); break;
                case HitKind.Clear: ClearAll(); break;
                case HitKind.Row: Shapes[index].Activate(); break;
                case HitKind.Close: Remove(Shapes[index]); break;
            }
        }

        void Button(Graphics g, Rectangle r, bool down, string label, bool enabled, bool left)
        {
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, r);
            int t = Math.Max(1, (int)Math.Round(k));
            if (down) Skin.Sunken(g, r, t); else Skin.Raised(g, r, t);
            if (down) r.Offset(t, t);
            if (left) { r.X += S(8); r.Width -= S(8); }
            TextRenderer.DrawText(g, label, fButton, r, enabled ? Skin.Text : Skin.Muted,
                (left ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter) | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Skin.Face);
            for (int i = 0; i < AddKinds.Length; i++)
                Button(g, RcAdd(i), pressed == HitKind.Add && pressedIndex == i, AddLabels[i], CanAdd, false);
            Button(g, RcAbout, pressed == HitKind.About, "?", true, false);
            Unit unit = CurrentUnit;
            Button(g, RcUnits, false, "Units: " + Units.Symbol(unit) + (Units.Scaled(unit) ? " (scaled)" : "") + "  ▾", true, true);
            Button(g, RcCopy, pressed == HitKind.Copy, "Copy", Shapes.Count > 0, false);
            Button(g, RcClear, pressed == HitKind.Clear, "Clear all", Shapes.Count > 0, false);

            var lcd = RcLcd;
            using (var b = new SolidBrush(Skin.Lcd)) g.FillRectangle(b, lcd);
            Skin.Sunken(g, lcd, Math.Max(1, (int)Math.Round(k)));
            const TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            const TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            if (Shapes.Count == 0)
            {
                TextRenderer.DrawText(g, "Add a line or a shape", fName, RcRow(0), Skin.LcdDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            for (int i = 0; i < Shapes.Count; i++)
            {
                var s = Shapes[i];
                var r = RcRow(i);
                var sw = new Rectangle(r.X + S(4), r.Y + r.Height / 2 - S(5), S(10), S(10));
                using (var b = new SolidBrush(s.Stroke)) g.FillRectangle(b, sw);
                using (var p = new Pen(Skin.LcdDigit)) g.DrawRectangle(p, sw);
                TextRenderer.DrawText(g, s.Title, fName, new Rectangle(r.X + S(20), r.Y, S(90), r.Height), Skin.LcdDigit, left);
                TextRenderer.DrawText(g, string.Join("   ", s.Readouts()), fValue,
                    new Rectangle(r.X + S(100), r.Y, r.Width - S(100 + 26), r.Height), Skin.LcdDigit, right);
                TextRenderer.DrawText(g, "×", fClose, RcClose(i), pressed == HitKind.Close && pressedIndex == i ? Skin.Marker : Skin.LcdDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.D1: Add(Kind.Line); return true;
                case Keys.Control | Keys.D2: Add(Kind.Rect); return true;
                case Keys.Control | Keys.D3: Add(Kind.Circle); return true;
                case Keys.Control | Keys.D4: Add(Kind.HLine); return true;
                case Keys.Control | Keys.D5: Add(Kind.VLine); return true;
                case Keys.Control | Keys.Shift | Keys.C: CopyTable(); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        public void Message(string title, string text)
        {
            MessageBox.Show(this, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        // Asks for a length and its unit; false if canceled
        public bool AskLength(string title, string text, string[] units, int selected, out double value, out int unit)
        {
            using (var f = new LengthForm(title, text, units, selected))
            {
                bool ok = f.ShowDialog(this) == DialogResult.OK;
                value = f.Value;
                unit = f.UnitIndex;
                return ok;
            }
        }

        public void ShowAbout()
        {
            using (var a = new AboutForm()) a.ShowDialog(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            Changed(true);
            if (WindowState == FormWindowState.Normal)
            {
                Settings.Set("PanelX", Location.X.ToString(CultureInfo.InvariantCulture));
                Settings.Set("PanelY", Location.Y.ToString(CultureInfo.InvariantCulture));
                Settings.Save();
            }
            base.OnFormClosing(e);
        }
    }

    class LengthForm : Form
    {
        readonly TextBox field;
        readonly ComboBox units;
        public double Value;
        public int UnitIndex;

        public LengthForm(string title, string text, string[] unitNames, int selected)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(360, 150);

            Controls.Add(new Label { Text = text, Location = new Point(16, 14), Size = new Size(328, 52) });
            field = new TextBox { Location = new Point(16, 72), Size = new Size(140, 23) };
            units = new ComboBox { Location = new Point(164, 72), Size = new Size(80, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            units.Items.AddRange(unitNames);
            units.SelectedIndex = selected;
            Controls.Add(field);
            Controls.Add(units);

            var ok = new Button { Text = "OK", Location = new Point(184, 110), Size = new Size(80, 28) };
            var cancel = new Button { Text = "Cancel", Location = new Point(270, 110), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };
            ok.Click += delegate
            {
                double v;
                string s = field.Text.Trim().Replace(',', '.');
                if (Measure.TryNum(s, out v) && v > 0 && !double.IsInfinity(v))
                {
                    Value = v;
                    UnitIndex = units.SelectedIndex;
                    DialogResult = DialogResult.OK;
                }
                else
                {
                    MessageBox.Show(this, "Type a number greater than 0.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    field.Focus();
                }
            };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ResumeLayout(false);
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
            Text = "About Screen Shapes";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 410);

            var icon = new PictureBox { Location = new Point(16, 16), Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom };
            var bmp = new Bitmap(96, 96, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) IconArt.Draw(g, 96);
            icon.Image = bmp;
            Controls.Add(icon);

            Controls.Add(new Label { Text = "Screen Shapes", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
            Controls.Add(new Label { Text = "Version 1.0  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Free software, provided \"as is\", without warranty of any kind. Don't rely on its " +
                       "measurements where an error could cause harm or loss.\r\n" +
                       "This app collects no personal data and never connects to the internet.",
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

    // App icon: a violet rectangle, a green circle and a sky-blue line
    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.03f;
            Skin.FillRound(g, new RectangleF(m, m, s - 2 * m, s - 2 * m), s * 0.2f, Skin.Face);
            float w = Math.Max(1.5f, s * 0.035f);
            // Same layout as the Mac icon, with y measured down
            var rect = new RectangleF(s * 0.22f, s * 0.26f, s * 0.38f, s * 0.32f);
            var circle = new RectangleF(s * 0.44f, s * 0.42f, s * 0.34f, s * 0.34f);
            using (var b = new SolidBrush(Skin.A(Skin.Hex("a78bfa"), 0.3))) g.FillRectangle(b, rect);
            using (var p = new Pen(Skin.Hex("a78bfa"), w)) g.DrawRectangle(p, rect.X, rect.Y, rect.Width, rect.Height);
            using (var b = new SolidBrush(Skin.A(Skin.Hex("34d399"), 0.3))) g.FillEllipse(b, circle);
            using (var p = new Pen(Skin.Hex("34d399"), w)) g.DrawEllipse(p, circle);
            using (var p = new Pen(Skin.Hex("38bdf8"), w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(p, s * 0.2f, s * 0.76f, s * 0.8f, s * 0.22f);
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
