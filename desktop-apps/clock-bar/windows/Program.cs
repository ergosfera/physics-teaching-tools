// Clock Bar for Windows: the date and time ("April 5  2:34") in bold, outlined letters on a see-through
// strip that stays on top of everything, readable over any background. Drag it into place once and it
// remembers; a click opens a month calendar.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Clock Bar")]
[assembly: AssemblyProduct("Clock Bar")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ClockBar
{
    static class Program
    {
        [DllImport("user32.dll")] static extern bool SetProcessDPIAware();

        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--write-icon")
            {
                Art.WriteIco(args[1]);
                return 0;
            }
            // --render out.png [scale]: draws the bar at 2:34 on 5 April, to check how it looks
            if ((args.Length == 2 || args.Length == 3) && args[0] == "--render")
            {
                float s = args.Length == 3 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 3;
                using (var bmp = Art.Bar(s, new DateTime(2026, 4, 5, 14, 34, 0)))
                    bmp.Save(args[1], ImageFormat.Png);
                return 0;
            }

            // One copy at a time: a second launch just exits
            bool fresh;
            using (var single = new Mutex(true, "ErgosferaClockBar", out fresh))
            {
                if (!fresh) return 0;
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new BarForm());
            }
            return 0;
        }
    }

    // ── Drawing: sizes are in pixels at scale 1 ──
    static class Art
    {
        public static readonly Color DateColor = Color.FromArgb(0xff, 0xc4, 0x2e);   // amber
        public static readonly Color TimeColor = Color.White;
        const float DateEm = 17, TimeEm = 21, Gap = 10, Pad = 4;
        static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        static FontFamily family;
        static FontFamily Family
        {
            get
            {
                if (family == null)
                {
                    try { family = new FontFamily("Segoe UI"); }
                    catch { family = FontFamily.GenericSansSerif; }
                }
                return family;
            }
        }

        public static string DateText(DateTime t) { return t.ToString("MMMM d", English); }   // April 5
        public static string TimeText(DateTime t) { return t.ToString("h:mm", English); }     // 2:34, no AM/PM

        static GraphicsPath Words(string text, float em)
        {
            var p = new GraphicsPath();
            p.AddString(text, Family, (int)FontStyle.Bold, em, PointF.Empty, StringFormat.GenericTypographic);
            return p;
        }

        // The bar keeps one size whatever the date and time, so it doesn't jump around: wide enough
        // for the longest month and time, and tall enough for capitals and descenders
        public static Size BarSize(float scale)
        {
            float w;
            using (var d = Words("September 30", DateEm))
            using (var t = Words("12:59", TimeEm))
                w = d.GetBounds().Width + Gap + t.GetBounds().Width + 2 * Pad;
            float h = TimeEm * 1.25f + 2 * Pad;
            return new Size((int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale));
        }

        // Dark outline under bright letters: readable on a white page and on a black window alike
        static void Outlined(Graphics g, GraphicsPath p, Color fill, float em)
        {
            using (var pen = new Pen(Color.FromArgb(150, 0, 0, 0), em * 0.30f) { LineJoin = LineJoin.Round })
            {
                g.TranslateTransform(0.6f, 0.9f);
                g.DrawPath(pen, p);
                g.TranslateTransform(-0.6f, -0.9f);
            }
            using (var pen = new Pen(Color.FromArgb(235, 0, 0, 0), em * 0.18f) { LineJoin = LineJoin.Round })
                g.DrawPath(pen, p);
            using (var b = new SolidBrush(fill)) g.FillPath(b, p);
        }

        public static void DrawBar(Graphics g, SizeF size, DateTime t)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            using (var d = Words(DateText(t), DateEm))
            using (var tm = Words(TimeText(t), TimeEm))
            {
                // Both sit on one baseline, the pair centered in the bar
                RectangleF db = d.GetBounds(), tb = tm.GetBounds();
                float baselineD = Family.GetCellAscent(FontStyle.Bold) * DateEm / Family.GetEmHeight(FontStyle.Bold);
                float baselineT = Family.GetCellAscent(FontStyle.Bold) * TimeEm / Family.GetEmHeight(FontStyle.Bold);
                float total = db.Width + Gap + tb.Width;
                float x = (size.Width - total) / 2;
                float capTop = baselineT - TimeEm * 0.70f;
                float baseline = (size.Height - (baselineT - capTop)) / 2 + (baselineT - capTop);
                using (var m = new Matrix())
                {
                    m.Translate(x - db.Left, baseline - baselineD);
                    d.Transform(m);
                }
                using (var m = new Matrix())
                {
                    m.Translate(x + db.Width + Gap - tb.Left, baseline - baselineT);
                    tm.Transform(m);
                }
                Outlined(g, d, DateColor, DateEm);
                Outlined(g, tm, TimeColor, TimeEm);
            }
        }

        // The bar as a bitmap with see-through background
        public static Bitmap Bar(float scale, DateTime t)
        {
            Size px = BarSize(scale);
            var bmp = new Bitmap(px.Width, px.Height, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                // Almost invisible fill, so the spaces between letters still catch the mouse
                g.Clear(Color.FromArgb(1, 0, 0, 0));
                g.ScaleTransform(scale, scale);
                DrawBar(g, new SizeF(px.Width / scale, px.Height / scale), t);
            }
            return bmp;
        }

        // App icon: "2:34" in the bar's colors on a dark rounded square
        public static void DrawAppIcon(Graphics g, int size)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float s = size;
            using (var p = new GraphicsPath())
            {
                float r = s * 0.2f, d = 2 * r, m = s * 0.04f;
                p.AddArc(m, m, d, d, 180, 90);
                p.AddArc(s - m - d, m, d, d, 270, 90);
                p.AddArc(s - m - d, s - m - d, d, d, 0, 90);
                p.AddArc(m, s - m - d, d, d, 90, 90);
                p.CloseFigure();
                using (var b = new SolidBrush(Color.FromArgb(0x26, 0x26, 0x24))) g.FillPath(b, p);
            }
            foreach (var part in new[] { new { Text = "APR 5", Em = 0.24f, Y = 0.30f, Color = DateColor },
                                         new { Text = "2:34", Em = 0.40f, Y = 0.64f, Color = TimeColor } })
                using (var p = Words(part.Text, part.Em * s))
                {
                    RectangleF b = p.GetBounds();
                    float k = Math.Min(1, s * 0.84f / b.Width);
                    using (var m = new Matrix())
                    {
                        m.Translate(s / 2, part.Y * s);
                        m.Scale(k, k);
                        m.Translate(-(b.Left + b.Width / 2), -(b.Top + b.Height / 2));
                        p.Transform(m);
                    }
                    using (var br = new SolidBrush(part.Color)) g.FillPath(br, p);
                }
        }

        public static void WriteIco(string path)
        {
            int[] sizes = { 256, 64, 48, 32, 24, 16 };
            var pngs = new List<byte[]>();
            foreach (int size in sizes)
                using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp)) DrawAppIcon(g, size);
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

    // ── Saved settings, in HKCU\Software\Ergosfera\ClockBar ──
    static class Settings
    {
        const string Key = @"Software\Ergosfera\ClockBar";

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

        public static float GetFloat(string name, float fallback)
        {
            float v;
            string s = Get(name);
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public static int GetInt(string name, int fallback)
        {
            int v;
            string s = Get(name);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }
    }

    // ── The month calendar that opens on a click ──
    class CalendarForm : Form
    {
        public DateTime HiddenAt = DateTime.MinValue;
        readonly MonthCalendar month = new MonthCalendar();

        public CalendarForm()
        {
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            Text = "Clock Bar";
            month.MaxSelectionCount = 1;
            month.Location = new Point(0, 0);
            Controls.Add(month);
        }

        public void ShowNear(Rectangle bar)
        {
            DateTime now = DateTime.Now;
            month.SetDate(now);
            month.TodayDate = now.Date;
            ClientSize = month.Size;

            // Below the bar, or above it in the lower half of the screen, kept on the screen
            Rectangle wa = Screen.FromRectangle(bar).WorkingArea;
            int gap = Math.Max(4, bar.Height / 4);
            int x = bar.Left + bar.Width / 2 - Width / 2;
            int y = bar.Top + bar.Height / 2 < wa.Top + wa.Height / 2 ? bar.Bottom + gap : bar.Top - Height - gap;
            x = Math.Max(wa.Left, Math.Min(x, wa.Right - Width));
            y = Math.Max(wa.Top, Math.Min(y, wa.Bottom - Height));
            Location = new Point(x, y);
            Show();
            Activate();
        }

        public void HideCalendar()
        {
            HiddenAt = DateTime.UtcNow;
            Hide();
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            HideCalendar();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) HideCalendar();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideCalendar(); }
            else base.OnFormClosing(e);
        }
    }

    // ── The bar: a per-pixel transparent window that stays on top ──
    class BarForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll")] static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Point pptDst, ref Size psize, IntPtr hdcSrc, ref Point pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaClockBar";
        static readonly float[] Sizes = { 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f };

        readonly Timer timer = new Timer();
        readonly ToolTip tip = new ToolTip();
        readonly CalendarForm calendar = new CalendarForm();
        readonly ToolStripMenuItem lockItem, startItem, sizeItem;
        float scale;
        bool locked;
        string shown = "";

        public BarForm()
        {
            float k;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            scale = Settings.GetFloat("Scale", k);
            locked = Settings.GetInt("Locked", 0) != 0;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Clock Bar";
            Size = Art.BarSize(scale);
            Location = StartLocation();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Show calendar", null, delegate { calendar.ShowNear(Bounds); });
            menu.Items.Add(new ToolStripSeparator());
            sizeItem = new ToolStripMenuItem("Size");
            foreach (float s in Sizes)
            {
                float size = s;
                var item = new ToolStripMenuItem(string.Format(CultureInfo.InvariantCulture, "{0} %", Math.Round(s * 100)), null, delegate { SetScale(size); });
                item.Tag = size;
                sizeItem.DropDownItems.Add(item);
            }
            menu.Items.Add(sizeItem);
            lockItem = new ToolStripMenuItem("Lock position", null, delegate
            {
                locked = !locked;
                Settings.Set("Locked", locked ? 1 : 0);
            });
            menu.Items.Add(lockItem);
            startItem = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            menu.Items.Add(startItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Clock Bar", null, delegate { Quit(); });
            menu.Opening += delegate { RefreshMenu(); };
            ContextMenuStrip = menu;

            tip.ShowAlways = true;

            // Twice a second: the minute turns over on time, and the bar gets back in front of the taskbar
            timer.Interval = 500;
            timer.Tick += delegate { Tick(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x80000 | 0x80;   // WS_EX_LAYERED, WS_EX_TOOLWINDOW (not in Alt+Tab)
                return cp;
            }
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Redraw(true);
            timer.Start();
        }

        // Where it was left; the first time, at the top center of the main screen
        Point StartLocation()
        {
            Size size = Art.BarSize(scale);
            int x = Settings.GetInt("X", int.MinValue), y = Settings.GetInt("Y", int.MinValue);
            if (x != int.MinValue && y != int.MinValue)
                foreach (Screen sc in Screen.AllScreens)
                    if (sc.Bounds.IntersectsWith(new Rectangle(x, y, size.Width, size.Height)))
                        return new Point(x, y);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Left + (wa.Width - size.Width) / 2, wa.Top + (int)(6 * scale));
        }

        void SetScale(float s)
        {
            // Grow or shrink around the center, so it stays where it was put
            Point c = new Point(Left + Width / 2, Top + Height / 2);
            scale = s;
            Settings.Set("Scale", s);
            Size = Art.BarSize(scale);
            Location = new Point(c.X - Width / 2, c.Y - Height / 2);
            SavePosition();
            Redraw(true);
        }

        void SavePosition()
        {
            Settings.Set("X", Location.X);
            Settings.Set("Y", Location.Y);
        }

        void Redraw(bool force)
        {
            DateTime now = DateTime.Now;
            string key = now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + scale;
            if (!force && key == shown) return;
            shown = key;
            tip.SetToolTip(this, now.ToString("D", CultureInfo.CurrentCulture) + "\n" + now.ToString("t", CultureInfo.CurrentCulture));
            using (var bmp = Art.Bar(scale, now)) Present(bmp);
        }

        // Hands the picture, with its transparency, to Windows
        void Present(Bitmap bmp)
        {
            if (!IsHandleCreated) return;
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr mem = CreateCompatibleDC(screen);
            IntPtr hbmp = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = SelectObject(mem, hbmp);
            try
            {
                Size size = bmp.Size;
                Point src = Point.Empty, dst = Location;
                var blend = new BLENDFUNCTION { BlendOp = 0, BlendFlags = 0, SourceConstantAlpha = 255, AlphaFormat = 1 };
                UpdateLayeredWindow(Handle, screen, ref dst, ref size, mem, ref src, 0, ref blend, 2);   // ULW_ALPHA
            }
            finally
            {
                SelectObject(mem, old);
                DeleteObject(hbmp);
                DeleteDC(mem);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        void Tick()
        {
            Redraw(false);
            // The taskbar comes back in front of everything when it's clicked; step in front of it again
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0010);   // HWND_TOPMOST; NOSIZE, NOMOVE, NOACTIVATE
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Point before = Location;
            if (!locked)
            {
                // Drag anywhere to move it
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN on the caption
            }
            if (Location != before) SavePosition();
            else ToggleCalendar();
        }

        void ToggleCalendar()
        {
            // Clicking the bar first deactivates the open calendar; don't reopen it right away
            if (calendar.Visible) calendar.HideCalendar();
            else if ((DateTime.UtcNow - calendar.HiddenAt).TotalMilliseconds > 300) calendar.ShowNear(Bounds);
        }

        void RefreshMenu()
        {
            lockItem.Checked = locked;
            startItem.Checked = StartsWithWindows();
            foreach (ToolStripMenuItem item in sizeItem.DropDownItems)
                item.Checked = Math.Abs((float)item.Tag - scale) < 0.01f;
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
                MessageBox.Show("Couldn't change Start with Windows.\n\n" + ex.Message, "Clock Bar");
            }
        }

        void Quit()
        {
            timer.Stop();
            calendar.Dispose();
            Close();
        }
    }
}
