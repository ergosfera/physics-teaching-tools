// Sticky Notes: sticky notes on the Windows desktop that sync through a Google Drive folder.
// Each note is a small text file in "My Drive\StickyNotes". The Mac app (../sticky-notes-mac)
// reads and writes the same files, and Google Drive for desktop copies them between computers.
// Builds with the C# compiler that ships with Windows (.NET Framework 4.x); see build.ps1.
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
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: AssemblyTitle("Sticky Notes")]
[assembly: AssemblyProduct("Sticky Notes")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mauricio Mendivelso. MIT License.")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace StickyNotes
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
            if (args.Length == 1 && args[0] == "--selftest")
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }
            string folder = null;
            if (args.Length == 2 && args[0] == "--folder") folder = Path.GetFullPath(args[1]);

            bool first;
            using (var mutex = new Mutex(true, "Ergosfera.StickyNotes", out first))
            using (var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Ergosfera.StickyNotes.Show"))
            {
                // Already running: ask that copy to bring its notes forward.
                if (!first) { showEvent.Set(); return; }
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new NotesApp(folder, showEvent));
            }
        }
    }

    // MARK: Note files (the Mac app uses the same format; see SelfTest)

    class NoteData
    {
        public string Id = "";
        public string Color = "yellow";
        public string Modified = "";
        public string By = "";
        public string Text = "";
    }

    // A note file is a short header, a "---" line, then the note text:
    //   color: yellow
    //   modified: 2026-10-18T14:03:22.123Z
    //   by: HOME-PC
    //   ---
    //   Buy milk
    // A plain .txt file without the header also works: all of it becomes the note text.
    static class NoteFile
    {
        static readonly Random Rng = new Random();

        public static string Now()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }

        public static string NewId()
        {
            return DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Rng.Next(0x10000).ToString("x4");
        }

        public static string TrashName(string id)
        {
            return id + " deleted " + DateTime.Now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".txt";
        }

        public static string Normalize(string s)
        {
            return s.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        public static string Serialize(NoteData n)
        {
            var sb = new StringBuilder();
            sb.Append("color: ").Append(n.Color).Append('\n');
            sb.Append("modified: ").Append(n.Modified).Append('\n');
            sb.Append("by: ").Append(n.By.Replace('\n', ' ')).Append('\n');
            sb.Append("---\n");
            sb.Append(Normalize(n.Text));
            return sb.ToString();
        }

        public static NoteData Parse(string id, string content)
        {
            var n = new NoteData { Id = id };
            string s = Normalize(content);
            if (s.Length > 0 && s[0] == '﻿') s = s.Substring(1);
            string header, body;
            if (s.StartsWith("---\n", StringComparison.Ordinal))
            {
                header = "";
                body = s.Substring(4);
            }
            else
            {
                int sep = s.IndexOf("\n---\n", StringComparison.Ordinal);
                if (sep < 0) { n.Text = s; return n; }
                header = s.Substring(0, sep);
                body = s.Substring(sep + 5);
            }

            var fields = new Dictionary<string, string>();
            foreach (string line in header.Split('\n'))
            {
                if (line.Length == 0) continue;
                int colon = line.IndexOf(':');
                bool key = colon > 0;
                for (int i = 0; key && i < colon; i++) key = line[i] >= 'a' && line[i] <= 'z';
                // Not a header after all (for example a plain note with a "---" line in it).
                if (!key) { n.Text = s; return n; }
                fields[line.Substring(0, colon)] = line.Substring(colon + 1).Trim();
            }
            string v;
            if (fields.TryGetValue("color", out v) && v.Length > 0) n.Color = v;
            if (fields.TryGetValue("modified", out v)) n.Modified = v;
            if (fields.TryGetValue("by", out v)) n.By = v;
            n.Text = body;
            return n;
        }
    }

    // StickyNotes.exe --selftest (run by build.ps1). The Mac app checks the same cases.
    static class SelfTest
    {
        public static int Run()
        {
            int fails = 0;
            var n = new NoteData { Id = "a", Color = "pink", Modified = "2026-10-18T10:00:00.000Z", By = "MAC", Text = "Hello\r\n---\nworld: yes" };
            string file = NoteFile.Serialize(n);
            if (file != "color: pink\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes") fails++;
            var back = NoteFile.Parse("a", file);
            if (back.Color != "pink" || back.Modified != n.Modified || back.By != "MAC" || back.Text != "Hello\n---\nworld: yes") fails++;
            var plain = NoteFile.Parse("b", "Just text\r\nline 2");
            if (plain.Text != "Just text\nline 2" || plain.Color != "yellow" || plain.Modified != "") fails++;
            var bom = NoteFile.Parse("c", "﻿color: blue\nmodified: x\nby: y\n---\n");
            if (bom.Color != "blue" || bom.Text != "" || bom.Modified != "x" || bom.By != "y") fails++;
            var list = NoteFile.Parse("d", "Shopping list\n---\nmilk");
            if (list.Text != "Shopping list\n---\nmilk" || list.Color != "yellow") fails++;
            var bodyOnly = NoteFile.Parse("e", "---\nonly body");
            if (bodyOnly.Text != "only body" || bodyOnly.Color != "yellow") fails++;
            return fails;
        }
    }

    // MARK: Look

    static class NoteColors
    {
        public static readonly string[] Names = { "yellow", "green", "pink", "purple", "blue", "gray" };
        public static readonly string[] Labels = { "Yellow", "Green", "Pink", "Purple", "Blue", "Gray" };
        static readonly int[] BackHex = { 0xFFF7AA, 0xD4F2C4, 0xFFD3E2, 0xE3D7FA, 0xCDE6FF, 0xE6E6E6 };
        static readonly int[] HeaderHex = { 0xF7E479, 0xB8E3A3, 0xF9B6CE, 0xCDBAF2, 0xA9D3FB, 0xD2D2D2 };
        public static readonly Color Ink = Color.FromArgb(34, 34, 34);
        public static readonly Color Glyph = Color.FromArgb(150, 40, 40, 40);

        public static int Index(string name)
        {
            int i = Array.IndexOf(Names, name);
            return i < 0 ? 0 : i;
        }

        public static Color Rgb(int hex) { return Color.FromArgb((hex >> 16) & 0xFF, (hex >> 8) & 0xFF, hex & 0xFF); }
        public static Color Back(string name) { return Rgb(BackHex[Index(name)]); }
        public static Color Header(string name) { return Rgb(HeaderHex[Index(name)]); }

        public static Image Swatch(int i, float s)
        {
            int size = Math.Max(16, (int)(16 * s));
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            using (var fill = new SolidBrush(Rgb(BackHex[i])))
            using (var pen = new Pen(Rgb(HeaderHex[i]), Math.Max(1f, s)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var r = new RectangleF(1.5f * s, 1.5f * s, size - 3 * s, size - 3 * s);
                g.FillRectangle(fill, r);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
            }
            return bmp;
        }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    // MARK: Settings kept on this computer only (sync folder, where each note sits on screen)

    class LocalState
    {
        readonly string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyNotes");
        readonly Dictionary<string, string> place = new Dictionary<string, string>();
        public string Folder = "";
        public bool Dirty;

        string FilePath { get { return Path.Combine(dir, "settings.txt"); } }

        public LocalState()
        {
            try
            {
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string k = line.Substring(0, eq), v = line.Substring(eq + 1);
                    if (k == "folder") Folder = v;
                    else if (k.StartsWith("note ", StringComparison.Ordinal)) place[k.Substring(5)] = v;
                }
            }
            catch { }
        }

        public bool TryGetPlacement(string id, out Rectangle r, out bool top)
        {
            r = Rectangle.Empty;
            top = false;
            string v;
            if (!place.TryGetValue(id, out v)) return false;
            string[] p = v.Split(',');
            int x, y, w, h, t;
            if (p.Length < 5 || !int.TryParse(p[0], out x) || !int.TryParse(p[1], out y) || !int.TryParse(p[2], out w)
                || !int.TryParse(p[3], out h) || !int.TryParse(p[4], out t)) return false;
            r = new Rectangle(x, y, w, h);
            top = t != 0;
            return true;
        }

        public void SetPlacement(string id, Rectangle r, bool top)
        {
            string v = string.Join(",", new[] { r.X, r.Y, r.Width, r.Height, top ? 1 : 0 });
            string old;
            if (place.TryGetValue(id, out old) && old == v) return;
            place[id] = v;
            Dirty = true;
        }

        public void Forget(string id)
        {
            if (place.Remove(id)) Dirty = true;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(dir);
                var lines = new List<string> { "folder=" + Folder };
                foreach (var kv in place) lines.Add("note " + kv.Key + "=" + kv.Value);
                File.WriteAllLines(FilePath, lines.ToArray(), new UTF8Encoding(false));
                Dirty = false;
            }
            catch { }
        }
    }

    // MARK: Finding Google Drive for desktop

    static class DriveLocator
    {
        static readonly string[] MyDriveNames = { "My Drive", "Mi unidad", "Meine Ablage", "Mon Drive", "Il mio Drive", "Meu Drive", "O meu disco", "Mijn Drive" };
        static readonly string[] Skip = { "Shared drives", "Unidades compartidas", "Other computers", "Otros ordenadores", "System Volume Information" };

        // Each result is a "My Drive" folder; the notes go in its StickyNotes subfolder.
        public static List<string> Find()
        {
            var found = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType == DriveType.CDRom || d.DriveType == DriveType.Network || !d.IsReady) continue;
                    string hit = MyDrive(d.RootDirectory.FullName, d.VolumeLabel == "Google Drive");
                    if (hit != null && !found.Contains(hit)) found.Add(hit);
                }
                catch { }
            }
            // Older "mirror" setups keep the Drive folder in the user profile.
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (string sub in new[] { "My Drive", "Google Drive" })
            {
                string p = Path.Combine(home, sub);
                if (!Directory.Exists(p)) continue;
                string hit = MyDrive(p, false) ?? p;
                if (!found.Contains(hit)) found.Add(hit);
            }
            return found;
        }

        static string MyDrive(string root, bool anySubfolder)
        {
            foreach (string name in MyDriveNames)
            {
                string p = Path.Combine(root, name);
                if (Directory.Exists(p)) return p;
            }
            if (!anySubfolder) return null;
            foreach (string p in Directory.GetDirectories(root))
            {
                string name = Path.GetFileName(p);
                if (name.StartsWith(".") || name.StartsWith("$") || Array.IndexOf(Skip, name) >= 0) continue;
                return p;
            }
            return null;
        }
    }

    class FolderPicker : Form
    {
        readonly ListBox list = new ListBox();
        string chosen;

        public static string Pick(string intro, List<string> candidates, float s)
        {
            using (var f = new FolderPicker(intro, candidates, s))
                return f.ShowDialog() == DialogResult.OK ? f.chosen : null;
        }

        FolderPicker(string intro, List<string> candidates, float s)
        {
            Text = "Sticky Notes: sync folder";
            Font = SystemFonts.MessageBoxFont;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = IconArt.SmallIcon(s);

            int W = (int)(500 * s), pad = (int)(14 * s), bh = (int)(30 * s), bw = (int)(96 * s);
            var label = new Label { Text = intro, AutoSize = true, MaximumSize = new Size(W - 2 * pad, 0), Location = new Point(pad, pad) };
            Controls.Add(label);
            int y = pad + label.GetPreferredSize(new Size(W - 2 * pad, 0)).Height + pad;

            list.IntegralHeight = false;
            list.SetBounds(pad, y, W - 2 * pad, (int)(100 * s));
            foreach (string c in candidates) list.Items.Add(c);
            if (list.Items.Count > 0) list.SelectedIndex = 0;
            list.DoubleClick += delegate { Accept(); };
            Controls.Add(list);
            y += list.Height + pad;

            var other = new Button { Text = "Other folder…" };
            other.SetBounds(pad, y, (int)(130 * s), bh);
            other.Click += delegate { Browse(); };
            var ok = new Button { Text = "OK" };
            ok.SetBounds(W - pad - 2 * bw - pad / 2, y, bw, bh);
            ok.Click += delegate { Accept(); };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            cancel.SetBounds(W - pad - bw, y, bw, bh);
            Controls.AddRange(new Control[] { other, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
            ClientSize = new Size(W, y + bh + pad);
        }

        void Accept()
        {
            if (list.SelectedItem == null) { Browse(); return; }
            chosen = (string)list.SelectedItem;
            DialogResult = DialogResult.OK;
        }

        void Browse()
        {
            using (var d = new FolderBrowserDialog())
            {
                d.Description = "Choose your Google Drive folder (My Drive). Notes are kept in a StickyNotes folder inside it.";
                d.ShowNewFolderButton = true;
                if (list.SelectedItem != null) d.SelectedPath = (string)list.SelectedItem;
                if (d.ShowDialog(this) != DialogResult.OK) return;
                chosen = d.SelectedPath;
                DialogResult = DialogResult.OK;
            }
        }
    }

    // MARK: The app: tray icon, the notes folder and the open notes

    class NotesApp : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunValue = "ErgosferaStickyNotes";
        public static readonly string Machine = Environment.MachineName;
        const string Welcome = "Welcome to Sticky Notes!\n\nWrite anything here. Notes save as you type and sync through your Google Drive folder, so they also appear on your other computer.\n\n+  makes a new note.\n⋯  changes the color or deletes the note.\nDrag the top bar to move a note, and an edge to resize it.";

        readonly LocalState local = new LocalState();
        readonly Dictionary<string, NoteForm> notes = new Dictionary<string, NoteForm>();
        // File stamp (time + size) of each note file as last read or written, to spot changes.
        readonly Dictionary<string, string> seen = new Dictionary<string, string>();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Timer poll = new Timer();
        readonly Control marshal = new Control();
        readonly ToolStripMenuItem startItem;
        readonly bool fixedFolder;
        public readonly float S;
        string folder;
        bool listedOnce, offerWelcome;
        int cascade;

        public NotesApp(string folderOverride, EventWaitHandle showEvent)
        {
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) S = g.DpiX / 96f;
            IntPtr unused = marshal.Handle; // lets the show event post to this thread
            ThreadPool.RegisterWaitForSingleObject(showEvent, delegate { marshal.BeginInvoke((MethodInvoker)ShowAll); }, null, -1, false);

            var menu = new ContextMenuStrip();
            menu.Items.Add("New note", null, delegate { NewNote(null); });
            menu.Items.Add("Show all notes", null, delegate { ShowAll(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Open notes folder", null, delegate { OpenFolder(); });
            menu.Items.Add("Change sync folder…", null, delegate { ChangeFolder(); });
            startItem = new ToolStripMenuItem("Start with Windows", null, delegate { ToggleStartup(); });
            menu.Items.Add(startItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("About Sticky Notes", null, delegate { About(); });
            menu.Items.Add("Quit", null, delegate { Quit(); });
            menu.Opening += delegate { startItem.Checked = StartsWithWindows(); };
            tray.ContextMenuStrip = menu;
            tray.Icon = IconArt.SmallIcon(S);
            tray.Text = "Sticky Notes";
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) ShowAll(); };
            tray.Visible = true;

            fixedFolder = folderOverride != null;
            if (fixedFolder) folder = folderOverride;
            else if (local.Folder != "") folder = local.Folder;
            else
            {
                offerWelcome = true;
                folder = FirstRunFolder();
                local.Folder = folder;
                local.Save();
            }

            poll.Interval = 2500;
            poll.Tick += delegate { Scan(); if (local.Dirty) local.Save(); };
            poll.Start();
            Scan();
            if (offerWelcome)
                tray.ShowBalloonTip(8000, "Sticky Notes", "Your notes sync through " + folder + ". Right-click this icon for options.", ToolTipIcon.Info);
            else if (notes.Count == 0)
                tray.ShowBalloonTip(5000, "Sticky Notes", "Sticky Notes is running. Click this icon to make a note.", ToolTipIcon.Info);
        }

        // MARK: Folder

        string FirstRunFolder()
        {
            var found = DriveLocator.Find();
            string root = found.Count == 1 ? found[0] : FolderPicker.Pick(found.Count == 0
                ? "Google Drive for desktop wasn't found. Choose your Google Drive folder (My Drive), or another folder that syncs between your computers."
                : "Choose the Google Drive folder where your notes should sync. Use the same Google account on your other computer.", found, S);
            if (root != null) return NotesFolderIn(root);
            MessageBox.Show("Notes will be kept in Documents\\StickyNotes on this computer only.\n\nTo sync them, choose a Google Drive folder later: right-click the Sticky Notes icon in the notification area, then Change sync folder.",
                "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return NotesFolderIn(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        }

        static string NotesFolderIn(string chosen)
        {
            chosen = chosen.TrimEnd('\\');
            return string.Equals(Path.GetFileName(chosen), "StickyNotes", StringComparison.OrdinalIgnoreCase) ? chosen : Path.Combine(chosen, "StickyNotes");
        }

        // The folder is only created when its parent exists, so a Drive that hasn't started yet
        // is waited for instead of being replaced by an empty local folder.
        bool EnsureFolder()
        {
            try
            {
                if (Directory.Exists(folder)) return true;
                string parent = Path.GetDirectoryName(folder);
                if (parent == null || !Directory.Exists(parent)) return false;
                Directory.CreateDirectory(folder);
                return true;
            }
            catch { return false; }
        }

        string PathOf(string id) { return Path.Combine(folder, id + ".txt"); }

        static string Stamp(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                if (!fi.Exists) return null;
                return fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "|" + fi.Length.ToString(CultureInfo.InvariantCulture);
            }
            catch { return null; }
        }

        public bool Write(NoteData d)
        {
            if (!EnsureFolder()) return false;
            string path = PathOf(d.Id);
            try { File.WriteAllText(path, NoteFile.Serialize(d), new UTF8Encoding(false)); }
            catch { return false; }
            string st = Stamp(path);
            if (st != null) seen[d.Id] = st;
            return true;
        }

        // Reads every note file that changed since the last look; opens new notes, updates
        // changed ones and closes notes whose file was deleted (on the other computer).
        void Scan()
        {
            if (!EnsureFolder()) { SetStatus("waiting for " + folder); return; }
            string[] files;
            try { files = Directory.GetFiles(folder, "*.txt"); }
            catch { SetStatus("can't read " + folder); return; }

            var present = new HashSet<string>();
            foreach (string f in files)
            {
                string id = Path.GetFileNameWithoutExtension(f);
                if (id.StartsWith(".")) continue;
                present.Add(id);
                string st = Stamp(f), old;
                if (st == null || (seen.TryGetValue(id, out old) && old == st)) continue;
                NoteData d;
                try { d = NoteFile.Parse(id, File.ReadAllText(f, Encoding.UTF8)); }
                catch { continue; } // still being written: try again next time
                seen[id] = st;
                NoteForm form;
                if (notes.TryGetValue(id, out form)) form.ApplyRemote(d);
                else Open(d, false, null);
            }
            foreach (string id in new List<string>(notes.Keys))
            {
                if (present.Contains(id)) continue;
                NoteForm form = notes[id];
                if (form.Dirty) { form.SaveNow(); continue; } // edited here after it was deleted elsewhere: keep it
                notes.Remove(id);
                seen.Remove(id);
                form.CloseQuietly();
            }

            if (!listedOnce)
            {
                listedOnce = true;
                if (offerWelcome && notes.Count == 0) NewNoteWith(Welcome, "yellow", null, true);
            }
            SetStatus(notes.Count == 1 ? "1 note" : notes.Count + " notes");
        }

        void SetStatus(string s)
        {
            string t = "Sticky Notes: " + s;
            tray.Text = t.Length > 63 ? t.Substring(0, 60) + "..." : t;
        }

        // MARK: Notes

        NoteForm Open(NoteData d, bool activate, NoteForm near)
        {
            var f = new NoteForm(this, d);
            Rectangle r;
            bool top;
            if (near == null && local.TryGetPlacement(d.Id, out r, out top) && OnScreen(r))
            {
                f.Bounds = r;
                f.TopMost = top;
            }
            else f.Bounds = DefaultBounds(near);
            notes[d.Id] = f;
            f.Show();
            if (activate) f.ActivateAndFocus();
            PlacementChanged(f);
            return f;
        }

        Rectangle DefaultBounds(NoteForm near)
        {
            var size = new Size((int)(260 * S), (int)(260 * S));
            if (near != null)
            {
                var r = new Rectangle(near.Left + (int)(30 * S), near.Top + (int)(30 * S), size.Width, size.Height);
                if (Screen.FromControl(near).WorkingArea.Contains(r)) return r;
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int step = (int)(30 * S) * (cascade++ % 8);
            return new Rectangle(wa.Right - size.Width - (int)(40 * S) - step, wa.Top + (int)(40 * S) + step, size.Width, size.Height);
        }

        static bool OnScreen(Rectangle r)
        {
            foreach (Screen s in Screen.AllScreens)
            {
                Rectangle i = Rectangle.Intersect(s.WorkingArea, r);
                if (i.Width >= 60 && i.Height >= 40) return true;
            }
            return false;
        }

        public void PlacementChanged(NoteForm f)
        {
            NoteForm known;
            if (notes.TryGetValue(f.Id, out known) && known == f) local.SetPlacement(f.Id, f.Bounds, f.TopMost);
        }

        public void NewNote(NoteForm near)
        {
            NewNoteWith("", near != null ? near.ColorName : "yellow", near, true);
        }

        // The note changed on the other computer while it was being edited here: keep the other
        // version as a new note next to it (this one is saved over the file a moment later).
        public void ConflictCopy(NoteData remote, NoteForm near)
        {
            NewNoteWith(remote.Text, remote.Color, near, false);
        }

        void NewNoteWith(string text, string color, NoteForm near, bool activate)
        {
            var d = new NoteData { Id = NoteFile.NewId(), Color = color, Text = text, Modified = NoteFile.Now(), By = Machine };
            bool saved = Write(d);
            NoteForm f = Open(d, activate, near);
            if (!saved) f.MarkDirty(); // keeps retrying until the folder is reachable
        }

        public void Delete(NoteForm f)
        {
            if (f.CurrentText.Trim().Length > 0 && MessageBox.Show(f, "Delete this note?\n\nIt moves to the Trash folder inside your notes folder.",
                "Sticky Notes", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            string src = PathOf(f.Id);
            try
            {
                if (File.Exists(src))
                {
                    string trash = Path.Combine(folder, "Trash");
                    Directory.CreateDirectory(trash);
                    File.Move(src, Path.Combine(trash, NoteFile.TrashName(f.Id)));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(f, "Couldn't delete the note.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            notes.Remove(f.Id);
            seen.Remove(f.Id);
            local.Forget(f.Id);
            f.CloseQuietly();
        }

        void ShowAll()
        {
            if (notes.Count == 0) { NewNote(null); return; }
            NoteForm last = null;
            foreach (NoteForm f in notes.Values)
            {
                // Toggling TopMost raises a window above other apps without activating it.
                bool top = f.TopMost;
                f.TopMost = true;
                f.TopMost = top;
                last = f;
            }
            last.ActivateAndFocus();
        }

        void FlushAll()
        {
            foreach (NoteForm f in notes.Values) if (f.Dirty) f.SaveNow();
        }

        // MARK: Menu commands

        void OpenFolder()
        {
            if (EnsureFolder()) Process.Start("explorer.exe", "\"" + folder + "\"");
            else MessageBox.Show("The notes folder isn't available right now:\n" + folder + "\n\nIs Google Drive running?", "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void ChangeFolder()
        {
            string root = FolderPicker.Pick("Notes are kept in a StickyNotes folder inside the folder you choose. Choose the same Google Drive folder on all your computers.\n\nNow: " + folder, DriveLocator.Find(), S);
            if (root == null) return;
            string next = NotesFolderIn(root);
            if (string.Equals(next, folder, StringComparison.OrdinalIgnoreCase)) return;
            FlushAll();
            foreach (NoteForm f in new List<NoteForm>(notes.Values)) f.CloseQuietly();
            notes.Clear();
            seen.Clear();
            string old = folder;
            folder = next;
            if (!fixedFolder) { local.Folder = next; local.Save(); }
            OfferCopy(old);
            Scan();
        }

        // Moving from a local folder to Drive (or between Drive accounts): bring the notes along.
        void OfferCopy(string from)
        {
            try
            {
                string[] old = Directory.GetFiles(from, "*.txt");
                if (old.Length == 0 || !EnsureFolder() || Directory.GetFiles(folder, "*.txt").Length > 0) return;
                if (MessageBox.Show("Copy your " + old.Length + " notes from\n" + from + "\nto the new folder?", "Sticky Notes",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                foreach (string f in old)
                {
                    string dst = Path.Combine(folder, Path.GetFileName(f));
                    if (!File.Exists(dst)) File.Copy(f, dst);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't copy the notes.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        static bool StartsWithWindows()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunValue) != null;
            }
            catch { return false; }
        }

        void ToggleStartup()
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (k.GetValue(RunValue) != null) k.DeleteValue(RunValue, false);
                    else k.SetValue(RunValue, "\"" + Application.ExecutablePath + "\"");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Couldn't change this setting.\n\n" + ex.Message, "Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void About()
        {
            MessageBox.Show("Sticky Notes 1.0\n\nNotes folder:\n" + folder +
                "\n\nEach note is a text file in this folder. Google Drive for desktop copies it to your other computers, where Sticky Notes for Windows or Mac shows it. Deleted notes go to the Trash folder inside it." +
                "\n\n© 2026 Mauricio Mendivelso. MIT License.\nProvided \"as is\", without warranty of any kind.",
                "About Sticky Notes", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void Quit()
        {
            poll.Stop();
            FlushAll();
            local.Save();
            foreach (NoteForm f in new List<NoteForm>(notes.Values)) f.CloseQuietly();
            tray.Visible = false;
            ExitThread();
        }
    }

    // MARK: One note on the desktop

    class NoteForm : Form
    {
        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);

        readonly NotesApp app;
        readonly RichTextBox box = new RichTextBox();
        readonly Timer saveTimer = new Timer();
        readonly ContextMenuStrip menu = new ContextMenuStrip();
        readonly ToolStripMenuItem topItem;
        readonly float S;
        NoteData data; // the version last read from or written to the file
        bool loading, quiet;
        int hot = -1;

        public bool Dirty { get; private set; }
        public string Id { get { return data.Id; } }
        public string ColorName { get { return data.Color; } }
        public string CurrentText { get { return box.Text; } }

        int HeaderH { get { return (int)(30 * S); } }
        int Edge { get { return Math.Max(4, (int)(6 * S)); } }
        int Pad { get { return (int)(10 * S); } }
        int ButtonSize { get { return HeaderH - (int)(6 * S); } }
        Rectangle PlusRect { get { return new Rectangle((int)(4 * S), (HeaderH - ButtonSize) / 2, ButtonSize, ButtonSize); } }
        Rectangle MenuRect { get { return new Rectangle(ClientSize.Width - (int)(4 * S) - ButtonSize, (HeaderH - ButtonSize) / 2, ButtonSize, ButtonSize); } }

        public NoteForm(NotesApp app, NoteData d)
        {
            this.app = app;
            data = d;
            S = app.S;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            KeyPreview = true;
            MinimumSize = new Size((int)(170 * S), (int)(120 * S));

            box.BorderStyle = BorderStyle.None;
            box.Font = new Font("Segoe UI", 11f);
            box.ForeColor = NoteColors.Ink;
            box.ScrollBars = RichTextBoxScrollBars.Vertical;
            box.DetectUrls = true;
            box.AcceptsTab = true;
            box.TextChanged += delegate
            {
                if (loading) return;
                Dirty = true;
                saveTimer.Stop();
                saveTimer.Interval = 700;
                saveTimer.Start();
                UpdateTitle();
            };
            box.LinkClicked += (s, e) => { try { Process.Start(e.LinkText); } catch { } };
            box.KeyDown += BoxKeyDown;
            box.ContextMenuStrip = EditMenu();
            Controls.Add(box);

            saveTimer.Tick += delegate { SaveNow(); };

            for (int i = 0; i < NoteColors.Names.Length; i++)
            {
                string name = NoteColors.Names[i];
                menu.Items.Add(new ToolStripMenuItem(NoteColors.Labels[i], NoteColors.Swatch(i, S), delegate { SetColor(name); }));
            }
            menu.Items.Add(new ToolStripSeparator());
            topItem = new ToolStripMenuItem("Keep on top", null, delegate { TopMost = !TopMost; app.PlacementChanged(this); });
            menu.Items.Add(topItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("New note", null, delegate { app.NewNote(this); }) { ShortcutKeyDisplayString = "Ctrl+N" });
            menu.Items.Add("Delete note", null, delegate { app.Delete(this); });
            menu.Opening += delegate
            {
                topItem.Checked = TopMost;
                for (int i = 0; i < NoteColors.Names.Length; i++)
                    ((ToolStripMenuItem)menu.Items[i]).Checked = NoteColors.Index(data.Color) == i;
            };

            loading = true;
            box.Text = d.Text;
            box.ClearUndo();
            loading = false;
            ApplyColor();
            UpdateTitle();

            Deactivate += delegate { if (Dirty) SaveNow(); };
            LocationChanged += delegate { app.PlacementChanged(this); };
            SizeChanged += delegate { app.PlacementChanged(this); LayoutBox(); Invalidate(); };
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ClassStyle |= 0x20000; // CS_DROPSHADOW
                return cp;
            }
        }

        // Notes arriving from the other computer appear without taking the keyboard focus.
        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = 2; // Windows 11: DWMWA_WINDOW_CORNER_PREFERENCE = DWMWCP_ROUND
                DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
            }
            catch { }
            LayoutBox();
        }

        void LayoutBox()
        {
            int top = HeaderH + (int)(4 * S);
            box.SetBounds(Pad, top, Math.Max(10, ClientSize.Width - 2 * Pad), Math.Max(10, ClientSize.Height - top - Pad));
        }

        void ApplyColor()
        {
            BackColor = NoteColors.Back(data.Color);
            box.BackColor = BackColor;
            Invalidate();
        }

        void UpdateTitle()
        {
            string t = box.Text.Trim();
            int nl = t.IndexOf('\n');
            if (nl >= 0) t = t.Substring(0, nl);
            if (t.Length > 60) t = t.Substring(0, 60);
            Text = t.Length == 0 ? "Sticky note" : t;
        }

        public void ActivateAndFocus()
        {
            Activate();
            box.Focus();
            box.Select(box.TextLength, 0);
        }

        // MARK: Saving and syncing

        public bool SaveNow()
        {
            saveTimer.Stop();
            var d = new NoteData { Id = data.Id, Color = data.Color, Text = box.Text, Modified = NoteFile.Now(), By = NotesApp.Machine };
            if (app.Write(d))
            {
                data = d;
                Dirty = false;
                return true;
            }
            MarkDirty();
            return false;
        }

        public void MarkDirty()
        {
            Dirty = true;
            saveTimer.Stop();
            saveTimer.Interval = 3000;
            saveTimer.Start();
        }

        public void ApplyRemote(NoteData d)
        {
            if (d.Modified == data.Modified && d.Color == data.Color && d.Text == data.Text) return; // our own save
            if (Dirty) { app.ConflictCopy(d, this); return; }
            if (d.Text != box.Text)
            {
                int caret = box.SelectionStart;
                loading = true;
                box.Text = d.Text;
                box.ClearUndo();
                loading = false;
                box.Select(Math.Min(caret, box.TextLength), 0);
            }
            data = d;
            ApplyColor();
            UpdateTitle();
        }

        void SetColor(string name)
        {
            data.Color = name;
            ApplyColor();
            SaveNow();
        }

        public void CloseQuietly()
        {
            quiet = true;
            Dirty = false;
            saveTimer.Stop();
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Alt+F4 doesn't close a note; notes are deleted from the ⋯ menu.
            if (!quiet && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
            if (Dirty) SaveNow();
            base.OnFormClosing(e);
        }

        // MARK: Text editing (plain text only, so both apps show the same thing)

        void BoxKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Control && e.KeyCode == Keys.V) || (e.Shift && e.KeyCode == Keys.Insert))
            {
                e.Handled = e.SuppressKeyPress = true;
                PastePlain();
                return;
            }
            // Rich-edit shortcuts (alignment, line spacing, font size) would change only this screen.
            Keys k = e.KeyCode;
            if (e.Control && (k == Keys.L || k == Keys.E || k == Keys.R || k == Keys.J || k == Keys.D1 || k == Keys.D2 || k == Keys.D5
                || k == Keys.OemPeriod || k == Keys.Oemcomma))
                e.Handled = e.SuppressKeyPress = true;
        }

        void PastePlain()
        {
            try { if (Clipboard.ContainsText()) box.SelectedText = NoteFile.Normalize(Clipboard.GetText()); }
            catch { }
        }

        ContextMenuStrip EditMenu()
        {
            var m = new ContextMenuStrip();
            var undo = m.Items.Add("Undo", null, delegate { box.Undo(); });
            m.Items.Add(new ToolStripSeparator());
            var cut = m.Items.Add("Cut", null, delegate { box.Cut(); });
            var copy = m.Items.Add("Copy", null, delegate { box.Copy(); });
            var paste = m.Items.Add("Paste", null, delegate { PastePlain(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Select all", null, delegate { box.SelectAll(); });
            m.Opening += delegate
            {
                undo.Enabled = box.CanUndo;
                cut.Enabled = copy.Enabled = box.SelectionLength > 0;
                paste.Enabled = Clipboard.ContainsText();
            };
            return m;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N)
            {
                e.Handled = e.SuppressKeyPress = true;
                app.NewNote(this);
                return;
            }
            base.OnKeyDown(e);
        }

        // MARK: Top bar, moving and resizing

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(NoteColors.Header(data.Color)))
                g.FillRectangle(b, 0, 0, ClientSize.Width, HeaderH);
            DrawButton(g, PlusRect, hot == 0, true);
            DrawButton(g, MenuRect, hot == 1, false);
        }

        void DrawButton(Graphics g, Rectangle r, bool hover, bool plus)
        {
            if (hover)
                using (var b = new SolidBrush(Color.FromArgb(28, 0, 0, 0)))
                using (var p = NoteColors.Round(r, 4 * S))
                    g.FillPath(b, p);
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f, a = r.Width * 0.24f;
            if (plus)
            {
                using (var pen = new Pen(NoteColors.Glyph, Math.Max(1.5f, 1.6f * S)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawLine(pen, cx - a, cy, cx + a, cy);
                    g.DrawLine(pen, cx, cy - a, cx, cy + a);
                }
            }
            else
            {
                float dot = Math.Max(2.5f, 3f * S);
                using (var b = new SolidBrush(NoteColors.Glyph))
                    for (int i = -1; i <= 1; i++) g.FillEllipse(b, cx + i * a - dot / 2, cy - dot / 2, dot, dot);
            }
        }

        int HitButton(Point p) { return PlusRect.Contains(p) ? 0 : MenuRect.Contains(p) ? 1 : -1; }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int h = HitButton(e.Location);
            if (h != hot) { hot = h; Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH)); }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            if (hot != -1) { hot = -1; Invalidate(new Rectangle(0, 0, ClientSize.Width, HeaderH)); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int h = HitButton(e.Location);
            if (e.Button == MouseButtons.Right) { menu.Show(this, e.Location); return; }
            if (e.Button != MouseButtons.Left) return;
            if (h == 0) app.NewNote(this);
            else if (h == 1) menu.Show(this, new Point(MenuRect.Left, MenuRect.Bottom));
            else if (e.Y < HeaderH)
            {
                ReleaseCapture();
                SendMessage(Handle, 0xA1, (IntPtr)2, IntPtr.Zero); // WM_NCLBUTTONDOWN on HTCAPTION: drag the window
            }
            else box.Focus();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x84) return; // WM_NCHITTEST: the margins around the text resize the note
            long lp = m.LParam.ToInt64();
            Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
            int e = Edge, w = ClientSize.Width, h = ClientSize.Height;
            bool left = p.X < e, right = p.X >= w - e, top = p.Y < Math.Max(3, (int)(4 * S)), bottom = p.Y >= h - e;
            bool corner = p.X >= w - 3 * e && p.Y >= h - 3 * e && !box.Bounds.Contains(p);
            int ht = 0;
            if (corner || (bottom && right)) ht = 17;
            else if (top && left) ht = 13;
            else if (top && right) ht = 14;
            else if (bottom && left) ht = 16;
            else if (left) ht = 10;
            else if (right) ht = 11;
            else if (top) ht = 12;
            else if (bottom) ht = 15;
            if (ht != 0) m.Result = (IntPtr)ht;
        }
    }

    // MARK: Icon (used by build.ps1: StickyNotes --write-icon <file>, and for the tray)

    static class IconArt
    {
        public static void Draw(Graphics g, int size)
        {
            float s = size;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            float m = s * 0.1f;
            var r = new RectangleF(m, m, s - 2 * m, s - 2 * m);
            float fold = r.Width * 0.28f;
            using (var body = new GraphicsPath())
            {
                body.AddPolygon(new[] {
                    new PointF(r.Left, r.Top), new PointF(r.Right, r.Top), new PointF(r.Right, r.Bottom - fold),
                    new PointF(r.Right - fold, r.Bottom), new PointF(r.Left, r.Bottom) });
                using (var b = new SolidBrush(NoteColors.Rgb(0xFFE25A))) g.FillPath(b, body);
                g.SetClip(body);
                using (var b = new SolidBrush(NoteColors.Rgb(0xF0C83C))) g.FillRectangle(b, r.Left, r.Top, r.Width, r.Height * 0.2f);
                g.ResetClip();
            }
            using (var b = new SolidBrush(NoteColors.Rgb(0xE6BE32)))
                g.FillPolygon(b, new[] { new PointF(r.Right, r.Bottom - fold), new PointF(r.Right - fold, r.Bottom - fold), new PointF(r.Right - fold, r.Bottom) });
            if (size >= 24)
                using (var pen = new Pen(NoteColors.Rgb(0x96781E), s * 0.05f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    float[,] lines = { { 0.42f, 0.62f }, { 0.58f, 0.62f }, { 0.74f, 0.4f } };
                    for (int i = 0; i < 3; i++)
                    {
                        float y = r.Top + lines[i, 0] * r.Height;
                        g.DrawLine(pen, r.Left + 0.14f * r.Width, y, r.Left + (0.14f + lines[i, 1]) * r.Width, y);
                    }
                }
        }

        public static Icon SmallIcon(float s)
        {
            int size = Math.Max(16, (int)Math.Round(16 * s));
            using (var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp)) Draw(g, size);
                return Icon.FromHandle(bmp.GetHicon());
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
