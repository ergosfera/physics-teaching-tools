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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Countdown Timer")]
[assembly: AssemblyProduct("Countdown Timer")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

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
            // One copy of the app holds all the timers. Opening it again asks that copy for a new timer.
            var newTimerSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Ergosfera.CountdownTimer.NewTimer");
            bool first;
            using (var mutex = new Mutex(true, @"Local\Ergosfera.CountdownTimer", out first))
            {
                if (!first) { newTimerSignal.Set(); return; }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                var app = new TimerApp();
                var listener = new Thread(delegate ()
                {
                    while (newTimerSignal.WaitOne()) app.PostNewTimer();
                }) { IsBackground = true };
                listener.Start();
                Application.Run(app);
                GC.KeepAlive(mutex);
            }
        }
    }

    // Holds every open timer and saves them all to one file.
    class TimerApp : ApplicationContext
    {
        readonly List<TimerForm> forms = new List<TimerForm>();
        readonly SynchronizationContext sync;
        bool loading;
        public bool Exiting;
        public TimerForm Current;

        public TimerApp()
        {
            loading = true;
            var saved = Store.Load();
            if (saved.Count == 0) saved.Add(new TimerData());
            foreach (var d in saved) Add(d);
            loading = false;
            Save();
            sync = SynchronizationContext.Current;
        }

        public int Count { get { return forms.Count; } }

        TimerForm Add(TimerData d)
        {
            var f = new TimerForm(this, d);
            forms.Add(f);
            f.Show();
            return f;
        }

        public void PostNewTimer()
        {
            if (sync != null) sync.Post(delegate { NewTimer(Current); }, null);
        }

        public void NewTimer(TimerForm source)
        {
            if (source == null || source.IsDisposed) source = forms.Count > 0 ? forms[forms.Count - 1] : null;
            var d = source != null ? source.Data.Copy() : new TimerData();
            if (source != null) d.PlaceNear(source.Bounds, source.BaseHeight);
            var f = Add(d);
            f.Activate();
            Save();
        }

        public void Save()
        {
            if (loading) return;
            var list = new List<TimerData>();
            foreach (var f in forms) list.Add(f.Snapshot());
            Store.Save(list);
        }

        public void ExitAll()
        {
            Exiting = true;
            Save();
            foreach (var f in forms.ToArray()) f.Close();
        }

        // Called when a timer window has closed. "removed" means the user closed that one timer for good.
        public void Gone(TimerForm f, bool removed)
        {
            forms.Remove(f);
            if (removed) Save();
            if (forms.Count == 0) ExitThread();
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

    // Everything about one timer that is kept between runs.
    class TimerData
    {
        public string Id = Guid.NewGuid().ToString("N");
        public int Hours = 0, Minutes = 5, Seconds = 0;
        public int YellowPct = 30, RedPct = 10;
        public bool TopMost = true, Sound = true;
        public string Note = "";
        public int X = int.MinValue, Y = int.MinValue;
        // How the countdown stood when it was saved (times in UTC ticks).
        public string Run = "";
        public long EndsAt, Total, Banked;

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

        public static int Clamp(int v, int lo, int hi) { return v < lo ? lo : v > hi ? hi : v; }

        // A new timer starts with the same time and settings as the one it came from, but no note.
        public TimerData Copy()
        {
            return new TimerData
            {
                Hours = Hours, Minutes = Minutes, Seconds = Seconds,
                YellowPct = YellowPct, RedPct = RedPct, TopMost = TopMost, Sound = Sound
            };
        }

        // Next to the timer it came from: above and to the left, or below if there is no room above.
        public void PlaceNear(Rectangle src, int newHeight)
        {
            X = src.X - 30;
            int up = src.Y - 10 - newHeight;
            bool fits = false;
            foreach (var scr in Screen.AllScreens)
                if (scr.WorkingArea.Contains(X + 40, up)) fits = true;
            Y = fits ? up : src.Bottom + 10;
        }

        public void Set(string key, string val)
        {
            int n; bool b; long l;
            bool isInt = int.TryParse(val, out n), isBool = bool.TryParse(val, out b), isLong = long.TryParse(val, out l);
            switch (key)
            {
                case "Id": if (val.Length > 0) Id = val; break;
                case "Hours": if (isInt) Hours = Clamp(n, 0, 99); break;
                case "Minutes": if (isInt) Minutes = Clamp(n, 0, 59); break;
                case "Seconds": if (isInt) Seconds = Clamp(n, 0, 59); break;
                case "YellowPct": if (isInt) YellowPct = Clamp(n, 1, 100); break;
                case "RedPct": if (isInt) RedPct = Clamp(n, 0, 99); break;
                case "TopMost": if (isBool) TopMost = b; break;
                case "Sound": if (isBool) Sound = b; break;
                case "Note": Note = Unescape(val); break;
                case "X": if (isInt) X = n; break;
                case "Y": if (isInt) Y = n; break;
                case "Run": Run = val; break;
                case "EndsAt": if (isLong) EndsAt = l; break;
                case "Total": if (isLong) Total = l; break;
                case "Banked": if (isLong) Banked = l; break;
            }
        }

        public void Fix() { if (RedPct >= YellowPct) RedPct = Math.Max(0, YellowPct - 1); }

        public IEnumerable<string> Lines()
        {
            return new[] {
                "Id=" + Id, "Hours=" + Hours, "Minutes=" + Minutes, "Seconds=" + Seconds,
                "YellowPct=" + YellowPct, "RedPct=" + RedPct, "TopMost=" + TopMost, "Sound=" + Sound,
                "Note=" + Escape(Note), "X=" + X, "Y=" + Y,
                "Run=" + Run, "EndsAt=" + EndsAt, "Total=" + Total, "Banked=" + Banked };
        }

        static string Escape(string s) { return s.Replace("\\", "\\\\").Replace("\r", "").Replace("\n", "\\n"); }

        static string Unescape(string s)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length) { i++; sb.Append(s[i] == 'n' ? '\n' : s[i]); }
                else sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }

    static class Store
    {
        static string Dir
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CountdownTimer"); }
        }
        static string TimersPath { get { return Path.Combine(Dir, "timers.ini"); } }
        static string OldPath { get { return Path.Combine(Dir, "settings.ini"); } }   // version 1.1 and earlier: one timer

        public static List<TimerData> Load()
        {
            var list = new List<TimerData>();
            try
            {
                bool old = !File.Exists(TimersPath);
                if (old && !File.Exists(OldPath)) return list;
                TimerData cur = old ? new TimerData() : null;
                if (old) list.Add(cur);
                foreach (string line in File.ReadAllLines(old ? OldPath : TimersPath))
                {
                    if (line.Trim() == "[timer]") { cur = new TimerData(); list.Add(cur); continue; }
                    int i = line.IndexOf('=');
                    if (i < 0 || cur == null) continue;
                    cur.Set(line.Substring(0, i).Trim(), line.Substring(i + 1));
                }
            }
            catch { }
            foreach (var d in list) d.Fix();
            return list;
        }

        public static void Save(List<TimerData> timers)
        {
            try
            {
                Directory.CreateDirectory(Dir);
                var lines = new List<string>();
                foreach (var t in timers)
                {
                    lines.Add("[timer]");
                    lines.AddRange(t.Lines());
                    lines.Add("");
                }
                File.WriteAllLines(TimersPath, lines.ToArray(), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // A note is plain text; web addresses and [name](address) become links.
    static class NoteText
    {
        static readonly Regex Pattern = new Regex(@"\[([^\]\n]+)\]\(([^()\s]+)\)|(?:https?://|www\.)[^\s<>""]+", RegexOptions.IgnoreCase);

        public class Link { public int Start, Length; public string Url; }

        public static string Url(string s)
        {
            return Regex.IsMatch(s, @"^[a-z][a-z0-9+.-]*:", RegexOptions.IgnoreCase) ? s : "https://" + s;
        }

        // Long bare addresses are shown short; the link still opens the full address.
        static string ShortLabel(string raw)
        {
            string s = raw;
            foreach (string p in new[] { "https://", "http://" })
                if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase)) s = s.Substring(p.Length);
            if (s.EndsWith("/")) s = s.Substring(0, s.Length - 1);
            return s.Length > 36 ? s.Substring(0, 35) + "\u2026" : s;
        }

        static int Count(string s, char c) { int n = 0; foreach (char x in s) if (x == c) n++; return n; }

        // Returns the text to show and fills in where the links are in it.
        public static string Parse(string src, List<Link> links)
        {
            var sb = new StringBuilder();
            int last = 0;
            foreach (Match m in Pattern.Matches(src))
            {
                sb.Append(src, last, m.Index - last);
                last = m.Index + m.Length;
                string label, target, trailing = "";
                if (m.Groups[1].Success)
                {
                    label = m.Groups[1].Value;
                    target = m.Groups[2].Value;
                }
                else
                {
                    // Punctuation right after an address belongs to the sentence, not the address.
                    string raw = m.Value;
                    while (raw.Length > 0)
                    {
                        char c = raw[raw.Length - 1];
                        if (".,;:!?'\"".IndexOf(c) < 0 && !(c == ')' && Count(raw, '(') < Count(raw, ')'))) break;
                        trailing = c + trailing;
                        raw = raw.Substring(0, raw.Length - 1);
                    }
                    label = ShortLabel(raw);
                    target = raw;
                }
                if (label.Length == 0) { sb.Append(m.Value); continue; }
                links.Add(new Link { Start = sb.Length, Length = label.Length, Url = Url(target) });
                sb.Append(label).Append(trailing);
            }
            sb.Append(src, last, src.Length - last);
            return sb.ToString();
        }
    }

    // A LinkLabel that can tell whether a point is on a link.
    class NoteLabel : LinkLabel
    {
        public string LinkAt(Point p)
        {
            var l = PointInLink(p.X, p.Y);
            return l == null ? null : l.LinkData as string;
        }
    }

    class TimerForm : Form
    {
        enum Hit { None, Close, Min, Pin, Gear, End, NoteBtn, Seg0, Seg1, Seg2, Main, Reset }
        enum State { Idle, Running, Paused, Finished }

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("user32.dll")] static extern bool FlashWindowEx(ref FLASHWINFO info);

        [StructLayout(LayoutKind.Sequential)]
        struct FLASHWINFO { public uint cbSize; public IntPtr hwnd; public uint dwFlags; public uint uCount; public uint dwTimeout; }

        const string GlyphClose = "", GlyphMin = "", GlyphPin = "", GlyphPinned = "", GlyphGear = "";

        const string GlyphNote = "\uE70F";   // Edit (pencil)
        const string GlyphClock = "";

        readonly TimerApp app;
        readonly TimerData cfg;
        readonly float k;
        readonly NoteLabel noteLabel;
        bool removed;          // the user closed this timer for good
        DateTime lastNoteClick;
        Point lastNoteClickAt;
        State state = State.Idle;
        readonly Stopwatch sw = new Stopwatch();
        readonly Stopwatch finishClock = new Stopwatch();
        TimeSpan total, banked;
        DateTime? endUtc;   // set while counting to a clock time instead of a duration
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
        ToolStripMenuItem miTop, miSound, miNote, miOpenLink, miCopyLink;
        ToolStripSeparator sepLink;
        string menuLink;

        public TimerData Data { get { return cfg; } }
        public int BaseHeight { get { return S(130); } }

        public TimerForm(TimerApp owner, TimerData data)
        {
            app = owner;
            cfg = data;
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

            tip = new ToolTip { InitialDelay = 600, ReshowDelay = 200 };

            // The note sits under the buttons; the window grows to fit it.
            noteLabel = new NoteLabel
            {
                AutoSize = false,
                Location = new Point(S(14), S(136)),
                Width = S(252),
                BackColor = Palette.Bg,
                ForeColor = Color.FromArgb(205, 208, 216),
                LinkColor = Color.FromArgb(110, 175, 255),
                ActiveLinkColor = Color.FromArgb(160, 205, 255),
                VisitedLinkColor = Color.FromArgb(110, 175, 255),
                LinkBehavior = LinkBehavior.AlwaysUnderline,
                Font = new Font("Segoe UI", 13 * k, GraphicsUnit.Pixel),
                Padding = Padding.Empty,
                Margin = Padding.Empty,
                Visible = false
            };
            noteLabel.LinkClicked += delegate (object s, LinkLabelLinkClickedEventArgs e) { OpenLink(e.Link.LinkData as string); };
            noteLabel.MouseDown += NoteMouseDown;
            noteLabel.MouseMove += delegate (object s, MouseEventArgs e)
            {
                string url = noteLabel.LinkAt(e.Location);
                string t = url ?? "Double-click to edit the note";
                if (tip.GetToolTip(noteLabel) != t) tip.SetToolTip(noteLabel, t);
            };
            Controls.Add(noteLabel);

            BuildMenu();
            noteLabel.ContextMenuStrip = ContextMenuStrip;
            LayoutNote();
            PlaceWindow();
            RestoreRun();

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
        Rectangle RcEnd { get { return R(140, 0, 28, 26); } }
        Rectangle RcNote { get { return R(112, 0, 28, 26); } }
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
            miOpenLink = new ToolStripMenuItem("Open link", null, delegate { OpenLink(menuLink); });
            miCopyLink = new ToolStripMenuItem("Copy link", null, delegate { try { Clipboard.SetText(menuLink); } catch { } });
            sepLink = new ToolStripSeparator();
            menu.Items.Add(miOpenLink);
            menu.Items.Add(miCopyLink);
            menu.Items.Add(sepLink);
            menu.Items.Add(new ToolStripMenuItem("New timer", null, delegate { app.NewTimer(this); }) { ShortcutKeyDisplayString = "Ctrl+N" });
            miNote = new ToolStripMenuItem("Add note...", null, delegate { OpenNote(); }) { ShortcutKeyDisplayString = "N" };
            menu.Items.Add(miNote);
            menu.Items.Add(new ToolStripSeparator());
            menu.Opening += delegate
            {
                menuLink = noteLabel.Visible ? noteLabel.LinkAt(noteLabel.PointToClient(Cursor.Position)) : null;
                miOpenLink.Visible = miCopyLink.Visible = sepLink.Visible = menuLink != null;
                miNote.Text = cfg.Note.Length == 0 ? "Add note..." : "Edit note...";
            };
            miTop = new ToolStripMenuItem("Always on top", null, delegate { SetTopMost(!TopMost); });
            miTop.Checked = TopMost;
            miSound = new ToolStripMenuItem("Sound at the end", null, delegate { cfg.Sound = !cfg.Sound; miSound.Checked = cfg.Sound; app.Save(); });
            miSound.Checked = cfg.Sound;
            var miEnd = new ToolStripMenuItem("Set end time...", null, delegate { OpenEndTime(); });
            menu.Opening += delegate { miEnd.Enabled = state == State.Idle || state == State.Finished; };
            menu.Items.Add(miEnd);
            menu.Items.Add(miTop);
            menu.Items.Add(miSound);
            menu.Items.Add(new ToolStripMenuItem("Settings...", null, delegate { OpenSettings(); }));
            menu.Items.Add(new ToolStripMenuItem("About Countdown Timer", null, delegate
            {
                using (var about = new AboutForm()) about.ShowDialog(this);
            }));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Close this timer", null, delegate { Close(); }) { ShortcutKeyDisplayString = "Ctrl+W" });
            menu.Items.Add(new ToolStripMenuItem("Exit (keeps all timers for next time)", null, delegate { app.ExitAll(); }));
            ContextMenuStrip = menu;
        }

        // ---- Note ----

        void LayoutNote()
        {
            string src = cfg.Note.Trim().Replace("\r\n", "\n").Replace("\n", "\r\n");
            var links = new List<NoteText.Link>();
            string text = NoteText.Parse(src, links);
            noteLabel.Text = text;
            noteLabel.Links.Clear();   // after the text: setting the text makes it all one link again
            foreach (var l in links) noteLabel.Links.Add(l.Start, l.Length, l.Url);
            if (text.Length == 0)
            {
                noteLabel.Visible = false;
                ClientSize = new Size(S(280), S(130));
            }
            else
            {
                int h = Math.Min(S(220), noteLabel.GetPreferredSize(new Size(S(252), 0)).Height);
                noteLabel.Height = h;
                noteLabel.Visible = true;
                ClientSize = new Size(S(280), S(136) + h + S(12));
            }
            int nl = text.IndexOf('\r');
            string first = nl >= 0 ? text.Substring(0, nl) : text;
            Text = first.Length > 0 ? first : "Countdown Timer";
            Invalidate();
        }

        void OpenLink(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try { Process.Start(url); }
            catch { MessageBox.Show(this, "Could not open " + url, "Countdown Timer", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        }

        void NoteMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || noteLabel.LinkAt(e.Location) != null) return;
            // A second click soon after, in about the same place, edits the note.
            var now = DateTime.Now;
            var d = SystemInformation.DoubleClickSize;
            if ((now - lastNoteClick).TotalMilliseconds <= SystemInformation.DoubleClickTime &&
                Math.Abs(e.X - lastNoteClickAt.X) <= d.Width && Math.Abs(e.Y - lastNoteClickAt.Y) <= d.Height)
            {
                lastNoteClick = DateTime.MinValue;
                OpenNote();
                return;
            }
            lastNoteClick = now;
            lastNoteClickAt = e.Location;
            sel = -1;
            Invalidate();
            ReleaseCapture();
            SendMessage(Handle, 0xA1 /* WM_NCLBUTTONDOWN */, (IntPtr)2 /* HTCAPTION */, IntPtr.Zero);
        }

        void OpenNote()
        {
            using (var dlg = new NoteForm(cfg.Note))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                cfg.Note = dlg.Note.Replace("\r\n", "\n").Trim();
                LayoutNote();
                app.Save();
            }
        }

        // ---- Saving and restoring ----

        public TimerData Snapshot()
        {
            var loc = WindowState == FormWindowState.Normal ? Location : RestoreBounds.Location;
            cfg.X = loc.X;
            cfg.Y = loc.Y;
            cfg.Run = "";
            cfg.EndsAt = cfg.Total = cfg.Banked = 0;
            switch (state)
            {
                case State.Running:
                    // Saved as the moment it ends, so it keeps counting while the app is closed.
                    cfg.Run = "running";
                    cfg.EndsAt = (DateTime.UtcNow + Remaining()).Ticks;
                    cfg.Total = total.Ticks;
                    break;
                case State.Paused:
                    cfg.Run = "paused";
                    cfg.Total = total.Ticks;
                    cfg.Banked = banked.Ticks;
                    break;
                case State.Idle:
                    if (endUtc.HasValue) { cfg.Run = "until"; cfg.EndsAt = endUtc.Value.Ticks; }
                    break;
            }
            return cfg;
        }

        void RestoreRun()
        {
            var now = DateTime.UtcNow;
            switch (cfg.Run)
            {
                case "running":
                    if (cfg.Total <= 0 || cfg.EndsAt <= 0) break;
                    total = new TimeSpan(cfg.Total);
                    banked = TimeSpan.Zero;
                    sw.Reset();
                    sw.Start();
                    state = State.Running;
                    if (cfg.EndsAt > now.Ticks) endUtc = new DateTime(cfg.EndsAt, DateTimeKind.Utc);
                    else Finish();
                    break;
                case "paused":
                    if (cfg.Total <= 0) break;
                    total = new TimeSpan(cfg.Total);
                    banked = new TimeSpan(Math.Max(0, Math.Min(cfg.Total, cfg.Banked)));
                    state = State.Paused;
                    break;
                case "until":
                    if (cfg.EndsAt > now.Ticks) endUtc = new DateTime(cfg.EndsAt, DateTimeKind.Utc);
                    break;
            }
        }

        // A timer with a note or a countdown going is worth a question before closing it.
        bool WorthKeeping
        {
            get { return cfg.Note.Length > 0 || state == State.Running || state == State.Paused || endUtc.HasValue; }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            app.Current = this;
        }

        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            app.Save();   // the window was moved
        }

        // ---- Timer logic ----

        TimeSpan Remaining()
        {
            if (state == State.Idle && !endUtc.HasValue) return cfg.Duration;
            if (state == State.Finished) return TimeSpan.Zero;
            // Counting to a clock time follows the wall clock, so it stays right after sleep.
            var r = endUtc.HasValue ? endUtc.Value - DateTime.UtcNow : total - banked - sw.Elapsed;
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
            total = endUtc.HasValue ? endUtc.Value - DateTime.UtcNow : cfg.Duration;
            if (total <= TimeSpan.Zero) { SystemSounds.Beep.Play(); return; }
            banked = TimeSpan.Zero;
            sw.Reset();
            sw.Start();
            state = State.Running;
            sel = -1;
            beepsLeft = 0;
            app.Save();
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
                    // Pausing lets go of the clock time: resuming continues from what was left.
                    if (endUtc.HasValue) { banked = total - Remaining(); endUtc = null; }
                    else banked += sw.Elapsed;
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
            app.Save();
        }

        void DoReset()
        {
            sw.Reset();
            banked = TimeSpan.Zero;
            beepsLeft = 0;
            endUtc = null;
            state = State.Idle;
            UpdateTip();
            Invalidate();
            app.Save();
        }

        void Finish()
        {
            sw.Stop();
            endUtc = null;
            state = State.Finished;
            finishClock.Reset();
            finishClock.Start();
            beepsLeft = cfg.Sound ? 3 : 0;
            nextBeepMs = 0;
            FlashTaskbar();
            UpdateTip();
            app.Save();
        }

        void OnTick(object sender, EventArgs e)
        {
            if (state == State.Running)
            {
                if (Remaining() <= TimeSpan.Zero) Finish();
                Invalidate();
            }
            else if (state == State.Idle && endUtc.HasValue)
            {
                // Nobody pressed Start before the clock time passed: go back to the set duration.
                if (Remaining() <= TimeSpan.Zero) { endUtc = null; UpdateTip(); }
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
            app.Save();
            UpdateTip();
            Invalidate();
        }

        void OpenSettings()
        {
            using (var dlg = new SettingsForm(cfg, TopMost))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var before = cfg.Duration;
                dlg.ApplyTo(cfg);
                if (state == State.Idle && cfg.Duration != before) endUtc = null;
                miSound.Checked = cfg.Sound;
                SetTopMost(dlg.AlwaysOnTop);
                if (state == State.Finished) DoReset();
                Invalidate();
            }
        }

        void OpenEndTime()
        {
            if (state == State.Running || state == State.Paused) return;
            DateTime shown = endUtc.HasValue ? endUtc.Value.ToLocalTime() : DateTime.Now.Add(cfg.Duration);
            using (var dlg = new EndTimeForm(shown))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (state == State.Finished) DoReset();
                // A time that has already passed today means tomorrow.
                DateTime now = DateTime.Now;
                DateTime end = now.Date.AddHours(dlg.Picked.Hour).AddMinutes(dlg.Picked.Minute);
                if (end <= now) end = end.AddDays(1);
                endUtc = end.ToUniversalTime();
                sel = -1;
                UpdateTip();
                Invalidate();
                app.Save();
            }
        }

        // ---- Editing the time (only before starting) ----

        // Editing the digits while an end time is set: carry on from what they show, as a plain duration.
        void ReleaseEndTime()
        {
            if (!endUtc.HasValue) return;
            long s = (long)Math.Ceiling(Remaining().TotalSeconds - 1e-9);
            endUtc = null;
            cfg[0] = (int)(s / 3600);
            cfg[1] = (int)((s / 60) % 60);
            cfg[2] = (int)(s % 60);
        }

        void Step(int s, int delta)
        {
            ReleaseEndTime();
            int max = s == 0 ? 99 : 59;
            int v = cfg[s] + delta;
            if (v > max) v = 0;
            if (v < 0) v = max;
            cfg[s] = v;
            Invalidate();
            app.Save();
        }

        void TypeDigit(int d)
        {
            ReleaseEndTime();
            if (sel < 0) { sel = 0; typed = 0; }
            cfg[sel] = typed == 0 ? d : cfg[sel] * 10 + d;
            typed++;
            if (typed >= 2)
            {
                typed = 0;
                if (sel < 2) sel++;
            }
            Invalidate();
            app.Save();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            bool idle = state == State.Idle;

            if (keyData == Keys.Space || keyData == Keys.Enter) { sel = -1; MainAction(); return true; }
            if (keyData == Keys.R) { DoReset(); return true; }
            if (keyData == Keys.E) { OpenEndTime(); return true; }
            if (keyData == Keys.N) { OpenNote(); return true; }
            if (keyData == (Keys.Control | Keys.N)) { app.NewTimer(this); return true; }
            if (keyData == (Keys.Control | Keys.W)) { Close(); return true; }
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
                case Keys.Delete: if (sel >= 0) { ReleaseEndTime(); cfg[sel] = 0; typed = 0; Invalidate(); app.Save(); } return true;
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
            if (RcEnd.Contains(p)) return Hit.End;
            if (RcNote.Contains(p)) return Hit.NoteBtn;
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
                case Hit.End: OpenEndTime(); break;
                case Hit.NoteBtn: OpenNote(); break;
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
                case Hit.Close: t = "Close this timer"; break;
                case Hit.NoteBtn: t = cfg.Note.Length == 0 ? "Add a note (N)" : "Edit the note (N)"; break;
                case Hit.Min: t = "Minimize"; break;
                case Hit.Pin: t = TopMost ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"; break;
                case Hit.Gear: t = "Settings"; break;
                case Hit.End: t = state == State.Running || state == State.Paused ? "Reset first to set an end time" : "Set an end time (E)"; break;
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
            string title = endUtc.HasValue ? "Until " + endUtc.Value.ToLocalTime().ToString("t")
                : state == State.Idle ? "Timer" : "Timer · " + Fmt(total);
            TextRenderer.DrawText(g, title, fTitle, new Rectangle(S(10), 0, S(100), S(26)), Palette.Dim,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            DrawIconButton(g, RcNote, GlyphNote, Hit.NoteBtn, cfg.Note.Length > 0 ? Palette.Text : Palette.Dim);
            DrawIconButton(g, RcEnd, GlyphClock, Hit.End, endUtc.HasValue ? Palette.Text : Palette.Dim);
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
            bool canStart = state != State.Idle || endUtc.HasValue || cfg.Duration > TimeSpan.Zero;
            DrawButton(g, RcMain, MainLabel(), Hit.Main, canStart ? Palette.Text : Palette.Dim);
            DrawButton(g, RcReset, "Reset", Hit.Reset, state == State.Idle && !endUtc.HasValue ? Palette.Dim : Palette.Text);

            if (noteLabel.Visible)
                using (var b = new SolidBrush(Palette.Track))
                    g.FillRectangle(b, S(14), S(130), S(252), Math.Max(1, S(1)));
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
            if (!app.Exiting)
            {
                if (e.CloseReason == CloseReason.UserClosing && app.Count > 1)
                {
                    if (WorthKeeping)
                    {
                        int nl = cfg.Note.IndexOf('\n');
                        string first = nl >= 0 ? cfg.Note.Substring(0, nl) : cfg.Note;
                        string what = first.Length > 0 ? "\u201C" + first + "\u201D will be gone." : "Its countdown will be gone.";
                        if (MessageBox.Show(this, what, "Close this timer?", MessageBoxButtons.OKCancel,
                                MessageBoxIcon.Question) != DialogResult.OK) { e.Cancel = true; return; }
                    }
                    removed = true;
                }
                else
                {
                    // The last timer, or Windows is shutting down: keep everything for next time.
                    app.Exiting = true;
                    app.Save();
                }
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            tick.Stop();
            base.OnFormClosed(e);
            app.Gone(this, removed);
        }
    }

    class SettingsForm : Form
    {
        readonly NumericUpDown nH, nM, nS, nY, nR;
        readonly CheckBox cTop, cSound;

        public bool AlwaysOnTop { get { return cTop.Checked; } }

        public SettingsForm(TimerData s, bool topMost)
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

        public void ApplyTo(TimerData s)
        {
            s.Hours = (int)nH.Value;
            s.Minutes = (int)nM.Value;
            s.Seconds = (int)nS.Value;
            s.YellowPct = (int)nY.Value;
            s.RedPct = (int)nR.Value;
            s.Sound = cSound.Checked;
            s.TopMost = cTop.Checked;
        }
    }

    class EndTimeForm : Form
    {
        readonly DateTimePicker picker;

        public DateTime Picked { get { return picker.Value; } }

        public EndTimeForm(DateTime shown)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "End time";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(340, 136);

            Controls.Add(new Label { Text = "Count down until", Location = new Point(14, 17), AutoSize = true, Font = new Font("Segoe UI Semibold", 9F) });
            picker = new DateTimePicker
            {
                Format = DateTimePickerFormat.Custom,
                CustomFormat = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern,
                ShowUpDown = true,
                Value = shown,
                Location = new Point(130, 14),
                Width = 100
            };
            Controls.Add(picker);
            Controls.Add(new Label { Text = "A time that has already passed today means tomorrow.", Location = new Point(14, 54), AutoSize = true });

            var ok = new Button { Text = "OK", Location = new Point(164, 94), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Location = new Point(250, 94), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
            ResumeLayout(false);
        }
    }

    class NoteForm : Form
    {
        readonly TextBox box;

        public string Note { get { return box.Text; } }

        public NoteForm(string note)
        {
            SuspendLayout();
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9F);
            Text = "Note";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(380, 268);

            Controls.Add(new Label { Text = "What this timer is counting down to. It shows under the timer.", Location = new Point(14, 12), AutoSize = true });
            box = new TextBox
            {
                Multiline = true, AcceptsReturn = true, WordWrap = true, ScrollBars = ScrollBars.Vertical,
                Location = new Point(14, 36), Size = new Size(352, 140),
                Text = (note ?? "").Replace("\r\n", "\n").Replace("\n", "\r\n")
            };
            box.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; DialogResult = DialogResult.OK; }
                if (e.KeyCode == Keys.A && e.Control) { e.SuppressKeyPress = true; box.SelectAll(); }
            };
            Controls.Add(box);
            Controls.Add(new Label
            {
                Text = "Paste a web address to make a link, or write [name](address).\r\nCtrl+Enter saves.",
                Location = new Point(14, 182), Size = new Size(352, 36), ForeColor = SystemColors.GrayText
            });

            var ok = new Button { Text = "OK", Location = new Point(204, 228), Size = new Size(80, 28), DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "Cancel", Location = new Point(290, 228), Size = new Size(80, 28), DialogResult = DialogResult.Cancel };
            Controls.Add(ok);
            Controls.Add(cancel);
            CancelButton = cancel;
            ResumeLayout(false);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            box.Focus();
            box.SelectionStart = box.TextLength;
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
            Controls.Add(new Label { Text = "Version 1.2  ·  © 2026 Mauricio Mendivelso", Location = new Point(77, 44), AutoSize = true });
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
