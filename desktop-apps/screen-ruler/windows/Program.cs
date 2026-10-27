// Screen Ruler for Windows: up to three rulers and a protractor that float over everything on screen
// (twin of ../mac/main.swift). Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
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
using System.Windows.Forms;

[assembly: AssemblyTitle("Screen Ruler")]
[assembly: AssemblyProduct("Screen Ruler")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ScreenRuler
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

        // The tools: a pale tint for each ruler, dark ticks, a red marker
        public static readonly Color[] Tints = {
            Color.FromArgb(0xf6, 0xe7, 0xa1), Color.FromArgb(0xbf, 0xe6, 0xf0), Color.FromArgb(0xf3, 0xc9, 0xd9) };
        public static readonly Color ProtractorTint = Color.FromArgb(0xcd, 0xec, 0xcc);
        public static readonly Color Ink = Color.FromArgb(0x1e, 0x1e, 0x22);
        public static readonly Color Marker = Color.FromArgb(0xd0, 0x1c, 0x1c);

        public static Color A(Color c, double alpha) { return Color.FromArgb((int)Math.Round(alpha * 255), c); }

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
    }

    // ── Units and tick marks (same rules as the Mac app; checked by --selftest) ──

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

    // Ticks every Step units; every Major-th is long and labeled, every Mid-th medium,
    // and for inches every Quarter-th and Eighth-th a little longer (0 = no such level).
    struct TickPlan
    {
        public double Step;
        public int Major, Mid, Quarter, Eighth, Decimals;

        public TickPlan(double step, int major, int mid, int quarter, int eighth, int decimals)
        {
            Step = step; Major = major; Mid = mid; Quarter = quarter; Eighth = eighth; Decimals = decimals;
        }

        public bool Same(TickPlan o)
        {
            return Math.Abs(Step - o.Step) < 1e-12 && Major == o.Major && Mid == o.Mid &&
                   Quarter == o.Quarter && Eighth == o.Eighth && Decimals == o.Decimals;
        }
    }

    static class Measure
    {
        public const double MinMajor = 70;     // least spacing between labels, in 96-dpi pixels
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // The smallest 1, 2 or 5 × 10^n that is at least v
        public static double NiceStep(double v)
        {
            double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
            foreach (double m in new[] { 1.0, 2, 5, 10 })
                if (m * p >= v * (1 - 1e-9)) return m * p;
            return 10 * p;
        }

        // u = screen pixels for one unit; k = screen pixels per 96-dpi pixel
        public static TickPlan Plan(Unit unit, double u, double k)
        {
            switch (unit)
            {
                case Unit.Cm:
                    if (0.1 * u >= 3 * k) return new TickPlan(0.1, 10, 5, 0, 0, 0);
                    return new TickPlan(0.5, 2, 0, 0, 0, 0);
                case Unit.In:
                    if (u / 16 >= 3 * k) return new TickPlan(1.0 / 16, 16, 8, 4, 2, 0);
                    return new TickPlan(1.0 / 8, 8, 4, 2, 0, 0);
                default:
                    // Labels at least 70 px apart, so tenths are always at least 7 px apart
                    double big = NiceStep(MinMajor * k / u);
                    int decimals = Math.Max(0, (int)(-Math.Floor(Math.Log10(big) + 1e-9)));
                    return new TickPlan(big / 10, 10, 5, 0, 0, decimals);
            }
        }

        public static string Fixed(double v, int decimals) { return v.ToString("F" + decimals, Inv); }

        // A reading: px whole, cm 2 decimals, in 3 decimals, scaled units 4 significant figures
        public static string Format(double v, Unit unit)
        {
            int d;
            switch (unit)
            {
                case Unit.Px: d = 0; break;
                case Unit.Cm: d = 2; break;
                case Unit.In: d = 3; break;
                default: d = v == 0 ? 2 : Math.Min(6, Math.Max(0, 3 - (int)Math.Floor(Math.Log10(Math.Abs(v))))); break;
            }
            return Fixed(v, d) + " " + Units.Symbol(unit);
        }

        public static string Angle(double a) { return Fixed(a, 1) + "°"; }

        public static double Norm180(double a)
        {
            double r = a % 360;
            if (r <= -180) r += 360;
            if (r > 180) r -= 360;
            return r;
        }

        // Angle between two directions, 0…180°
        public static double Between(double a, double b) { return Math.Abs(Norm180(a - b)); }

        // Where line p + t·d meets line q + s·e; false if they're parallel
        public static bool Cross(PointF p, PointF d, PointF q, PointF e, out double t, out double s)
        {
            double den = (double)d.X * e.Y - (double)d.Y * e.X;
            t = s = 0;
            if (Math.Abs(den) < 1e-6) return false;
            double wx = (double)q.X - p.X, wy = (double)q.Y - p.Y;
            t = (wx * e.Y - wy * e.X) / den;
            s = (wx * d.Y - wy * d.X) / den;
            return true;
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
            Func<double, double, bool> near = delegate(double a, double b) { return Math.Abs(a - b) < 1e-9; };
            check(near(NiceStep(70), 100), "niceStep 70");
            check(near(NiceStep(0.7), 1), "niceStep 0.7");
            check(near(NiceStep(1.3), 2), "niceStep 1.3");
            check(near(NiceStep(3), 5), "niceStep 3");
            check(near(NiceStep(0.012), 0.02), "niceStep 0.012");
            check(near(NiceStep(500), 500), "niceStep 500");
            check(Plan(Unit.Cm, 37.8, 1).Same(new TickPlan(0.1, 10, 5, 0, 0, 0)), "plan cm");
            check(Plan(Unit.Cm, 20, 1).Same(new TickPlan(0.5, 2, 0, 0, 0, 0)), "plan cm dense");
            check(Plan(Unit.In, 96, 1).Same(new TickPlan(1.0 / 16, 16, 8, 4, 2, 0)), "plan in");
            check(Plan(Unit.Px, 1, 1).Same(new TickPlan(10, 10, 5, 0, 0, 0)), "plan px");
            check(Plan(Unit.Px, 0.5, 1).Same(new TickPlan(20, 10, 5, 0, 0, 0)), "plan px retina");
            check(Plan(Unit.Skm, 30, 1).Same(new TickPlan(0.5, 10, 5, 0, 0, 0)), "plan km");
            check(Plan(Unit.Sm, 1000, 1).Same(new TickPlan(0.01, 10, 5, 0, 0, 1)), "plan m");
            check(Plan(Unit.Sm, 40, 1).Same(new TickPlan(0.2, 10, 5, 0, 0, 0)), "plan m 2");
            check(Format(12.3456, Unit.Cm) == "12.35 cm", "format cm");
            check(Format(3.14159, Unit.In) == "3.142 in", "format in");
            check(Format(399.6, Unit.Px) == "400 px", "format px");
            check(Format(1234.56, Unit.Skm) == "1235 km", "format km");
            check(Format(0.012346, Unit.Sm) == "0.01235 m", "format m");
            check(Format(5.4321, Unit.Scm) == "5.432 cm", "format scaled cm");
            check(Format(0, Unit.Sm) == "0.00 m", "format zero");
            check(Angle(23.44) == "23.4°", "angle");
            check(near(Norm180(270), -90), "norm 270");
            check(near(Norm180(-180), 180), "norm -180");
            check(near(Norm180(-450), -90), "norm -450");
            check(near(Between(10, 200), 170), "between 10 200");
            check(near(Between(350, 20), 30), "between 350 20");
            double t, s;
            check(Cross(new PointF(0, 0), new PointF(1, 0), new PointF(5, -3), new PointF(0, 1), out t, out s) &&
                  near(t, 5) && near(s, 3), "cross");
            check(!Cross(new PointF(0, 0), new PointF(1, 1), new PointF(0, 1), new PointF(2, 2), out t, out s), "parallel");
            Console.WriteLine(fails == 0 ? "Self-test passed" : "Self-test: " + fails + " failed");
            return fails;
        }
    }

    // ── Settings: %APPDATA%\ScreenRuler\settings.ini ──
    static class Settings
    {
        static readonly Dictionary<string, string> values = new Dictionary<string, string>();

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenRuler"); }
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

    enum Part { None, Body, Marker, Knob, Grip, Arm, Rim }

    // A ruler or the protractor in its own layered window: drawn in its own coordinates (x along the
    // baseline, y down), rotated by Angle degrees counterclockwise about Origin (screen pixels).
    // Fully transparent pixels let clicks through to whatever is underneath.
    abstract class Tool : Form
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

        public PointF Origin;
        public double Angle;
        public float K = 1;              // screen pixels per 96-dpi pixel where the tool is
        public Part Grabbed = Part.None;
        protected PointF grabLocal;
        protected float grabValue;
        protected PanelForm app;
        Rectangle frame;

        protected Tool(PanelForm app)
        {
            this.app = app;
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

        public abstract RectangleF LocalBounds { get; }
        public abstract Part Hit(PointF p);
        protected abstract void DrawLocal(Graphics g);
        public abstract string Title { get; }
        public abstract Color Tint { get; }
        public abstract string[] Readouts();
        public abstract string Serialize();

        public bool Flipped { get { return Math.Abs(Measure.Norm180(Angle)) > 90; } }
        public float S(float v) { return v * K; }

        public PointF ToLocal(PointF g)
        {
            double r = Angle * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            double dx = g.X - Origin.X, dy = g.Y - Origin.Y;
            return new PointF((float)(dx * c - dy * s), (float)(dx * s + dy * c));
        }

        public PointF ToScreen(PointF l)
        {
            double r = Angle * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            return new PointF((float)(Origin.X + l.X * c + l.Y * s), (float)(Origin.Y - l.X * s + l.Y * c));
        }

        // Recomputes the window around the turned tool and redraws it
        public void Relayout()
        {
            K = Screens.Scale(Origin);
            var b = LocalBounds;
            var pts = new[] { ToScreen(new PointF(b.Left, b.Top)), ToScreen(new PointF(b.Right, b.Top)),
                              ToScreen(new PointF(b.Left, b.Bottom)), ToScreen(new PointF(b.Right, b.Bottom)) };
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var p in pts)
            {
                x0 = Math.Min(x0, p.X); y0 = Math.Min(y0, p.Y);
                x1 = Math.Max(x1, p.X); y1 = Math.Max(y1, p.Y);
            }
            frame = new Rectangle((int)Math.Floor(x0) - 2, (int)Math.Floor(y0) - 2,
                                  (int)Math.Ceiling(x1 - x0) + 5, (int)Math.Ceiling(y1 - y0) + 5);
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
                    g.TranslateTransform(Origin.X - frame.X, Origin.Y - frame.Y);
                    g.RotateTransform((float)-Angle);
                    DrawLocal(g);
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

        // Text that stays upright when the tool is turned past 90°
        protected void Label(Graphics g, string s, Font f, Color c, PointF at, bool left)
        {
            var sz = g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic);
            var state = g.Save();
            g.TranslateTransform(at.X, at.Y);
            if (Flipped) g.RotateTransform(180);
            float x = left ? (Flipped ? -sz.Width : 0) : -sz.Width / 2;
            using (var b = new SolidBrush(c))
                g.DrawString(s, f, b, x, -sz.Height / 2, StringFormat.GenericTypographic);
            g.Restore(state);
        }

        protected Font Px(string family, float px, bool bold)
        {
            return new Font(family, px * K, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        // A round knob with a circular arrow: drag it to turn the tool
        protected void DrawKnob(Graphics g, PointF c, float r)
        {
            using (var b = new SolidBrush(Skin.A(Color.White, 0.85)))
                g.FillEllipse(b, c.X - r, c.Y - r, 2 * r, 2 * r);
            using (var p = new Pen(Skin.Ink, S(1)))
                g.DrawEllipse(p, c.X - r, c.Y - r, 2 * r, 2 * r);
            float a = r * 0.55f;
            using (var p = new Pen(Skin.Ink, S(1.6f)))
            {
                // Same arrow as the Mac knob, mirrored for y down: from 120° round to 40° (counterclockwise)
                g.DrawArc(p, c.X - a, c.Y - a, 2 * a, 2 * a, -120, -280);
                double e = 40 * Math.PI / 180;
                var tip = new PointF(c.X + a * (float)Math.Cos(e), c.Y - a * (float)Math.Sin(e));
                g.DrawLines(p, new[] {
                    new PointF(tip.X - r * 0.38f, tip.Y - r * 0.05f), tip, new PointF(tip.X - r * 0.05f, tip.Y - r * 0.4f) });
            }
        }

        // ── Dragging (screen pixels, so the window can move under the mouse) ──

        public virtual void Begin(Part region, PointF g)
        {
            Grabbed = region;
            grabLocal = ToLocal(g);
        }

        public virtual void Drag(PointF g, bool shift)
        {
            switch (Grabbed)
            {
                case Part.Body:
                    // Keep the grabbed point under the mouse
                    var now = ToScreen(grabLocal);
                    Origin = new PointF(Origin.X + g.X - now.X, Origin.Y + g.Y - now.Y);
                    break;
                case Part.Knob:
                    double then = Math.Atan2(-grabLocal.Y, grabLocal.X) * 180 / Math.PI;
                    double to = Math.Atan2(-(g.Y - Origin.Y), g.X - Origin.X) * 180 / Math.PI;
                    double a = Measure.Norm180(to - then);
                    if (shift) a = Math.Round(a / 15) * 15;
                    Angle = a;
                    break;
            }
            Relayout();
        }

        public void Nudge(float dx, float dy, double turn)
        {
            Origin = new PointF(Origin.X + dx, Origin.Y + dy);
            Angle = Measure.Norm180(Math.Round((Angle + turn) * 10) / 10);
            Relayout();
            app.Changed(true);
        }

        protected virtual Cursor CursorFor(Part r)
        {
            switch (r)
            {
                case Part.Body: return Cursors.SizeAll;
                case Part.None: return Cursors.Default;
                default: return Cursors.Hand;
            }
        }

        static PointF Mouse { get { var p = Control.MousePosition; return new PointF(p.X, p.Y); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Activate();
            if (e.Button != MouseButtons.Left) return;
            var g = Mouse;
            var r = Hit(ToLocal(g));
            if (r == Part.None) return;
            Begin(r, g);
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var g = Mouse;
            if (Grabbed != Part.None)
            {
                Drag(g, (ModifierKeys & Keys.Shift) != 0);
                app.Changed(false);
            }
            else Cursor = CursorFor(Hit(ToLocal(g)));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                if (Hit(ToLocal(Mouse)) != Part.None) BuildMenu().Show(Control.MousePosition);
                return;
            }
            if (Grabbed == Part.None) return;
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
            float big = e.Shift ? 10 : 1;
            double step = e.Shift ? 1 : 0.1;
            switch (e.KeyCode)
            {
                case Keys.Left: if (e.Alt) Nudge(0, 0, step); else Nudge(-big, 0, 0); break;
                case Keys.Right: if (e.Alt) Nudge(0, 0, -step); else Nudge(big, 0, 0); break;
                case Keys.Up: Nudge(0, -big, 0); break;
                case Keys.Down: Nudge(0, big, 0); break;
                case Keys.Delete: app.Remove(this); break;
                default: base.OnKeyDown(e); return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        protected virtual ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            AddBaseItems(m);
            return m;
        }

        protected void AddBaseItems(ContextMenuStrip m)
        {
            if (m.Items.Count > 0) m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Turn to 0°", null, delegate { Angle = 0; Relayout(); app.Changed(true); }));
            m.Items.Add(new ToolStripMenuItem("Turn to 90°", null, delegate { Angle = 90; Relayout(); app.Changed(true); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("New ruler", null, delegate { app.AddRuler(); }) { Enabled = app.CanAddRuler });
            m.Items.Add(new ToolStripMenuItem("New protractor", null, delegate { app.AddProtractor(); }) { Enabled = app.CanAddProtractor });
            m.Items.Add(new ToolStripMenuItem("Remove " + Title.ToLowerInvariant(), null, delegate { app.Remove(this); }));
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Show panel", null, delegate { app.ShowPanel(); }));
            m.Items.Add(new ToolStripMenuItem("About Screen Ruler", null, delegate { app.ShowAbout(); }));
            m.Items.Add(new ToolStripMenuItem("Exit", null, delegate { app.Close(); }));
        }
    }

    class Ruler : Tool
    {
        public const float Height_ = 72, ZeroMargin = 16, EndMargin = 52, TagRoom = 26, MinLength = 80, MaxLength = 4000;

        public readonly int Index;
        public float Length = 400, MarkerAt = 200;      // screen pixels
        public Unit Unit = Unit.Cm;
        public double MetersPerPixel;                    // set with Set scale…; 0 = not set yet
        public List<KeyValuePair<float, string>> CrossTags = new List<KeyValuePair<float, string>>();

        public Ruler(PanelForm app, int index) : base(app)
        {
            Index = index;
            Text = "Ruler " + (index + 1);
        }

        public override string Title { get { return "Ruler " + (Index + 1); } }
        public override Color Tint { get { return Skin.Tints[Index]; } }

        RectangleF Body { get { return new RectangleF(-S(ZeroMargin), 0, Length + S(ZeroMargin + EndMargin), S(Height_)); } }
        public override RectangleF LocalBounds
        {
            get { var b = Body; return new RectangleF(b.X, -S(TagRoom), b.Width, b.Height + S(TagRoom)); }
        }
        PointF KnobAt { get { return new PointF(Length + S(24), S(Height_) / 2); } }

        Screen Display { get { return Screens.At(Origin); } }

        // Screen pixels for one unit
        public double UnitPixels
        {
            get
            {
                switch (Unit)
                {
                    case Unit.Cm: return Screens.PixelsPerMM(Display, Origin) * 10;
                    case Unit.In: return Screens.PixelsPerMM(Display, Origin) * 25.4;
                    case Unit.Px: return 1;
                    default: return MetersPerPixel > 0 ? Units.Meters(Unit) / MetersPerPixel : Screens.PixelsPerMM(Display, Origin) * 10;
                }
            }
        }

        public string Reading { get { return Measure.Format(MarkerAt / UnitPixels, Unit); } }

        public override Part Hit(PointF p)
        {
            if (!Body.Contains(p)) return Part.None;
            if (Math.Abs(p.X - MarkerAt) <= S(6) && p.X <= Length + S(6)) return Part.Marker;
            var k = KnobAt;
            if (Math.Sqrt((p.X - k.X) * (p.X - k.X) + (p.Y - k.Y) * (p.Y - k.Y)) <= S(15)) return Part.Knob;
            if (p.X >= Length + S(40)) return Part.Grip;
            return Part.Body;
        }

        protected override Cursor CursorFor(Part r)
        {
            if (r == Part.Grip) return Cursors.SizeWE;
            return base.CursorFor(r);
        }

        public override void Begin(Part region, PointF g)
        {
            base.Begin(region, g);
            grabValue = region == Part.Grip ? Length - grabLocal.X : MarkerAt - grabLocal.X;
        }

        public override void Drag(PointF g, bool shift)
        {
            float x = ToLocal(g).X;
            switch (Grabbed)
            {
                case Part.Marker:
                    float m = x + grabValue;
                    if (shift)     // snap to the nearest tick
                    {
                        double step = Measure.Plan(Unit, UnitPixels, K).Step * UnitPixels;
                        m = (float)(Math.Round(m / step) * step);
                    }
                    MarkerAt = Math.Min(Length, Math.Max(0, m));
                    Render();
                    break;
                case Part.Grip:
                    Length = Math.Min(MaxLength, Math.Max(S(MinLength), x + grabValue));
                    MarkerAt = Math.Min(MarkerAt, Length);
                    Relayout();
                    break;
                default:
                    base.Drag(g, shift);
                    break;
            }
        }

        protected override void DrawLocal(Graphics g)
        {
            var b = Body;
            using (var path = Skin.Round(b, S(4)))
            {
                using (var br = new SolidBrush(Skin.A(Tint, 0.84))) g.FillPath(br, path);
                using (var p = new Pen(Skin.A(Skin.Ink, 0.55), S(1))) g.DrawPath(p, path);
            }

            // The measuring edge and its ticks
            double u = UnitPixels;
            var plan = Measure.Plan(Unit, u, K);
            int n = (int)Math.Floor(Length / (plan.Step * u) + 1e-6);
            using (var fLabel = Px("Segoe UI", 11, false))
            using (var pen = new Pen(Skin.Ink, S(1)))
            {
                for (int i = 0; i <= Math.Max(0, n); i++)
                {
                    float x = (float)(i * plan.Step * u), h;
                    if (i % plan.Major == 0) h = 22;
                    else if (plan.Mid > 0 && i % plan.Mid == 0) h = 15;
                    else if (plan.Quarter > 0 && i % plan.Quarter == 0) h = 11;
                    else if (plan.Eighth > 0 && i % plan.Eighth == 0) h = 8;
                    else h = 6;
                    g.DrawLine(pen, x, 0, x, S(h));
                    if (i % plan.Major == 0)
                        Label(g, Measure.Fixed(i * plan.Step, plan.Decimals), fLabel, Skin.Ink, new PointF(x, S(31)), false);
                }
                g.DrawLine(pen, b.Left + S(2), S(0.5f), b.Right - S(2), S(0.5f));
            }

            // Unit and angle, near the zero end
            using (var fInfo = Px("Segoe UI Symbol", 11, true))     // Segoe UI has no ∠
            {
                string info = (Units.Scaled(Unit) ? Units.Symbol(Unit) + " (scaled)" : Units.Symbol(Unit)) +
                              "   ∠ " + Measure.Angle(Measure.Norm180(Angle));
                Label(g, info, fInfo, Skin.A(Skin.Ink, 0.8), new PointF(Flipped ? Length : S(4), S(62)), true);
            }

            // The red marker: a line across the ruler and the reading from 0
            using (var p = new Pen(Skin.Marker, S(1.5f)))
                g.DrawLine(p, MarkerAt, 0, MarkerAt, S(Height_ - 1));
            using (var br = new SolidBrush(Skin.Marker))
                g.FillPolygon(br, new[] { new PointF(MarkerAt - S(6), S(Height_ - 1)), new PointF(MarkerAt + S(6), S(Height_ - 1)),
                                          new PointF(MarkerAt, S(Height_ - 9)) });
            using (var fRead = Px("Consolas", 12, true))
            {
                string r = Reading;
                float w = g.MeasureString(r, fRead, PointF.Empty, StringFormat.GenericTypographic).Width;
                bool right = MarkerAt + S(8) + w <= Length + S(EndMargin - 4);
                var tag = new RectangleF(right ? MarkerAt + S(5) : MarkerAt - S(9) - w, S(39), w + S(4), S(15));
                Skin.FillRound(g, tag, S(2), Skin.A(Color.White, 0.8));
                Label(g, r, fRead, Skin.Marker, new PointF(tag.X + tag.Width / 2, tag.Y + tag.Height / 2), false);
            }

            DrawKnob(g, KnobAt, S(12));

            // Grip at the far end: drag to make the ruler longer or shorter
            using (var p = new Pen(Skin.A(Skin.Ink, 0.5), S(1)))
                foreach (float dx in new[] { 43f, 46f, 49f })
                    g.DrawLine(p, Length + S(dx), S(Height_ / 2 - 12), Length + S(dx), S(Height_ / 2 + 12));

            // Where another ruler's edge crosses this one, the angle between them
            using (var fTag = Px("Segoe UI", 11, true))
            using (var p = new Pen(Skin.Ink, S(1)))
                foreach (var t in CrossTags)
                {
                    float w = g.MeasureString(t.Value, fTag, PointF.Empty, StringFormat.GenericTypographic).Width + S(8);
                    var box = new RectangleF(t.Key - w / 2, -S(21), w, S(17));
                    Skin.FillRound(g, box, S(3), Skin.A(Color.White, 0.92));
                    using (var path = Skin.Round(box, S(3))) g.DrawPath(p, path);
                    Label(g, t.Value, fTag, Skin.Ink, new PointF(box.X + box.Width / 2, box.Y + box.Height / 2), false);
                }
        }

        protected override ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            foreach (Unit u in Units.All)
            {
                if (u == Unit.Sm) m.Items.Add(new ToolStripSeparator());
                Unit pick = u;
                m.Items.Add(new ToolStripMenuItem(Units.MenuTitle(u), null, delegate { PickUnit(pick); }) { Checked = u == Unit });
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Set scale…", null, delegate { AskScale(Units.Scaled(Unit) ? Unit : Unit.Sm); }));
            m.Items.Add(new ToolStripMenuItem("Correct screen size…", null, delegate { CorrectSize(); }));
            if (Screens.Fixed(Display) > 0)
                m.Items.Add(new ToolStripMenuItem("Use the display's reported size", null, delegate
                {
                    Settings.Set(Screens.FixKey(Display), null);
                    app.Changed(true);
                }));
            AddBaseItems(m);
            return m;
        }

        void PickUnit(Unit u)
        {
            if (Units.Scaled(u) && MetersPerPixel <= 0) { AskScale(u); return; }
            Unit = u;
            Render();
            app.Changed(true);
        }

        void AskScale(Unit want)
        {
            if (MarkerAt < S(10))
            {
                app.Message("Move the red marker first",
                    "Lay " + Title + " along a length you know (a map's scale bar, or an object of known size in a photo), " +
                    "with 0 at one end. Then drag the red marker to the other end and choose Set scale… again.");
                return;
            }
            var units = new[] { Unit.Sm, Unit.Skm, Unit.Scm };
            double v;
            int i;
            if (!app.AskLength("Set the scale of " + Title,
                    "From 0 to the red marker is " + Measure.Format(MarkerAt, Unit.Px) + " on the screen. How long is it in reality?",
                    new[] { "m", "km", "cm" }, Math.Max(0, Array.IndexOf(units, want)), out v, out i)) return;
            MetersPerPixel = v * Units.Meters(units[i]) / MarkerAt;
            Unit = units[i];
            Render();
            app.Changed(true);
        }

        void CorrectSize()
        {
            if (MarkerAt < S(10))
            {
                app.Message("Move the red marker first",
                    "Hold a real ruler (or a bank card, 8.56 cm wide) against the screen, along " + Title + ". " +
                    "Drag the red marker to a length you can read on it, then choose Correct screen size… again.");
                return;
            }
            double v;
            int i;
            if (!app.AskLength("Correct the screen size",
                    "Hold a real ruler against the screen. From 0 to the red marker is " + Measure.Format(MarkerAt, Unit.Px) +
                    ". How long is it on your real ruler?",
                    new[] { "cm", "in" }, Unit == Unit.In ? 1 : 0, out v, out i)) return;
            double mm = v * (i == 0 ? 10 : 25.4);
            Settings.Set(Screens.FixKey(Display), Measure.Num(MarkerAt / mm));
            app.Changed(true);
        }

        public override string Serialize()
        {
            return string.Join(",", new[] { Measure.Num(Origin.X), Measure.Num(Origin.Y), Measure.Num(Angle),
                Measure.Num(Length), Measure.Num(MarkerAt), Units.Key(Unit), Measure.Num(MetersPerPixel) });
        }

        public static Ruler Parse(PanelForm app, string s, int index)
        {
            if (s == null) return null;
            var f = s.Split(',');
            double x, y, a, l, m, k;
            Unit u;
            if (f.Length != 7 || !Measure.TryNum(f[0], out x) || !Measure.TryNum(f[1], out y) || !Measure.TryNum(f[2], out a) ||
                !Measure.TryNum(f[3], out l) || !Measure.TryNum(f[4], out m) || !Units.Parse(f[5], out u) ||
                !Measure.TryNum(f[6], out k)) return null;
            var r = new Ruler(app, index);
            r.Origin = new PointF((float)x, (float)y);
            r.Angle = Measure.Norm180(a);
            r.Length = (float)Math.Min(MaxLength, Math.Max(MinLength, l));
            r.MarkerAt = (float)Math.Min(r.Length, Math.Max(0, m));
            r.MetersPerPixel = Math.Max(0, k);
            r.Unit = Units.Scaled(u) && r.MetersPerPixel <= 0 ? Unit.Cm : u;
            return r;
        }

        public override string[] Readouts() { return new[] { Reading, "∠ " + Measure.Angle(Measure.Norm180(Angle)) }; }
    }

    class Protractor : Tool
    {
        public const float Bar = 26, MinRadius = 70, MaxRadius = 1400;
        public float Radius = 150;      // screen pixels
        public double Arm = 60;         // degrees from the baseline, 0…180

        public Protractor(PanelForm app) : base(app) { Text = "Protractor"; }

        public override string Title { get { return "Protractor"; } }
        public override Color Tint { get { return Skin.ProtractorTint; } }

        float ArmLength { get { return Radius + S(36); } }
        PointF KnobAt { get { return new PointF(Radius + S(24), S(Bar) / 2); } }
        PointF ArmEnd
        {
            get
            {
                double r = Arm * Math.PI / 180;
                return new PointF(ArmLength * (float)Math.Cos(r), -ArmLength * (float)Math.Sin(r));
            }
        }

        public override RectangleF LocalBounds
        {
            get { return new RectangleF(-Radius - S(48), -Radius - S(50), 2 * Radius + S(96), Radius + S(50) + S(Bar + 2)); }
        }

        static float Len(float x, float y) { return (float)Math.Sqrt(x * x + y * y); }

        public override Part Hit(PointF p)
        {
            var e = ArmEnd;
            if (Len(p.X - e.X, p.Y - e.Y) <= S(11)) return Part.Arm;
            float len = Len(e.X, e.Y), t = (p.X * e.X + p.Y * e.Y) / len;
            if (t > S(20) && t <= len && Math.Abs(p.X * e.Y - p.Y * e.X) / len <= S(5)) return Part.Arm;
            var k = KnobAt;
            if (Len(p.X - k.X, p.Y - k.Y) <= S(14)) return Part.Knob;
            float r = Len(p.X, p.Y);
            if (p.Y <= 0 && r <= Radius) return r >= Radius - S(8) ? Part.Rim : Part.Body;
            if (p.Y > 0 && p.Y <= S(Bar) && p.X >= -Radius - S(8) && p.X <= Radius + S(40)) return Part.Body;
            return Part.None;
        }

        public override void Begin(Part region, PointF g)
        {
            base.Begin(region, g);
            grabValue = Radius - Len(grabLocal.X, grabLocal.Y);
        }

        public override void Drag(PointF g, bool shift)
        {
            var p = ToLocal(g);
            switch (Grabbed)
            {
                case Part.Arm:
                    double a = Math.Atan2(-p.Y, p.X) * 180 / Math.PI;
                    if (a < -90) a = 180; else if (a < 0) a = 0;
                    if (shift) a = Math.Round(a / 5) * 5;
                    Arm = Math.Min(180, Math.Max(0, a));
                    Render();
                    break;
                case Part.Rim:
                    Radius = Math.Min(MaxRadius, Math.Max(S(MinRadius), Len(p.X, p.Y) + grabValue));
                    Relayout();
                    break;
                default:
                    base.Drag(g, shift);
                    break;
            }
        }

        protected override void DrawLocal(Graphics g)
        {
            float R = Radius;
            using (var shape = new GraphicsPath())
            {
                shape.AddPie(-R, -R, 2 * R, 2 * R, 180, 180);
                shape.AddPath(Skin.Round(new RectangleF(-R - S(8), 0, 2 * R + S(48), S(Bar)), S(4)), false);
                shape.FillMode = FillMode.Winding;
                using (var b = new SolidBrush(Skin.A(Tint, 0.8))) g.FillPath(b, shape);
                using (var p = new Pen(Skin.A(Skin.Ink, 0.55), S(1))) g.DrawPath(p, shape);
            }

            // Degree ticks around the rim, with the outer scale counterclockwise and the inner one clockwise
            using (var pen = new Pen(Skin.Ink, S(1)))
            using (var fOuter = Px("Segoe UI", 10, false))
            using (var fInner = Px("Segoe UI", 8.5f, false))
            {
                for (int d = 0; d <= 180; d++)
                {
                    double r = d * Math.PI / 180;
                    float c = (float)Math.Cos(r), s = -(float)Math.Sin(r);
                    float h = S(d % 10 == 0 ? 15 : d % 5 == 0 ? 10 : 6);
                    g.DrawLine(pen, R * c, R * s, (R - h) * c, (R - h) * s);
                    if (d % 10 == 0)
                    {
                        Label(g, d.ToString(CultureInfo.InvariantCulture), fOuter, Skin.Ink,
                             new PointF((R - S(25)) * c, (R - S(25)) * s), false);
                        if (R >= S(110) && d % 180 != 0)
                            Label(g, (180 - d).ToString(CultureInfo.InvariantCulture), fInner, Skin.A(Skin.Ink, 0.55),
                                 new PointF((R - S(40)) * c, (R - S(40)) * s), false);
                    }
                }
                // Baseline and center cross
                g.DrawLine(pen, -R, 0, R, 0);
                g.DrawLine(pen, 0, -S(8), 0, S(8));
            }

            // The arm, from the center, and the angle it makes with the baseline
            float ar = S(30);
            if (Arm > 0.05)
                using (var p = new Pen(Skin.A(Skin.Marker, 0.7), S(2)))
                    g.DrawArc(p, -ar, -ar, 2 * ar, 2 * ar, 0, (float)-Arm);
            var e = ArmEnd;
            using (var p = new Pen(Skin.Marker, S(1.5f))) g.DrawLine(p, 0, 0, e.X, e.Y);
            using (var b = new SolidBrush(Skin.A(Skin.Marker, 0.85))) g.FillEllipse(b, e.X - S(8), e.Y - S(8), S(16), S(16));
            using (var b = new SolidBrush(Skin.Ink)) g.FillEllipse(b, -S(2.5f), -S(2.5f), S(5), S(5));

            using (var fBig = Px("Consolas", 16, true))
            {
                string label = Measure.Angle(Arm);
                double mid = (Arm < 90 ? Arm + 90 : Arm - 90) * Math.PI / 180;     // away from the arm
                var at = new PointF(R * 0.36f * (float)Math.Cos(mid), -Math.Max(R * 0.18f, R * 0.36f * (float)Math.Sin(mid)));
                float w = g.MeasureString(label, fBig, PointF.Empty, StringFormat.GenericTypographic).Width + S(8);
                Skin.FillRound(g, new RectangleF(at.X - w / 2, at.Y - S(10), w, S(20)), S(3), Skin.A(Color.White, 0.8));
                Label(g, label, fBig, Skin.Marker, at, false);
            }

            using (var f = Px("Segoe UI Symbol", 10, true))
                Label(g, "∠ " + Measure.Angle(Measure.Norm180(Angle)), f, Skin.A(Skin.Ink, 0.75), new PointF(-R * 0.55f, S(Bar) / 2), false);
            DrawKnob(g, KnobAt, S(11));
        }

        public override string Serialize()
        {
            return string.Join(",", new[] { Measure.Num(Origin.X), Measure.Num(Origin.Y), Measure.Num(Angle),
                Measure.Num(Radius), Measure.Num(Arm) });
        }

        public static Protractor Parse(PanelForm app, string s)
        {
            if (s == null) return null;
            var f = s.Split(',');
            var v = new double[5];
            if (f.Length != 5) return null;
            for (int i = 0; i < 5; i++) if (!Measure.TryNum(f[i], out v[i])) return null;
            var p = new Protractor(app);
            p.Origin = new PointF((float)v[0], (float)v[1]);
            p.Angle = Measure.Norm180(v[2]);
            p.Radius = (float)Math.Min(MaxRadius, Math.Max(MinRadius, v[3]));
            p.Arm = Math.Min(180, Math.Max(0, v[4]));
            return p;
        }

        public override string[] Readouts() { return new[] { Measure.Angle(Arm), "∠ " + Measure.Angle(Measure.Norm180(Angle)) }; }
    }

    // ── The panel: add tools and read every measurement. Closing it quits. ──
    class PanelForm : Form
    {
        enum HitKind { None, AddRuler, AddProtractor, About, Row, Close }

        class Row
        {
            public Tool Tool;
            public string Name;
            public string[] Values;
            public bool Swatch;
            public Color Tint;
        }

        public readonly List<Ruler> Rulers = new List<Ruler>();
        public Protractor ProtractorTool;
        readonly List<Row> rows = new List<Row>();
        readonly float k;
        HitKind pressed = HitKind.None;
        int pressedRow = -1;
        readonly Font fButton, fName, fValue, fClose;

        const float Width_ = 340, RowH = 22, TopPad = 44;

        public PanelForm()
        {
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Text = "Screen Ruler";
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
            for (int i = 0; i < 3; i++)
            {
                var r = Ruler.Parse(this, Settings.Get("Ruler" + i), i);
                if (r != null) Attach(r);
            }
            var p = Protractor.Parse(this, Settings.Get("Protractor"));
            if (p != null) Attach(p);
            foreach (var t in Tools())
            {
                bool seen = false;
                foreach (var scr in Screen.AllScreens) if (scr.Bounds.Contains(Point.Round(t.Origin))) seen = true;
                if (!seen) Place(t);
            }
            if (Rulers.Count == 0 && ProtractorTool == null) AddRuler();
            Changed(false);
        }

        public List<Tool> Tools()
        {
            var list = new List<Tool>();
            foreach (var r in Rulers) list.Add(r);
            if (ProtractorTool != null) list.Add(ProtractorTool);
            return list;
        }

        public bool CanAddRuler { get { return Rulers.Count < 3; } }
        public bool CanAddProtractor { get { return ProtractorTool == null; } }

        void Attach(Tool t)
        {
            var r = t as Ruler;
            if (r != null) { Rulers.Add(r); Rulers.Sort(delegate(Ruler a, Ruler b) { return a.Index.CompareTo(b.Index); }); }
            var p = t as Protractor;
            if (p != null) ProtractorTool = p;
            t.Show();
        }

        // A new tool in the middle of the screen with the mouse, a little apart from the others
        void Place(Tool t)
        {
            var wa = Screen.FromPoint(Control.MousePosition).WorkingArea;
            float sk = Screens.Scale(new PointF(wa.X + wa.Width / 2, wa.Y + wa.Height / 2));
            float cx = wa.X + wa.Width / 2f, cy = wa.Y + wa.Height / 2f;
            var r = t as Ruler;
            if (r != null)
            {
                r.Length = (float)Math.Floor(Math.Min(600 * sk, wa.Width * 0.45));
                r.MarkerAt = (float)Math.Floor(r.Length / 2);
                r.Origin = new PointF(cx - r.Length / 2, cy - 120 * sk + r.Index * 110 * sk - 72 * sk);
            }
            var p = t as Protractor;
            if (p != null)
            {
                p.Radius = 150 * sk;
                p.Origin = new PointF(cx, Math.Min(wa.Bottom - 50 * sk, cy + 120 * sk + p.Radius + 30 * sk));
            }
            t.Angle = 0;
            t.Relayout();
        }

        public void AddRuler()
        {
            if (!CanAddRuler) return;
            int i = 0;
            while (Rulers.Exists(delegate(Ruler x) { return x.Index == i; })) i++;
            var r = new Ruler(this, i);
            if (Rulers.Count > 0) { r.Unit = Rulers[0].Unit; r.MetersPerPixel = Rulers[0].MetersPerPixel; }
            Place(r);
            Attach(r);
            Changed(true);
        }

        public void AddProtractor()
        {
            if (!CanAddProtractor) return;
            var p = new Protractor(this);
            Place(p);
            Attach(p);
            Changed(true);
        }

        public void Remove(Tool t)
        {
            var r = t as Ruler;
            if (r != null) { Rulers.Remove(r); Settings.Set("Ruler" + r.Index, null); }
            if (t == ProtractorTool) { ProtractorTool = null; Settings.Set("Protractor", null); }
            t.Close();
            Changed(true);
        }

        public void ShowPanel()
        {
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        // Something moved or changed: update the crossing angles and the panel, and remember it
        public void Changed(bool save)
        {
            UpdateCrossings();
            RefreshRows();
            if (!save) return;
            foreach (var r in Rulers) Settings.Set("Ruler" + r.Index, r.Serialize());
            if (ProtractorTool != null) Settings.Set("Protractor", ProtractorTool.Serialize());
            Settings.Save();
        }

        void UpdateCrossings()
        {
            var tags = new Dictionary<int, List<KeyValuePair<float, string>>>();
            for (int i = 0; i < Rulers.Count; i++)
                for (int j = i + 1; j < Rulers.Count; j++)
                {
                    Ruler a = Rulers[i], b = Rulers[j];
                    var da = a.ToScreen(new PointF(1, 0)); var db = b.ToScreen(new PointF(1, 0));
                    var ea = new PointF(da.X - a.Origin.X, da.Y - a.Origin.Y);
                    var eb = new PointF(db.X - b.Origin.X, db.Y - b.Origin.Y);
                    double ta, tb;
                    float slack = a.S(12);
                    if (!Measure.Cross(a.Origin, ea, b.Origin, eb, out ta, out tb) ||
                        ta < -slack || ta > a.Length + slack || tb < -slack || tb > b.Length + slack) continue;
                    if (!tags.ContainsKey(a.Index)) tags[a.Index] = new List<KeyValuePair<float, string>>();
                    tags[a.Index].Add(new KeyValuePair<float, string>((float)ta, Measure.Angle(Measure.Between(a.Angle, b.Angle))));
                }
            foreach (var r in Rulers)
            {
                var now = tags.ContainsKey(r.Index) ? tags[r.Index] : new List<KeyValuePair<float, string>>();
                bool same = now.Count == r.CrossTags.Count;
                for (int i = 0; same && i < now.Count; i++)
                    same = now[i].Key == r.CrossTags[i].Key && now[i].Value == r.CrossTags[i].Value;
                if (same) continue;
                r.CrossTags = now;
                r.Render();
            }
        }

        void RefreshRows()
        {
            rows.Clear();
            foreach (var r in Rulers)
                rows.Add(new Row { Tool = r, Name = r.Title, Values = r.Readouts(), Swatch = true, Tint = r.Tint });
            for (int i = 0; i < Rulers.Count; i++)
                for (int j = i + 1; j < Rulers.Count; j++)
                    rows.Add(new Row
                    {
                        Name = "Ruler " + (Rulers[i].Index + 1) + " ∠ Ruler " + (Rulers[j].Index + 1),
                        Values = new[] { Measure.Angle(Measure.Between(Rulers[i].Angle, Rulers[j].Angle)) }
                    });
            if (ProtractorTool != null)
                rows.Add(new Row { Tool = ProtractorTool, Name = "Protractor", Values = ProtractorTool.Readouts(), Swatch = true,
                                   Tint = ProtractorTool.Tint });
            var want = new Size(S(Width_), S(TopPad + Math.Max(1, rows.Count) * RowH + 16));
            if (ClientSize != want) ClientSize = want;
            Invalidate();
        }

        Rectangle RcRuler { get { return new Rectangle(S(8), S(8), S(92), S(26)); } }
        Rectangle RcProtractor { get { return new Rectangle(S(106), S(8), S(112), S(26)); } }
        Rectangle RcAbout { get { return new Rectangle(ClientSize.Width - S(34), S(8), S(26), S(26)); } }
        Rectangle RcLcd { get { return new Rectangle(S(8), S(TopPad), ClientSize.Width - S(16), S(Math.Max(1, rows.Count) * RowH + 8)); } }
        Rectangle RcRow(int i) { var l = RcLcd; return new Rectangle(l.X + S(4), l.Y + S(4 + i * RowH), l.Width - S(8), S(RowH)); }
        Rectangle RcClose(int i) { var r = RcRow(i); return new Rectangle(r.Right - S(20), r.Y + S(2), S(18), S(18)); }

        HitKind HitTest(Point p, out int row)
        {
            row = -1;
            if (RcRuler.Contains(p)) return CanAddRuler ? HitKind.AddRuler : HitKind.None;
            if (RcProtractor.Contains(p)) return CanAddProtractor ? HitKind.AddProtractor : HitKind.None;
            if (RcAbout.Contains(p)) return HitKind.About;
            for (int i = 0; i < rows.Count; i++)
                if (RcRow(i).Contains(p) && rows[i].Tool != null)
                {
                    row = i;
                    return RcClose(i).Contains(p) ? HitKind.Close : HitKind.Row;
                }
            return HitKind.None;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = HitTest(e.Location, out pressedRow);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            int row;
            var h = HitTest(e.Location, out row);
            var was = pressed;
            int wasRow = pressedRow;
            pressed = HitKind.None;
            Invalidate();
            if (h != was || row != wasRow) return;
            switch (h)
            {
                case HitKind.AddRuler: AddRuler(); break;
                case HitKind.AddProtractor: AddProtractor(); break;
                case HitKind.About: ShowAbout(); break;
                case HitKind.Row: rows[row].Tool.Activate(); break;
                case HitKind.Close: Remove(rows[row].Tool); break;
            }
        }

        void Button(Graphics g, Rectangle r, HitKind h, string label, bool enabled)
        {
            bool down = pressed == h;
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, r);
            int t = Math.Max(1, (int)Math.Round(k));
            if (down) Skin.Sunken(g, r, t); else Skin.Raised(g, r, t);
            if (down) r.Offset(t, t);
            TextRenderer.DrawText(g, label, fButton, r, enabled ? Skin.Text : Skin.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Skin.Face);
            Button(g, RcRuler, HitKind.AddRuler, "+ Ruler", CanAddRuler);
            Button(g, RcProtractor, HitKind.AddProtractor, "+ Protractor", CanAddProtractor);
            Button(g, RcAbout, HitKind.About, "?", true);

            var lcd = RcLcd;
            using (var b = new SolidBrush(Skin.Lcd)) g.FillRectangle(b, lcd);
            Skin.Sunken(g, lcd, Math.Max(1, (int)Math.Round(k)));
            const TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            const TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            if (rows.Count == 0)
            {
                TextRenderer.DrawText(g, "Add a ruler or a protractor", fName, RcRow(0), Skin.LcdDim,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var r = RcRow(i);
                int x = r.X + S(4);
                if (row.Swatch)
                {
                    var sw = new Rectangle(x, r.Y + r.Height / 2 - S(5), S(10), S(10));
                    using (var b = new SolidBrush(row.Tint)) g.FillRectangle(b, sw);
                    using (var p = new Pen(Skin.LcdDigit)) g.DrawRectangle(p, sw);
                }
                x += S(16);
                TextRenderer.DrawText(g, row.Name, fName, new Rectangle(x, r.Y, S(150), r.Height), Skin.LcdDigit, left);
                int end = r.Right - (row.Tool != null ? S(26) : S(4));
                if (row.Values.Length > 1)
                    TextRenderer.DrawText(g, row.Values[1], fValue, new Rectangle(end - S(80), r.Y, S(80), r.Height), Skin.LcdDigit, right);
                if (row.Values.Length > 0)
                {
                    int w = S(row.Values.Length > 1 ? 88 : 80);
                    TextRenderer.DrawText(g, row.Values[0], fValue, new Rectangle(end - S(90) - w, r.Y, w + S(8), r.Height), Skin.LcdDigit,
                        row.Values.Length > 1 ? right : left);
                }
                if (row.Tool != null)
                    TextRenderer.DrawText(g, "×", fClose, RcClose(i), pressed == HitKind.Close && pressedRow == i ? Skin.Marker : Skin.LcdDim,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
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
            Text = "About Screen Ruler";
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

            Controls.Add(new Label { Text = "Screen Ruler", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
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

    // App icon: a yellow ruler, tilted, with a red marker
    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.03f;
            Skin.FillRound(g, new RectangleF(m, m, s - 2 * m, s - 2 * m), s * 0.2f, Skin.Face);

            var state = g.Save();
            g.TranslateTransform(s / 2, s / 2);
            g.RotateTransform(-30);
            float w = s * 0.86f, h = s * 0.26f;
            var strip = new RectangleF(-w / 2, -h / 2, w, h);
            Skin.FillRound(g, strip, s * 0.02f, Skin.Tints[0]);
            using (var p = new Pen(Skin.Ink, Math.Max(1, s * 0.012f)))
            {
                const int n = 12;
                for (int i = 0; i <= n; i++)
                {
                    float x = strip.Left + s * 0.05f + i * (w - s * 0.1f) / n;
                    float th = i % 4 == 0 ? h * 0.55f : i % 2 == 0 ? h * 0.38f : h * 0.24f;
                    g.DrawLine(p, x, strip.Top, x, strip.Top + th);
                }
            }
            using (var p = new Pen(Skin.Marker, Math.Max(1.5f, s * 0.022f)))
                g.DrawLine(p, s * 0.1f, strip.Top, s * 0.1f, strip.Bottom);
            g.Restore(state);
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
