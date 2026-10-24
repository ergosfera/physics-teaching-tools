// Reading Window for Windows: shades the whole screen except a clear window that follows the pointer,
// to help focus while reading (twin of ../mac/main.swift). Builds with the C# compiler that ships with
// Windows (.NET Framework 4.x); see build.ps1.
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
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Reading Window")]
[assembly: AssemblyProduct("Reading Window")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]

namespace ReadingWindow
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
                return Geometry.SelfTest();

            // Per-monitor DPI awareness, so the shade lines up with the screen on every display
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
        public static readonly Color Accent = Color.FromArgb(0x16, 0x3a, 0x70);

        // Shade colors: dark and calm, so the clear window stands out
        public static readonly string[,] Palette = {
            { "Black", "000000" }, { "Charcoal", "2b2b2b" }, { "Navy", "0b1f3a" }, { "Deep blue", "1e3a8a" },
            { "Dark green", "0f3d2e" }, { "Sepia", "3b2a1a" }, { "Plum", "3b1d3f" }, { "Warm gray", "57534e" } };

        public static bool TryHex(string s, out Color c)
        {
            int v;
            c = Color.Black;
            if (s == null || s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out v)) return false;
            c = Color.FromArgb(v >> 16 & 255, v >> 8 & 255, v & 255);
            return true;
        }

        public static Color Hex(string s) { Color c; TryHex(s, out c); return c; }
        public static string HexString(Color c) { return c.R.ToString("x2") + c.G.ToString("x2") + c.B.ToString("x2"); }

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

        public static Bitmap Swatch(Color c)
        {
            var bmp = new Bitmap(12, 12);
            using (var g = Graphics.FromImage(bmp))
            {
                using (var b = new SolidBrush(c)) g.FillRectangle(b, 0, 0, 11, 11);
                using (var p = new Pen(Color.FromArgb(150, 150, 150))) g.DrawRectangle(p, 0, 0, 11, 11);
            }
            return bmp;
        }
    }

    // ── The clear window's shape and geometry (same rules as the Mac app; checked by --selftest) ──

    enum HoleShape { Rect, Rounded, Ellipse, Band }

    static class Geometry
    {
        public static readonly HoleShape[] Shapes = { HoleShape.Rect, HoleShape.Rounded, HoleShape.Ellipse, HoleShape.Band };
        public const float MinW = 60, MaxW = 3000, MinH = 16, MaxH = 2000;      // 96-dpi pixels
        public const float Grip = 10;      // how far outside the window's edge you can grab it to resize

        public static string Title(HoleShape s)
        {
            switch (s)
            {
                case HoleShape.Rect: return "Rect";
                case HoleShape.Rounded: return "Round";
                case HoleShape.Ellipse: return "Ellipse";
                default: return "Band";
            }
        }

        // Saved names, the same as on the Mac
        public static string Key(HoleShape s)
        {
            switch (s)
            {
                case HoleShape.Rect: return "rect";
                case HoleShape.Rounded: return "rounded";
                case HoleShape.Ellipse: return "ellipse";
                default: return "band";
            }
        }

        public static bool Parse(string v, out HoleShape s)
        {
            foreach (var x in Shapes) if (Key(x) == v) { s = x; return true; }
            s = HoleShape.Rounded;
            return false;
        }

        // The clear window's bounding box; a band spans the whole width of its screen
        public static RectangleF Hole(PointF c, float w, float h, HoleShape shape, Rectangle screen)
        {
            if (shape == HoleShape.Band) return new RectangleF(screen.Left, c.Y - h / 2, screen.Width, h);
            return new RectangleF(c.X - w / 2, c.Y - h / 2, w, h);
        }

        public static float CornerRadius(RectangleF r) { return Math.Min(24, Math.Min(r.Width, r.Height) / 4); }

        public static GraphicsPath Path(RectangleF r, HoleShape shape, float k)
        {
            switch (shape)
            {
                case HoleShape.Rounded: return Skin.Round(r, CornerRadius(new RectangleF(0, 0, r.Width / k, r.Height / k)) * k);
                case HoleShape.Ellipse: var e = new GraphicsPath(); e.AddEllipse(r); return e;
                default: var p = new GraphicsPath(); p.AddRectangle(r); return p;
            }
        }

        // Which edges of the box p is just outside of (or on): -1 left/top, +1 right/bottom, 0 neither.
        // False if p isn't near the box at all, or is inside it.
        public static bool Edge(PointF p, RectangleF r, bool band, float grip, out int dx, out int dy)
        {
            dx = dy = 0;
            var outer = r; outer.Inflate(grip, grip);
            var inner = r; inner.Inflate(-2, -2);
            if (!outer.Contains(p) || inner.Contains(p)) return false;
            if (!band)
            {
                if (p.X < r.Left + 2) dx = -1; else if (p.X > r.Right - 2) dx = 1;
            }
            if (p.Y < r.Top + 2) dy = -1; else if (p.Y > r.Bottom - 2) dy = 1;
            return dx != 0 || dy != 0;
        }

        public static float ClampW(float w) { return Math.Min(MaxW, Math.Max(MinW, w)); }
        public static float ClampH(float h) { return Math.Min(MaxH, Math.Max(MinH, h)); }
        public static string Percent(double v) { return ((int)Math.Round(v * 100, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture) + "%"; }

        public static int SelfTest()
        {
            int fails = 0;
            Action<bool, string> check = delegate(bool ok, string what)
            {
                if (!ok) { fails++; Console.WriteLine("FAIL: " + what); }
            };
            var screen = new Rectangle(0, 0, 1000, 800);
            check(Hole(new PointF(500, 400), 300, 100, HoleShape.Rect, screen) == new RectangleF(350, 350, 300, 100), "hole rect");
            check(Hole(new PointF(500, 400), 300, 100, HoleShape.Band, screen) == new RectangleF(0, 350, 1000, 100), "hole band");
            check(CornerRadius(new RectangleF(0, 0, 300, 100)) == 24, "corner big");
            check(CornerRadius(new RectangleF(0, 0, 300, 40)) == 10, "corner small");
            var r = new RectangleF(100, 100, 200, 100);
            int dx, dy;
            check(Edge(new PointF(95, 150), r, false, Grip, out dx, out dy) && dx == -1 && dy == 0, "edge left");
            check(Edge(new PointF(305, 205), r, false, Grip, out dx, out dy) && dx == 1 && dy == 1, "edge corner");
            check(!Edge(new PointF(305, 150), r, true, Grip, out dx, out dy), "band has no side edges");
            check(!Edge(new PointF(200, 150), r, false, Grip, out dx, out dy), "inside");
            check(!Edge(new PointF(50, 150), r, false, Grip, out dx, out dy), "far");
            check(ClampW(10) == MinW && ClampW(5000) == MaxW && ClampH(100) == 100, "clamp");
            check(Percent(0.6) == "60%" && Percent(0.955) == "96%", "percent");
            Console.WriteLine(fails == 0 ? "Self-test passed" : "Self-test: " + fails + " failed");
            return fails;
        }
    }

    // ── Settings: %APPDATA%\ReadingWindow\settings.ini ──
    class Settings
    {
        public float Width = 640, Height = 120;        // 96-dpi pixels
        public HoleShape Shape = HoleShape.Rounded;
        public double Darkness = 0.6;
        public Color ShadeColor = Color.Black;
        public bool Enabled = true;
        public Point PanelAt = new Point(int.MinValue, 0);

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReadingWindow"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                int px = int.MinValue, py = 0;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string key = line.Substring(0, i).Trim(), val = line.Substring(i + 1).Trim();
                    double d; int n; bool b;
                    bool isNum = double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                    bool isInt = int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out n);
                    switch (key)
                    {
                        case "Width": if (isNum) s.Width = Geometry.ClampW((float)d); break;
                        case "Height": if (isNum) s.Height = Geometry.ClampH((float)d); break;
                        case "Shape": HoleShape sh; if (Geometry.Parse(val, out sh)) s.Shape = sh; break;
                        case "Darkness": if (isNum) s.Darkness = Math.Min(0.95, Math.Max(0.1, d)); break;
                        case "Color": Color c; if (Skin.TryHex(val, out c)) s.ShadeColor = c; break;
                        case "Enabled": if (bool.TryParse(val, out b)) s.Enabled = b; break;
                        case "PanelX": if (isInt) px = n; break;
                        case "PanelY": if (isInt) py = n; break;
                    }
                }
                s.PanelAt = new Point(px, py);
            }
            catch { }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var inv = CultureInfo.InvariantCulture;
                var lines = new List<string> {
                    "Width=" + Width.ToString("R", inv), "Height=" + Height.ToString("R", inv), "Shape=" + Geometry.Key(Shape),
                    "Darkness=" + Darkness.ToString("R", inv), "Color=" + Skin.HexString(ShadeColor), "Enabled=" + Enabled };
                if (PanelAt.X != int.MinValue)
                {
                    lines.Add("PanelX=" + PanelAt.X.ToString(inv));
                    lines.Add("PanelY=" + PanelAt.Y.ToString(inv));
                }
                File.WriteAllLines(FilePath, lines.ToArray());
            }
            catch { }
        }
    }

    static class Screens
    {
        [DllImport("user32.dll")] static extern IntPtr MonitorFromPoint(Point pt, int flags);
        [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);

        // Screen pixels per 96-dpi pixel (the Windows display scale)
        public static float Scale(Point p)
        {
            try
            {
                uint x, y;
                if (GetDpiForMonitor(MonitorFromPoint(p, 2), 0, out x, out y) == 0 && x > 0) return x / 96f;
            }
            catch { }
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) return g.DpiX / 96f;
        }
    }

    // ── The shade over one screen: a dark window with constant transparency, the clear window cut out of
    // its region. While the clear window follows the pointer the whole shade lets clicks and scrolling through. ──
    class ShadeForm : Form
    {
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20;

        readonly PanelForm app;
        public readonly Rectangle ScreenBounds;
        bool clickThrough;
        int resizeX, resizeY;
        bool resizing;

        public ShadeForm(PanelForm app, Rectangle screen)
        {
            this.app = app;
            ScreenBounds = screen;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Bounds = screen;
            Text = "Reading Window shade";
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00080000 | 0x00000080 | 0x08000000;     // WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        public void SetClickThrough(bool on)
        {
            if (!IsHandleCreated || on == clickThrough) return;
            clickThrough = on;
            int ex = GetWindowLong(Handle, GWL_EXSTYLE);
            SetWindowLong(Handle, GWL_EXSTYLE, on ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
        }

        // Cut the clear window out of the shade
        public void UpdateShade(RectangleF hole, HoleShape shape, float k)
        {
            var local = new RectangleF(hole.X - ScreenBounds.X, hole.Y - ScreenBounds.Y, hole.Width, hole.Height);
            var rgn = new Region(new Rectangle(0, 0, ScreenBounds.Width, ScreenBounds.Height));
            using (var p = Geometry.Path(local, shape, k)) rgn.Exclude(p);
            var old = Region;
            Region = rgn;
            if (old != null) old.Dispose();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_MOUSEACTIVATE = 0x21, MA_NOACTIVATE = 3;
            if (m.Msg == WM_MOUSEACTIVATE) { m.Result = new IntPtr(MA_NOACTIVATE); return; }
            base.WndProc(ref m);
        }

        // While held: drag just outside the clear window's edge to resize it; click anywhere else in the shade to make it follow
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            resizing = Geometry.Edge(Control.MousePosition, app.Hole, app.Cfg.Shape == HoleShape.Band, Geometry.Grip * app.K,
                                     out resizeX, out resizeY);
            if (resizing) Capture = true; else app.Grab();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var g = Control.MousePosition;
            if (resizing)
            {
                var c = app.Center;
                if (resizeX != 0) app.Cfg.Width = Geometry.ClampW(2 * Math.Abs(g.X - c.X) / app.K);
                if (resizeY != 0) app.Cfg.Height = Geometry.ClampH(2 * Math.Abs(g.Y - c.Y) / app.K);
                app.HoleChanged();
                return;
            }
            int dx, dy;
            if (Geometry.Edge(g, app.Hole, app.Cfg.Shape == HoleShape.Band, Geometry.Grip * app.K, out dx, out dy))
                Cursor = dx != 0 && dy == 0 ? Cursors.SizeWE : dx == 0 ? Cursors.SizeNS : (dx == dy ? Cursors.SizeNWSE : Cursors.SizeNESW);
            else
                Cursor = Cursors.Hand;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!resizing) return;
            resizing = false;
            Capture = false;
            app.Cfg.Save();
        }
    }

    // ── The panel: shade on/off, shape, darkness, size and color. Closing it quits. ──
    class PanelForm : Form
    {
        [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        enum HitKind { None, OnOff, About, Shape, Color, Slider }

        public readonly Settings Cfg = Settings.Load();
        readonly List<ShadeForm> shades = new List<ShadeForm>();
        readonly Timer tick;
        readonly LowLevelMouseProc hookProc;
        IntPtr hook = IntPtr.Zero;
        public Point Center;
        public bool Following = true;
        public float K = 1;             // screen pixels per 96-dpi pixel where the clear window is
        readonly float k;               // the panel's own scale
        HitKind pressed = HitKind.None;
        int pressedIndex = -1;
        readonly Font fButton, fLabel, fStatus, fHint;

        const float Width_ = 380, Height_ = 246;

        public PanelForm()
        {
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Text = "Reading Window";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Skin.Face;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            KeyPreview = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            fButton = Px("Tahoma", 12, true);
            fLabel = Px("Tahoma", 12, true);
            fStatus = Px("Tahoma", 11.5f, true);
            fHint = Px("Tahoma", 11, false);
            ClientSize = new Size(S(Width_), S(Height_));

            var wa = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(wa.Right - Width - S(40), wa.Top + S(40));
            if (Cfg.PanelAt.X != int.MinValue)
                foreach (var scr in Screen.AllScreens)
                    if (scr.WorkingArea.Contains(new Point(Cfg.PanelAt.X + S(40), Cfg.PanelAt.Y + S(10)))) { Location = Cfg.PanelAt; break; }

            Center = Control.MousePosition;
            hookProc = MouseHook;
            tick = new Timer { Interval = 15 };
            tick.Tick += delegate { Tick(); };
        }

        int S(float v) { return (int)Math.Round(v * k); }
        Font Px(string family, float px, bool bold)
        {
            return new Font(family, px * k, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildShades();
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            try
            {
                using (var cur = Process.GetCurrentProcess())
                using (var mod = cur.MainModule)
                    hook = SetWindowsHookEx(14, hookProc, GetModuleHandle(mod.ModuleName), 0);     // WH_MOUSE_LL
            }
            catch { }
            tick.Start();
        }

        void OnDisplayChanged(object sender, EventArgs e) { BeginInvoke((Action)BuildShades); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            tick.Stop();
            if (hook != IntPtr.Zero) UnhookWindowsHookEx(hook);
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            if (WindowState == FormWindowState.Normal) Cfg.PanelAt = Location;
            Cfg.Save();
            foreach (var s in shades) s.Close();
            base.OnFormClosing(e);
        }

        // ── The shade ──

        // One shade over each screen, under the panel
        void BuildShades()
        {
            foreach (var s in shades) s.Close();
            shades.Clear();
            foreach (var scr in Screen.AllScreens) shades.Add(new ShadeForm(this, scr.Bounds));
            UpdateVisibility();
        }

        void UpdateVisibility()
        {
            foreach (var s in shades)
            {
                s.BackColor = Cfg.ShadeColor;
                s.Opacity = Cfg.Darkness;
                if (Cfg.Enabled) { if (!s.Visible) s.Show(); } else s.Hide();
            }
            HoleChanged();
            KeepOnTop();
        }

        // The panel stays above the shade
        void KeepOnTop()
        {
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);     // HWND_TOPMOST, NOSIZE | NOMOVE | NOACTIVATE
        }

        public RectangleF Hole
        {
            get
            {
                var scr = Screen.FromPoint(Center).Bounds;
                return Geometry.Hole(Center, Cfg.Width * K, Cfg.Height * K, Cfg.Shape, scr);
            }
        }

        public void HoleChanged()
        {
            K = Screens.Scale(Center);
            var h = Hole;
            foreach (var s in shades) if (s.Visible) s.UpdateShade(h, Cfg.Shape, K);
            Invalidate();
        }

        bool MouseOverPanel { get { return Visible && WindowState != FormWindowState.Minimized && Bounds.Contains(Control.MousePosition); } }

        void Tick()
        {
            if (!Cfg.Enabled) return;
            foreach (var s in shades) s.SetClickThrough(Following);
            if (!Following) return;
            // Follow the pointer, but not onto the panel, so its controls can be used
            var g = Control.MousePosition;
            if (!MouseOverPanel && g != Center)
            {
                Center = g;
                HoleChanged();
            }
        }

        // Each click outside the panel holds the window, or makes a held one follow again (the click also reaches
        // the page below). A click just outside a held window's edge is left to the shade, which resizes it.
        IntPtr MouseHook(int nCode, IntPtr wParam, IntPtr lParam)
        {
            const int WM_LBUTTONDOWN = 0x0201;
            if (nCode >= 0 && wParam.ToInt32() == WM_LBUTTONDOWN && Cfg.Enabled && !MouseOverPanel)
            {
                int dx, dy;
                bool onEdge = Geometry.Edge(Control.MousePosition, Hole, Cfg.Shape == HoleShape.Band, Geometry.Grip * K, out dx, out dy);
                if (Following) BeginInvoke((Action)Hold);
                else if (!onEdge) BeginInvoke((Action)Grab);
            }
            return CallNextHookEx(hook, nCode, wParam, lParam);
        }

        // Hold the window where it is
        void Hold()
        {
            if (!Following || MouseOverPanel) return;
            Following = false;
            Center = Control.MousePosition;
            foreach (var s in shades) s.SetClickThrough(false);
            HoleChanged();
        }

        public void Grab()
        {
            Following = true;
            Center = Control.MousePosition;
            foreach (var s in shades) s.SetClickThrough(true);
            HoleChanged();
            KeepOnTop();
        }

        void ToggleShade()
        {
            Cfg.Enabled = !Cfg.Enabled;
            Following = true;
            Cfg.Save();
            UpdateVisibility();
        }

        void SetShape(HoleShape s)
        {
            Cfg.Shape = s;
            Cfg.Save();
            HoleChanged();
        }

        void SetColor(Color c)
        {
            Cfg.ShadeColor = c;
            Cfg.Save();
            foreach (var s in shades) s.BackColor = c;
            Invalidate();
        }

        // ── Panel layout ──

        Rectangle RcOnOff { get { return new Rectangle(S(8), S(8), S(104), S(26)); } }
        Rectangle RcAbout { get { return new Rectangle(ClientSize.Width - S(34), S(8), S(26), S(26)); } }
        Rectangle RcStatus { get { return new Rectangle(S(118), S(8), ClientSize.Width - S(118 + 40), S(26)); } }
        Rectangle RcShape(int i) { return new Rectangle(S(8 + i * 92), S(42), S(86), S(26)); }
        Rectangle RcSliderRow(int i) { return new Rectangle(S(8), S(78 + i * 34), ClientSize.Width - S(16), S(28)); }
        Rectangle RcTrack(int i) { var r = RcSliderRow(i); return new Rectangle(r.X + S(88), r.Y + r.Height / 2 - S(4), r.Width - S(88 + 70), S(8)); }
        Rectangle RcColor { get { return new Rectangle(S(8), S(184), S(160), S(26)); } }

        // Sliders: 0 darkness, 1 width, 2 height
        double SliderValue(int i)
        {
            switch (i)
            {
                case 0: return (Cfg.Darkness - 0.1) / 0.85;
                case 1: return (Cfg.Width - Geometry.MinW) / (Geometry.MaxW - Geometry.MinW);
                default: return (Cfg.Height - Geometry.MinH) / (Geometry.MaxH - Geometry.MinH);
            }
        }

        void SetSlider(int i, double f)
        {
            f = Math.Min(1, Math.Max(0, f));
            switch (i)
            {
                case 0:
                    Cfg.Darkness = Math.Round((0.1 + 0.85 * f) * 100) / 100;
                    foreach (var s in shades) s.Opacity = Cfg.Darkness;
                    break;
                case 1: Cfg.Width = Geometry.ClampW((float)Math.Round(Geometry.MinW + f * (Geometry.MaxW - Geometry.MinW))); break;
                default: Cfg.Height = Geometry.ClampH((float)Math.Round(Geometry.MinH + f * (Geometry.MaxH - Geometry.MinH))); break;
            }
            HoleChanged();
        }

        string SliderText(int i)
        {
            switch (i)
            {
                case 0: return Geometry.Percent(Cfg.Darkness);
                case 1: return Cfg.Shape == HoleShape.Band ? "full" : ((int)Cfg.Width).ToString(CultureInfo.InvariantCulture) + " px";
                default: return ((int)Cfg.Height).ToString(CultureInfo.InvariantCulture) + " px";
            }
        }

        HitKind HitTest(Point p, out int index)
        {
            index = -1;
            if (RcOnOff.Contains(p)) return HitKind.OnOff;
            if (RcAbout.Contains(p)) return HitKind.About;
            for (int i = 0; i < Geometry.Shapes.Length; i++) if (RcShape(i).Contains(p)) { index = i; return HitKind.Shape; }
            for (int i = 0; i < 3; i++)
            {
                var row = RcSliderRow(i);
                var t = RcTrack(i);
                if (p.Y >= row.Top + S(2) && p.Y < row.Bottom - S(2) && p.X >= t.Left - S(8) && p.X <= t.Right + S(8))
                {
                    index = i;
                    return i == 1 && Cfg.Shape == HoleShape.Band ? HitKind.None : HitKind.Slider;
                }
            }
            if (RcColor.Contains(p)) return HitKind.Color;
            return HitKind.None;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            pressed = HitTest(e.Location, out pressedIndex);
            if (pressed == HitKind.Slider) SetSlider(pressedIndex, (e.X - RcTrack(pressedIndex).Left) / (double)RcTrack(pressedIndex).Width);
            if (pressed == HitKind.Color)
            {
                ColorMenu().Show(this, new Point(RcColor.Left, RcColor.Bottom + S(2)));
                pressed = HitKind.None;
            }
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressed == HitKind.Slider) SetSlider(pressedIndex, (e.X - RcTrack(pressedIndex).Left) / (double)RcTrack(pressedIndex).Width);
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
            if (was == HitKind.Slider) { Cfg.Save(); return; }
            if (h != was || index != wasIndex) return;
            switch (h)
            {
                case HitKind.OnOff: ToggleShade(); break;
                case HitKind.About: ShowAbout(); break;
                case HitKind.Shape: SetShape(Geometry.Shapes[index]); break;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            // Scroll over a slider to fine-tune it
            int index;
            if (HitTest(e.Location, out index) != HitKind.Slider) return;
            double step = index == 0 ? 0.01 / 0.85 : 10.0 / (index == 1 ? Geometry.MaxW - Geometry.MinW : Geometry.MaxH - Geometry.MinH);
            SetSlider(index, SliderValue(index) + (e.Delta > 0 ? step : -step));
            Cfg.Save();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Control | Keys.S: ToggleShade(); return true;
                case Keys.Control | Keys.D1: SetShape(HoleShape.Rect); return true;
                case Keys.Control | Keys.D2: SetShape(HoleShape.Rounded); return true;
                case Keys.Control | Keys.D3: SetShape(HoleShape.Ellipse); return true;
                case Keys.Control | Keys.D4: SetShape(HoleShape.Band); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        ContextMenuStrip ColorMenu()
        {
            var m = new ContextMenuStrip();
            string current = Skin.HexString(Cfg.ShadeColor);
            for (int i = 0; i < Skin.Palette.GetLength(0); i++)
            {
                string hex = Skin.Palette[i, 1];
                m.Items.Add(new ToolStripMenuItem(Skin.Palette[i, 0], Skin.Swatch(Skin.Hex(hex)), delegate { SetColor(Skin.Hex(hex)); })
                    { Checked = hex == current });
            }
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Other color…", null, delegate
            {
                using (var dlg = new ColorDialog { Color = Cfg.ShadeColor, FullOpen = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK) SetColor(Color.FromArgb(dlg.Color.R, dlg.Color.G, dlg.Color.B));
            }));
            return m;
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
            int t = Math.Max(1, (int)Math.Round(k));
            const TextFormatFlags left = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            const TextFormatFlags right = TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            Button(g, RcOnOff, Cfg.Enabled || pressed == HitKind.OnOff, Cfg.Enabled ? "Shade: On" : "Shade: Off", true, false);
            Button(g, RcAbout, pressed == HitKind.About, "?", true, false);

            var st = Rectangle.Inflate(RcStatus, -S(2), -S(2));
            using (var b = new SolidBrush(Skin.Lcd)) g.FillRectangle(b, st);
            Skin.Sunken(g, st, t);
            string status = !Cfg.Enabled ? "Shade is off" : Following ? "Following · click to hold" : "Held · click to follow";
            TextRenderer.DrawText(g, status, fStatus, Rectangle.Inflate(st, -S(6), 0), Skin.LcdDigit, left);

            for (int i = 0; i < Geometry.Shapes.Length; i++)
                Button(g, RcShape(i), Cfg.Shape == Geometry.Shapes[i] || (pressed == HitKind.Shape && pressedIndex == i),
                       Geometry.Title(Geometry.Shapes[i]), true, false);

            string[] names = { "Darkness", "Width", "Height" };
            for (int i = 0; i < 3; i++)
            {
                var row = RcSliderRow(i);
                bool on = !(i == 1 && Cfg.Shape == HoleShape.Band);
                var fg = on ? Skin.Text : Skin.Muted;
                TextRenderer.DrawText(g, names[i], fLabel, new Rectangle(row.X, row.Y, S(84), row.Height), fg, left);
                var tr = RcTrack(i);
                using (var b = new SolidBrush(Skin.Sh)) g.FillRectangle(b, tr);
                Skin.Sunken(g, tr, t);
                if (on)
                {
                    int x = tr.Left + (int)Math.Round(SliderValue(i) * tr.Width);
                    using (var b = new SolidBrush(Skin.Accent)) g.FillRectangle(b, tr.Left + 2 * t, tr.Top + 2 * t, Math.Max(0, x - tr.Left - 2 * t), tr.Height - 4 * t);
                    var thumb = new Rectangle(x - S(6), row.Y + row.Height / 2 - S(10), S(12), S(20));
                    using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, thumb);
                    Skin.Raised(g, thumb, t);
                }
                TextRenderer.DrawText(g, SliderText(i), fLabel, new Rectangle(tr.Right + S(8), row.Y, row.Right - tr.Right - S(8), row.Height), fg, right);
            }

            Button(g, RcColor, pressed == HitKind.Color, "Color", true, true);
            var sw = new Rectangle(RcColor.Right - S(44), RcColor.Y + RcColor.Height / 2 - S(7), S(22), S(14));
            using (var b = new SolidBrush(Cfg.ShadeColor)) g.FillRectangle(b, sw);
            using (var p = new Pen(Skin.Text)) g.DrawRectangle(p, sw);
            TextRenderer.DrawText(g, "▾", fButton, new Rectangle(RcColor.Right - S(20), RcColor.Y, S(14), RcColor.Height), Skin.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            TextRenderer.DrawText(g, "Click: hold / follow  ·  Drag just outside its edge: resize", fHint,
                new Rectangle(S(8), RcColor.Bottom + S(4), ClientSize.Width - S(16), S(20)), Skin.Muted, left);
        }

        public void ShowAbout()
        {
            using (var a = new AboutForm()) a.ShowDialog(this);
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
            Text = "About Reading Window";
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

            Controls.Add(new Label { Text = "Reading Window", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
            Controls.Add(new Label { Text = "Version 1.0.1  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Free software, provided \"as is\", without warranty of any kind.\r\n" +
                       "This app collects no personal data and never connects to the internet. " +
                       "It doesn't capture or record the screen.",
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

    // App icon: lines of text, all shaded but the ones in a clear window
    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.03f;
            var body = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            using (var p = Skin.Round(body, s * 0.2f))
            {
                using (var b = new SolidBrush(Color.FromArgb(0xf4, 0xf1, 0xea))) g.FillPath(b, p);
                float lineH = s * 0.045f;
                using (var b = new SolidBrush(Color.FromArgb(0x55, 0x55, 0x5c)))
                    for (int i = 0; i < 6; i++)
                    {
                        float y = body.Top + s * 0.17f + i * s * 0.11f - lineH;
                        float w = body.Width * (i % 3 == 2 ? 0.5f : 0.72f);
                        using (var line = Skin.Round(new RectangleF(body.Left + s * 0.1f, y, w, lineH), lineH / 2)) g.FillPath(b, line);
                    }
                var hole = new RectangleF(body.Left + s * 0.05f, body.Top + body.Height / 2 - s * 0.08f, body.Width - s * 0.1f, s * 0.17f);
                using (var shade = new Region(p))
                using (var h = Skin.Round(hole, s * 0.03f))
                using (var b = new SolidBrush(Color.FromArgb(199, 0x0b, 0x1f, 0x3a)))
                {
                    shade.Exclude(h);
                    g.FillRegion(b, shade);
                }
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
