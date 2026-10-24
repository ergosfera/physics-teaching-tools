// Net LEDs for Windows: two little lights in the system tray, like the activity LEDs on an old modem
// (twin of ../mac/main.swift). Green blinks when data comes in (download), red when data goes out
// (upload); a click opens a live graph. Builds with the C# compiler that ships with Windows
// (.NET Framework 4.x); see build.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Net LEDs")]
[assembly: AssemblyProduct("Net LEDs")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace NetLeds
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
            if (args.Length == 1 && args[0] == "--selftest")
            {
                long rx, tx;
                Net.Read(out rx, out tx);
                return rx > 0 || tx > 0 ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--render-graph")
            {
                GraphArt.RenderSample(args[1]);
                return 0;
            }

            // One copy at a time: a second launch just exits
            bool fresh;
            using (var single = new Mutex(true, "ErgosferaNetLEDs", out fresh))
            {
                if (!fresh) return 0;
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new TrayApp());
            }
            return 0;
        }
    }

    // ── Byte counters of the network adapters (loopback and tunnels left out) ──
    static class Net
    {
        static NetworkInterface[] nics;
        static DateTime listed = DateTime.MinValue;
        // Changes whenever the adapter list is re-read, so a jump in the totals isn't shown as traffic
        public static int Generation;

        public static void Read(out long rx, out long tx)
        {
            if (nics == null || (DateTime.UtcNow - listed).TotalSeconds > 5)
            {
                var list = new List<NetworkInterface>();
                try
                {
                    foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                        if (n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                            list.Add(n);
                }
                catch { }
                var ids = string.Join(",", list.ConvertAll(n => n.Id).ToArray());
                var old = nics == null ? null : string.Join(",", Array.ConvertAll(nics, n => n.Id));
                if (ids != old) Generation++;
                nics = list.ToArray();
                listed = DateTime.UtcNow;
            }
            rx = 0; tx = 0;
            foreach (var n in nics)
            {
                try
                {
                    var s = n.GetIPStatistics();
                    rx += s.BytesReceived;
                    tx += s.BytesSent;
                }
                catch { }
            }
        }

        public static string Rate(double bytesPerSec)
        {
            string[] units = { "B/s", "KB/s", "MB/s", "GB/s" };
            double v = bytesPerSec;
            int i = 0;
            while (v >= 1000 && i < units.Length - 1) { v /= 1000; i++; }
            return string.Format(CultureInfo.InvariantCulture, i == 0 ? "{0:0} {1}" : "{0:0.0} {1}", v, units[i]);
        }
    }

    // ── Lamps, the tray icons and the app icon ──
    static class Art
    {
        public static readonly Color Green = Color.FromArgb(51, 255, 77);
        public static readonly Color Red = Color.FromArgb(255, 56, 46);

        static Color Mix(Color a, Color b, float t)
        {
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        // level 0 = dark, 1 = full brightness
        public static void Lamp(Graphics g, Color color, float level, PointF c, float r)
        {
            Color dim = Mix(color, Color.Black, 0.78f);
            Color lit = Mix(dim, color, level);
            if (level > 0.05f)
            {
                // Glow around the lit lamp
                var glow = new RectangleF(c.X - r * 1.7f, c.Y - r * 1.7f, r * 3.4f, r * 3.4f);
                using (var p = new GraphicsPath())
                {
                    p.AddEllipse(glow);
                    using (var b = new PathGradientBrush(p))
                    {
                        b.CenterColor = Color.FromArgb((int)(140 * level), color);
                        b.SurroundColors = new[] { Color.FromArgb(0, color) };
                        g.FillEllipse(b, glow);
                    }
                }
            }
            var body = new RectangleF(c.X - r, c.Y - r, 2 * r, 2 * r);
            using (var b = new SolidBrush(lit)) g.FillEllipse(b, body);
            using (var pen = new Pen(Color.FromArgb(140, 0, 0, 0), Math.Max(0.8f, r * 0.15f))) g.DrawEllipse(pen, body);
            // Little highlight, like a plastic dome
            using (var b = new SolidBrush(Color.FromArgb((int)(255 * (0.25f + 0.35f * level)), 255, 255, 255)))
                g.FillEllipse(b, c.X - r * 0.55f, c.Y - r * 0.6f, r * 0.7f, r * 0.55f);
        }

        // Tray picture: green lamp on the left, red on the right
        public static Bitmap Tray(int size, float rx, float tx)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                float r = size * 0.21f;
                Lamp(g, Green, rx, new PointF(size * 0.26f, size * 0.5f), r);
                Lamp(g, Red, tx, new PointF(size * 0.74f, size * 0.5f), r);
            }
            return bmp;
        }

        public static void DrawAppIcon(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.04f;
            using (var p = GraphArt.Round(new RectangleF(m, s * 0.22f, s - 2 * m, s * 0.56f), s * 0.14f))
            {
                using (var b = new SolidBrush(Color.FromArgb(0x2b, 0x2b, 0x2b))) g.FillPath(b, p);
                using (var pen = new Pen(Color.FromArgb(0x6e, 0x6e, 0x6e), Math.Max(1f, s * 0.02f))) g.DrawPath(pen, p);
            }
            Lamp(g, Green, 1, new PointF(s * 0.3f, s * 0.5f), s * 0.13f);
            Lamp(g, Red, 1, new PointF(s * 0.7f, s * 0.5f), s * 0.13f);
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

    // ── Live graph: download grows up in green, upload grows down in red ──
    static class GraphArt
    {
        public const int Samples = 120;   // 60 s at two samples a second
        public const int W = 420, H = 220;
        public const string Hint = "Last 60 s · drag to keep it open · right-click the lights for options";

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

        // Round the scale up to 1, 2 or 5 × a power of ten, so the grid labels are friendly
        public static double NiceMax(double v)
        {
            v = Math.Max(v, 1000);
            double p = Math.Pow(10, Math.Floor(Math.Log10(v)));
            foreach (double m in new[] { 1.0, 2.0, 5.0, 10.0 })
                if (m * p >= v) return m * p;
            return 10 * p;
        }

        static double Max(List<double> d)
        {
            double m = 0;
            foreach (double v in d) m = Math.Max(m, v);
            return m;
        }

        // k is the screen scale (1 at 100 %); closeBox gets where the × was drawn
        public static void Draw(Graphics g, float k, List<double> rx, List<double> tx, out RectangleF closeBox)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float w = W * k, h = H * k;
            using (var b = new SolidBrush(Color.FromArgb(0x2b, 0x2b, 0x2b))) g.FillRectangle(b, 0, 0, w, h);
            using (var pen = new Pen(Color.FromArgb(0x55, 0x55, 0x55))) g.DrawRectangle(pen, 0, 0, w - 1, h - 1);

            double nowRx = rx.Count > 0 ? rx[rx.Count - 1] : 0, nowTx = tx.Count > 0 ? tx[tx.Count - 1] : 0;
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                using (var b = new SolidBrush(Art.Green)) g.DrawString("↓ " + Net.Rate(nowRx), f, b, 10 * k, 6 * k);
                string up = "↑ " + Net.Rate(nowTx);
                var size = g.MeasureString(up, f);
                using (var b = new SolidBrush(Art.Red)) g.DrawString(up, f, b, w - 34 * k - size.Width, 6 * k);
            }
            closeBox = new RectangleF(w - 28 * k, 6 * k, 20 * k, 20 * k);
            using (var pen = new Pen(Color.FromArgb(0x9a, 0x9a, 0x9a), 1.5f * k))
            {
                float c = 5 * k;
                PointF mid = new PointF(closeBox.X + closeBox.Width / 2, closeBox.Y + closeBox.Height / 2);
                g.DrawLine(pen, mid.X - c, mid.Y - c, mid.X + c, mid.Y + c);
                g.DrawLine(pen, mid.X - c, mid.Y + c, mid.X + c, mid.Y - c);
            }

            var plot = new RectangleF(12 * k, 32 * k, w - 24 * k, h - 58 * k);
            using (var path = Round(plot, 6 * k))
            {
                using (var b = new SolidBrush(Color.FromArgb(217, 0, 0, 0))) g.FillPath(b, path);
                float midY = plot.Top + plot.Height / 2, half = plot.Height / 2 - 4 * k;
                double topRx = NiceMax(Max(rx)), topTx = NiceMax(Max(tx));

                // Grid: center line, half and full scale on each side
                using (var pen = new Pen(Color.FromArgb(26, 255, 255, 255), 0.5f * k))
                    foreach (float f in new[] { -1f, -0.5f, 0.5f, 1f })
                        g.DrawLine(pen, plot.Left, midY + f * half, plot.Right, midY + f * half);
                using (var pen = new Pen(Color.FromArgb(77, 255, 255, 255), 0.8f * k))
                    g.DrawLine(pen, plot.Left, midY, plot.Right, midY);

                var oldClip = g.Clip;
                g.SetClip(path);
                Area(g, plot, midY, half, rx, topRx, -1, Art.Green);
                Area(g, plot, midY, half, tx, topTx, 1, Art.Red);
                g.Clip = oldClip;

                // Scale labels at full height of each side
                using (var f = new Font("Segoe UI", 7f))
                using (var b = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                {
                    g.DrawString(Net.Rate(topRx), f, b, plot.Left + 4 * k, midY - half);
                    g.DrawString(Net.Rate(topTx), f, b, plot.Left + 4 * k, midY + half - 13 * k);
                }
            }

            using (var f = new Font("Segoe UI", 7.5f))
            using (var b = new SolidBrush(Color.FromArgb(0x9a, 0x9a, 0x9a)))
                g.DrawString(Hint, f, b, 10 * k, h - 22 * k);
        }

        // sign -1 draws upward (download), +1 downward (upload)
        static void Area(Graphics g, RectangleF plot, float midY, float half, List<double> data, double top, int sign, Color color)
        {
            if (data.Count < 2) return;
            float step = plot.Width / (Samples - 1);
            float x0 = plot.Right - (data.Count - 1) * step;
            var pts = new PointF[data.Count];
            for (int i = 0; i < data.Count; i++)
                pts[i] = new PointF(x0 + i * step, midY + sign * (float)Math.Min(data[i] / top, 1) * half);

            using (var fill = new GraphicsPath())
            {
                fill.AddLines(pts);
                fill.AddLine(plot.Right, midY, x0, midY);
                fill.CloseFigure();
                var far = new PointF(0, midY + sign * (half + 1));
                using (var b = new LinearGradientBrush(far, new PointF(0, midY), Color.FromArgb(140, color), Color.FromArgb(20, color)))
                    g.FillPath(b, fill);
            }
            using (var pen = new Pen(color, 1.5f * (plot.Height / (H - 58))))
            {
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, pts);
            }
        }

        // --render-graph out.png: draws the graph with made-up traffic, to check how it looks
        public static void RenderSample(string path)
        {
            var rx = new List<double>();
            var tx = new List<double>();
            for (int i = 0; i < Samples; i++)
            {
                rx.Add(Math.Max(0, 2.5e6 * (Math.Sin(i / 9.0) + 1) + (i % 17 == 0 ? 4e6 : 0)));
                tx.Add(Math.Max(0, 2e5 * (Math.Cos(i / 5.0) + 1.2)));
            }
            using (var bmp = new Bitmap(W, H))
            {
                RectangleF unused;
                using (var g = Graphics.FromImage(bmp)) Draw(g, 1, rx, tx, out unused);
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }

    class GraphForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public readonly List<double> Rx = new List<double>();
        public readonly List<double> Tx = new List<double>();
        public bool Pinned;            // dragged away from the tray: stays open until closed
        public DateTime HiddenAt = DateTime.MinValue;
        readonly float k;
        RectangleF closeBox;

        public GraphForm(float scale)
        {
            k = scale;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            BackColor = Color.FromArgb(0x2b, 0x2b, 0x2b);
            ClientSize = new Size((int)(GraphArt.W * k), (int)(GraphArt.H * k));
            Text = "Net LEDs";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Rounded corners on Windows 11 (ignored elsewhere)
            try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        }

        public void Push(double rx, double tx)
        {
            Rx.Add(rx); Tx.Add(tx);
            if (Rx.Count > GraphArt.Samples) { Rx.RemoveAt(0); Tx.RemoveAt(0); }
            if (Visible) Invalidate();
        }

        public void ShowNearTray()
        {
            if (!Pinned)
            {
                // Next to the taskbar corner the pointer is in, whichever side the taskbar is on
                Point cur = Cursor.Position;
                Rectangle wa = Screen.FromPoint(cur).WorkingArea;
                int gap = (int)(8 * k);
                int x = Math.Max(wa.Left + gap, Math.Min(cur.X - Width / 2, wa.Right - Width - gap));
                int y = cur.Y > wa.Top + wa.Height / 2 ? wa.Bottom - Height - gap : wa.Top + gap;
                if (cur.X >= wa.Right) x = wa.Right - Width - gap;
                if (cur.X < wa.Left) x = wa.Left + gap;
                Location = new Point(x, y);
            }
            Show();
            Activate();
        }

        public void HideGraph()
        {
            Pinned = false;
            HiddenAt = DateTime.UtcNow;
            Hide();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            GraphArt.Draw(e.Graphics, k, Rx, Tx, out closeBox);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (closeBox.Contains(e.Location)) { HideGraph(); return; }
            // Drag anywhere to move it; once moved it stays open
            Point before = Location;
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN on the caption
            if (Location != before) Pinned = true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!Pinned) HideGraph();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) HideGraph();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; HideGraph(); }
            else base.OnFormClosing(e);
        }
    }

    // ── The app: tray lamps, the menu and the graph ──
    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaNetLEDs";
        static readonly float[] Levels = { 0f, 0.4f, 0.7f, 1f };

        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer timer = new Timer();
        readonly Icon[,] icons = new Icon[4, 4];
        readonly ToolStripLabel downItem = new ToolStripLabel();
        readonly ToolStripLabel upItem = new ToolStripLabel();
        readonly ToolStripMenuItem startItem;
        readonly GraphForm graph;
        readonly Stopwatch clock = Stopwatch.StartNew();

        long lastRx, lastTx, halfRx, halfTx;
        double halfAt;
        int gen;
        double rxRate, txRate;
        float rxLevel, txLevel;
        int shownRx = -1, shownTx = -1;

        public TrayApp()
        {
            float k;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            graph = new GraphForm(k);

            // All 16 lamp combinations are drawn once, so blinking costs nothing
            int size = SystemInformation.SmallIconSize.Width;
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                    using (var bmp = Art.Tray(size, Levels[a], Levels[b]))
                        icons[a, b] = Icon.FromHandle(bmp.GetHicon());

            var menu = new ContextMenuStrip();
            downItem.ForeColor = Color.FromArgb(0, 150, 40);
            upItem.ForeColor = Color.FromArgb(200, 30, 20);
            menu.Items.Add(downItem);
            menu.Items.Add(upItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Show graph", null, delegate { graph.ShowNearTray(); });
            startItem = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            menu.Items.Add(startItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Net LEDs", null, delegate { Quit(); });
            menu.Opening += delegate { RefreshMenu(); };

            tray.ContextMenuStrip = menu;
            tray.Icon = icons[0, 0];
            tray.Text = "Net LEDs";
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleGraph(); };
            tray.Visible = true;

            Net.Read(out lastRx, out lastTx);
            halfRx = lastRx; halfTx = lastTx;
            gen = Net.Generation;
            RefreshMenu();

            // 30 ticks a second: a burst of packets makes the lamp flicker, a steady stream keeps it lit
            timer.Interval = 33;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        void ToggleGraph()
        {
            // Clicking the tray first deactivates the open graph; don't reopen it right away
            if (graph.Visible) graph.HideGraph();
            else if ((DateTime.UtcNow - graph.HiddenAt).TotalMilliseconds > 300) graph.ShowNearTray();
        }

        static float Level(long bytes, float before)
        {
            // Any traffic in this tick lights the lamp; with nothing it fades out quickly
            if (bytes > 0) return Math.Min(1f, 0.55f + 0.09f * (float)Math.Log10(bytes));
            float l = before * 0.35f;
            return l < 0.03f ? 0 : l;
        }

        static int Index(float level)
        {
            return level < 0.08f ? 0 : level < 0.5f ? 1 : level < 0.8f ? 2 : 3;
        }

        void Tick()
        {
            long rx, tx;
            Net.Read(out rx, out tx);
            if (Net.Generation != gen)
            {
                // An adapter came or went: start counting again from here
                gen = Net.Generation;
                lastRx = halfRx = rx; lastTx = halfTx = tx;
                halfAt = clock.Elapsed.TotalSeconds;
                return;
            }
            rxLevel = Level(Math.Max(0, rx - lastRx), rxLevel);
            txLevel = Level(Math.Max(0, tx - lastTx), txLevel);
            lastRx = rx; lastTx = tx;

            // Two samples a second feed the speeds and the graph (history keeps filling while it's closed)
            double now = clock.Elapsed.TotalSeconds, dt = now - halfAt;
            if (dt >= 0.5)
            {
                rxRate = Math.Max(0, rx - halfRx) / dt;
                txRate = Math.Max(0, tx - halfTx) / dt;
                halfRx = rx; halfTx = tx; halfAt = now;
                graph.Push(rxRate, txRate);
                tray.Text = "↓ " + Net.Rate(rxRate) + "   ↑ " + Net.Rate(txRate);
                RefreshMenu();
            }

            int a = Index(rxLevel), b = Index(txLevel);
            if (a != shownRx || b != shownTx)
            {
                shownRx = a; shownTx = b;
                tray.Icon = icons[a, b];
            }
        }

        void RefreshMenu()
        {
            downItem.Text = "●  Download   " + Net.Rate(rxRate);
            upItem.Text = "●  Upload        " + Net.Rate(txRate);
            startItem.Checked = StartsWithWindows();
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
                MessageBox.Show("Couldn't change Start with Windows.\n\n" + ex.Message, "Net LEDs");
            }
            RefreshMenu();
        }

        void Quit()
        {
            timer.Stop();
            tray.Visible = false;
            tray.Dispose();
            graph.Dispose();
            ExitThread();
        }
    }
}
