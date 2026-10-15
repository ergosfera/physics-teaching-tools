// Field for Windows: the Field dot timer (tools/teaching/field-timer.html) as a desktop app.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Field")]
[assembly: AssemblyProduct("Field")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace FieldTimer
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
            Application.Run(new FieldForm());
        }
    }

    // ── Dark Windows 95 skin (same tokens as the web page) ──
    static class Skin
    {
        public static readonly Color Face = Color.FromArgb(0x3b, 0x3b, 0x3b);
        public static readonly Color Hi = Color.FromArgb(0x6e, 0x6e, 0x6e);
        public static readonly Color Lt = Color.FromArgb(0x54, 0x54, 0x54);
        public static readonly Color Sh = Color.FromArgb(0x1c, 0x1c, 0x1c);
        public static readonly Color Dk = Color.Black;
        public static readonly Color Text = Color.FromArgb(0xe8, 0xe8, 0xe8);
        public static readonly Color TitleFrom = Color.FromArgb(0x0a, 0x0a, 0x5c);
        public static readonly Color TitleTo = Color.FromArgb(0x16, 0x3a, 0x70);
        public static readonly Color CloseHover = Color.FromArgb(0xc0, 0x00, 0x00);

        public static readonly Color Bg = Color.FromArgb(0x06, 0x06, 0x07);
        public static readonly Color DotIdle = Color.FromArgb(26, 255, 255, 255);
        public static readonly Color SetupFill = Color.FromArgb(0xcf, 0xcd, 0xc6);
        public static readonly Color Active = Color.FromArgb(0xf4, 0xf1, 0xea);
        public static readonly Color Elapsed = Color.FromArgb(0x33, 0x33, 0x38);

        public static readonly Color Lcd = Color.FromArgb(0x7f, 0xae, 0x86);
        public static readonly Color LcdDigit = Color.FromArgb(0x16, 0x32, 0x1c);

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

    class Settings
    {
        public const int MinSeconds = 1, MaxSeconds = 6 * 60 * 60;
        public int Seconds = 600;
        public bool TopMost = false, Sound = true;
        public Rectangle Bounds = Rectangle.Empty;

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FieldTimer"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public static int ClampSeconds(int v) { return v < MinSeconds ? MinSeconds : v > MaxSeconds ? MaxSeconds : v; }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                int x = 0, y = 0, w = 0, h = 0;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string key = line.Substring(0, i).Trim(), val = line.Substring(i + 1).Trim();
                    int n; bool b;
                    bool isInt = int.TryParse(val, out n), isBool = bool.TryParse(val, out b);
                    switch (key)
                    {
                        case "Seconds": if (isInt) s.Seconds = ClampSeconds(n); break;
                        case "TopMost": if (isBool) s.TopMost = b; break;
                        case "Sound": if (isBool) s.Sound = b; break;
                        case "X": if (isInt) x = n; break;
                        case "Y": if (isInt) y = n; break;
                        case "W": if (isInt) w = n; break;
                        case "H": if (isInt) h = n; break;
                    }
                }
                if (w > 0 && h > 0) s.Bounds = new Rectangle(x, y, w, h);
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
                    "Seconds=" + Seconds, "TopMost=" + TopMost, "Sound=" + Sound,
                    "X=" + Bounds.X, "Y=" + Bounds.Y, "W=" + Bounds.Width, "H=" + Bounds.Height });
            }
            catch { }
        }
    }

    class FieldForm : Form
    {
        enum State { Setup, Running, Paused, Done }
        enum Hit { None, Full, Pin, About, Start, Cancel, Ok, Field }

        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

        // Same geometry as the web page, in 96-dpi pixels
        const float DOT = 11, GAP = 11, PAD = 18;
        static readonly double LogMin = Math.Log(Settings.MinSeconds), LogMax = Math.Log(Settings.MaxSeconds);
        const string HintText = "Drag the field to set time";
        const string GlyphFull = "", GlyphWindow = "", GlyphPin = "", GlyphPinned = "";
        const TextFormatFlags Center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                                       TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

        readonly Settings cfg = Settings.Load();
        readonly float k;
        State state = State.Setup;
        readonly Stopwatch sw = new Stopwatch();         // runs while counting down, stops while paused
        readonly Stopwatch doneClock = new Stopwatch();
        double durationMs;
        int lastGray = -1;
        bool dragging;
        Hit pressed = Hit.None, hover = Hit.None;
        int beepsLeft;
        long nextBeepMs;
        bool fullScreen, wasMaximized;
        Rectangle windowedBounds;

        readonly Timer tick;
        readonly ToolTip tip;
        string tipText = "";
        ToolStripMenuItem miTop, miSound, miFull;

        readonly Font fTitle, fGlyph, fAbout, fHint, fStart, fCancel, fPaused, fAlertTitle, fDone, fOk;
        Font fReadout;
        float readoutPx;
        Bitmap spIdle, spSetup, spActive, spGray;
        int spPad;

        Rectangle rcHeader, rcFull, rcPin, rcAbout, rcScreen, rcApp;
        int cols = 1, rows = 1, total = 1;
        float gx0, gy0;

        public FieldForm()
        {
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            Text = "Field";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Skin.Face;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            MinimumSize = SizeFromClientSize(new Size(S(260), S(240)));

            fTitle = Px("Tahoma", 12, true);
            fGlyph = Px("Segoe MDL2 Assets", 12, false);
            fAbout = Px("Tahoma", 12.5f, true);
            fHint = Px("Tahoma", 12, false);
            fStart = Px("Tahoma", 13.6f, true);
            fCancel = Px("Tahoma", 15, false);
            fPaused = Px("Tahoma", 11.5f, true);
            fAlertTitle = Px("Tahoma", 11.5f, true);
            fDone = Px("Tahoma", 13.6f, false);
            fOk = Px("Tahoma", 12.5f, true);
            MakeSprites();

            tip = new ToolTip { InitialDelay = 600, ReshowDelay = 200 };
            BuildMenu();
            PlaceWindow();
            SetTopMost(cfg.TopMost);
            DoLayout();

            tick = new Timer { Interval = 30 };
            tick.Tick += OnTick;
            tick.Start();
        }

        int S(float v) { return (int)Math.Round(v * k); }
        int T { get { return Math.Max(1, (int)Math.Round(k)); } }
        Font Px(string family, float px, bool bold)
        {
            return new Font(family, px * k, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }

        // ── Window ──

        void PlaceWindow()
        {
            var wa = Screen.PrimaryScreen.WorkingArea;
            var size = SizeFromClientSize(new Size(S(520), S(420)));
            var b = new Rectangle(wa.X + (wa.Width - size.Width) / 2, wa.Y + (wa.Height - size.Height) / 2, size.Width, size.Height);
            var saved = cfg.Bounds;
            if (saved.Width > 0)
                foreach (var scr in Screen.AllScreens)
                    if (scr.WorkingArea.IntersectsWith(new Rectangle(saved.X + S(40), saved.Y, saved.Width - S(80), S(30))))
                    {
                        b = saved;
                        break;
                    }
            Bounds = b;
        }

        void BuildMenu()
        {
            var menu = new ContextMenuStrip();
            miTop = new ToolStripMenuItem("Always on top", null, delegate { SetTopMost(!TopMost); });
            miSound = new ToolStripMenuItem("Sound at the end", null, delegate { cfg.Sound = !cfg.Sound; miSound.Checked = cfg.Sound; cfg.Save(); });
            miSound.Checked = cfg.Sound;
            miFull = new ToolStripMenuItem("Full screen", null, delegate { ToggleFullScreen(); }) { ShortcutKeyDisplayString = "F11" };
            menu.Items.Add(miTop);
            menu.Items.Add(miSound);
            menu.Items.Add(miFull);
            menu.Items.Add(new ToolStripMenuItem("About Field", null, delegate { ShowAbout(); }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, delegate { Close(); }));
            ContextMenuStrip = menu;
        }

        void SetTopMost(bool on)
        {
            TopMost = on;
            cfg.TopMost = on;
            miTop.Checked = on;
            cfg.Save();
            UpdateTip();
            Invalidate(rcHeader);
        }

        void ToggleFullScreen()
        {
            if (!fullScreen)
            {
                wasMaximized = WindowState == FormWindowState.Maximized;
                windowedBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                var scr = Screen.FromControl(this).Bounds;
                fullScreen = true;
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                Bounds = scr;
            }
            else
            {
                fullScreen = false;
                FormBorderStyle = FormBorderStyle.Sizable;
                Bounds = windowedBounds;
                if (wasMaximized) WindowState = FormWindowState.Maximized;
            }
            miFull.Checked = fullScreen;
            UpdateTip();
            DoLayout();
            Invalidate();
        }

        void ShowAbout()
        {
            using (var a = new AboutForm()) a.ShowDialog(this);
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            if (tip == null) return;   // still in the constructor
            DoLayout();
            Invalidate();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            cfg.Bounds = fullScreen ? windowedBounds : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            cfg.Save();
            base.OnFormClosing(e);
        }

        // ── Layout ──

        void DoLayout()
        {
            int W = ClientSize.Width, H = ClientSize.Height;
            int hh = S(34);
            rcHeader = new Rectangle(0, 0, W, hh);
            int bw = S(26), bh = S(24), by = (hh - bh) / 2, x = W - S(8);
            rcAbout = new Rectangle(x - bw, by, bw, bh); x -= bw + S(3);
            rcPin = new Rectangle(x - bw, by, bw, bh); x -= bw + S(3);
            rcFull = new Rectangle(x - bw, by, bw, bh);

            int m = S(6), b = 2 * T;
            rcScreen = new Rectangle(m, hh + m, Math.Max(2 * b + 1, W - 2 * m), Math.Max(2 * b + 1, H - hh - 2 * m));
            rcApp = Rectangle.Inflate(rcScreen, -b, -b);

            float dot = DOT * k, cell = (DOT + GAP) * k, pad = PAD * k;
            cols = Math.Max(1, (int)Math.Floor((rcApp.Width - 2 * pad) / cell));
            rows = Math.Max(1, (int)Math.Floor((rcApp.Height - 2 * pad) / cell));
            total = cols * rows;
            float gw = cols * dot + (cols - 1) * GAP * k, gh = rows * dot + (rows - 1) * GAP * k;
            gx0 = rcApp.X + (rcApp.Width - gw) / 2;
            gy0 = rcApp.Y + (rcApp.Height - gh) / 2;
            lastGray = -1;

            // The readout grows with the window, like clamp(2.4rem, 10vw, 4.4rem) on the web page
            float px = Math.Max(38, Math.Min(70, 0.10f * rcApp.Width / k));
            if (fReadout == null || Math.Abs(px - readoutPx) > 0.5f)
            {
                if (fReadout != null) fReadout.Dispose();
                fReadout = Px("Consolas", px, true);
                readoutPx = px;
            }
        }

        Size Measure(string s, Font f)
        {
            return TextRenderer.MeasureText(s, f, Size.Empty, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        void SetupRects(out Rectangle lcd, out Rectangle hint, out Rectangle start)
        {
            var ts = Measure(FormatDuration(cfg.Seconds), fReadout);
            var hs = Measure(HintText, fHint);
            var ss = Measure("Start", fStart);
            int lw = ts.Width + S(48) + 4 * T, lh = ts.Height + S(24) + 4 * T;
            int bw = ss.Width + S(16) + S(48), bh = ss.Height + S(18);
            int gap = S(18);
            int totalH = lh + gap + hs.Height + gap + S(2) + bh;
            int cx = rcApp.X + rcApp.Width / 2, y = rcApp.Y + (rcApp.Height - totalH) / 2;
            lcd = new Rectangle(cx - lw / 2, y, lw, lh); y += lh + gap;
            hint = new Rectangle(cx - hs.Width / 2 - S(4), y, hs.Width + S(8), hs.Height); y += hs.Height + gap + S(2);
            start = new Rectangle(cx - bw / 2, y, bw, bh);
        }

        void DoneRects(out Rectangle box, out Rectangle title, out Rectangle label, out Rectangle ok)
        {
            int bw = S(230), t = 2 * T;
            int th = Measure("Field Timer", fAlertTitle).Height + S(10);
            int lh = Measure("Time's up!", fDone).Height + S(32);
            var os = Measure("OK", fOk);
            int okw = os.Width + S(48), okh = os.Height + S(12);
            int bh = t + th + lh + okh + S(16) + t;
            box = new Rectangle(rcApp.X + (rcApp.Width - bw) / 2, rcApp.Y + (rcApp.Height - bh) / 2, bw, bh);
            title = new Rectangle(box.X + t, box.Y + t, bw - 2 * t, th);
            label = new Rectangle(box.X + t, title.Bottom, bw - 2 * t, lh);
            ok = new Rectangle(box.X + (bw - okw) / 2, label.Bottom, okw, okh);
        }

        Rectangle RcCancel { get { return new Rectangle(rcApp.Right - S(6) - S(22), rcApp.Y + S(6), S(22), S(20)); } }

        // ── Time ──

        static double SecondsToFrac(int seconds)
        {
            seconds = Settings.ClampSeconds(seconds);
            return (Math.Log(seconds) - LogMin) / (LogMax - LogMin);
        }

        static int FracToSeconds(double frac)
        {
            frac = Math.Min(1, Math.Max(0, frac));
            return (int)Math.Round(Math.Exp(LogMin + frac * (LogMax - LogMin)));
        }

        static string FormatDuration(int totalSeconds)
        {
            int h = totalSeconds / 3600, m = (totalSeconds % 3600) / 60, s = totalSeconds % 60;
            if (h > 0) return string.Format("{0}:{1:00}:{2:00}", h, m, s);
            return string.Format("{0}:{1:00}", m, s);
        }

        int GrayCount()
        {
            double frac = Math.Min(1, sw.Elapsed.TotalMilliseconds / durationMs);
            return (int)Math.Floor(frac * total);
        }

        void SetSeconds(int seconds)
        {
            seconds = Settings.ClampSeconds(seconds);
            if (seconds == cfg.Seconds) return;
            cfg.Seconds = seconds;
            Invalidate(rcApp);
        }

        void SetFromY(int y)
        {
            double frac = 1 - (y - rcApp.Top) / (double)Math.Max(1, rcApp.Height);
            SetSeconds(FracToSeconds(frac));
        }

        // Wheel and arrow keys: about 10% more or less time per step
        void Nudge(int dir)
        {
            int s = FracToSeconds(SecondsToFrac(cfg.Seconds) + dir * 0.01);
            if (s == cfg.Seconds) s += dir;
            SetSeconds(s);
        }

        void Start()
        {
            durationMs = cfg.Seconds * 1000.0;
            sw.Reset();
            sw.Start();
            lastGray = -1;
            beepsLeft = 0;
            state = State.Running;
            cfg.Save();
            UpdateTip();
            Invalidate();
        }

        void TogglePause()
        {
            if (state == State.Running) { sw.Stop(); state = State.Paused; }
            else if (state == State.Paused) { sw.Start(); state = State.Running; }
            UpdateTip();
            Invalidate(rcApp);
        }

        void DoReset()
        {
            sw.Reset();
            doneClock.Reset();
            beepsLeft = 0;
            state = State.Setup;
            UpdateTip();
            Invalidate();
        }

        void Finish()
        {
            sw.Stop();
            state = State.Done;
            doneClock.Reset();
            doneClock.Start();
            beepsLeft = cfg.Sound ? 3 : 0;
            nextBeepMs = 0;
            FlashTaskbar();
            UpdateTip();
            Invalidate();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (state == State.Running)
            {
                if (sw.Elapsed.TotalMilliseconds >= durationMs) { Finish(); return; }
                int gray = GrayCount();
                if (gray != lastGray) { lastGray = gray; Invalidate(rcApp); }
            }
            else if (state == State.Done)
            {
                long t = doneClock.ElapsedMilliseconds;
                if (beepsLeft > 0 && t >= nextBeepMs)
                {
                    SystemSounds.Exclamation.Play();
                    beepsLeft--;
                    nextBeepMs += 1300;
                }
                if (t < 2400) Invalidate(rcApp);
            }
        }

        void FlashTaskbar()
        {
            if (ContainsFocus) return;
            try
            {
                var fi = new FLASHWINFO();
                fi.cbSize = (uint)Marshal.SizeOf(typeof(FLASHWINFO));
                fi.hwnd = Handle;
                fi.dwFlags = 3 | 12; // FLASHW_ALL | FLASHW_TIMERNOFG
                FlashWindowEx(ref fi);
            }
            catch { }
        }

        // ── Keyboard ──

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Space:
                case Keys.Enter:
                    if (state == State.Setup) Start();
                    else if (state == State.Done) DoReset();
                    else TogglePause();
                    return true;
                case Keys.Escape:
                    if (fullScreen) ToggleFullScreen();
                    else if (state != State.Setup) DoReset();
                    return true;
                case Keys.F11:
                    ToggleFullScreen();
                    return true;
                case Keys.Up:
                case Keys.Down:
                    if (state == State.Setup) Nudge(keyData == Keys.Up ? 1 : -1);
                    return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ── Mouse ──

        Hit HitTest(Point p)
        {
            if (rcFull.Contains(p)) return Hit.Full;
            if (rcPin.Contains(p)) return Hit.Pin;
            if (rcAbout.Contains(p)) return Hit.About;
            if (!rcApp.Contains(p)) return Hit.None;
            Rectangle a, b, c, d;
            switch (state)
            {
                case State.Setup:
                    SetupRects(out a, out b, out c);
                    return c.Contains(p) ? Hit.Start : Hit.Field;
                case State.Running:
                case State.Paused:
                    return RcCancel.Contains(p) ? Hit.Cancel : Hit.Field;
                default:
                    DoneRects(out a, out b, out c, out d);
                    return d.Contains(p) ? Hit.Ok : Hit.None;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var h = HitTest(e.Location);
            if (h == Hit.Field && state == State.Setup)
            {
                dragging = true;
                Capture = true;
                SetFromY(e.Y);
                return;
            }
            if (h != Hit.None)
            {
                pressed = h;
                Invalidate();
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) { SetFromY(e.Y); return; }
            var h = HitTest(e.Location);
            if (h != hover)
            {
                if (h == Hit.Cancel || hover == Hit.Cancel) Invalidate(RcCancel);
                hover = h;
                UpdateTip();
                Cursor = h == Hit.Field && state == State.Setup ? Cursors.SizeNS : Cursors.Default;
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover == Hit.Cancel) Invalidate(RcCancel);
            hover = Hit.None;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                dragging = false;
                Capture = false;
                cfg.Save();
                return;
            }
            if (pressed == Hit.None) return;
            var p = pressed;
            pressed = Hit.None;
            Invalidate();
            if (HitTest(e.Location) != p) return;
            switch (p)
            {
                case Hit.Full: ToggleFullScreen(); break;
                case Hit.Pin: SetTopMost(!TopMost); break;
                case Hit.About: ShowAbout(); break;
                case Hit.Start: Start(); break;
                case Hit.Cancel:
                case Hit.Ok: DoReset(); break;
                case Hit.Field: TogglePause(); break;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (state == State.Setup && rcApp.Contains(e.Location)) Nudge(e.Delta > 0 ? 1 : -1);
        }

        void UpdateTip()
        {
            string t = "";
            switch (hover)
            {
                case Hit.Full: t = fullScreen ? "Exit full screen (F11)" : "Full screen (F11)"; break;
                case Hit.Pin: t = TopMost ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"; break;
                case Hit.About: t = "About Field"; break;
                case Hit.Start: t = "Start (Space)"; break;
                case Hit.Cancel: t = "Cancel (Esc)"; break;
                case Hit.Field:
                    t = state == State.Setup ? "Drag up or down, or scroll, to set the time" :
                        state == State.Paused ? "Click to resume (Space)" : "Click to pause (Space)";
                    break;
            }
            if (t == tipText) return;
            tipText = t;
            tip.SetToolTip(this, t);
        }

        // ── Painting ──

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Skin.Face);

            // Header: title, then full screen, pin and about buttons
            using (var b = new SolidBrush(Skin.Sh)) g.FillRectangle(b, 0, rcHeader.Bottom - 2 * T, rcHeader.Width, T);
            using (var b = new SolidBrush(Skin.Lt)) g.FillRectangle(b, 0, rcHeader.Bottom - T, rcHeader.Width, T);
            TextRenderer.DrawText(g, "FIELD", fTitle, new Rectangle(S(10), 0, Math.Max(0, rcFull.X - S(14)), rcHeader.Height - 2 * T), Skin.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            DrawButton(g, rcFull, fullScreen ? GlyphWindow : GlyphFull, fGlyph, Hit.Full, fullScreen);
            DrawButton(g, rcPin, TopMost ? GlyphPinned : GlyphPin, fGlyph, Hit.Pin, TopMost);
            DrawButton(g, rcAbout, "?", fAbout, Hit.About, false);

            // Screen
            Skin.Sunken(g, rcScreen, T);
            using (var b = new SolidBrush(Skin.Bg)) g.FillRectangle(b, rcApp);
            g.SetClip(rcApp);
            DrawDots(g);
            if (state == State.Paused) Veil(g, 0.55);
            if (state == State.Done && doneClock.ElapsedMilliseconds < 2200)
            {
                // Two slow "breaths" of the gray field, as on the web page
                double t = doneClock.ElapsedMilliseconds / 1000.0;
                Veil(g, 0.25 * (1 - Math.Cos(2 * Math.PI * t / 1.1)));
            }

            switch (state)
            {
                case State.Setup: DrawSetup(g); break;
                case State.Running:
                case State.Paused:
                    DrawCancel(g);
                    if (state == State.Paused) DrawPausedLabel(g);
                    break;
                case State.Done: DrawDone(g); break;
            }
            g.ResetClip();
        }

        void Veil(Graphics g, double alpha)
        {
            int a = (int)Math.Round(255 * Math.Max(0, Math.Min(1, alpha)));
            using (var b = new SolidBrush(Color.FromArgb(a, Skin.Bg))) g.FillRectangle(b, rcApp);
        }

        // Dots fill and empty from the bottom-right corner, row by row
        void DrawDots(Graphics g)
        {
            Bitmap first, rest;
            int n;
            switch (state)
            {
                case State.Setup:
                    first = spSetup; rest = spIdle;
                    n = (int)Math.Round(SecondsToFrac(cfg.Seconds) * total);
                    break;
                case State.Done:
                    first = spGray; rest = spGray; n = total;
                    break;
                default:
                    first = spGray; rest = spActive; n = GrayCount();
                    break;
            }
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            float cell = (DOT + GAP) * k;
            int size = spIdle.Width;
            for (int r = 0; r < rows; r++)
            {
                int y = (int)Math.Round(gy0 + r * cell) - spPad;
                for (int c = 0; c < cols; c++)
                {
                    int order = (rows - 1 - r) * cols + (cols - 1 - c);
                    int x = (int)Math.Round(gx0 + c * cell) - spPad;
                    g.DrawImage(order < n ? first : rest, new Rectangle(x, y, size, size));
                }
            }
            g.InterpolationMode = InterpolationMode.Default;
            g.PixelOffsetMode = PixelOffsetMode.Default;
        }

        void DrawSetup(Graphics g)
        {
            Rectangle lcd, hint, start;
            SetupRects(out lcd, out hint, out start);

            using (var b = new SolidBrush(Skin.Lcd)) g.FillRectangle(b, lcd);
            Skin.Sunken(g, lcd, T);
            TextRenderer.DrawText(g, FormatDuration(cfg.Seconds), fReadout, lcd, Skin.LcdDigit, Center);

            using (var b = new SolidBrush(Skin.Bg)) g.FillRectangle(b, Rectangle.Inflate(hint, S(4), S(3)));   // keeps the hint readable over lit dots
            TextRenderer.DrawText(g, HintText, fHint, hint, Skin.Text, Center);

            bool down = pressed == Hit.Start;
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, start);
            if (down) Skin.Sunken(g, start, T); else Skin.Raised(g, start, T);
            var r = down ? new Rectangle(start.X + T, start.Y + T, start.Width, start.Height) : start;
            var ts = Measure("Start", fStart);
            int tri = S(9), gap = S(7);
            int x = r.X + (r.Width - (tri + gap + ts.Width)) / 2, cy = r.Y + r.Height / 2;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Skin.Text))
                g.FillPolygon(b, new[] { new PointF(x, cy - tri * 0.55f), new PointF(x + tri, cy), new PointF(x, cy + tri * 0.55f) });
            g.SmoothingMode = SmoothingMode.Default;
            TextRenderer.DrawText(g, "Start", fStart, new Rectangle(x + tri + gap, r.Y, ts.Width + S(2), r.Height), Skin.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }

        void DrawCancel(Graphics g)
        {
            var r = RcCancel;
            bool down = pressed == Hit.Cancel, hot = hover == Hit.Cancel;
            using (var b = new SolidBrush(hot ? Skin.CloseHover : Skin.Face)) g.FillRectangle(b, r);
            if (down) Skin.Sunken(g, r, T); else Skin.Raised(g, r, T);
            TextRenderer.DrawText(g, "×", fCancel, r, hot ? Color.White : Skin.Text, Center);
        }

        void DrawPausedLabel(Graphics g)
        {
            const string text = "Paused — click to resume";
            var s = Measure(text, fPaused);
            int w = s.Width + S(32), h = s.Height + S(14);
            var r = new Rectangle(rcApp.X + (rcApp.Width - w) / 2, rcApp.Y + (rcApp.Height - h) / 2, w, h);
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, r);
            Skin.Raised(g, r, T);
            TextRenderer.DrawText(g, text, fPaused, r, Skin.Text, Center);
        }

        void DrawDone(Graphics g)
        {
            Rectangle box, title, label, ok;
            DoneRects(out box, out title, out label, out ok);
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, box);
            Skin.Raised(g, box, T);
            using (var b = new LinearGradientBrush(new Rectangle(title.X - 1, title.Y, title.Width + 2, title.Height),
                                                   Skin.TitleFrom, Skin.TitleTo, LinearGradientMode.Horizontal))
                g.FillRectangle(b, title);
            TextRenderer.DrawText(g, "Field Timer", fAlertTitle, new Rectangle(title.X + S(8), title.Y, title.Width - S(16), title.Height), Color.White,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, "Time’s up!", fDone, new Rectangle(label.X, label.Y + S(8), label.Width, label.Height - S(8)), Skin.Text, Center);

            bool down = pressed == Hit.Ok;
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, ok);
            if (down) Skin.Sunken(g, ok, T); else Skin.Raised(g, ok, T);
            var r = down ? new Rectangle(ok.X + T, ok.Y + T, ok.Width, ok.Height) : ok;
            TextRenderer.DrawText(g, "OK", fOk, r, Skin.Text, Center);
        }

        void DrawButton(Graphics g, Rectangle r, string text, Font f, Hit h, bool active)
        {
            bool down = active || pressed == h;
            using (var b = new SolidBrush(Skin.Face)) g.FillRectangle(b, r);
            if (down) Skin.Sunken(g, r, T); else Skin.Raised(g, r, T);
            var tr = down ? new Rectangle(r.X + T, r.Y + T, r.Width, r.Height) : r;
            TextRenderer.DrawText(g, text, f, tr, Skin.Text, Center);
        }

        // ── Dot sprites, drawn once ──

        void MakeSprites()
        {
            int d = Math.Max(3, (int)Math.Round(DOT * k));
            spPad = (int)Math.Ceiling(4 * k);
            int size = d + 2 * spPad;
            float pw = Math.Max(1f, k);
            spIdle = Sprite(size, g =>
            {
                using (var pen = new Pen(Skin.DotIdle, pw))
                    g.DrawEllipse(pen, spPad + pw / 2, spPad + pw / 2, d - pw, d - pw);
            });
            spSetup = Sprite(size, g => Disc(g, spPad, d, Skin.SetupFill));
            spActive = Sprite(size, g =>
            {
                // Soft glow, like box-shadow: 0 0 5px rgba(244,241,234,0.30)
                int[] alpha = { 14, 28, 46 };
                for (int i = 0; i < 3; i++)
                {
                    float grow = (3 - i) * 1.2f * k;
                    using (var b = new SolidBrush(Color.FromArgb(alpha[i], Skin.Active)))
                        g.FillEllipse(b, spPad - grow, spPad - grow, d + 2 * grow, d + 2 * grow);
                }
                Disc(g, spPad, d, Skin.Active);
            });
            spGray = Sprite(size, g => Disc(g, spPad, d, Skin.Elapsed));
        }

        static void Disc(Graphics g, int at, int d, Color c)
        {
            using (var b = new SolidBrush(c)) g.FillEllipse(b, at, at, d, d);
        }

        static Bitmap Sprite(int size, Action<Graphics> draw)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                draw(g);
            }
            return bmp;
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
            Text = "About Field";
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

            Controls.Add(new Label { Text = "Field", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
            Controls.Add(new Label { Text = "Version 1.0  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Free software, provided \"as is\", without warranty of any kind. Don't rely on it " +
                       "where an interrupted countdown could cause harm or loss.\r\n" +
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

    // App icon: a small field of dots, the bottom ones already gray
    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.03f;
            var body = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            Skin.FillRound(g, body, s * 0.2f, Skin.Face);
            float inset = s * 0.09f;
            var inner = new RectangleF(body.X + inset, body.Y + inset, body.Width - 2 * inset, body.Height - 2 * inset);
            Skin.FillRound(g, inner, s * 0.12f, Skin.Bg);

            const int n = 4;
            float cell = inner.Width / n, d = cell * 0.56f;
            var gray = Color.FromArgb(0x4a, 0x4a, 0x50);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    int order = (n - 1 - r) * n + (n - 1 - c);
                    float cx = inner.X + (c + 0.5f) * cell, cy = inner.Y + (r + 0.5f) * cell;
                    using (var b = new SolidBrush(order < 6 ? gray : Skin.Active))
                        g.FillEllipse(b, cx - d / 2, cy - d / 2, d, d);
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
