// Field for Windows: the Field dot timer (tools/teaching/field-timer.html) as a desktop app.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
//
// Field is a tribute to Untime, the iPhone and iPad timer that Perceptor, the Dutch design studio of
// Bastiaan Terhorst and Casper Schipper, made in 2012 (https://perceptor.nl/everything/untime.html).
// The idea of time as a field of dots, the dot-matrix numbers and the glowing end are theirs.
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
[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]

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

    // ── Look (same palette and glyphs as the web page) ──
    static class Skin
    {
        static readonly double[] Dark = { 0x24, 0x24, 0x24 }, Lit = { 0x8c, 0x8c, 0x8c }, LitPaused = { 0x4a, 0x4a, 0x4a };
        static readonly double[] Halo = { 0x0e, 0x0e, 0x0e }, Glyph = { 0xff, 0xff, 0xff }, Unit = { 0xe6, 0xe6, 0xe6 };
        // The glow at the end, from the centre (0) to the edge (1)
        static readonly double[] WarmAt0 = { 0, 0.3, 0.5, 0.7, 0.86, 1 };
        static readonly double[][] WarmRgb = {
            new double[] { 255, 255, 255 }, new double[] { 255, 243, 176 }, new double[] { 255, 210, 90 },
            new double[] { 255, 143, 42 }, new double[] { 168, 64, 26 }, new double[] { 42, 18, 8 } };
        public const int PartialSteps = 16, WarmSteps = 48;

        // Colour slots: 0 dark, 1 lit, 2 lit paused, 3 halo, 4 glyph, 5 unit, then partial and warm ramps
        public const int PPartial = 6, PPartialPaused = PPartial + PartialSteps, PWarm = PPartialPaused + PartialSteps;
        public static readonly Color[] Palette = BuildPalette();

        static double[] Mix(double[] a, double[] b, double t)
        {
            return new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t };
        }

        static double[] Warm(double t)
        {
            t = Math.Min(1, Math.Max(0, t));
            for (int i = 1; i < WarmAt0.Length; i++)
                if (t <= WarmAt0[i])
                    return Mix(WarmRgb[i - 1], WarmRgb[i], (t - WarmAt0[i - 1]) / (WarmAt0[i] - WarmAt0[i - 1]));
            return WarmRgb[WarmRgb.Length - 1];
        }

        static Color ToColor(double[] c)
        {
            return Color.FromArgb((int)Math.Round(c[0]), (int)Math.Round(c[1]), (int)Math.Round(c[2]));
        }

        static Color[] BuildPalette()
        {
            var p = new List<Color> { ToColor(Dark), ToColor(Lit), ToColor(LitPaused), ToColor(Halo), ToColor(Glyph), ToColor(Unit) };
            for (int i = 0; i < PartialSteps; i++) p.Add(ToColor(Mix(Dark, Lit, (i + 0.5) / PartialSteps)));
            for (int i = 0; i < PartialSteps; i++) p.Add(ToColor(Mix(Dark, LitPaused, (i + 0.5) / PartialSteps)));
            for (int i = 0; i < WarmSteps; i++) p.Add(ToColor(Warm(i / (double)(WarmSteps - 1))));
            return p.ToArray();
        }

        // Dot-matrix glyphs: digits 5×7, letters 3×5 (small caps)
        public static readonly Dictionary<char, string[]> Big = new Dictionary<char, string[]>
        {
            { '0', new[] { "01110", "10001", "10001", "10001", "10001", "10001", "01110" } },
            { '1', new[] { "00100", "01100", "00100", "00100", "00100", "00100", "01110" } },
            { '2', new[] { "01110", "10001", "00001", "00010", "00100", "01000", "11111" } },
            { '3', new[] { "11110", "00001", "00001", "01110", "00001", "00001", "11110" } },
            { '4', new[] { "00010", "00110", "01010", "10010", "11111", "00010", "00010" } },
            { '5', new[] { "11111", "10000", "11110", "00001", "00001", "10001", "01110" } },
            { '6', new[] { "00110", "01000", "10000", "11110", "10001", "10001", "01110" } },
            { '7', new[] { "11111", "00001", "00010", "00100", "01000", "01000", "01000" } },
            { '8', new[] { "01110", "10001", "10001", "01110", "10001", "10001", "01110" } },
            { '9', new[] { "01110", "10001", "10001", "01111", "00001", "00010", "01100" } },
            { ':', new[] { "0", "0", "1", "0", "1", "0", "0" } },
        };
        public static readonly Dictionary<char, string[]> Small = new Dictionary<char, string[]>
        {
            { 'A', new[] { "010", "101", "111", "101", "101" } }, { 'C', new[] { "011", "100", "100", "100", "011" } },
            { 'D', new[] { "110", "101", "101", "101", "110" } }, { 'E', new[] { "111", "100", "110", "100", "111" } },
            { 'H', new[] { "101", "101", "111", "101", "101" } }, { 'I', new[] { "111", "010", "010", "010", "111" } },
            { 'M', new[] { "10001", "11011", "10101", "10001", "10001" } }, { 'N', new[] { "1001", "1101", "1011", "1001", "1001" } },
            { 'P', new[] { "110", "101", "110", "100", "100" } }, { 'R', new[] { "110", "101", "110", "101", "101" } },
            { 'S', new[] { "011", "100", "010", "001", "110" } }, { 'T', new[] { "111", "010", "010", "010", "010" } },
            { 'U', new[] { "101", "101", "101", "101", "111" } },
        };

        public static int TextWidth(string s, Dictionary<char, string[]> font)
        {
            int w = 0;
            for (int i = 0; i < s.Length; i++)
            {
                string[] g;
                if (font.TryGetValue(s[i], out g)) w += g[0].Length + (i > 0 ? 1 : 0);
            }
            return w;
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

        // Seconds below a minute, whole minutes below an hour, then 5-minute steps
        public static int Snap(int v)
        {
            int s = ClampSeconds(v);
            if (s < 60) return s;
            if (s < 3600) return ClampSeconds((int)Math.Round(s / 60.0, MidpointRounding.AwayFromZero) * 60);
            return ClampSeconds((int)Math.Round(s / 300.0, MidpointRounding.AwayFromZero) * 300);
        }

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
                        case "Seconds": if (isInt) s.Seconds = Snap(n); break;
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

        class Btn
        {
            public string Id, Label;
            public Rectangle R;
        }

        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);
        [DllImport("user32.dll")] static extern bool SystemParametersInfo(uint action, uint param, ref bool value, uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

        // Same geometry as the web page, in 96-dpi pixels
        const float DOT = 8, CELL = 14, PAD = 14;
        const double GlowMs = 60000;   // how long the glow at the end keeps moving
        static readonly double LogMin = Math.Log(Settings.MinSeconds), LogMax = Math.Log(Settings.MaxSeconds);

        readonly Settings cfg = Settings.Load();
        readonly float k;
        readonly bool reduceMotion;
        State state = State.Setup;
        readonly Stopwatch sw = new Stopwatch();         // runs while counting down, stops while paused
        readonly Stopwatch doneClock = new Stopwatch();
        double durationMs, startLit;
        string lastFrame = "";
        int doneFrames;
        bool dragging;
        string pressed, hover;
        int beepsLeft;
        long nextBeepMs;
        bool fullScreen, wasMaximized;
        Rectangle windowedBounds;

        readonly Timer tick;
        readonly ToolTip tip;
        string tipText = "";
        ContextMenuStrip menu;
        ToolStripMenuItem miTop, miSound, miFull;

        Bitmap[] sprites;
        int spSize, spPad;
        int cols = 1, rows = 1, total = 1;
        float gx0, gy0, px = 3;
        List<Btn> buttons = new List<Btn>();

        public FieldForm()
        {
            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            try
            {
                bool anim = true;
                if (SystemParametersInfo(0x1042, 0, ref anim, 0)) reduceMotion = !anim;   // SPI_GETCLIENTAREAANIMATION
            }
            catch { }
            Text = "Field";
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.Black;
            KeyPreview = true;
            StartPosition = FormStartPosition.Manual;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            MinimumSize = SizeFromClientSize(new Size(S(260), S(240)));
            cfg.Seconds = Settings.Snap(cfg.Seconds);
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

        // ── Window ──

        // A dark title bar, so the window is black all over (Windows 10 2004 and later)
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4);
            }
            catch { }
        }

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
            menu = new ContextMenuStrip();
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
            float dot = DOT * k, cell = CELL * k, pad = PAD * k;
            cols = Math.Max(1, (int)Math.Floor((W - 2 * pad - dot) / cell) + 1);
            rows = Math.Max(1, (int)Math.Floor((H - 2 * pad - dot) / cell) + 1);
            total = cols * rows;
            gx0 = (W - (cols - 1) * cell) / 2f;
            gy0 = (H - (rows - 1) * cell) / 2f;
            px = k * Math.Max(2, Math.Min(6, (float)Math.Round(Math.Min(W, H) / k / 150f)));
            if (state == State.Running || state == State.Paused) startLit = LitFor(durationMs / 1000.0);
            lastFrame = "";
            LayoutButtons();
        }

        Btn Card(string id, string label, float cx, float cy)
        {
            int w = (int)Math.Round((Skin.TextWidth(label, Skin.Small) + 8) * px), h = (int)Math.Round(11 * px);
            return new Btn { Id = id, Label = label, R = new Rectangle((int)Math.Round(cx - w / 2f), (int)Math.Round(cy - h / 2f), w, h) };
        }

        void LayoutButtons()
        {
            float W = ClientSize.Width, cy = (float)Math.Round(ClientSize.Height * 0.74);
            var b = new List<Btn>();
            switch (state)
            {
                case State.Setup: b.Add(Card("start", "START", W / 2, cy)); break;
                case State.Running: b.Add(Card("pause", "PAUSE", W / 2, cy)); break;
                case State.Paused:
                    var a = Card("resume", "RESUME", 0, cy);
                    var r = Card("reset", "RESET", 0, cy);
                    int gap = (int)Math.Round(4 * px);
                    int x = (int)Math.Round(W / 2 - (a.R.Width + gap + r.R.Width) / 2f);
                    a.R.X = x;
                    r.R.X = x + a.R.Width + gap;
                    b.Add(a);
                    b.Add(r);
                    break;
                default: b.Add(Card("dismiss", "DISMISS", W / 2, cy)); break;
            }
            int mw = (int)Math.Round(11 * px), mh = (int)Math.Round(7 * px);
            b.Add(new Btn { Id = "menu", Label = "", R = new Rectangle((int)Math.Round(W / 2 - mw / 2f), (int)Math.Round(cy + 11 * px), mw, mh) });
            buttons = b;
        }

        Btn ButtonAt(Point p)
        {
            foreach (var b in buttons) if (b.R.Contains(p)) return b;
            return null;
        }

        // ── Time ──

        static double SecondsToFrac(double seconds)
        {
            seconds = Math.Min(Settings.MaxSeconds, Math.Max(Settings.MinSeconds, seconds));
            return (Math.Log(seconds) - LogMin) / (LogMax - LogMin);
        }

        static int FracToSeconds(double frac)
        {
            frac = Math.Min(1, Math.Max(0, frac));
            return Settings.Snap((int)Math.Round(Math.Exp(LogMin + frac * (LogMax - LogMin))));
        }

        // What the big numbers say: seconds, whole minutes, or h:mm
        static void Display(double seconds, out string text, out string unit)
        {
            int s = Math.Max(0, (int)Math.Ceiling(seconds));
            if (s < 60) { text = s.ToString(); unit = "SEC"; return; }
            int m = (s + 59) / 60;
            if (m < 60) { text = m.ToString(); unit = "MIN"; return; }
            text = string.Format("{0}:{1:00}", m / 60, m % 60);
            unit = "H";
        }

        static string Spoken(int seconds)
        {
            string text, unit;
            Display(seconds, out text, out unit);
            return text + (unit == "SEC" ? " seconds" : unit == "MIN" ? " minutes" : " hours");
        }

        double LitFor(double seconds) { return Math.Round(SecondsToFrac(seconds) * total); }

        double RemainingMs { get { return Math.Max(0, durationMs - sw.Elapsed.TotalMilliseconds); } }

        void SetSeconds(int seconds)
        {
            seconds = Settings.Snap(seconds);
            if (seconds == cfg.Seconds) return;
            cfg.Seconds = seconds;
            UpdateTip();
            Invalidate();
        }

        // Drag: the higher the pointer, the less time; the lit dots end where you point
        void SetFromY(int y)
        {
            float cell = CELL * k;
            SetSeconds(FracToSeconds((y - (gy0 - cell / 2)) / (rows * cell)));
        }

        void Nudge(int dir)
        {
            int s = FracToSeconds(SecondsToFrac(cfg.Seconds) + dir * 0.012);
            if (s == cfg.Seconds)
            {
                int step = cfg.Seconds < 60 ? 1 : cfg.Seconds < 3600 ? 60 : 300;
                s = cfg.Seconds + dir * step;
            }
            SetSeconds(s);
        }

        void StateChanged()
        {
            pressed = null;
            lastFrame = "";
            LayoutButtons();
            UpdateTip();
            Invalidate();
        }

        void Start()
        {
            durationMs = cfg.Seconds * 1000.0;
            startLit = LitFor(cfg.Seconds);
            sw.Reset();
            sw.Start();
            beepsLeft = 0;
            state = State.Running;
            cfg.Save();
            StateChanged();
        }

        void TogglePause()
        {
            if (state == State.Running) { sw.Stop(); state = State.Paused; }
            else if (state == State.Paused) { sw.Start(); state = State.Running; }
            else return;
            StateChanged();
        }

        void DoReset()
        {
            sw.Reset();
            doneClock.Reset();
            beepsLeft = 0;
            state = State.Setup;
            StateChanged();
        }

        void Finish()
        {
            sw.Stop();
            state = State.Done;
            doneClock.Reset();
            doneClock.Start();
            doneFrames = 0;
            beepsLeft = cfg.Sound ? 3 : 0;
            nextBeepMs = 0;
            FlashTaskbar();
            StateChanged();
        }

        void Press(string id)
        {
            switch (id)
            {
                case "start": Start(); break;
                case "pause": case "resume": TogglePause(); break;
                case "reset": case "dismiss": DoReset(); break;
                case "menu":
                    var b = buttons[buttons.Count - 1];
                    menu.Show(this, new Point(b.R.X + b.R.Width / 2 - S(60), b.R.Bottom + S(4)));
                    break;
            }
        }

        // What a countdown frame shows; the window repaints only when this changes
        string FrameKey()
        {
            double lit = startLit * RemainingMs / Math.Max(1, durationMs);
            int full = (int)Math.Floor(lit);
            int step = (int)Math.Floor((lit - full) * Skin.PartialSteps);
            string text, unit;
            Display(RemainingMs / 1000.0, out text, out unit);
            return full + "/" + step + "/" + text + unit;
        }

        void OnTick(object sender, EventArgs e)
        {
            if (state == State.Running)
            {
                if (sw.Elapsed.TotalMilliseconds >= durationMs) { Finish(); return; }
                string key = FrameKey();
                if (key != lastFrame) { lastFrame = key; Invalidate(); }
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
                // About 16 frames a second, for the first minute
                if (!reduceMotion && t < GlowMs && (++doneFrames & 1) == 0) Invalidate();
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

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var b = ButtonAt(e.Location);
            if (b != null)
            {
                pressed = b.Id;
                Invalidate(b.R);
                return;
            }
            if (state == State.Setup)
            {
                dragging = true;
                Capture = true;
                SetFromY(e.Y);
            }
            else if (state == State.Running || state == State.Paused) pressed = "field";
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) { SetFromY(e.Y); return; }
            var b = ButtonAt(e.Location);
            string h = b == null ? null : b.Id;
            Cursor = b != null ? Cursors.Hand : state == State.Setup ? Cursors.SizeNS : Cursors.Default;
            if (h != hover)
            {
                hover = h;
                UpdateTip();
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover == null) return;
            hover = null;
            Invalidate();
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
            if (pressed == null) return;
            string p = pressed;
            pressed = null;
            Invalidate();
            var b = ButtonAt(e.Location);
            if (p == "field") { if (b == null) TogglePause(); return; }
            if (b != null && b.Id == p) Press(p);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (state == State.Setup) Nudge(e.Delta > 0 ? 1 : -1);
        }

        void UpdateTip()
        {
            string t;
            switch (hover)
            {
                case "start": t = "Start (Space)"; break;
                case "pause": t = "Pause (Space)"; break;
                case "resume": t = "Resume (Space)"; break;
                case "reset": case "dismiss": t = "Reset (Esc)"; break;
                case "menu": t = "Options and About"; break;
                default:
                    t = state == State.Setup ? "Drag down for more time, or scroll (now " + Spoken(cfg.Seconds) + ")" :
                        state == State.Paused ? "Click to resume (Space)" :
                        state == State.Running ? "Click to pause (Space)" : "Time's up";
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
            g.Clear(Color.Black);

            var slot = new byte[total];
            var over = new byte[total];
            bool paused = state == State.Paused;
            float cell = CELL * k;

            if (state == State.Setup)
            {
                int n = (int)Math.Min(total, LitFor(cfg.Seconds));
                for (int i = 0; i < n; i++) slot[i] = 1;
                string text, unit;
                Display(cfg.Seconds, out text, out unit);
                Stamp(over, text, unit);
            }
            else if (state == State.Running || paused)
            {
                double remaining = RemainingMs;
                double lit = startLit * remaining / Math.Max(1, durationMs);
                int full = (int)Math.Min(total, Math.Floor(lit));
                for (int i = 0; i < full; i++) slot[i] = (byte)(paused ? 2 : 1);
                if (full < total && lit > full)
                {
                    int step = Math.Min(Skin.PartialSteps - 1, (int)Math.Floor((lit - full) * Skin.PartialSteps));
                    slot[full] = (byte)((paused ? Skin.PPartialPaused : Skin.PPartial) + step);
                }
                string text, unit;
                Display(remaining / 1000.0, out text, out unit);
                Stamp(over, text, unit);
            }
            else
            {
                // The glow: warm colours spreading from the centre, breathing, with a little sparkle
                double t = reduceMotion ? 3 : Math.Min(doneClock.ElapsedMilliseconds, GlowMs) / 1000.0;
                double grow = Math.Min(1, 0.2 + t / 1.4);
                double breathe = 1 + 0.08 * Math.Sin(t * 2 * Math.PI / 2.4);
                double R = 0.62 * Math.Max(ClientSize.Width, ClientSize.Height) * grow * breathe;
                double cx = ClientSize.Width / 2.0, cy = ClientSize.Height * 0.42;
                for (int r = 0; r < rows; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        int i = r * cols + c;
                        double dx = gx0 + c * cell - cx, dy = gy0 + r * cell - cy;
                        double tw = reduceMotion ? 0 : 0.07 * Math.Sin(Hash(i) * 6.283 + t * (1.5 + 2 * Hash(i + 7)));
                        double v = Math.Sqrt(dx * dx + dy * dy) / R + tw;
                        slot[i] = (byte)(Skin.PWarm + Math.Min(Skin.WarmSteps - 1, Math.Max(0, (int)Math.Round(v * (Skin.WarmSteps - 1)))));
                    }
                Stamp(over, "0", "");
            }

            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            var clip = e.ClipRectangle;
            for (int i = 0; i < total; i++)
            {
                int o = over[i];
                int p = o == 2 ? 4 : o == 3 ? 5 : o == 1 ? 3 : slot[i];
                int x = (int)Math.Round(gx0 + (i % cols) * cell) - spSize / 2;
                int y = (int)Math.Round(gy0 + (i / cols) * cell) - spSize / 2;
                if (x > clip.Right || y > clip.Bottom || x + spSize < clip.X || y + spSize < clip.Y) continue;
                g.DrawImage(sprites[p], new Rectangle(x, y, spSize, spSize));
            }
            g.InterpolationMode = InterpolationMode.Default;
            g.PixelOffsetMode = PixelOffsetMode.Default;

            g.SmoothingMode = SmoothingMode.AntiAlias;
            foreach (var b in buttons) DrawButton(g, b);
            g.SmoothingMode = SmoothingMode.Default;
        }

        static double Hash(int i)
        {
            double x = Math.Sin(i * 12.9898) * 43758.5453;
            return x - Math.Floor(x);
        }

        // Marks which dots carry the numbers (2), the unit (3) or the dark ring around the numbers (1)
        void Stamp(byte[] over, string text, string unit)
        {
            int digits = Skin.TextWidth(text, Skin.Big);
            int s = Math.Max(1, Math.Min(Math.Min((cols - 4) / Math.Max(1, digits), (int)(rows * 0.5) / 7),
                                         (int)Math.Round(Math.Min(cols / 34.0, rows / 20.0))));
            if (digits * s > cols - 2) return;
            // Keep clear of the buttons: drop the unit first, then make the numbers smaller
            float cell = CELL * k, buttonTop = ClientSize.Height * 0.74f - 5.5f * px;
            int limit = Math.Min(rows - 1, (int)Math.Floor((buttonTop - gy0 - cell) / cell) + 1);
            string wanted = unit;
            int u, blockH, top;
            while (true)
            {
                u = Math.Max(1, (int)Math.Round(s * 0.6));
                blockH = 7 * s + (unit.Length == 0 ? 0 : 2 * s + 5 * u);
                top = Math.Max(1, Math.Min((int)Math.Round(rows * 0.40 - blockH / 2.0), limit - blockH));
                if (top + blockH <= limit) break;
                if (unit.Length > 0) unit = "";
                else if (s > 1) { s--; unit = wanted; }
                else return;
            }
            int x = (cols - digits * s) / 2;
            foreach (char ch in text)
            {
                string[] g;
                if (!Skin.Big.TryGetValue(ch, out g)) continue;
                PaintGlyph(over, g, x, top, s, 2);
                x += (g[0].Length + 1) * s;
            }
            if (unit.Length == 0) return;
            x = (cols - Skin.TextWidth(unit, Skin.Small) * u) / 2;
            int uy = top + 7 * s + 2 * s;
            foreach (char ch in unit)
            {
                string[] g;
                if (!Skin.Small.TryGetValue(ch, out g)) continue;
                PaintGlyph(over, g, x, uy, u, 3);
                x += (g[0].Length + 1) * u;
            }
        }

        void PaintGlyph(byte[] over, string[] glyph, int col, int row, int s, byte value)
        {
            for (int r = 0; r < glyph.Length; r++)
                for (int c = 0; c < glyph[r].Length; c++)
                {
                    if (glyph[r][c] != '1') continue;
                    for (int dy = -1; dy <= s; dy++)
                        for (int dx = -1; dx <= s; dx++)
                        {
                            int gr = row + r * s + dy, gc = col + c * s + dx;
                            if (gr < 0 || gc < 0 || gr >= rows || gc >= cols) continue;
                            int i = gr * cols + gc;
                            if (dy >= 0 && dx >= 0 && dy < s && dx < s) over[i] = value;
                            else if (over[i] == 0 && value == 2) over[i] = 1;
                        }
                }
        }

        // White cards with dot letters, and the ··· menu button
        void DrawButton(Graphics g, Btn b)
        {
            bool hot = hover == b.Id, down = hot && pressed == b.Id;
            if (b.Id == "menu")
            {
                Skin.FillRound(g, b.R, px * 1.5f, Color.Black);
                using (var br = new SolidBrush(hot ? Color.FromArgb(0xe0, 0xe0, 0xe0) : Color.FromArgb(0x80, 0x80, 0x80)))
                    for (int i = -1; i <= 1; i++)
                    {
                        float cx = b.R.X + b.R.Width / 2f + i * 2.6f * px, cy = b.R.Y + b.R.Height / 2f, rr = px * 0.65f;
                        g.FillEllipse(br, cx - rr, cy - rr, 2 * rr, 2 * rr);
                    }
                return;
            }
            var r = b.R;
            if (down) r.Offset(0, 1);
            Skin.FillRound(g, r, px, down ? Color.FromArgb(0xc8, 0xc8, 0xc8) : hot ? Color.White : Color.FromArgb(0xec, 0xec, 0xec));
            float x = r.X + 4 * px, y = r.Y + 3 * px, dr = px * 0.42f;
            using (var br = new SolidBrush(Color.FromArgb(0x11, 0x11, 0x11)))
                foreach (char ch in b.Label)
                {
                    string[] gl;
                    if (!Skin.Small.TryGetValue(ch, out gl)) continue;
                    for (int row = 0; row < gl.Length; row++)
                        for (int col = 0; col < gl[row].Length; col++)
                        {
                            if (gl[row][col] != '1') continue;
                            float cx = x + col * px + px / 2, cy = y + row * px + px / 2;
                            g.FillEllipse(br, cx - dr, cy - dr, 2 * dr, 2 * dr);
                        }
                    x += (gl[0].Length + 1) * px;
                }
        }

        // ── Dot sprites, one per colour, drawn once ──

        void MakeSprites()
        {
            int d = Math.Max(3, (int)Math.Round(DOT * k));
            spPad = 1;
            spSize = d + 2 * spPad;
            sprites = new Bitmap[Skin.Palette.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                var bmp = new Bitmap(spSize, spSize, PixelFormat.Format32bppPArgb);
                using (var g = Graphics.FromImage(bmp))
                using (var b = new SolidBrush(Skin.Palette[i]))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    g.FillEllipse(b, spPad, spPad, d, d);
                }
                sprites[i] = bmp;
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

        const string UntimeUrl = "https://perceptor.nl/everything/untime.html";

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
            ClientSize = new Size(420, 500);

            var icon = new PictureBox { Location = new Point(16, 16), Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom };
            var bmp = new Bitmap(96, 96, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) IconArt.Draw(g, 96);
            icon.Image = bmp;
            Controls.Add(icon);

            Controls.Add(new Label { Text = "Field", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
            Controls.Add(new Label { Text = "Version 2.0  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Field is a tribute to Untime, the timer app made in 2012 by Perceptor, the Dutch design studio of " +
                       "Bastiaan Terhorst and Casper Schipper. Showing time as a quiet field of dots was their idea, and the " +
                       "numbers drawn with dots and the glow at the end come from their app. Thank you for a wonderful idea.",
                Location = new Point(16, 80), Size = new Size(390, 84)
            });
            var link = new LinkLabel { Text = "Untime on perceptor.nl", Location = new Point(16, 166), AutoSize = true };
            link.LinkClicked += delegate
            {
                try { Process.Start(UntimeUrl); } catch { }
            };
            Controls.Add(link);
            Controls.Add(new Label
            {
                Text = "Free software, provided \"as is\", without warranty of any kind. Don't rely on it " +
                       "where an interrupted countdown could cause harm or loss.\r\n" +
                       "This app collects no personal data and never connects to the internet.",
                Location = new Point(16, 196), Size = new Size(390, 54)
            });

            Controls.Add(new TextBox
            {
                Text = License, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 256), Size = new Size(388, 190), TabStop = false,
                BackColor = SystemColors.Window
            });

            var ok = new Button { Text = "OK", Location = new Point(324, 458), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
            ResumeLayout(false);
        }
    }

    // App icon: a small field of dots on black, the first ones lit
    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.03f;
            var body = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            Skin.FillRound(g, body, s * 0.2f, Color.FromArgb(0x0a, 0x0a, 0x0a));
            float inset = s * 0.12f;
            var inner = new RectangleF(body.X + inset, body.Y + inset, body.Width - 2 * inset, body.Height - 2 * inset);

            const int n = 4;
            float cell = inner.Width / n, d = cell * 0.56f;
            var dim = Color.FromArgb(0x3a, 0x3a, 0x3a);
            var lit = Color.FromArgb(0xf4, 0xf4, 0xf4);
            for (int r = 0; r < n; r++)
                for (int c = 0; c < n; c++)
                {
                    float cx = inner.X + (c + 0.5f) * cell, cy = inner.Y + (r + 0.5f) * cell;
                    using (var b = new SolidBrush(r * n + c < 6 ? lit : dim))
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
