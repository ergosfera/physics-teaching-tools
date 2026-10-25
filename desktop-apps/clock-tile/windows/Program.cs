// Clock Tile for Windows: a small tile to put on the taskbar, with a flip clock on top and a
// tear-off calendar page (month and day) below. It's a borderless window that stays on top;
// drag it into place once and it remembers. A click opens a month calendar.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Clock Tile")]
[assembly: AssemblyProduct("Clock Tile")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace ClockTile
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
            // --render out.png [scale]: draws the tile at 14:37 on 24 October, to check how it looks
            if ((args.Length == 2 || args.Length == 3) && args[0] == "--render")
            {
                float s = args.Length == 3 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 4;
                using (var bmp = Art.Tile(s, new DateTime(2026, 10, 24, 14, 37, 0), true))
                    bmp.Save(args[1], ImageFormat.Png);
                return 0;
            }

            // One copy at a time: a second launch just exits
            bool fresh;
            using (var single = new Mutex(true, "ErgosferaClockTile", out fresh))
            {
                if (!fresh) return 0;
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TileForm());
            }
            return 0;
        }
    }

    // ── Drawing: everything is laid out on a 40 × 70 grid and scaled ──
    static class Art
    {
        public const float W = 40, H = 70;
        static readonly Color Blue = Color.FromArgb(0x2f, 0x6f, 0xd6);
        static readonly Color BlueLight = Color.FromArgb(0x4a, 0x8a, 0xec);
        static readonly Color BlueDark = Color.FromArgb(0x22, 0x58, 0xb4);

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

        public static GraphicsPath Round(RectangleF r, float rad)
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

        // Rectangle with only the top corners rounded
        static GraphicsPath RoundTop(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = rad * 2;
            p.AddArc(r.Left, r.Top, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            p.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
            p.CloseFigure();
            return p;
        }

        // Quadratic curve from a through control c to b, as the cubic GDI+ draws
        static void AddQuad(GraphicsPath p, PointF a, PointF c, PointF b)
        {
            p.AddBezier(a,
                new PointF(a.X + 2f / 3 * (c.X - a.X), a.Y + 2f / 3 * (c.Y - a.Y)),
                new PointF(b.X + 2f / 3 * (c.X - b.X), b.Y + 2f / 3 * (c.Y - b.Y)),
                b);
        }

        // Text centered on its ink (not its line box) in the given box, shrunk if it's too wide
        static void Text(Graphics g, string text, float em, PointF center, float maxWidth, Brush brush)
        {
            using (var p = new GraphicsPath())
            {
                p.AddString(text, Family, (int)FontStyle.Bold, em, PointF.Empty, StringFormat.GenericTypographic);
                RectangleF ink = p.GetBounds();
                if (ink.Width <= 0) return;
                float k = Math.Min(1, maxWidth / ink.Width);
                using (var m = new Matrix())
                {
                    m.Translate(center.X, center.Y);
                    m.Scale(k, k);
                    m.Translate(-(ink.Left + ink.Width / 2), -(ink.Top + ink.Height / 2));
                    p.Transform(m);
                }
                g.FillPath(brush, p);
            }
        }

        static void FlipCard(Graphics g, RectangleF r, string digits)
        {
            using (var p = Round(r, 3))
            {
                using (var b = new LinearGradientBrush(r, Color.FromArgb(0x48, 0x48, 0x45), Color.FromArgb(0x26, 0x26, 0x24), 90f))
                    g.FillPath(b, p);
                // The top flap is a shade lighter than the bottom one
                var oldClip = g.Clip;
                g.SetClip(new RectangleF(r.Left, r.Top, r.Width, r.Height / 2));
                using (var b = new SolidBrush(Color.FromArgb(20, 255, 255, 255))) g.FillPath(b, p);
                g.Clip = oldClip;
                Text(g, digits, 14.5f, new PointF(r.Left + r.Width / 2, r.Top + r.Height / 2), r.Width - 2.5f, Brushes.White);
                using (var pen = new Pen(Color.FromArgb(0x10, 0x10, 0x10), 0.9f))
                    g.DrawLine(pen, r.Left, r.Top + r.Height / 2, r.Right, r.Top + r.Height / 2);
                using (var pen = new Pen(Color.FromArgb(90, 0, 0, 0), 0.5f)) g.DrawPath(pen, p);
            }
        }

        static string MonthLabel(DateTime t)
        {
            var c = CultureInfo.CurrentCulture;
            string m = c.DateTimeFormat.GetAbbreviatedMonthName(t.Month).Trim().TrimEnd('.');
            if (m.Length == 0) m = t.Month.ToString(CultureInfo.InvariantCulture);
            return m.ToUpper(c);
        }

        static void CalendarPage(Graphics g, DateTime t)
        {
            var page = new RectangleF(2.5f, 33, 35, 35);
            // The page underneath, which shows at the curled corner
            using (var p = Round(page, 4))
            using (var b = new SolidBrush(Color.FromArgb(0xc9, 0xc7, 0xbe)))
                g.FillPath(b, p);

            // The top page, its bottom-right corner curled up
            var top = new PointF(37.5f, 58.5f);
            var left = new PointF(28.5f, 68);
            using (var p = new GraphicsPath())
            {
                p.AddArc(2.5f, 33, 8, 8, 180, 90);
                p.AddArc(29.5f, 33, 8, 8, 270, 90);
                p.AddLine(37.5f, 37, top.X, top.Y);
                AddQuad(p, top, new PointF(31, 62), left);
                p.AddArc(2.5f, 60, 8, 8, 90, 90);
                p.CloseFigure();
                using (var b = new LinearGradientBrush(page, Color.White, Color.FromArgb(0xe6, 0xe5, 0xe0), 90f))
                    g.FillPath(b, p);
            }
            // The back of the curl
            using (var p = new GraphicsPath())
            {
                AddQuad(p, left, new PointF(31, 62), top);
                AddQuad(p, top, new PointF(32.5f, 65), left);
                p.CloseFigure();
                using (var b = new LinearGradientBrush(new RectangleF(28, 58, 10, 10), Color.FromArgb(0xb8, 0xb6, 0xae), Color.FromArgb(0xf4, 0xf3, 0xef), 45f))
                    g.FillPath(b, p);
            }

            // Blue binding with the month
            var band = new RectangleF(2.5f, 33, 35, 10.5f);
            using (var p = RoundTop(band, 4))
            using (var b = new LinearGradientBrush(band, BlueLight, BlueDark, 90f))
                g.FillPath(b, p);
            Text(g, MonthLabel(t), 7.4f, new PointF(20, 39), 26, Brushes.White);

            // Two rings
            foreach (float x in new[] { 11f, 29f })
            {
                var ring = new RectangleF(x - 1.9f, 31.6f, 3.8f, 3.8f);
                using (var b = new LinearGradientBrush(ring, Color.FromArgb(0xf2, 0xf2, 0xf0), Color.FromArgb(0x7c, 0x7b, 0x76), 90f))
                    g.FillEllipse(b, ring);
                using (var pen = new Pen(Color.FromArgb(0x55, 0x55, 0x52), 0.4f)) g.DrawEllipse(pen, ring);
            }

            using (var b = new SolidBrush(Blue))
                Text(g, t.Day.ToString(CultureInfo.InvariantCulture), 20, new PointF(19.5f, 55), 30, b);
        }

        public static void DrawTile(Graphics g, DateTime t, bool hour24)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            int h = t.Hour;
            if (!hour24) { h %= 12; if (h == 0) h = 12; }
            FlipCard(g, new RectangleF(2.5f, 3, 16.5f, 24), hour24 ? h.ToString("00") : h.ToString(CultureInfo.InvariantCulture));
            FlipCard(g, new RectangleF(21, 3, 16.5f, 24), t.Minute.ToString("00"));
            CalendarPage(g, t);
        }

        // The tile as a bitmap with see-through background, scale 1 = 40 × 70 pixels
        public static Bitmap Tile(float scale, DateTime t, bool hour24)
        {
            int w = Math.Max(1, (int)Math.Round(W * scale)), h = Math.Max(1, (int)Math.Round(H * scale));
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                // Almost invisible fill, so the gaps between the pieces still catch the mouse
                g.Clear(Color.FromArgb(1, 0, 0, 0));
                g.ScaleTransform(w / W, h / H);
                DrawTile(g, t, hour24);
            }
            return bmp;
        }

        // App icon: the calendar page alone
        public static void DrawAppIcon(Graphics g, int size)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float k = size / 38f;
            g.ScaleTransform(k, k);
            g.TranslateTransform(-1, -31);
            CalendarPage(g, new DateTime(2026, 10, 31));
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

    // ── Saved settings, in HKCU\Software\Ergosfera\ClockTile ──
    static class Settings
    {
        const string Key = @"Software\Ergosfera\ClockTile";

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
        readonly Label today = new Label();

        public CalendarForm()
        {
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            Text = "Clock Tile";
            month.MaxSelectionCount = 1;
            month.ShowWeekNumbers = true;
            month.Location = new Point(0, 0);
            today.AutoSize = false;
            today.TextAlign = ContentAlignment.MiddleCenter;
            today.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            Controls.Add(month);
            Controls.Add(today);
        }

        public void ShowNear(Rectangle tile)
        {
            DateTime now = DateTime.Now;
            month.SetDate(now);
            month.TodayDate = now.Date;
            today.Text = now.ToString("D", CultureInfo.CurrentCulture);
            int pad = month.Height / 24;
            today.SetBounds(0, month.Bottom + pad, month.Width, today.PreferredHeight + pad);
            ClientSize = new Size(month.Width, today.Bottom + pad);

            // Beside the tile, on the side with more room, kept on the screen
            Rectangle wa = Screen.FromRectangle(tile).WorkingArea;
            Rectangle all = Screen.FromRectangle(tile).Bounds;
            int gap = Math.Max(4, tile.Width / 5);
            int cx = tile.Left + tile.Width / 2, cy = tile.Top + tile.Height / 2;
            int x = cx < all.Left + all.Width / 2 ? tile.Right + gap : tile.Left - Width - gap;
            int y = cy - Height / 2;
            if (cx >= wa.Left && cx <= wa.Right && (cy > wa.Bottom || cy < wa.Top))
            {
                // On a taskbar at the bottom or top: above or below it instead
                x = cx - Width / 2;
                y = cy > wa.Bottom ? tile.Top - Height - gap : tile.Bottom + gap;
            }
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

    // ── The tile: a per-pixel transparent window that stays on top ──
    class TileForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

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
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);

        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaClockTile";
        static readonly float[] Sizes = { 1f, 1.25f, 1.5f, 1.75f, 2f, 2.5f };

        readonly Timer timer = new Timer();
        readonly ToolTip tip = new ToolTip();
        readonly CalendarForm calendar = new CalendarForm();
        readonly ToolStripMenuItem hour24Item, lockItem, startItem, sizeItem;
        float scale;
        bool hour24, locked, hiddenForFullScreen;
        string shown = "";

        public TileForm()
        {
            float k;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            scale = Settings.GetFloat("Scale", k);
            string pattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
            hour24 = Settings.GetInt("Hour24", pattern.Contains("H") ? 1 : 0) != 0;
            locked = Settings.GetInt("Locked", 0) != 0;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Text = "Clock Tile";
            Size = TileSize();
            Location = StartLocation();

            var menu = new ContextMenuStrip();
            menu.Items.Add("Show calendar", null, delegate { calendar.ShowNear(Bounds); });
            menu.Items.Add(new ToolStripSeparator());
            hour24Item = new ToolStripMenuItem("24-hour clock", null, delegate
            {
                hour24 = !hour24;
                Settings.Set("Hour24", hour24 ? 1 : 0);
                Redraw(true);
            });
            menu.Items.Add(hour24Item);
            sizeItem = new ToolStripMenuItem("Size");
            foreach (float s in Sizes)
            {
                float size = s;
                var item = new ToolStripMenuItem(string.Format(CultureInfo.InvariantCulture, "{0} × {1} pixels",
                    Math.Round(Art.W * s), Math.Round(Art.H * s)), null, delegate { SetScale(size); });
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
            menu.Items.Add("Quit Clock Tile", null, delegate { Quit(); });
            menu.Opening += delegate { RefreshMenu(); };
            ContextMenuStrip = menu;

            tip.ShowAlways = true;

            // Twice a second: the minute turns over on time, and the tile gets back on top of the taskbar
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

        Size TileSize()
        {
            return new Size(Math.Max(1, (int)Math.Round(Art.W * scale)), Math.Max(1, (int)Math.Round(Art.H * scale)));
        }

        // Where it was left; the first time, on the taskbar near the clock
        Point StartLocation()
        {
            Size size = TileSize();
            int x = Settings.GetInt("X", int.MinValue), y = Settings.GetInt("Y", int.MinValue);
            if (x != int.MinValue && y != int.MinValue)
                foreach (Screen sc in Screen.AllScreens)
                    if (sc.Bounds.IntersectsWith(new Rectangle(x, y, size.Width, size.Height)))
                        return new Point(x, y);

            Rectangle all = Screen.PrimaryScreen.Bounds, wa = Screen.PrimaryScreen.WorkingArea;
            int below = (int)(80 * scale);
            if (wa.Left > all.Left)       // taskbar on the left
                return new Point(all.Left + (wa.Left - all.Left - size.Width) / 2, wa.Bottom - size.Height - below);
            if (wa.Right < all.Right)     // taskbar on the right
                return new Point(wa.Right + (all.Right - wa.Right - size.Width) / 2, wa.Bottom - size.Height - below);
            if (wa.Top > all.Top)         // taskbar at the top
                return new Point(wa.Right - size.Width - 3 * below, all.Top + (wa.Top - all.Top - size.Height) / 2);
            if (wa.Bottom < all.Bottom)   // taskbar at the bottom
                return new Point(wa.Right - size.Width - 3 * below, wa.Bottom + (all.Bottom - wa.Bottom - size.Height) / 2);
            return new Point(wa.Right - size.Width - below, wa.Bottom - size.Height - below);
        }

        void SetScale(float s)
        {
            scale = s;
            Settings.Set("Scale", s);
            Size = TileSize();
            Redraw(true);
        }

        void Redraw(bool force)
        {
            DateTime now = DateTime.Now;
            string key = now.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + hour24 + scale;
            if (!force && key == shown) return;
            shown = key;
            tip.SetToolTip(this, now.ToString("D", CultureInfo.CurrentCulture) + "\n" + now.ToString("t", CultureInfo.CurrentCulture));
            using (var bmp = Art.Tile(scale, now, hour24)) Present(bmp);
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

        // A full-screen window in front (a video, a slide show) on the tile's screen
        bool FullScreenInFront()
        {
            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero || fg == Handle || fg == calendar.Handle) return false;
            var name = new StringBuilder(64);
            GetClassName(fg, name, name.Capacity);
            string cls = name.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd" || cls == "Shell_SecondaryTrayWnd") return false;
            RECT r;
            if (!GetWindowRect(fg, out r)) return false;
            Rectangle screen = Screen.FromHandle(Handle).Bounds;
            return r.Left <= screen.Left && r.Top <= screen.Top && r.Right >= screen.Right && r.Bottom >= screen.Bottom;
        }

        void Tick()
        {
            bool full = FullScreenInFront();
            if (full != hiddenForFullScreen)
            {
                hiddenForFullScreen = full;
                if (full) Hide();
                else { Show(); shown = ""; }
            }
            if (full) return;
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
            if (Location != before)
            {
                Settings.Set("X", Location.X);
                Settings.Set("Y", Location.Y);
            }
            else ToggleCalendar();
        }

        void ToggleCalendar()
        {
            // Clicking the tile first deactivates the open calendar; don't reopen it right away
            if (calendar.Visible) calendar.HideCalendar();
            else if ((DateTime.UtcNow - calendar.HiddenAt).TotalMilliseconds > 300) calendar.ShowNear(Bounds);
        }

        void RefreshMenu()
        {
            hour24Item.Checked = hour24;
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
                MessageBox.Show("Couldn't change Start with Windows.\n\n" + ex.Message, "Clock Tile");
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
