// Countdown Timer: a small always-on-top-capable countdown for Windows.
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

[assembly: AssemblyTitle("Countdown Timer")]
[assembly: AssemblyProduct("Countdown Timer")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace CountdownTimer
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
            Application.Run(new TimerForm());
        }
    }

    static class Palette
    {
        public static readonly Color Bg = Color.FromArgb(30, 31, 36);
        public static readonly Color Border = Color.FromArgb(62, 64, 72);
        public static readonly Color Dim = Color.FromArgb(150, 154, 162);
        public static readonly Color Track = Color.FromArgb(52, 54, 62);
        public static readonly Color Btn = Color.FromArgb(44, 46, 53);
        public static readonly Color BtnHover = Color.FromArgb(62, 65, 75);
        public static readonly Color SegHover = Color.FromArgb(40, 42, 49);
        public static readonly Color SegSelected = Color.FromArgb(52, 55, 64);
        public static readonly Color CloseHover = Color.FromArgb(196, 43, 28);
        public static readonly Color Text = Color.FromArgb(235, 236, 240);
        // Pure, saturated signal colors (no amber/sepia).
        public static readonly Color Green = Color.FromArgb(34, 197, 94);
        public static readonly Color Yellow = Color.FromArgb(255, 230, 0);
        public static readonly Color Red = Color.FromArgb(255, 45, 45);

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
        public int Hours = 0, Minutes = 5, Seconds = 0;
        public int YellowPct = 30, RedPct = 10;
        public bool TopMost = true, Sound = true;
        public int X = int.MinValue, Y = int.MinValue;

        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CountdownTimer"); }
        }
        static string FilePath { get { return Path.Combine(Dir, "settings.ini"); } }

        public int this[int seg]
        {
            get { return seg == 0 ? Hours : seg == 1 ? Minutes : Seconds; }
            set
            {
                int v = Clamp(value, 0, seg == 0 ? 99 : 59);
                if (seg == 0) Hours = v; else if (seg == 1) Minutes = v; else Seconds = v;
            }
        }

        public TimeSpan Duration { get { return new TimeSpan(Hours, Minutes, Seconds); } }

        static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (!File.Exists(FilePath)) return s;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int i = line.IndexOf('=');
                    if (i < 0) continue;
                    string key = line.Substring(0, i).Trim(), val = line.Substring(i + 1).Trim();
                    int n; bool b;
                    bool isInt = int.TryParse(val, out n), isBool = bool.TryParse(val, out b);
                    switch (key)
                    {
                        case "Hours": if (isInt) s.Hours = Clamp(n, 0, 99); break;
                        case "Minutes": if (isInt) s.Minutes = Clamp(n, 0, 59); break;
                        case "Seconds": if (isInt) s.Seconds = Clamp(n, 0, 59); break;
                        case "YellowPct": if (isInt) s.YellowPct = Clamp(n, 1, 100); break;
                        case "RedPct": if (isInt) s.RedPct = Clamp(n, 0, 99); break;
                        case "TopMost": if (isBool) s.TopMost = b; break;
                        case "Sound": if (isBool) s.Sound = b; break;
                        case "X": if (isInt) s.X = n; break;
                        case "Y": if (isInt) s.Y = n; break;
                    }
                }
                if (s.RedPct >= s.YellowPct) s.RedPct = Math.Max(0, s.YellowPct - 1);
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
                    "Hours=" + Hours, "Minutes=" + Minutes, "Seconds=" + Seconds,
                    "YellowPct=" + YellowPct, "RedPct=" + RedPct,
                    "TopMost=" + TopMost, "Sound=" + Sound,
                    "X=" + X, "Y=" + Y });
            }
            catch { }
        }
    }

    class TimerForm : Form
    {
        enum Hit { None, Close, Min, Pin, Gear, Seg0, Seg1, Seg2, Main, Reset }
        enum State { Idle, Running, Paused, Finished }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

        const string GlyphClose = "", GlyphMin = "", GlyphPin = "", GlyphPinned = "", GlyphGear = "";

        readonly Settings cfg;
        readonly float k;
        State state = State.Idle;
        readonly Stopwatch sw = new Stopwatch();
        readonly Stopwatch finishClock = new Stopwatch();
        TimeSpan total, banked;
        int sel = -1, typed;
        int beepsLeft;
        long nextBeepMs;
        Hit hover = Hit.None, pressed = Hit.None;
        bool dwmBorder;

        readonly Font fDigits, fUi, fIcon, fTitle;
        readonly Rectangle[] seg = new Rectangle[3];
        readonly Rectangle[] colon = new Rectangle[2];
        readonly Timer tick;
        readonly ToolTip tip;
        string tipText = "";
        ToolStripMenuItem miTop, miSound;

        public TimerForm()
        {
            cfg = Settings.Load();
            Text = "Countdown Timer";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = true;
            KeyPreview = true;
            BackColor = Palette.Bg;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            using (var g = CreateGraphics()) k = g.DpiX / 96f;
            ClientSize = new Size(S(280), S(130));

            fDigits = new Font("Segoe UI Semibold", 44 * k, GraphicsUnit.Pixel);
            fUi = new Font("Segoe UI", 13 * k, GraphicsUnit.Pixel);
            fTitle = new Font("Segoe UI", 12 * k, GraphicsUnit.Pixel);
            fIcon = new Font("Segoe MDL2 Assets", 10 * k, GraphicsUnit.Pixel);
            ComputeLayout();

            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            TopMost = cfg.TopMost;
            PlaceWindow();

            tip = new ToolTip { InitialDelay = 600, ReshowDelay = 200 };
            BuildMenu();

            tick = new Timer { Interval = 40 };
            tick.Tick += OnTick;
            tick.Start();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.Style |= 0x00020000;        // WS_MINIMIZEBOX: lets the taskbar button minimize/restore
                cp.ClassStyle |= 0x00020000;   // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // DWMWCP_ROUND (Windows 11)
                int border = Palette.Border.R | (Palette.Border.G << 8) | (Palette.Border.B << 16);
                dwmBorder = DwmSetWindowAttribute(Handle, 33, ref round, 4) == 0 &&
                            DwmSetWindowAttribute(Handle, 34, ref border, 4) == 0;
            }
            catch { dwmBorder = false; }
        }

        int S(float v) { return (int)Math.Round(v * k); }
        Rectangle R(float x, float y, float w, float h) { return new Rectangle(S(x), S(y), S(w), S(h)); }

        Rectangle RcClose { get { return R(252, 0, 28, 26); } }
        Rectangle RcMin { get { return R(224, 0, 28, 26); } }
        Rectangle RcPin { get { return R(196, 0, 28, 26); } }
        Rectangle RcGear { get { return R(168, 0, 28, 26); } }
        Rectangle RcBar { get { return R(14, 86, 252, 8); } }
        Rectangle RcMain { get { return R(46, 100, 90, 24); } }
        Rectangle RcReset { get { return R(144, 100, 90, 24); } }

        void ComputeLayout()
        {
            // MeasureText adds overhang padding to every call, so measure true advances by difference.
            var flags = TextFormatFlags.NoPadding;
            int base2 = TextRenderer.MeasureText("00", fDigits, Size.Empty, flags).Width;
            int d2 = TextRenderer.MeasureText("0000", fDigits, Size.Empty, flags).Width - base2;
            int c = TextRenderer.MeasureText("00:", fDigits, Size.Empty, flags).Width - base2;
            int w = d2 + S(10), cw = c + S(4);
            int x = (ClientSize.Width - (3 * w + 2 * cw)) / 2;
            int top = S(26), h = S(56);
            for (int i = 0; i < 3; i++)
            {
                seg[i] = new Rectangle(x, top, w, h);
                x += w;
                if (i < 2) { colon[i] = new Rectangle(x, top - S(3), cw, h); x += cw; }
            }
        }

        void PlaceWindow()
        {
            var pos = new Point(cfg.X, cfg.Y);
            bool onScreen = false;
            if (cfg.X != int.MinValue)
            {
                var r = new Rectangle(pos, Size);
                foreach (var scr in Screen.AllScreens)
                    if (scr.WorkingArea.IntersectsWith(new Rectangle(r.X + S(20), r.Y, r.Width - S(40), S(26)))) onScreen = true;
            }
            if (!onScreen)
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                pos = new Point(wa.Right - Width - S(24), wa.Bottom - Height - S(24));
            }
            Location = pos;
        }

        void BuildMenu()
        {
            var menu = new ContextMenuStrip();
            miTop = new ToolStripMenuItem("Always on top", null, delegate { SetTopMost(!TopMost); });
            miTop.Checked = TopMost;
            miSound = new ToolStripMenuItem("Sound at the end", null, delegate { cfg.Sound = !cfg.Sound; miSound.Checked = cfg.Sound; cfg.Save(); });
            miSound.Checked = cfg.Sound;
            menu.Items.Add(miTop);
            menu.Items.Add(miSound);
            menu.Items.Add(new ToolStripMenuItem("Settings...", null, delegate { OpenSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("About Countdown Timer", null, delegate
            {
                using (var about = new AboutForm()) about.ShowDialog(this);
            }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Exit", null, delegate { Close(); }));
            ContextMenuStrip = menu;
        }

        // ---- Timer logic ----

        TimeSpan Remaining()
        {
            if (state == State.Idle) return cfg.Duration;
            if (state == State.Finished) return TimeSpan.Zero;
            var r = total - banked - sw.Elapsed;
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }

        double Fraction()
        {
            if (state == State.Idle) return 1;
            if (total.Ticks <= 0) return 0;
            return Math.Max(0, Math.Min(1, (double)Remaining().Ticks / total.Ticks));
        }

        Color Level(double frac)
        {
            double pct = frac * 100;
            if (pct <= cfg.RedPct) return Palette.Red;
            if (pct <= cfg.YellowPct) return Palette.Yellow;
            return Palette.Green;
        }

        static string Fmt(TimeSpan t)
        {
            long s = (long)Math.Ceiling(t.TotalSeconds - 1e-9);
            if (s < 0) s = 0;
            return string.Format("{0:00}:{1:00}:{2:00}", s / 3600, (s / 60) % 60, s % 60);
        }

        void Start()
        {
            total = cfg.Duration;
            if (total <= TimeSpan.Zero) { SystemSounds.Beep.Play(); return; }
            banked = TimeSpan.Zero;
            sw.Reset();
            sw.Start();
            state = State.Running;
            sel = -1;
            beepsLeft = 0;
            cfg.Save();
        }

        void MainAction()
        {
            switch (state)
            {
                case State.Idle:
                case State.Finished:
                    Start();
                    break;
                case State.Running:
                    sw.Stop();
                    banked += sw.Elapsed;
                    sw.Reset();
                    state = State.Paused;
                    break;
                case State.Paused:
                    sw.Start();
                    state = State.Running;
                    break;
            }
            UpdateTip();
            Invalidate();
        }

        void DoReset()
        {
            sw.Reset();
            banked = TimeSpan.Zero;
            beepsLeft = 0;
            state = State.Idle;
            UpdateTip();
            Invalidate();
        }

        void Finish()
        {
            sw.Stop();
            state = State.Finished;
            finishClock.Reset();
            finishClock.Start();
            beepsLeft = cfg.Sound ? 3 : 0;
            nextBeepMs = 0;
            FlashTaskbar();
            UpdateTip();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (state == State.Running)
            {
                if (total - banked - sw.Elapsed <= TimeSpan.Zero) Finish();
                Invalidate();
            }
            else if (state == State.Finished)
            {
                if (beepsLeft > 0 && finishClock.ElapsedMilliseconds >= nextBeepMs)
                {
                    SystemSounds.Exclamation.Play();
                    beepsLeft--;
                    nextBeepMs += 1300;
                }
                if (finishClock.ElapsedMilliseconds < 11000) Invalidate();
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

        void SetTopMost(bool on)
        {
            TopMost = on;
            cfg.TopMost = on;
            miTop.Checked = on;
            cfg.Save();
            UpdateTip();
            Invalidate();
        }

        void OpenSettings()
        {
            using (var dlg = new SettingsForm(cfg, TopMost))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                dlg.ApplyTo(cfg);
                miSound.Checked = cfg.Sound;
                SetTopMost(dlg.AlwaysOnTop);
                if (state == State.Finished) DoReset();
                Invalidate();
            }
        }

        // ---- Editing the time (only before starting) ----

        void Step(int s, int delta)
        {
            int max = s == 0 ? 99 : 59;
            int v = cfg[s] + delta;
            if (v > max) v = 0;
            if (v < 0) v = max;
            cfg[s] = v;
            Invalidate();
        }

        void TypeDigit(int d)
        {
            if (sel < 0) { sel = 0; typed = 0; }
            cfg[sel] = typed == 0 ? d : cfg[sel] * 10 + d;
            typed++;
            if (typed >= 2)
            {
                typed = 0;
                if (sel < 2) sel++;
            }
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool idle = state == State.Idle;

            if (keyData == Keys.Space || keyData == Keys.Enter) { sel = -1; MainAction(); return true; }
            if (keyData == Keys.R) { DoReset(); return true; }
            if (keyData == Keys.Escape) { sel = -1; Invalidate(); return true; }
            if (!idle) return base.ProcessCmdKey(ref msg, keyData);

            int digit = -1;
            if (key >= Keys.D0 && key <= Keys.D9 && keyData == key) digit = key - Keys.D0;
            if (key >= Keys.NumPad0 && key <= Keys.NumPad9) digit = key - Keys.NumPad0;
            if (digit >= 0) { TypeDigit(digit); return true; }

            switch (keyData)
            {
                case Keys.Up: if (sel < 0) sel = 1; typed = 0; Step(sel, +1); return true;
                case Keys.Down: if (sel < 0) sel = 1; typed = 0; Step(sel, -1); return true;
                case Keys.Left: sel = sel <= 0 ? 0 : sel - 1; typed = 0; Invalidate(); return true;
                case Keys.Right:
                case Keys.Tab: sel = sel < 0 ? 0 : Math.Min(2, sel + 1); typed = 0; Invalidate(); return true;
                case Keys.Shift | Keys.Tab: sel = sel <= 0 ? 0 : sel - 1; typed = 0; Invalidate(); return true;
                case Keys.Back:
                case Keys.Delete: if (sel >= 0) { cfg[sel] = 0; typed = 0; Invalidate(); } return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ---- Mouse ----

        Hit HitTest(Point p)
        {
            if (RcClose.Contains(p)) return Hit.Close;
            if (RcMin.Contains(p)) return Hit.Min;
            if (RcPin.Contains(p)) return Hit.Pin;
            if (RcGear.Contains(p)) return Hit.Gear;
            if (RcMain.Contains(p)) return Hit.Main;
            if (RcReset.Contains(p)) return Hit.Reset;
            if (state == State.Idle)
                for (int i = 0; i < 3; i++)
                    if (seg[i].Contains(p)) return Hit.Seg0 + i;
            return Hit.None;
        }

        static bool IsButton(Hit h) { return h != Hit.None && (h < Hit.Seg0 || h > Hit.Seg2); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var h = HitTest(e.Location);
            if (h != hover) { hover = h; UpdateTip(); Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hover = Hit.None;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            var h = HitTest(e.Location);
            if (h >= Hit.Seg0 && h <= Hit.Seg2)
            {
                sel = h - Hit.Seg0;
                typed = 0;
                Invalidate();
                return;
            }
            if (IsButton(h))
            {
                pressed = h;
                Invalidate();
                return;
            }
            sel = -1;
            Invalidate();
            ReleaseCapture();
            SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (pressed == Hit.None) return;
            var p = pressed;
            pressed = Hit.None;
            Invalidate();
            if (HitTest(e.Location) != p) return;
            switch (p)
            {
                case Hit.Close: Close(); break;
                case Hit.Min: WindowState = FormWindowState.Minimized; break;
                case Hit.Pin: SetTopMost(!TopMost); break;
                case Hit.Gear: OpenSettings(); break;
                case Hit.Main: sel = -1; MainAction(); break;
                case Hit.Reset: DoReset(); break;
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            var h = HitTest(e.Location);
            if (state != State.Idle || h < Hit.Seg0 || h > Hit.Seg2) return;
            sel = h - Hit.Seg0;
            typed = 0;
            Step(sel, e.Delta > 0 ? 1 : -1);
        }

        string MainLabel()
        {
            switch (state)
            {
                case State.Running: return "Pause";
                case State.Paused: return "Resume";
                case State.Finished: return "Restart";
                default: return "Start";
            }
        }

        void UpdateTip()
        {
            string t = "";
            switch (hover)
            {
                case Hit.Close: t = "Close"; break;
                case Hit.Min: t = "Minimize"; break;
                case Hit.Pin: t = TopMost ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"; break;
                case Hit.Gear: t = "Settings"; break;
                case Hit.Seg0:
                case Hit.Seg1:
                case Hit.Seg2: t = "Scroll, or click and type, to set the time"; break;
                case Hit.Main: t = MainLabel() + " (Space)"; break;
                case Hit.Reset: t = "Reset (R)"; break;
            }
            if (t == tipText) return;
            tipText = t;
            tip.SetToolTip(this, t);
        }

        // ---- Painting ----

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Palette.Bg);
            if (!dwmBorder)
                using (var pen = new Pen(Palette.Border))
                    g.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            var center = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

            // Title bar
            string title = state == State.Idle ? "Timer" : "Timer  ·  " + Fmt(total);
            TextRenderer.DrawText(g, title, fTitle, new Rectangle(S(10), 0, S(155), S(26)), Palette.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            DrawIconButton(g, RcGear, GlyphGear, Hit.Gear, Palette.Dim);
            DrawIconButton(g, RcPin, TopMost ? GlyphPinned : GlyphPin, Hit.Pin, TopMost ? Palette.Text : Palette.Dim);
            DrawIconButton(g, RcMin, GlyphMin, Hit.Min, Palette.Dim);
            DrawIconButton(g, RcClose, GlyphClose, Hit.Close, Palette.Dim);

            // Digits
            double frac = Fraction();
            Color level = state == State.Finished ? Palette.Red : Level(frac);
            bool blinkOff = state == State.Finished && finishClock.ElapsedMilliseconds < 11000 &&
                            (finishClock.ElapsedMilliseconds / 500) % 2 == 1;
            Color digitColor = blinkOff ? Color.FromArgb(110, 30, 30) : level;
            if (state == State.Paused) digitColor = Blend(level, Palette.Bg, 0.35);

            string[] parts = Fmt(Remaining()).Split(':');
            for (int i = 0; i < 3; i++)
            {
                if (state == State.Idle && (sel == i || hover == Hit.Seg0 + i))
                {
                    Palette.FillRound(g, seg[i], S(6), sel == i ? Palette.SegSelected : Palette.SegHover);
                    if (sel == i)
                        Palette.FillRound(g, new RectangleF(seg[i].X + S(8), seg[i].Bottom - S(5), seg[i].Width - S(16), S(2.5f)), S(1), level);
                }
                TextRenderer.DrawText(g, parts[i], fDigits, seg[i], digitColor, center | TextFormatFlags.NoClipping);
                if (i < 2) TextRenderer.DrawText(g, ":", fDigits, colon[i], digitColor, center | TextFormatFlags.NoClipping);
            }

            // Bar: shrinks from right to left
            Rectangle bar = RcBar;
            float radius = bar.Height / 2f;
            Palette.FillRound(g, bar, radius, state == State.Finished && !blinkOff ? Blend(Palette.Red, Palette.Bg, 0.45) : Palette.Track);
            int fw = (int)Math.Round(bar.Width * frac);
            if (fw > 0)
            {
                g.SetClip(new Rectangle(bar.X, bar.Y - 1, fw, bar.Height + 2));
                Palette.FillRound(g, bar, radius, level);
                g.ResetClip();
            }

            // Buttons
            bool canStart = state != State.Idle || cfg.Duration > TimeSpan.Zero;
            DrawButton(g, RcMain, MainLabel(), Hit.Main, canStart ? Palette.Text : Palette.Dim);
            DrawButton(g, RcReset, "Reset", Hit.Reset, state == State.Idle ? Palette.Dim : Palette.Text);
        }

        void DrawIconButton(Graphics g, Rectangle r, string glyph, Hit h, Color fg)
        {
            if (hover == h || pressed == h)
            {
                bool close = h == Hit.Close;
                using (var b = new SolidBrush(close ? Palette.CloseHover : Palette.BtnHover)) g.FillRectangle(b, r);
                fg = close ? Color.White : Palette.Text;
            }
            TextRenderer.DrawText(g, glyph, fIcon, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        void DrawButton(Graphics g, Rectangle r, string text, Hit h, Color fg)
        {
            Color bg = pressed == h ? Palette.Border : hover == h ? Palette.BtnHover : Palette.Btn;
            Palette.FillRound(g, r, S(5), bg);
            TextRenderer.DrawText(g, text, fUi, r, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        static Color Blend(Color a, Color b, double t)
        {
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            var loc = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
            cfg.X = loc.X;
            cfg.Y = loc.Y;
            cfg.Save();
            base.OnFormClosing(e);
        }
    }

    class SettingsForm : Form
    {
        readonly NumericUpDown nH, nM, nS, nY, nR;
        readonly CheckBox cTop, cSound;

        public bool AlwaysOnTop { get { return cTop.Checked; } }

        public SettingsForm(Settings s, bool topMost)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "Timer settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 262);

            AddLabel(14, 14, "Countdown time").Font = new Font("Segoe UI Semibold", 9F);
            nH = Num(14, 38, 0, 99, s.Hours); AddLabel(72, 41, "h");
            nM = Num(98, 38, 0, 59, s.Minutes); AddLabel(156, 41, "min");
            nS = Num(190, 38, 0, 59, s.Seconds); AddLabel(248, 41, "s");

            AddLabel(14, 82, "Bar colors").Font = new Font("Segoe UI Semibold", 9F);
            AddLabel(14, 109, "Turn yellow at");
            nY = Num(130, 106, 1, 100, s.YellowPct); AddLabel(188, 109, "% of time left");
            AddLabel(14, 141, "Turn red at");
            nR = Num(130, 138, 0, 99, s.RedPct); AddLabel(188, 141, "% of time left");

            cTop = new CheckBox { Text = "Always on top", Location = new Point(14, 176), AutoSize = true, Checked = topMost };
            cSound = new CheckBox { Text = "Sound at the end", Location = new Point(150, 176), AutoSize = true, Checked = s.Sound };
            Controls.Add(cTop);
            Controls.Add(cSound);

            var ok = new Button { Text = "OK", Location = new Point(124, 220), Size = new Size(80, 28) };
            var cancel = new Button { Text = "Cancel", Location = new Point(210, 220), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };
            ok.Click += delegate
            {
                if (nR.Value >= nY.Value)
                {
                    MessageBox.Show(this, "The red level must be lower than the yellow level.", "Timer settings",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                DialogResult = DialogResult.OK;
            };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ResumeLayout(false);
        }

        Label AddLabel(int x, int y, string text)
        {
            var l = new Label { Text = text, Location = new Point(x, y), AutoSize = true };
            Controls.Add(l);
            return l;
        }

        NumericUpDown Num(int x, int y, int min, int max, int value)
        {
            var n = new NumericUpDown { Location = new Point(x, y), Width = 54, Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), TextAlign = HorizontalAlignment.Right };
            n.Enter += delegate { n.Select(0, n.Text.Length); };
            Controls.Add(n);
            return n;
        }

        public void ApplyTo(Settings s)
        {
            s.Hours = (int)nH.Value;
            s.Minutes = (int)nM.Value;
            s.Seconds = (int)nS.Value;
            s.YellowPct = (int)nY.Value;
            s.RedPct = (int)nR.Value;
            s.Sound = cSound.Checked;
            s.TopMost = cTop.Checked;
            s.Save();
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
            Text = "About Countdown Timer";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 392);

            var icon = new PictureBox { Location = new Point(16, 16), Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom };
            var bmp = new Bitmap(96, 96, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp)) IconArt.Draw(g, 96);
            icon.Image = bmp;
            Controls.Add(icon);

            Controls.Add(new Label { Text = "Countdown Timer", Location = new Point(76, 16), AutoSize = true, Font = new Font("Segoe UI Semibold", 12F) });
            Controls.Add(new Label { Text = "Version 1.0  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
            Controls.Add(new Label
            {
                Text = "Free software, provided \"as is\", without warranty of any kind.\r\n" +
                       "This app collects no personal data and never connects to the internet.",
                Location = new Point(16, 80), Size = new Size(390, 36)
            });

            Controls.Add(new TextBox
            {
                Text = License, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(16, 124), Size = new Size(388, 214), TabStop = false,
                BackColor = SystemColors.Window
            });

            var ok = new Button { Text = "OK", Location = new Point(324, 350), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = ok;
            ResumeLayout(false);
        }
    }

    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.03f;
            Palette.FillRound(g, new RectangleF(m, m, s - 2 * m, s - 2 * m), s * 0.2f, Palette.Bg);

            float cx = s * 0.5f, cy = s * 0.42f, rad = s * 0.25f, w = Math.Max(1.4f, s * 0.075f);
            using (var pen = new Pen(Palette.Text, w))
                g.DrawEllipse(pen, cx - rad, cy - rad, 2 * rad, 2 * rad);
            using (var pen = new Pen(Palette.Yellow, w) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, cx, cy, cx, cy - rad * 0.62f);
                g.DrawLine(pen, cx, cy, cx + rad * 0.45f, cy);
            }

            float bx = s * 0.17f, bw = s * 0.66f, by = s * 0.77f, bh = Math.Max(2f, s * 0.085f);
            var bar = new RectangleF(bx, by, bw, bh);
            Palette.FillRound(g, bar, bh / 2, Palette.Red);
            g.SetClip(new RectangleF(bx - 1, by - 1, bw * 0.8f + 1, bh + 2));
            Palette.FillRound(g, bar, bh / 2, Palette.Yellow);
            g.SetClip(new RectangleF(bx - 1, by - 1, bw * 0.55f + 1, bh + 2));
            Palette.FillRound(g, bar, bh / 2, Palette.Green);
            g.ResetClip();
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
