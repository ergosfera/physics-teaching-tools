// Disk LED for Windows: the hard-disk activity light of an old PC, in the system tray
// (twin of ../mac/main.swift). It blinks when the drives read or write, like the LED on the front of a
// computer with a mechanical disk; brighter with more data. A click opens a live graph.
// Reads the drives' own counters (the ones Task Manager uses), so it works with SSDs, without admin rights.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Disk LED")]
[assembly: AssemblyProduct("Disk LED")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace DiskLed
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
                DiskStats s = Disk.Read();
                Console.WriteLine("drives " + s.Drives + " read " + s.ReadBytes + " B, write " + s.WriteBytes + " B");
                return s.Drives > 0 ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--render-graph")
            {
                GraphArt.RenderSample(args[1]);
                return 0;
            }

            // One copy at a time: a second launch just exits
            bool fresh;
            using (var single = new Mutex(true, "ErgosferaDiskLED", out fresh))
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

    // ── Drive counters (since Windows started, all physical drives together) ──
    struct DiskStats
    {
        public long ReadBytes, WriteBytes, ReadOps, WriteOps;
        public long BusyTicks;      // time spent reading + writing, in 100 ns units
        public int Queue;           // operations waiting right now
        public int Drives;
    }

    static class Disk
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr inBuf, int inSize, byte[] outBuf, int outSize, out int returned, IntPtr overlapped);

        const uint IOCTL_DISK_PERFORMANCE = 0x00070020;   // CTL_CODE(IOCTL_DISK_BASE, 8, METHOD_BUFFERED, FILE_ANY_ACCESS)
        static readonly byte[] buf = new byte[128];
        static List<SafeFileHandle> drives = new List<SafeFileHandle>();
        static DateTime listed = DateTime.MinValue;
        // Changes whenever the drive list changes, so a jump in the totals isn't shown as activity
        public static int Generation;
        static string lastIds = "";

        static void RefreshList()
        {
            foreach (var h in drives) h.Dispose();
            var found = new List<SafeFileHandle>();
            var ids = new List<string>();
            for (int i = 0; i < 32; i++)
            {
                // No access rights needed for the performance counters, so no admin
                SafeFileHandle h = CreateFile(@"\\.\PhysicalDrive" + i, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
                if (h.IsInvalid) { h.Dispose(); continue; }
                int got;
                if (DeviceIoControl(h, IOCTL_DISK_PERFORMANCE, IntPtr.Zero, 0, buf, buf.Length, out got, IntPtr.Zero) && got >= 52)
                {
                    found.Add(h);
                    ids.Add(i.ToString(CultureInfo.InvariantCulture));
                }
                else h.Dispose();
            }
            drives = found;
            string joined = string.Join(",", ids.ToArray());
            if (joined != lastIds) { Generation++; lastIds = joined; }
            listed = DateTime.UtcNow;
        }

        public static DiskStats Read()
        {
            if ((DateTime.UtcNow - listed).TotalSeconds > 5) RefreshList();
            var s = new DiskStats();
            foreach (var h in drives)
            {
                int got;
                if (!DeviceIoControl(h, IOCTL_DISK_PERFORMANCE, IntPtr.Zero, 0, buf, buf.Length, out got, IntPtr.Zero) || got < 52) continue;
                // DISK_PERFORMANCE: BytesRead, BytesWritten, ReadTime, WriteTime, IdleTime (64-bit),
                // then ReadCount, WriteCount, QueueDepth (32-bit)
                s.ReadBytes += BitConverter.ToInt64(buf, 0);
                s.WriteBytes += BitConverter.ToInt64(buf, 8);
                s.BusyTicks += BitConverter.ToInt64(buf, 16) + BitConverter.ToInt64(buf, 24);
                s.ReadOps += BitConverter.ToUInt32(buf, 40);
                s.WriteOps += BitConverter.ToUInt32(buf, 44);
                s.Queue += (int)BitConverter.ToUInt32(buf, 48);
                s.Drives++;
            }
            return s;
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

    // ── Drive sounds: short synthesized clicks, like the heads of a hard disk seeking ──
    static class Clicks
    {
        const int Rate = 22050;
        static SoundPlayer[] players;

        // A few milliseconds of decaying noise over a low thump; each variant a little different
        static byte[] Wav(uint seed, double length, double thump, double volume)
        {
            int n = (int)(Rate * length);
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + n * 2);
            w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
            w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(n * 2);
            uint x = seed;
            double lp = 0;
            for (int i = 0; i < n; i++)
            {
                x = unchecked(x * 1664525 + 1013904223);
                double noise = (int)x / (double)int.MaxValue;
                lp += (noise - lp) * 0.45;                         // soften the hiss
                double t = (double)i / Rate;
                double env = Math.Exp(-t / (length * 0.22));
                double low = Math.Sin(2 * Math.PI * thump * t) * Math.Exp(-t / (length * 0.35));
                double v = Math.Max(-1, Math.Min(1, (lp * 0.55 + low * 0.45) * env));
                w.Write((short)(v * 9000 * volume));
            }
            w.Flush();
            return ms.ToArray();
        }

        public static void Play(Random rng)
        {
            if (players == null)
            {
                var list = new List<SoundPlayer>();
                double[][] specs = { new[] { 11, 0.010, 180, 0.5 }, new[] { 23, 0.007, 240, 0.35 }, new[] { 37, 0.013, 150, 0.6 }, new[] { 41, 0.006, 300, 0.3 } };
                foreach (double[] s in specs)
                {
                    var p = new SoundPlayer(new MemoryStream(Wav((uint)s[0], s[1], s[2], s[3])));
                    try { p.Load(); list.Add(p); } catch { }
                }
                players = list.ToArray();
            }
            if (players.Length == 0) return;
            try { players[rng.Next(players.Length)].Play(); } catch { }
        }
    }

    // ── Lamps, the tray icons and the app icon ──
    static class Art
    {
        public static readonly Color Amber = Color.FromArgb(255, 168, 26);
        public static readonly Color Red = Color.FromArgb(255, 56, 46);
        public static readonly Color Green = Color.FromArgb(51, 255, 77);
        public static readonly Color Blue = Color.FromArgb(64, 140, 255);

        public static Color Mix(Color a, Color b, float t)
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

        // One lamp in the middle, or read (green) on the left and write (red) on the right
        public static Bitmap Tray(int size, Color one, bool pair, float read, float write)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                if (pair)
                {
                    float r = size * 0.21f;
                    Lamp(g, Green, read, new PointF(size * 0.26f, size * 0.5f), r);
                    Lamp(g, Red, write, new PointF(size * 0.74f, size * 0.5f), r);
                }
                else Lamp(g, one, Math.Max(read, write), new PointF(size * 0.5f, size * 0.5f), size * 0.3f);
            }
            return bmp;
        }

        // The front of an old PC case: a beige bezel with a drive slot and two lamps
        public static void DrawAppIcon(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float inset = s * 0.098f;
            using (var p = GraphArt.Round(new RectangleF(inset, inset, s - 2 * inset, s - 2 * inset), s * 0.16f))
            using (var b = new SolidBrush(Color.FromArgb(212, 209, 199))) g.FillPath(b, p);
            using (var p = GraphArt.Round(new RectangleF(s * 0.2f, s * 0.34f, s * 0.6f, s * 0.1f), Math.Max(0.5f, s * 0.02f)))
            using (var b = new SolidBrush(Color.FromArgb(64, 64, 64))) g.FillPath(b, p);
            using (var b = new SolidBrush(Color.FromArgb(115, 115, 115))) g.FillRectangle(b, s * 0.24f, s * 0.38f, s * 0.52f, Math.Max(1, s * 0.02f));
            Lamp(g, Green, 0.9f, new PointF(s * 0.34f, s * 0.67f), s * 0.075f);
            Lamp(g, Amber, 1, new PointF(s * 0.66f, s * 0.67f), s * 0.075f);
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

    // ── Live graph: reading grows up, writing grows down ──
    static class GraphArt
    {
        public const int Samples = 120;   // 60 s at two samples a second
        public const int W = 420, H = 220;
        public const string Hint = "Last 60 s · drag to keep it open · right-click the lamp for options";

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
            v = Math.Max(v, 100000);
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
        public static void Draw(Graphics g, float k, List<double> rd, List<double> wr, double busy, int queue,
                                Color readColor, Color writeColor, out RectangleF closeBox)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            float w = W * k, h = H * k;
            using (var b = new SolidBrush(Color.FromArgb(0x2b, 0x2b, 0x2b))) g.FillRectangle(b, 0, 0, w, h);
            using (var pen = new Pen(Color.FromArgb(0x55, 0x55, 0x55))) g.DrawRectangle(pen, 0, 0, w - 1, h - 1);

            double nowR = rd.Count > 0 ? rd[rd.Count - 1] : 0, nowW = wr.Count > 0 ? wr[wr.Count - 1] : 0;
            using (var f = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                using (var b = new SolidBrush(readColor)) g.DrawString("R " + Disk.Rate(nowR), f, b, 10 * k, 6 * k);
                string busyText = "Busy " + Math.Round(busy * 100) + " %" + (queue > 0 ? " · queue " + queue : "");
                var bs = g.MeasureString(busyText, f);
                using (var b = new SolidBrush(busy > 0.9 ? Art.Amber : Color.White)) g.DrawString(busyText, f, b, (w - bs.Width) / 2, 6 * k);
                string wt = "W " + Disk.Rate(nowW);
                var size = g.MeasureString(wt, f);
                using (var b = new SolidBrush(writeColor)) g.DrawString(wt, f, b, w - 34 * k - size.Width, 6 * k);
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
                double topR = NiceMax(Max(rd)), topW = NiceMax(Max(wr));

                using (var pen = new Pen(Color.FromArgb(26, 255, 255, 255), 0.5f * k))
                    foreach (float f in new[] { -1f, -0.5f, 0.5f, 1f })
                        g.DrawLine(pen, plot.Left, midY + f * half, plot.Right, midY + f * half);
                using (var pen = new Pen(Color.FromArgb(77, 255, 255, 255), 0.8f * k))
                    g.DrawLine(pen, plot.Left, midY, plot.Right, midY);

                var oldClip = g.Clip;
                g.SetClip(path);
                Area(g, plot, midY, half, rd, topR, -1, readColor);
                Area(g, plot, midY, half, wr, topW, 1, writeColor);
                g.Clip = oldClip;

                using (var f = new Font("Segoe UI", 7f))
                using (var b = new SolidBrush(Color.FromArgb(140, 255, 255, 255)))
                {
                    g.DrawString("read " + Disk.Rate(topR), f, b, plot.Left + 4 * k, midY - half);
                    g.DrawString("write " + Disk.Rate(topW), f, b, plot.Left + 4 * k, midY + half - 13 * k);
                }
            }

            using (var f = new Font("Segoe UI", 7.5f))
            using (var b = new SolidBrush(Color.FromArgb(0x9a, 0x9a, 0x9a)))
                g.DrawString(Hint, f, b, 10 * k, h - 22 * k);
        }

        // sign -1 draws upward (read), +1 downward (write)
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

        // --render-graph out.png: draws the graph with made-up activity, to check how it looks
        public static void RenderSample(string path)
        {
            var rd = new List<double>();
            var wr = new List<double>();
            for (int i = 0; i < Samples; i++)
            {
                rd.Add(Math.Max(0, 40e6 * (Math.Sin(i / 9.0) + 1) + (i % 23 == 0 ? 300e6 : 0)));
                wr.Add(Math.Max(0, 8e6 * (Math.Cos(i / 5.0) + 1.1)));
            }
            using (var bmp = new Bitmap(W, H))
            {
                RectangleF unused;
                using (var g = Graphics.FromImage(bmp)) Draw(g, 1, rd, wr, 0.37, 0, Art.Amber, Art.Mix(Art.Amber, Color.White, 0.35f), out unused);
                bmp.Save(path, ImageFormat.Png);
            }
        }
    }

    class GraphForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wp, IntPtr lp);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public readonly List<double> Rd = new List<double>();
        public readonly List<double> Wr = new List<double>();
        public double Busy;
        public int Queue;
        public Color ReadColor = Art.Amber, WriteColor = Art.Amber;
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
            Text = "Disk LED";
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // Rounded corners on Windows 11 (ignored elsewhere)
            try { int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, 4); } catch { }
        }

        public void Push(double rd, double wr, double busy, int queue)
        {
            Rd.Add(rd); Wr.Add(wr);
            if (Rd.Count > GraphArt.Samples) { Rd.RemoveAt(0); Wr.RemoveAt(0); }
            Busy = busy; Queue = queue;
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
            GraphArt.Draw(e.Graphics, k, Rd, Wr, Busy, Queue, ReadColor, WriteColor, out closeBox);
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

    // ── The app: tray lamp, the menu and the graph ──
    class TrayApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaDiskLED";
        const string SettingsKey = @"Software\Ergosfera\DiskLED";
        static readonly float[] Levels = { 0f, 0.4f, 0.7f, 1f };
        static readonly string[] StyleNames = { "amber", "red", "green", "blue", "pair" };
        static readonly string[] StyleTitles = { "One lamp: amber", "One lamp: red", "One lamp: green", "One lamp: blue", "Two lamps: read (green) and write (red)" };
        static readonly Color[] StyleColors = { Art.Amber, Art.Red, Art.Green, Art.Blue, Art.Green };

        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer timer = new Timer();
        readonly Icon[,] icons = new Icon[4, 4];
        readonly ToolStripLabel readItem = new ToolStripLabel();
        readonly ToolStripLabel writeItem = new ToolStripLabel();
        readonly ToolStripLabel busyItem = new ToolStripLabel();
        readonly ToolStripMenuItem startItem, soundItem;
        readonly List<ToolStripMenuItem> styleItems = new List<ToolStripMenuItem>();
        readonly GraphForm graph;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly Random rng = new Random();

        DiskStats last, half;
        double halfAt, lastClick = -1;
        int gen;
        double readRate, writeRate, busy;
        int queue;
        float readLevel, writeLevel;
        int shownRead = -1, shownWrite = -1;
        int style;
        bool sound;

        public TrayApp()
        {
            float k;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) k = g.DpiX / 96f;
            graph = new GraphForm(k);
            style = Math.Max(0, Array.IndexOf(StyleNames, GetSetting("style") ?? "amber"));
            sound = GetSetting("sound") == "1";

            var menu = new ContextMenuStrip();
            menu.Items.Add(readItem);
            menu.Items.Add(writeItem);
            menu.Items.Add(busyItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Show graph", null, delegate { graph.ShowNearTray(); });
            menu.Items.Add(new ToolStripSeparator());
            for (int i = 0; i < StyleNames.Length; i++)
            {
                int pick = i;
                var item = new ToolStripMenuItem(StyleTitles[i], null, delegate { SetStyle(pick); });
                styleItems.Add(item);
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            soundItem = new ToolStripMenuItem("Drive sounds", null, delegate { sound = !sound; SetSetting("sound", sound ? "1" : "0"); RefreshMenu(); });
            soundItem.ToolTipText = "Soft clicks with each burst of activity, like the heads of a hard disk seeking";
            menu.Items.Add(soundItem);
            startItem = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            menu.Items.Add(startItem);
            menu.Items.Add("About Disk LED", null, delegate { About(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Quit Disk LED", null, delegate { Quit(); });
            menu.Opening += delegate { RefreshMenu(); };

            tray.ContextMenuStrip = menu;
            tray.Text = "Disk LED";
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ToggleGraph(); };
            MakeIcons();
            tray.Icon = icons[0, 0];
            tray.Visible = true;

            last = half = Disk.Read();
            gen = Disk.Generation;
            RefreshMenu();

            // 30 ticks a second: each burst of disk activity lights the lamp for a moment, steady work keeps it lit
            timer.Interval = 33;
            timer.Tick += delegate { Tick(); };
            timer.Start();
        }

        static string GetSetting(string name)
        {
            try { using (var key = Registry.CurrentUser.OpenSubKey(SettingsKey)) { object v = key == null ? null : key.GetValue(name); return v == null ? null : v.ToString(); } }
            catch { return null; }
        }

        static void SetSetting(string name, string value)
        {
            try { using (var key = Registry.CurrentUser.CreateSubKey(SettingsKey)) key.SetValue(name, value); } catch { }
        }

        // All lamp combinations are drawn once, so blinking costs nothing
        void MakeIcons()
        {
            int size = SystemInformation.SmallIconSize.Width;
            bool pair = StyleNames[style] == "pair";
            for (int a = 0; a < 4; a++)
                for (int b = 0; b < 4; b++)
                {
                    Icon old = icons[a, b];
                    using (var bmp = Art.Tray(size, StyleColors[style], pair, Levels[a], Levels[b]))
                        icons[a, b] = Icon.FromHandle(bmp.GetHicon());
                    if (old != null) old.Dispose();
                }
            graph.ReadColor = pair ? Art.Green : StyleColors[style];
            graph.WriteColor = pair ? Art.Red : Art.Mix(StyleColors[style], Color.White, 0.35f);
            shownRead = shownWrite = -1;
        }

        void SetStyle(int i)
        {
            style = i;
            SetSetting("style", StyleNames[i]);
            MakeIcons();
            RefreshMenu();
        }

        void ToggleGraph()
        {
            // Clicking the tray first deactivates the open graph; don't reopen it right away
            if (graph.Visible) graph.HideGraph();
            else if ((DateTime.UtcNow - graph.HiddenAt).TotalMilliseconds > 300) graph.ShowNearTray();
        }

        // Brighter with more data: a few kilobytes give a dim blink, megabytes a full one
        static float Level(long bytes, long ops, float before)
        {
            if (ops > 0 || bytes > 0) return Math.Min(1f, 0.5f + 0.08f * (float)Math.Log10(Math.Max(bytes, 512)));
            float l = before * 0.35f;
            return l < 0.03f ? 0 : l;
        }

        static int Index(float level)
        {
            return level < 0.08f ? 0 : level < 0.5f ? 1 : level < 0.8f ? 2 : 3;
        }

        void Tick()
        {
            DiskStats now = Disk.Read();
            if (Disk.Generation != gen)
            {
                // A drive came or went: start counting again from here
                gen = Disk.Generation;
                last = half = now;
                halfAt = clock.Elapsed.TotalSeconds;
                return;
            }
            long dro = Math.Max(0, now.ReadOps - last.ReadOps), dwo = Math.Max(0, now.WriteOps - last.WriteOps);
            readLevel = Level(Math.Max(0, now.ReadBytes - last.ReadBytes), dro, readLevel);
            writeLevel = Level(Math.Max(0, now.WriteBytes - last.WriteBytes), dwo, writeLevel);
            last = now;

            // Drive sounds: a click for a burst, not more than about 20 a second, with some randomness
            double t = clock.Elapsed.TotalSeconds;
            if (sound && (dro > 0 || dwo > 0) && t - lastClick > 0.045 && rng.NextDouble() < 0.75)
            {
                Clicks.Play(rng);
                lastClick = t;
            }

            // Two samples a second feed the speeds, the busy share and the graph
            double dt = t - halfAt;
            if (dt >= 0.5)
            {
                readRate = Math.Max(0, now.ReadBytes - half.ReadBytes) / dt;
                writeRate = Math.Max(0, now.WriteBytes - half.WriteBytes) / dt;
                busy = Math.Min(1, Math.Max(0, now.BusyTicks - half.BusyTicks) / 1e7 / dt);
                queue = now.Queue;
                half = now; halfAt = t;
                graph.Push(readRate, writeRate, busy, queue);
                string tip = "Read " + Disk.Rate(readRate) + "\nWrite " + Disk.Rate(writeRate) + "\nBusy " + Math.Round(busy * 100) + " %";
                tray.Text = tip.Length > 63 ? tip.Substring(0, 63) : tip;
                RefreshMenu();
            }

            int a = Index(readLevel), b = Index(writeLevel);
            if (a != shownRead || b != shownWrite)
            {
                shownRead = a; shownWrite = b;
                tray.Icon = icons[a, b];
            }
        }

        void RefreshMenu()
        {
            readItem.Text = "●  Read     " + Disk.Rate(readRate);
            writeItem.Text = "●  Write    " + Disk.Rate(writeRate);
            busyItem.Text = "●  Busy     " + Math.Round(busy * 100) + " %" + (queue > 0 ? "   (queue " + queue + ")" : "");
            readItem.ForeColor = Art.Mix(graph.ReadColor, Color.Black, 0.3f);
            writeItem.ForeColor = Art.Mix(graph.WriteColor, Color.Black, 0.3f);
            busyItem.ForeColor = busy > 0.9 ? Color.FromArgb(190, 110, 0) : SystemColors.GrayText;
            for (int i = 0; i < styleItems.Count; i++) styleItems[i].Checked = i == style;
            soundItem.Checked = sound;
            startItem.Checked = StartsWithWindows();
        }

        void About()
        {
            MessageBox.Show(
                "The hard-disk activity light of an old PC, for any drive, SSDs included. It blinks when the drives read or write, " +
                "brighter with more data. \"Busy\" is the share of time the drives spent working, and \"queue\" the operations waiting: " +
                "near 100 % for a while, or a long queue, means the drives are the bottleneck.\n\n" +
                "It reads the drives' own activity counters, the same ones Task Manager uses. It collects no data and never connects to the internet.\n\n" +
                "© 2026 Mauricio Mendivelso. MIT License.", "About Disk LED 1.0");
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
                MessageBox.Show("Couldn't change Start with Windows.\n\n" + ex.Message, "Disk LED");
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
