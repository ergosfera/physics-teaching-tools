// Sticky Notes for macOS: the Mac twin of desktop-apps/sticky-notes (Windows).
// Each note is a small text file in "My Drive/StickyNotes". Both apps read and write the same
// files, and Google Drive for desktop copies them between computers.
// Single-file AppKit app; build.sh compiles it into Sticky Notes.app and a .dmg.
import AppKit
import ServiceManagement

// MARK: - Note files (same format as the Windows app; see selfTest)

struct NoteData {
    var id: String
    var color = "yellow"
    var title = ""
    var modified = ""
    var by = ""
    var text = ""

    init(id: String) { self.id = id }
}

// A note file is a short header, a "---" line, then the note text:
//   color: yellow
//   title: Groceries
//   modified: 2026-10-18T14:03:22.123Z
//   by: WORK-MAC
//   ---
//   Buy milk
// The title line is there only when the note has a title.
// A plain .txt file without the header also works: all of it becomes the note text.
enum NoteFile {
    static let machine: String = (Host.current().localizedName ?? "Mac").replacingOccurrences(of: "\n", with: " ")

    private static func formatter(_ format: String, utc: Bool) -> DateFormatter {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        f.timeZone = utc ? TimeZone(identifier: "UTC") : TimeZone.current
        f.dateFormat = format
        return f
    }
    private static let stampFormat = NoteFile.formatter("yyyy-MM-dd'T'HH:mm:ss.SSS'Z'", utc: true)
    private static let idFormat = NoteFile.formatter("yyyyMMdd-HHmmss", utc: true)
    private static let trashFormat = NoteFile.formatter("yyyy-MM-dd HHmmss", utc: false)

    static func now() -> String { stampFormat.string(from: Date()) }

    static func newId() -> String {
        let r = String(Int.random(in: 0..<0x10000), radix: 16)
        return idFormat.string(from: Date()) + "-" + String(repeating: "0", count: 4 - r.count) + r
    }

    static func trashName(_ id: String) -> String { id + " deleted " + trashFormat.string(from: Date()) + ".txt" }

    static func normalize(_ s: String) -> String {
        s.replacingOccurrences(of: "\r\n", with: "\n").replacingOccurrences(of: "\r", with: "\n")
    }

    // A title is one line, without spaces at either end
    static func cleanTitle(_ s: String) -> String {
        normalize(s).replacingOccurrences(of: "\n", with: " ").trimmingCharacters(in: .whitespaces)
    }

    static func serialize(_ n: NoteData) -> String {
        let by = n.by.replacingOccurrences(of: "\n", with: " ")
        let title = cleanTitle(n.title)
        return "color: \(n.color)\n" + (title.isEmpty ? "" : "title: \(title)\n")
            + "modified: \(n.modified)\nby: \(by)\n---\n" + normalize(n.text)
    }

    static func parse(id: String, content: String) -> NoteData {
        var n = NoteData(id: id)
        var s = normalize(content)
        if s.hasPrefix("\u{FEFF}") { s.removeFirst() }
        let header: String
        let body: String
        if s.hasPrefix("---\n") {
            header = ""
            body = String(s.dropFirst(4))
        } else if let r = s.range(of: "\n---\n") {
            header = String(s[..<r.lowerBound])
            body = String(s[r.upperBound...])
        } else {
            n.text = s
            return n
        }

        var fields: [String: String] = [:]
        for line in header.components(separatedBy: "\n") where !line.isEmpty {
            guard let colon = line.firstIndex(of: ":"), colon != line.startIndex,
                  line[..<colon].allSatisfy({ $0 >= "a" && $0 <= "z" }) else {
                // Not a header after all (for example a plain note with a "---" line in it).
                n.text = s
                return n
            }
            fields[String(line[..<colon])] = line[line.index(after: colon)...].trimmingCharacters(in: .whitespaces)
        }
        if let c = fields["color"], !c.isEmpty { n.color = c }
        n.title = fields["title"] ?? ""
        n.modified = fields["modified"] ?? ""
        n.by = fields["by"] ?? ""
        n.text = body
        return n
    }
}

// Checkboxes, bold and text colors, kept in the note text as plain markers so the file stays readable anywhere:
//   "[ ] milk" / "[x] milk" at the start of a line is a checkbox (shown as ☐ / ☑), "**word**" is bold,
//   and "{red}word{/red}" is red text (the color names are Markup.inks).
// On screen the markers become a box character plus a space, bold text and colored text. Ranges count
// UTF-16 units, like the Windows app.
struct InkSpan: Equatable {
    var range: NSRange
    var name: String
}

enum Markup {
    static let unchecked = "\u{2610}", checked = "\u{2611}"
    static let uncheckedChar: unichar = 0x2610, checkedChar: unichar = 0x2611
    // Text colors besides the usual dark ink
    static let inks = ["red", "orange", "green", "blue", "purple"]

    static func isBox(_ c: unichar) -> Bool { c == uncheckedChar || c == checkedChar }

    // Is there a box (and its space) at the start of the line beginning at index i?
    static func hasBox(_ s: NSString, at i: Int) -> Bool {
        i + 1 < s.length && isBox(s.character(at: i)) && s.character(at: i + 1) == 32
    }

    static func parse(_ markup: String) -> (text: String, bold: [NSRange], ink: [InkSpan]) {
        let out = NSMutableString()
        var bold: [NSRange] = []
        var ink: [InkSpan] = []
        func addBold(_ i: Int) {
            if let last = bold.last, NSMaxRange(last) == i { bold[bold.count - 1].length += 1 } else { bold.append(NSRange(location: i, length: 1)) }
        }
        func addInk(_ i: Int, _ name: String) {
            if let last = ink.last, last.name == name, NSMaxRange(last.range) == i { ink[ink.count - 1].range.length += 1 }
            else { ink.append(InkSpan(range: NSRange(location: i, length: 1), name: name)) }
        }
        for (n, raw) in markup.components(separatedBy: "\n").enumerated() {
            if n > 0 { out.append("\n") }
            var line = raw as NSString
            if line.hasPrefix("[ ] ") {
                out.append(unchecked + " ")
                line = line.substring(from: 4) as NSString
            } else if line.hasPrefix("[x] ") || line.hasPrefix("[X] ") {
                out.append(checked + " ")
                line = line.substring(from: 4) as NSString
            }
            let len = line.length
            var drop = [Bool](repeating: false, count: len)
            var isBold = [Bool](repeating: false, count: len)
            var color = [String?](repeating: nil, count: len)

            // "{name}" and "{/name}" pair up within the line; a tag without its partner, or an empty pair, stays as typed
            var i = 0
            scan: while i < len {
                if line.character(at: i) == 123 {
                    for name in inks {
                        let open = "{" + name + "}", close = "{/" + name + "}"
                        let ol = (open as NSString).length, cl = (close as NSString).length
                        guard line.range(of: open, options: .anchored, range: NSRange(location: i, length: len - i)).location != NSNotFound
                        else { continue }
                        let c = line.range(of: close, options: [], range: NSRange(location: i + ol, length: len - i - ol)).location
                        guard c != NSNotFound, c > i + ol else { continue }
                        for k in i..<(i + ol) { drop[k] = true }
                        for k in c..<(c + cl) { drop[k] = true }
                        for k in (i + ol)..<c { color[k] = name }
                        i = c + cl
                        continue scan
                    }
                }
                i += 1
            }

            // "**" marks pair up within the line; an odd one out, or an empty pair, stays as typed
            var marks: [Int] = []
            var j = 0
            while j + 1 < len {
                if line.character(at: j) == 42 && line.character(at: j + 1) == 42 { marks.append(j); j += 2 } else { j += 1 }
            }
            if marks.count % 2 == 1 { marks.removeLast() }
            var k = 0
            while k < marks.count {
                let a = marks[k], b = marks[k + 1]
                k += 2
                if b == a + 2 { continue }
                for x in [a, a + 1, b, b + 1] { drop[x] = true }
                for x in (a + 2)..<b { isBold[x] = true }
            }

            // Copy what is left in runs (markers are plain ASCII, so a run never splits a character)
            var x = 0
            while x < len {
                if drop[x] { x += 1; continue }
                var y = x
                while y < len && !drop[y] { y += 1 }
                let at = out.length
                out.append(line.substring(with: NSRange(location: x, length: y - x)))
                for z in x..<y {
                    if isBold[z] { addBold(at + z - x) }
                    if let c = color[z] { addInk(at + z - x, c) }
                }
                x = y
            }
        }
        return (out as String, bold, ink)
    }

    static func serialize(_ text: String, bold: [NSRange], ink: [InkSpan] = []) -> String {
        let t = text as NSString
        var isBold = [Bool](repeating: false, count: t.length)
        for r in bold {
            for i in max(0, r.location)..<max(0, min(t.length, r.location + r.length)) { isBold[i] = true }
        }
        var color = [String?](repeating: nil, count: t.length)
        for s in ink where inks.contains(s.name) {
            for i in max(0, s.range.location)..<max(0, min(t.length, s.range.location + s.range.length)) { color[i] = s.name }
        }
        let out = NSMutableString()
        // Bold runs get "**" around them
        func writeBold(_ from: Int, _ to: Int) {
            var i = from
            while i < to {
                let b = isBold[i]
                var j = i
                while j < to && isBold[j] == b { j += 1 }
                let piece = t.substring(with: NSRange(location: i, length: j - i))
                if b && !piece.trimmingCharacters(in: .whitespaces).isEmpty { out.append("**" + piece + "**") } else { out.append(piece) }
                i = j
            }
        }
        var start = 0
        while start <= t.length {
            var end = t.range(of: "\n", options: [], range: NSRange(location: start, length: t.length - start)).location
            if end == NSNotFound { end = t.length }
            var i = start
            if hasBox(t, at: i) && i + 1 < end {
                out.append(t.character(at: i) == uncheckedChar ? "[ ] " : "[x] ")
                i += 2
            }
            // Colored runs get "{name}" and "{/name}" around them, outside any "**"; each line closes its own
            while i < end {
                let c = color[i]
                var j = i
                while j < end && color[j] == c { j += 1 }
                let piece = t.substring(with: NSRange(location: i, length: j - i))
                if let c = c, !piece.trimmingCharacters(in: .whitespaces).isEmpty {
                    out.append("{\(c)}")
                    writeBold(i, j)
                    out.append("{/\(c)}")
                } else {
                    writeBold(i, j)
                }
                i = j
            }
            if end < t.length { out.append("\n") }
            start = end + 1
        }
        return out as String
    }
}

// "Sticky Notes --selftest" (run by build.sh). The Windows app checks the same cases.
func selfTest() -> Int32 {
    var fails: Int32 = 0
    func check(_ ok: Bool, _ name: String) {
        if !ok { print("FAIL: \(name)"); fails += 1 }
    }
    var n = NoteData(id: "a")
    n.color = "pink"
    n.modified = "2026-10-18T10:00:00.000Z"
    n.by = "MAC"
    n.text = "Hello\r\n---\nworld: yes"
    let file = NoteFile.serialize(n)
    check(file == "color: pink\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes", "serialize")
    let back = NoteFile.parse(id: "a", content: file)
    check(back.color == "pink" && back.modified == n.modified && back.by == "MAC" && back.text == "Hello\n---\nworld: yes", "round trip")
    let plain = NoteFile.parse(id: "b", content: "Just text\r\nline 2")
    check(plain.text == "Just text\nline 2" && plain.color == "yellow" && plain.modified == "", "plain text file")
    let bom = NoteFile.parse(id: "c", content: "\u{FEFF}color: blue\nmodified: x\nby: y\n---\n")
    check(bom.color == "blue" && bom.text == "" && bom.modified == "x" && bom.by == "y", "byte order mark")
    let list = NoteFile.parse(id: "d", content: "Shopping list\n---\nmilk")
    check(list.text == "Shopping list\n---\nmilk" && list.color == "yellow", "dashes in a plain note")
    let bodyOnly = NoteFile.parse(id: "e", content: "---\nonly body")
    check(bodyOnly.text == "only body" && bodyOnly.color == "yellow", "empty header")

    // Checkboxes and bold
    let boxes = Markup.parse("[ ] milk\n[x] eggs\nplain")
    check(boxes.text == "\u{2610} milk\n\u{2611} eggs\nplain" && boxes.bold.isEmpty, "parse checkboxes")
    let b1 = Markup.parse("a **b** c")
    check(b1.text == "a b c" && b1.bold == [NSRange(location: 2, length: 1)], "parse bold")
    check(Markup.parse("x ** y").text == "x ** y" && Markup.parse("x ** y").bold.isEmpty, "lone marks stay")
    check(Markup.parse("****").text == "****" && Markup.parse("****").bold.isEmpty, "empty bold stays")
    check(Markup.serialize("\u{2610} a\n\u{2611} b", bold: []) == "[ ] a\n[x] b", "write checkboxes")
    check(Markup.serialize("abc d", bold: [NSRange(location: 0, length: 3)]) == "**abc** d", "write bold")
    check(Markup.serialize("ab\ncd", bold: [NSRange(location: 1, length: 3)]) == "a**b**\n**c**d", "bold across lines")
    check(Markup.serialize("\u{2610}x", bold: []) == "\u{2610}x", "box without space")
    let mixed = "[ ] **buy** milk\n[x] done\n**Title**\nend\n"
    let p = Markup.parse(mixed)
    check(Markup.serialize(p.text, bold: p.bold) == mixed, "round trip with markers")

    // Titles
    check(back.title == "", "no title")
    var titled = n
    titled.title = " Groceries\nweek 2 "
    let titledFile = NoteFile.serialize(titled)
    check(titledFile == "color: pink\ntitle: Groceries week 2\nmodified: 2026-10-18T10:00:00.000Z\nby: MAC\n---\nHello\n---\nworld: yes", "write title")
    check(NoteFile.parse(id: "a", content: titledFile).title == "Groceries week 2", "read title")

    // Text colors
    let c1 = Markup.parse("a {red}b{/red} c")
    check(c1.text == "a b c" && c1.ink == [InkSpan(range: NSRange(location: 2, length: 1), name: "red")] && c1.bold.isEmpty, "parse color")
    check(Markup.parse("{red}x").text == "{red}x" && Markup.parse("{red}x").ink.isEmpty, "lone tag stays")
    check(Markup.parse("{pink}x{/pink}").text == "{pink}x{/pink}", "unknown color stays")
    check(Markup.parse("{red}{/red}").text == "{red}{/red}", "empty color stays")
    let c2 = Markup.parse("{blue}**b**{/blue}")
    check(c2.text == "b" && c2.bold == [NSRange(location: 0, length: 1)]
          && c2.ink == [InkSpan(range: NSRange(location: 0, length: 1), name: "blue")], "bold inside color")
    check(Markup.serialize("ab cd", bold: [], ink: [InkSpan(range: NSRange(location: 0, length: 2), name: "green")]) == "{green}ab{/green} cd", "write color")
    check(Markup.serialize("abc", bold: [NSRange(location: 1, length: 2)], ink: [InkSpan(range: NSRange(location: 0, length: 2), name: "red")])
          == "{red}a**b**{/red}**c**", "write bold and color")
    check(Markup.serialize("ab\ncd", bold: [], ink: [InkSpan(range: NSRange(location: 1, length: 3), name: "purple")])
          == "a{purple}b{/purple}\n{purple}c{/purple}d", "color across lines")
    check(Markup.serialize("a b", bold: [], ink: [InkSpan(range: NSRange(location: 1, length: 1), name: "red")]) == "a b", "colored space")
    let colored = "[ ] {red}**buy** milk{/red} now\n[x] {orange}done{/orange}\n**Title** {blue}x{/blue}\nend\n"
    let q = Markup.parse(colored)
    check(q.text == "\u{2610} buy milk now\n\u{2611} done\nTitle x\nend\n", "parse mixed colors")
    check(Markup.serialize(q.text, bold: q.bold, ink: q.ink) == colored, "round trip with colors")
    print(fails == 0 ? "Note format self-test passed" : "Note format self-test failed")
    return fails
}

// MARK: - Look

enum NoteColors {
    static let names = ["yellow", "green", "pink", "purple", "blue", "gray"]
    static let labels = ["Yellow", "Green", "Pink", "Purple", "Blue", "Gray"]
    static let backHex = [0xFFF7AA, 0xD4F2C4, 0xFFD3E2, 0xE3D7FA, 0xCDE6FF, 0xE6E6E6]
    static let headerHex = [0xF7E479, 0xB8E3A3, 0xF9B6CE, 0xCDBAF2, 0xA9D3FB, 0xD2D2D2]
    static let ink = NSColor(srgbRed: 0.133, green: 0.133, blue: 0.133, alpha: 1)
    static let glyph = NSColor(srgbRed: 0.16, green: 0.16, blue: 0.16, alpha: 0.6)
    // Text colors: the usual ink, then Markup.inks. Dark enough to read on every note color.
    static let inkLabels = ["Black", "Red", "Orange", "Green", "Blue", "Purple"]
    static let inkHex = [0x222222, 0xB71C1C, 0xA03F00, 0x1B6E20, 0x0D47A1, 0x6A1B9A]

    static func index(_ name: String) -> Int { names.firstIndex(of: name) ?? 0 }

    static func inkIndex(_ name: String?) -> Int { name.flatMap { Markup.inks.firstIndex(of: $0) }.map { $0 + 1 } ?? 0 }
    static func inkColor(_ name: String?) -> NSColor { inkIndex(name) == 0 ? ink : rgb(inkHex[inkIndex(name)]) }

    static func inkSwatch(_ i: Int) -> NSImage {
        NSImage(size: NSSize(width: 14, height: 14), flipped: false) { r in
            NoteColors.rgb(NoteColors.inkHex[i]).setFill()
            NSBezierPath(ovalIn: r.insetBy(dx: 2, dy: 2)).fill()
            return true
        }
    }

    static func rgb(_ hex: Int) -> NSColor {
        NSColor(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255,
                blue: CGFloat(hex & 0xFF) / 255, alpha: 1)
    }

    static func back(_ name: String) -> NSColor { rgb(backHex[index(name)]) }
    static func header(_ name: String) -> NSColor { rgb(headerHex[index(name)]) }

    static func swatch(_ i: Int) -> NSImage {
        NSImage(size: NSSize(width: 14, height: 14), flipped: false) { r in
            let p = NSBezierPath(rect: r.insetBy(dx: 1.5, dy: 1.5))
            NoteColors.rgb(NoteColors.backHex[i]).setFill()
            p.fill()
            NoteColors.rgb(NoteColors.headerHex[i]).setStroke()
            p.lineWidth = 1
            p.stroke()
            return true
        }
    }
}

// MARK: - Settings kept on this Mac only (sync folder, where each note sits on screen)

final class LocalState {
    private let d = UserDefaults.standard

    var folder: String {
        get { d.string(forKey: "folder") ?? "" }
        set { d.set(newValue, forKey: "folder") }
    }

    func placement(_ id: String) -> (rect: NSRect, top: Bool)? {
        guard let a = (d.dictionary(forKey: "placements") ?? [:])[id] as? [Double], a.count >= 5 else { return nil }
        return (NSRect(x: a[0], y: a[1], width: a[2], height: a[3]), a[4] != 0)
    }

    func setPlacement(_ id: String, _ r: NSRect, _ top: Bool) {
        var all = d.dictionary(forKey: "placements") ?? [:]
        let value = [Double(r.minX), Double(r.minY), Double(r.width), Double(r.height), top ? 1.0 : 0.0]
        if let old = all[id] as? [Double], old == value { return }
        all[id] = value
        d.set(all, forKey: "placements")
    }

    func forget(_ id: String) {
        var all = d.dictionary(forKey: "placements") ?? [:]
        if all.removeValue(forKey: id) != nil { d.set(all, forKey: "placements") }
    }
}

// MARK: - Finding Google Drive for desktop

enum DriveLocator {
    struct Candidate {
        let path: String  // a "My Drive" folder; the notes go in its StickyNotes subfolder
        let label: String
    }

    static let myDriveNames = ["My Drive", "Mi unidad", "Meine Ablage", "Mon Drive", "Il mio Drive", "Meu Drive", "O meu disco", "Mijn Drive"]
    static let skip: Set<String> = ["Shared drives", "Unidades compartidas", "Other computers", "Otros ordenadores"]

    static func find() -> [Candidate] {
        var out: [Candidate] = []
        let cloud = (NSHomeDirectory() as NSString).appendingPathComponent("Library/CloudStorage")
        let accounts = ((try? FileManager.default.contentsOfDirectory(atPath: cloud)) ?? []).sorted()
        for name in accounts where name.hasPrefix("GoogleDrive") {
            guard let my = myDrive(in: (cloud as NSString).appendingPathComponent(name), anySubfolder: true) else { continue }
            let account = name.hasPrefix("GoogleDrive-") ? String(name.dropFirst("GoogleDrive-".count)) : "Google Drive"
            out.append(Candidate(path: my, label: account))
        }
        // Older versions of Google Drive used these places.
        for root in ["/Volumes/GoogleDrive", (NSHomeDirectory() as NSString).appendingPathComponent("Google Drive")] {
            if let my = myDrive(in: root, anySubfolder: false), !out.contains(where: { $0.path == my }) {
                out.append(Candidate(path: my, label: "Google Drive"))
            }
        }
        return out
    }

    private static func isDirectory(_ path: String) -> Bool {
        var isDir: ObjCBool = false
        return FileManager.default.fileExists(atPath: path, isDirectory: &isDir) && isDir.boolValue
    }

    private static func myDrive(in root: String, anySubfolder: Bool) -> String? {
        for name in myDriveNames {
            let p = (root as NSString).appendingPathComponent(name)
            if isDirectory(p) { return p }
        }
        guard anySubfolder, let subs = try? FileManager.default.contentsOfDirectory(atPath: root) else { return nil }
        for name in subs.sorted() where !name.hasPrefix(".") && !skip.contains(name) {
            let p = (root as NSString).appendingPathComponent(name)
            if isDirectory(p) { return p }
        }
        return nil
    }
}

// MARK: - One note on the desktop

final class NoteWindow: NSWindow {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { true }
}

// Bottom-right corner handle for resizing (every edge resizes too; see NoteView).
final class GripView: NSView {
    private var startMouse = NSPoint.zero
    private var startFrame = NSRect.zero

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        NSColor(white: 0, alpha: 0.35).setStroke()
        let p = NSBezierPath()
        p.lineWidth = 1.2
        p.lineCapStyle = .round
        for k: CGFloat in [4, 8, 12] {
            p.move(to: NSPoint(x: bounds.width - 3, y: bounds.height - 3 - k))
            p.line(to: NSPoint(x: bounds.width - 3 - k, y: bounds.height - 3))
        }
        p.stroke()
    }

    override func resetCursorRects() { addCursorRect(bounds, cursor: .crosshair) }

    override func mouseDown(with event: NSEvent) {
        startMouse = NSEvent.mouseLocation
        startFrame = window?.frame ?? .zero
    }

    override func mouseDragged(with event: NSEvent) {
        guard let w = window else { return }
        let p = NSEvent.mouseLocation
        let width = max(w.minSize.width, startFrame.width + p.x - startMouse.x)
        let height = max(w.minSize.height, startFrame.height - (p.y - startMouse.y))
        w.setFrame(NSRect(x: startFrame.minX, y: startFrame.maxY - height, width: width, height: height), display: true)
    }
}

// The note's text: clicking a ☐ / ☑ checks it, Enter continues a checklist, typing "[ ] " at the start of
// a line makes a checkbox, and paste brings in plain text (with any checkbox and bold markers in it).
final class NoteTextView: NSTextView {
    weak var note: NoteController?

    override func paste(_ sender: Any?) {
        if let note = note { note.pasteMarkup() } else { super.paste(sender) }
    }

    override func mouseDown(with event: NSEvent) {
        if let note = note, let i = checkbox(at: convert(event.locationInWindow, from: nil)) {
            note.toggleCheck(at: i)
            return
        }
        super.mouseDown(with: event)
    }

    // The index of the checkbox under p, if p is on one
    func checkbox(at p: NSPoint) -> Int? {
        guard let lm = layoutManager, let tc = textContainer, !string.isEmpty else { return nil }
        let q = NSPoint(x: p.x - textContainerOrigin.x, y: p.y - textContainerOrigin.y)
        let g = lm.glyphIndex(for: q, in: tc)
        let c = lm.characterIndexForGlyph(at: g)
        let s = string as NSString
        guard Markup.hasBox(s, at: c), c == 0 || s.character(at: c - 1) == 10 else { return nil }
        let r = lm.boundingRect(forGlyphRange: NSRange(location: g, length: 1), in: tc).insetBy(dx: -3, dy: -2)
        return r.contains(q) ? c : nil
    }

    override func resetCursorRects() {
        super.resetCursorRects()
        guard let lm = layoutManager, let tc = textContainer else { return }
        let s = string as NSString
        var i = 0
        while i < s.length {
            let line = s.lineRange(for: NSRange(location: i, length: 0))
            if Markup.hasBox(s, at: line.location) {
                let g = lm.glyphIndexForCharacter(at: line.location)
                var r = lm.boundingRect(forGlyphRange: NSRange(location: g, length: 1), in: tc)
                r.origin.x += textContainerOrigin.x
                r.origin.y += textContainerOrigin.y
                addCursorRect(r, cursor: .pointingHand)
            }
            i = NSMaxRange(line)
        }
    }

    override func insertText(_ string: Any, replacementRange: NSRange) {
        // "[ ]" or "[x]" then a space at the start of a line becomes a checkbox
        if let typed = string as? String, typed == " ", replacementRange.location == NSNotFound {
            let sel = selectedRange()
            let s = self.string as NSString
            let start = s.lineRange(for: NSRange(location: sel.location, length: 0)).location
            if sel.length == 0 && sel.location - start == 3 {
                let pre = s.substring(with: NSRange(location: start, length: 3))
                if pre == "[ ]" || pre == "[x]" || pre == "[X]" {
                    super.insertText((pre == "[ ]" ? Markup.unchecked : Markup.checked) + " ",
                                     replacementRange: NSRange(location: start, length: 3))
                    return
                }
            }
        }
        super.insertText(string, replacementRange: replacementRange)
    }

    override func insertNewline(_ sender: Any?) {
        let s = string as NSString
        let sel = selectedRange()
        let line = s.lineRange(for: NSRange(location: sel.location, length: 0))
        if sel.length == 0 && Markup.hasBox(s, at: line.location) && sel.location >= line.location + 2 {
            var end = NSMaxRange(line)
            if end > line.location && s.character(at: end - 1) == 10 { end -= 1 }
            let rest = s.substring(with: NSRange(location: line.location + 2, length: end - line.location - 2))
            if rest.trimmingCharacters(in: .whitespaces).isEmpty {
                // Enter on an empty checkbox ends the list
                super.insertText("", replacementRange: NSRange(location: line.location, length: end - line.location))
            } else {
                super.insertText("\n" + Markup.unchecked + " ", replacementRange: sel)
            }
            return
        }
        super.insertNewline(sender)
    }
}

// The note's colored top bar (+, title, ☐, A, B and ⋯ buttons, drag to move, double-click to edit the title)
// and its text area. Every edge of the note resizes it, and so does the corner handle.
final class NoteView: NSView, NSTextFieldDelegate {
    weak var note: NoteController?
    let scroll = NSScrollView()
    let grip = GripView()
    let headerH: CGFloat = 28
    let edge: CGFloat = 6
    private var hot = -1
    private var tracking: NSTrackingArea?
    private var resizing: (l: Bool, r: Bool, t: Bool, b: Bool)?
    private var startMouse = NSPoint.zero
    private var startFrame = NSRect.zero
    private var titleField: NSTextField?
    private var titleCancelled = false

    var plusRect: NSRect { NSRect(x: 4, y: 3, width: 22, height: 22) }
    var menuRect: NSRect { NSRect(x: bounds.width - 26, y: 3, width: 22, height: 22) }
    var boldRect: NSRect { NSRect(x: bounds.width - 52, y: 3, width: 22, height: 22) }
    var inkRect: NSRect { NSRect(x: bounds.width - 78, y: 3, width: 22, height: 22) }
    var checkRect: NSRect { NSRect(x: bounds.width - 104, y: 3, width: 22, height: 22) }
    var titleRect: NSRect { NSRect(x: plusRect.maxX + 4, y: 3, width: max(0, checkRect.minX - plusRect.maxX - 8), height: 22) }

    override var isFlipped: Bool { true }

    override init(frame: NSRect) {
        super.init(frame: frame)
        addSubview(scroll)
        addSubview(grip)
        layoutParts()
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        layoutParts()
    }

    private func layoutParts() {
        scroll.frame = NSRect(x: 10, y: headerH + 2, width: max(10, bounds.width - 20), height: max(10, bounds.height - headerH - 12))
        grip.frame = NSRect(x: bounds.width - 18, y: bounds.height - 18, width: 18, height: 18)
        titleField?.frame = titleRect.insetBy(dx: 0, dy: 2)
        window?.invalidateCursorRects(for: self)
    }

    // MARK: Title

    static let titleFont = NSFont.boldSystemFont(ofSize: 13)

    func beginTitleEdit() {
        guard let note = note, titleField == nil else { return }
        let f = NSTextField(frame: titleRect.insetBy(dx: 0, dy: 2))
        f.stringValue = note.data.title
        f.placeholderString = "Title"
        f.font = NoteView.titleFont
        f.textColor = NoteColors.ink
        f.isBordered = false
        f.focusRingType = .none
        f.drawsBackground = true
        f.backgroundColor = NoteColors.back(note.data.color)
        f.cell?.wraps = false
        f.cell?.isScrollable = true
        f.delegate = self
        addSubview(f)
        titleField = f
        titleCancelled = false
        setNeedsDisplay(NSRect(x: 0, y: 0, width: bounds.width, height: headerH))
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(f)
    }

    // Enter keeps the title and Esc puts the old one back; both go on to the note's text
    func control(_ control: NSControl, textView: NSTextView, doCommandBy sel: Selector) -> Bool {
        if sel == #selector(NSResponder.cancelOperation(_:)) { titleCancelled = true }
        guard sel == #selector(NSResponder.cancelOperation(_:)) || sel == #selector(NSResponder.insertNewline(_:)) else { return false }
        if let note = note { window?.makeFirstResponder(note.text) }
        return true
    }

    func controlTextDidEndEditing(_ obj: Notification) {
        guard let f = titleField else { return }
        titleField = nil
        f.delegate = nil
        f.removeFromSuperview()
        if !titleCancelled { note?.setTitle(f.stringValue) }
        setNeedsDisplay(NSRect(x: 0, y: 0, width: bounds.width, height: headerH))
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let note = note else { return }
        NoteColors.back(note.data.color).setFill()
        bounds.fill()
        NoteColors.header(note.data.color).setFill()
        NSRect(x: 0, y: 0, width: bounds.width, height: headerH).fill()
        drawButton(plusRect, hover: hot == 0) { r in
            let cx = r.midX, cy = r.midY, a = r.width * 0.24
            let p = NSBezierPath()
            p.move(to: NSPoint(x: cx - a, y: cy))
            p.line(to: NSPoint(x: cx + a, y: cy))
            p.move(to: NSPoint(x: cx, y: cy - a))
            p.line(to: NSPoint(x: cx, y: cy + a))
            p.lineWidth = 1.6
            p.lineCapStyle = .round
            p.stroke()
        }
        drawButton(menuRect, hover: hot == 1) { r in
            let cx = r.midX, cy = r.midY, a = r.width * 0.24, d: CGFloat = 3
            for i in -1...1 {
                NSBezierPath(ovalIn: NSRect(x: cx + CGFloat(i) * a - d / 2, y: cy - d / 2, width: d, height: d)).fill()
            }
        }
        drawButton(checkRect, hover: hot == 2) { r in
            let box = NSRect(x: r.midX - 5.5, y: r.midY - 5.5, width: 11, height: 11)
            let b = NSBezierPath(roundedRect: box, xRadius: 2, yRadius: 2)
            b.lineWidth = 1.3
            b.stroke()
            let tick = NSBezierPath()
            tick.move(to: NSPoint(x: box.minX + 2.5, y: box.midY))
            tick.line(to: NSPoint(x: box.minX + 4.7, y: box.maxY - 2.5))
            tick.line(to: NSPoint(x: box.maxX - 2.2, y: box.minY + 2.5))
            tick.lineWidth = 1.5
            tick.lineCapStyle = .round
            tick.lineJoinStyle = .round
            tick.stroke()
        }
        drawButton(boldRect, hover: hot == 3) { r in
            let attrs: [NSAttributedString.Key: Any] = [.font: NSFont.boldSystemFont(ofSize: 14), .foregroundColor: NoteColors.glyph]
            let sz = ("B" as NSString).size(withAttributes: attrs)
            ("B" as NSString).draw(at: NSPoint(x: r.midX - sz.width / 2, y: r.midY - sz.height / 2), withAttributes: attrs)
        }
        // "A" over a bar in the text color picked last
        drawButton(inkRect, hover: hot == 4) { r in
            let attrs: [NSAttributedString.Key: Any] = [.font: NSFont.systemFont(ofSize: 13, weight: .semibold), .foregroundColor: NoteColors.glyph]
            let sz = ("A" as NSString).size(withAttributes: attrs)
            ("A" as NSString).draw(at: NSPoint(x: r.midX - sz.width / 2, y: r.midY - sz.height / 2 - 2), withAttributes: attrs)
            NoteColors.inkColor(note.lastInk).setFill()
            NSBezierPath(roundedRect: NSRect(x: r.midX - 6, y: r.maxY - 6, width: 12, height: 3), xRadius: 1, yRadius: 1).fill()
        }
        if titleField == nil && !note.data.title.isEmpty && titleRect.width > 10 {
            let style = NSMutableParagraphStyle()
            style.lineBreakMode = .byTruncatingTail
            let attrs: [NSAttributedString.Key: Any] = [.font: NoteView.titleFont, .foregroundColor: NoteColors.ink, .paragraphStyle: style]
            let h = ceil(NoteView.titleFont.ascender - NoteView.titleFont.descender + NoteView.titleFont.leading)
            (note.data.title as NSString).draw(in: NSRect(x: titleRect.minX, y: titleRect.midY - h / 2, width: titleRect.width, height: h),
                                               withAttributes: attrs)
        }
    }

    private func drawButton(_ r: NSRect, hover: Bool, _ glyph: (NSRect) -> Void) {
        if hover {
            NSColor(white: 0, alpha: 0.1).setFill()
            NSBezierPath(roundedRect: r, xRadius: 4, yRadius: 4).fill()
        }
        NoteColors.glyph.set()
        glyph(r)
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        if let t = tracking { removeTrackingArea(t) }
        let t = NSTrackingArea(rect: .zero, options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                               owner: self, userInfo: nil)
        addTrackingArea(t)
        tracking = t
    }

    override func resetCursorRects() {
        let w = bounds.width, h = bounds.height
        addCursorRect(NSRect(x: 0, y: 0, width: edge, height: h), cursor: .resizeLeftRight)
        addCursorRect(NSRect(x: w - edge, y: 0, width: edge, height: h), cursor: .resizeLeftRight)
        addCursorRect(NSRect(x: 0, y: 0, width: w, height: 3), cursor: .resizeUpDown)
        addCursorRect(NSRect(x: 0, y: h - edge, width: w, height: edge), cursor: .resizeUpDown)
    }

    // The edges belong to the note, not to the text view (which otherwise takes clicks in the margin)
    override func hitTest(_ point: NSPoint) -> NSView? {
        let p = convert(point, from: superview)
        if bounds.contains(p) && !grip.frame.contains(p)
            && (p.x < edge || p.x > bounds.width - edge || p.y < 3 || p.y > bounds.height - edge) { return self }
        return super.hitTest(point)
    }

    private func hit(_ event: NSEvent) -> Int {
        let p = convert(event.locationInWindow, from: nil)
        if plusRect.contains(p) { return 0 }
        if menuRect.contains(p) { return 1 }
        if checkRect.contains(p) { return 2 }
        if boldRect.contains(p) { return 3 }
        if inkRect.contains(p) { return 4 }
        return -1
    }

    private func setHot(_ h: Int) {
        if h == hot { return }
        hot = h
        setNeedsDisplay(NSRect(x: 0, y: 0, width: bounds.width, height: headerH))
    }

    override func mouseMoved(with event: NSEvent) { setHot(hit(event)) }
    override func mouseExited(with event: NSEvent) { setHot(-1) }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func mouseDown(with event: NSEvent) {
        guard let note = note else { return }
        let p = convert(event.locationInWindow, from: nil)
        let l = p.x < edge, r = p.x > bounds.width - edge, t = p.y < 3, b = p.y > bounds.height - edge
        if (l || r || t || b) && hit(event) < 0 {
            resizing = (l, r, t, b)
            startMouse = NSEvent.mouseLocation
            startFrame = window?.frame ?? .zero
            return
        }
        switch hit(event) {
        case 0: note.app.newNote(near: note)
        case 1: _ = note.menu().popUp(positioning: nil, at: NSPoint(x: menuRect.minX, y: menuRect.maxY), in: self)
        case 2: note.toggleChecklist()
        case 3: note.toggleBold()
        case 4: _ = note.inkMenu().popUp(positioning: nil, at: NSPoint(x: inkRect.minX, y: inkRect.maxY), in: self)
        default:
            if p.y < headerH && event.clickCount == 2 {
                beginTitleEdit()
            } else if p.y < headerH {
                window?.performDrag(with: event)
            } else {
                window?.makeFirstResponder(note.text)
            }
        }
    }

    override func mouseDragged(with event: NSEvent) {
        guard let e = resizing, let w = window else { return }
        let m = NSEvent.mouseLocation
        let dx = m.x - startMouse.x, dy = m.y - startMouse.y
        var minX = startFrame.minX, maxX = startFrame.maxX, minY = startFrame.minY, maxY = startFrame.maxY
        if e.l { minX = min(startFrame.minX + dx, maxX - w.minSize.width) }
        if e.r { maxX = max(startFrame.maxX + dx, minX + w.minSize.width) }
        if e.b { minY = min(startFrame.minY + dy, maxY - w.minSize.height) }     // the view is flipped: its bottom is the window's minY
        if e.t { maxY = max(startFrame.maxY + dy, minY + w.minSize.height) }
        w.setFrame(NSRect(x: minX, y: minY, width: maxX - minX, height: maxY - minY), display: true)
    }

    override func mouseUp(with event: NSEvent) { resizing = nil }

    override func rightMouseDown(with event: NSEvent) {
        guard let note = note else { return }
        NSMenu.popUpContextMenu(note.menu(), with: event, for: self)
    }
}

final class NoteController: NSObject, NSWindowDelegate, NSTextViewDelegate {
    unowned let app: AppDelegate
    var data: NoteData  // the version last read from or written to the file
    let window: NoteWindow
    let view: NoteView
    let text: NoteTextView
    private(set) var dirty = false
    private var saveTimer: Timer?
    private var restyling = false
    var ready = false
    var lastInk: String? = "red"  // shown under the A button

    static let regular = NSFont.systemFont(ofSize: 14)
    static let bold = NSFont.boldSystemFont(ofSize: 14)
    static let boxFont = NSFont.systemFont(ofSize: 16)
    // The text color by name (see Markup.inks); restyle() turns it into the color on screen
    static let inkKey = NSAttributedString.Key("noteInk")
    static let linkFinder = try? NSDataDetector(types: NSTextCheckingResult.CheckingType.link.rawValue)

    init(app: AppDelegate, data: NoteData, frame: NSRect) {
        self.app = app
        self.data = data
        window = NoteWindow(contentRect: frame, styleMask: [.borderless, .resizable], backing: .buffered, defer: false)
        view = NoteView(frame: NSRect(origin: .zero, size: frame.size))
        text = NoteTextView(frame: NSRect(x: 0, y: 0, width: max(10, frame.width - 20), height: 100))
        super.init()

        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = true
        window.isReleasedWhenClosed = false
        window.minSize = NSSize(width: 170, height: 120)
        window.acceptsMouseMovedEvents = true
        window.appearance = NSAppearance(named: .aqua)
        window.delegate = self

        view.note = self
        view.wantsLayer = true
        view.layer?.cornerRadius = 8
        view.layer?.masksToBounds = true

        // Only bold, text colors and checkboxes, so both apps show the same thing (restyle() keeps it that way)
        text.note = self
        text.isRichText = true
        text.usesFontPanel = false
        text.importsGraphics = false
        text.allowsUndo = true
        text.drawsBackground = false
        text.isVerticallyResizable = true
        text.isHorizontallyResizable = false
        text.autoresizingMask = [.width]
        text.minSize = NSSize(width: 0, height: 0)
        text.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        text.textContainer?.widthTracksTextView = true
        text.textContainer?.containerSize = NSSize(width: text.frame.width, height: CGFloat.greatestFiniteMagnitude)
        text.textContainerInset = NSSize(width: 0, height: 4)
        text.insertionPointColor = NoteColors.ink
        text.typingAttributes = [.font: NoteController.regular, .foregroundColor: NoteColors.ink]
        setMarkup(data.text)
        text.delegate = self

        view.scroll.documentView = text
        view.scroll.hasVerticalScroller = true
        view.scroll.autohidesScrollers = true
        view.scroll.drawsBackground = false
        view.scroll.borderType = .noBorder
        window.contentView = view
        updateTitle()
    }

    // MARK: Checkboxes, bold and text colors

    // The note as it is saved: plain text with "[ ] ", "[x] ", "**" and "{red}" markers
    var markup: String {
        var bold: [NSRange] = []
        var ink: [InkSpan] = []
        if let ts = text.textStorage {
            let all = NSRange(location: 0, length: ts.length)
            ts.enumerateAttribute(.font, in: all) { v, r, _ in
                if let f = v as? NSFont, f.fontDescriptor.symbolicTraits.contains(.bold) { bold.append(r) }
            }
            ts.enumerateAttribute(NoteController.inkKey, in: all) { v, r, _ in
                if let name = v as? String { ink.append(InkSpan(range: r, name: name)) }
            }
        }
        return Markup.serialize(text.string, bold: bold, ink: ink)
    }

    func attributed(_ markup: String) -> NSAttributedString {
        let (t, bold, ink) = Markup.parse(markup)
        let a = NSMutableAttributedString(string: t, attributes: [.font: NoteController.regular, .foregroundColor: NoteColors.ink])
        for r in bold { a.addAttribute(.font, value: NoteController.bold, range: r) }
        for s in ink { a.addAttribute(NoteController.inkKey, value: s.name, range: s.range) }
        return a
    }

    func setMarkup(_ m: String) {
        text.textStorage?.setAttributedString(attributed(m))
        restyle()
    }

    // Keeps the look in step with the text: only regular or bold, the text colors, boxes a little bigger,
    // the text of a checked item faded and crossed out, and web and mail addresses as links you can click.
    func restyle() {
        guard let ts = text.textStorage, !restyling else { return }
        restyling = true
        defer { restyling = false }
        let s = ts.string as NSString
        let all = NSRange(location: 0, length: ts.length)
        ts.beginEditing()
        ts.enumerateAttribute(.font, in: all) { v, r, _ in
            let isBold = (v as? NSFont)?.fontDescriptor.symbolicTraits.contains(.bold) ?? false
            ts.addAttribute(.font, value: isBold ? NoteController.bold : NoteController.regular, range: r)
        }
        ts.removeAttribute(.strikethroughStyle, range: all)
        ts.enumerateAttribute(NoteController.inkKey, in: all) { v, r, _ in
            ts.addAttribute(.foregroundColor, value: NoteColors.inkColor(v as? String), range: r)
        }
        var i = 0
        while i < s.length {
            let line = s.lineRange(for: NSRange(location: i, length: 0))
            if Markup.hasBox(s, at: line.location) {
                ts.addAttribute(.font, value: NoteController.boxFont, range: NSRange(location: line.location, length: 1))
                ts.addAttribute(.foregroundColor, value: NoteColors.ink, range: NSRange(location: line.location, length: 2))
                if s.character(at: line.location) == Markup.checkedChar {
                    var end = NSMaxRange(line)
                    if end > line.location && s.character(at: end - 1) == 10 { end -= 1 }
                    let r = NSRange(location: line.location + 2, length: max(0, end - line.location - 2))
                    ts.addAttribute(.strikethroughStyle, value: NSUnderlineStyle.single.rawValue, range: r)
                    ts.enumerateAttribute(NoteController.inkKey, in: r) { v, rr, _ in
                        ts.addAttribute(.foregroundColor, value: NoteColors.inkColor(v as? String).withAlphaComponent(0.5), range: rr)
                    }
                }
            }
            i = NSMaxRange(line)
        }
        // Links are found again on every change; they aren't saved in the note file
        ts.removeAttribute(.link, range: all)
        NoteController.linkFinder?.enumerateMatches(in: ts.string, options: [], range: all) { m, _, _ in
            guard let m = m, let url = m.url, ["http", "https", "mailto"].contains(url.scheme?.lowercased() ?? "") else { return }
            ts.addAttribute(.link, value: url, range: m.range)
        }
        ts.endEditing()
        // New typing takes the look of the line it's on, never the box's
        var typing = text.typingAttributes
        typing.removeValue(forKey: .strikethroughStyle)
        typing.removeValue(forKey: .link)
        typing[.foregroundColor] = NoteColors.inkColor(typing[NoteController.inkKey] as? String)
        if let f = typing[.font] as? NSFont, f.pointSize != 14 { typing[.font] = NoteController.regular }
        text.typingAttributes = typing
        text.window?.invalidateCursorRects(for: text)
    }

    // Replaces a range as an undoable edit
    private func edit(_ r: NSRange, _ s: String) {
        guard text.shouldChangeText(in: r, replacementString: s) else { return }
        text.textStorage?.replaceCharacters(in: r, with: s)
        text.didChangeText()
    }

    func toggleCheck(at i: Int) {
        let s = text.string as NSString
        guard Markup.hasBox(s, at: i) else { return }
        let sel = text.selectedRange()
        edit(NSRange(location: i, length: 1), s.character(at: i) == Markup.uncheckedChar ? Markup.checked : Markup.unchecked)
        text.setSelectedRange(sel)
    }

    // Adds a checkbox to each selected line, or takes them away if they all have one
    @objc func toggleChecklist() {
        let s = text.string as NSString
        let lines = s.lineRange(for: text.selectedRange())
        var starts: [Int] = []
        var i = lines.location
        repeat {
            starts.append(i)
            i = NSMaxRange(s.lineRange(for: NSRange(location: i, length: 0)))
        } while i < NSMaxRange(lines)
        let allBoxed = starts.allSatisfy { Markup.hasBox(s, at: $0) }
        text.undoManager?.beginUndoGrouping()
        for start in starts.reversed() {
            if allBoxed {
                edit(NSRange(location: start, length: 2), "")
            } else if !Markup.hasBox(s, at: start) {
                edit(NSRange(location: start, length: 0), Markup.unchecked + " ")
            }
        }
        text.undoManager?.endUndoGrouping()
        window.makeFirstResponder(text)
    }

    @objc func toggleBold() {
        let r = text.selectedRange()
        guard let ts = text.textStorage else { return }
        if r.length == 0 {
            let isBold = (text.typingAttributes[.font] as? NSFont)?.fontDescriptor.symbolicTraits.contains(.bold) ?? false
            text.typingAttributes[.font] = isBold ? NoteController.regular : NoteController.bold
            return
        }
        var allBold = true
        ts.enumerateAttribute(.font, in: r) { v, _, stop in
            if !((v as? NSFont)?.fontDescriptor.symbolicTraits.contains(.bold) ?? false) { allBold = false; stop.pointee = true }
        }
        guard text.shouldChangeText(in: r, replacementString: nil) else { return }
        ts.addAttribute(.font, value: allBold ? NoteController.regular : NoteController.bold, range: r)
        text.didChangeText()
        window.makeFirstResponder(text)
    }

    // Colors the selected text, or what is typed next; nil is the usual ink
    func setInk(_ name: String?) {
        lastInk = name
        view.setNeedsDisplay(NSRect(x: 0, y: 0, width: view.bounds.width, height: view.headerH))
        window.makeFirstResponder(text)
        let r = text.selectedRange()
        guard r.length > 0 else {
            text.typingAttributes[NoteController.inkKey] = name
            text.typingAttributes[.foregroundColor] = NoteColors.inkColor(name)
            return
        }
        guard let ts = text.textStorage, text.shouldChangeText(in: r, replacementString: nil) else { return }
        if let n = name { ts.addAttribute(NoteController.inkKey, value: n, range: r) } else { ts.removeAttribute(NoteController.inkKey, range: r) }
        text.didChangeText()
    }

    @objc func pickInk(_ sender: NSMenuItem) { setInk(sender.tag == 0 ? nil : Markup.inks[sender.tag - 1]) }

    func inkMenu() -> NSMenu {
        let m = NSMenu()
        for (i, label) in NoteColors.inkLabels.enumerated() {
            let item = m.addItem(withTitle: label, action: #selector(pickInk(_:)), keyEquivalent: "")
            item.tag = i
            item.target = self
            item.image = NoteColors.inkSwatch(i)
        }
        return m
    }

    // Pastes plain text; any checkbox, bold and color markers in it come in formatted
    func pasteMarkup() {
        guard let s = NSPasteboard.general.string(forType: .string) else { return }
        text.insertText(attributed(NoteFile.normalize(s)), replacementRange: text.selectedRange())
    }

    func setTitle(_ t: String) {
        let clean = NoteFile.cleanTitle(t)
        guard clean != data.title else { return }
        data.title = clean
        view.needsDisplay = true
        updateTitle()
        saveNow()
    }

    @objc func editTitle(_ sender: Any?) { view.beginTitleEdit() }

    // The window's name (in the Window menu and Mission Control): the title, or else the first line
    private func updateTitle() {
        if !data.title.isEmpty {
            window.title = String(data.title.prefix(60))
            return
        }
        var first = markup.trimmingCharacters(in: .whitespacesAndNewlines).components(separatedBy: "\n").first ?? ""
        if first.hasPrefix("[ ] ") || first.hasPrefix("[x] ") { first = String(first.dropFirst(4)) }
        first = first.replacingOccurrences(of: "**", with: "")
        for name in Markup.inks { first = first.replacingOccurrences(of: "{\(name)}", with: "").replacingOccurrences(of: "{/\(name)}", with: "") }
        window.title = first.isEmpty ? "Sticky Note" : String(first.prefix(60))
    }

    var onTop: Bool {
        get { window.level == .floating }
        set { window.level = newValue ? .floating : .normal }
    }

    // MARK: Saving and syncing

    func textDidChange(_ notification: Notification) {
        restyle()
        dirty = true
        scheduleSave(after: 0.7)
        updateTitle()
    }

    private func scheduleSave(after delay: TimeInterval) {
        saveTimer?.invalidate()
        saveTimer = Timer.scheduledTimer(withTimeInterval: delay, repeats: false) { [weak self] _ in self?.saveNow() }
    }

    @discardableResult
    func saveNow() -> Bool {
        saveTimer?.invalidate()
        saveTimer = nil
        var d = data
        d.text = markup
        d.modified = NoteFile.now()
        d.by = NoteFile.machine
        if app.write(d) {
            data = d
            dirty = false
            return true
        }
        markDirty()
        return false
    }

    // Keeps retrying until the folder is reachable.
    func markDirty() {
        dirty = true
        scheduleSave(after: 3)
    }

    func applyRemote(_ d: NoteData) {
        if d.modified == data.modified && d.color == data.color && d.title == data.title && d.text == data.text { return }  // our own save
        if dirty {
            app.conflictCopy(d, near: self)
            return
        }
        if d.text != markup {
            let caret = text.selectedRange().location
            setMarkup(d.text)
            text.setSelectedRange(NSRange(location: min(caret, (text.string as NSString).length), length: 0))
        }
        data = d
        view.needsDisplay = true
        updateTitle()
    }

    func close() {
        saveTimer?.invalidate()
        saveTimer = nil
        dirty = false
        text.delegate = nil
        window.delegate = nil
        window.orderOut(nil)
        window.close()
    }

    func windowDidMove(_ notification: Notification) {
        if ready { app.placementChanged(self) }
    }

    func windowDidResize(_ notification: Notification) {
        if ready { app.placementChanged(self) }
        window.invalidateShadow()
    }

    func windowDidResignKey(_ notification: Notification) {
        if dirty { saveNow() }
    }

    // MARK: The ⋯ menu

    func menu() -> NSMenu {
        let m = NSMenu()
        for (i, label) in NoteColors.labels.enumerated() {
            let item = m.addItem(withTitle: label, action: #selector(pickColor(_:)), keyEquivalent: "")
            item.tag = i
            item.target = self
            item.image = NoteColors.swatch(i)
            item.state = NoteColors.index(data.color) == i ? .on : .off
        }
        m.addItem(.separator())
        m.addItem(withTitle: "Checklist", action: #selector(toggleChecklist), keyEquivalent: "").target = self
        m.addItem(withTitle: "Bold", action: #selector(toggleBold), keyEquivalent: "").target = self
        m.addItem(withTitle: "Text Color", action: nil, keyEquivalent: "").submenu = inkMenu()
        m.addItem(withTitle: data.title.isEmpty ? "Add Title" : "Edit Title", action: #selector(editTitle(_:)), keyEquivalent: "").target = self
        m.addItem(.separator())
        let top = m.addItem(withTitle: "Keep on Top", action: #selector(toggleTop(_:)), keyEquivalent: "")
        top.target = self
        top.state = onTop ? .on : .off
        m.addItem(.separator())
        m.addItem(withTitle: "New Note", action: #selector(newNote(_:)), keyEquivalent: "").target = self
        m.addItem(withTitle: "Delete Note", action: #selector(deleteNote(_:)), keyEquivalent: "").target = self
        return m
    }

    @objc func pickColor(_ sender: NSMenuItem) {
        data.color = NoteColors.names[sender.tag]
        view.needsDisplay = true
        saveNow()
    }

    @objc func toggleTop(_ sender: Any?) {
        onTop.toggle()
        app.placementChanged(self)
    }

    @objc func newNote(_ sender: Any?) { app.newNote(near: self) }
    @objc func deleteNote(_ sender: Any?) { app.delete(self) }
}

// MARK: - App icon (used by build.sh: StickyNotes --write-iconset <dir>)

enum IconArt {
    static func draw(_ s: CGFloat) {
        let m = s * 0.1
        let r = NSRect(x: m, y: m, width: s - 2 * m, height: s - 2 * m)
        let fold = r.width * 0.28
        let body = NSBezierPath()
        body.move(to: NSPoint(x: r.minX, y: r.maxY))
        body.line(to: NSPoint(x: r.maxX, y: r.maxY))
        body.line(to: NSPoint(x: r.maxX, y: r.minY + fold))
        body.line(to: NSPoint(x: r.maxX - fold, y: r.minY))
        body.line(to: NSPoint(x: r.minX, y: r.minY))
        body.close()
        NoteColors.rgb(0xFFE25A).setFill()
        body.fill()

        NSGraphicsContext.saveGraphicsState()
        body.addClip()
        NoteColors.rgb(0xF0C83C).setFill()
        NSRect(x: r.minX, y: r.maxY - r.height * 0.2, width: r.width, height: r.height * 0.2).fill()
        NSGraphicsContext.restoreGraphicsState()

        let flap = NSBezierPath()
        flap.move(to: NSPoint(x: r.maxX, y: r.minY + fold))
        flap.line(to: NSPoint(x: r.maxX - fold, y: r.minY + fold))
        flap.line(to: NSPoint(x: r.maxX - fold, y: r.minY))
        flap.close()
        NoteColors.rgb(0xE6BE32).setFill()
        flap.fill()

        guard s >= 24 else { return }
        NoteColors.rgb(0x96781E).setStroke()
        let lines: [(CGFloat, CGFloat)] = [(0.42, 0.62), (0.58, 0.62), (0.74, 0.4)]
        for (fromTop, length) in lines {
            let y = r.maxY - fromTop * r.height
            let p = NSBezierPath()
            p.lineWidth = s * 0.05
            p.lineCapStyle = .round
            p.move(to: NSPoint(x: r.minX + 0.14 * r.width, y: y))
            p.line(to: NSPoint(x: r.minX + (0.14 + length) * r.width, y: y))
            p.stroke()
        }
    }

    static func png(_ size: Int) -> Data? {
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size,
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0) else { return nil }
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        draw(CGFloat(size))
        NSGraphicsContext.restoreGraphicsState()
        return rep.representation(using: .png, properties: [:])
    }

    static func writeIconset(to dir: String) throws {
        try FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
        for base in [16, 32, 128, 256, 512] {
            for scale in [1, 2] {
                let name = scale == 1 ? "icon_\(base)x\(base).png" : "icon_\(base)x\(base)@2x.png"
                guard let data = png(base * scale) else { continue }
                try data.write(to: URL(fileURLWithPath: dir).appendingPathComponent(name))
            }
        }
    }
}

// MARK: - App: the notes folder and the open notes

final class AppDelegate: NSObject, NSApplicationDelegate {
    static let welcome = "Welcome to Sticky Notes!\n\nWrite anything here. Notes save as you type and sync through your Google Drive folder, so they also appear on your other computer.\n\n+  makes a new note.\n☐  makes a checklist: click a box to check it.\nB  makes the selected text bold.\nA  colors the selected text.\n⋯  changes the color or deletes the note.\nDouble-click the top bar to give a note a title.\nDrag the top bar to move a note, and an edge or the corner to resize it."

    let local = LocalState()
    let fm = FileManager.default
    var notes: [String: NoteController] = [:]
    // File stamp (time + size) of each note file as last read or written, to spot changes.
    var seen: [String: String] = [:]
    var folder = ""
    var folderOverride: String?
    var timer: Timer?
    var cascade = 0
    var listedOnce = false
    var offerWelcome = false

    func applicationDidFinishLaunching(_ notification: Notification) {
        buildMainMenu()
        NSApp.activate(ignoringOtherApps: true)
        if let f = folderOverride {
            folder = f
        } else if !local.folder.isEmpty {
            folder = local.folder
        } else {
            offerWelcome = true
            folder = firstRunFolder()
            local.folder = folder
        }
        scan()
        timer = Timer.scheduledTimer(withTimeInterval: 2.5, repeats: true) { [weak self] _ in self?.scan() }
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showAll(nil)
        return true
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { false }

    func applicationWillTerminate(_ notification: Notification) { flushAll() }

    // MARK: Folder

    func firstRunFolder() -> String {
        let found = DriveLocator.find()
        let root: String? = found.count == 1 ? found[0].path : chooseRoot(found, intro: found.isEmpty
            ? "Google Drive for desktop wasn't found. Choose your Google Drive folder (My Drive), or another folder that syncs between your computers."
            : "Choose the Google Drive account where your notes should sync. Use the same account on your other computer.")
        if let root = root { return notesFolder(in: root) }
        alert("Notes will be kept in Documents/StickyNotes on this Mac only.",
              "To sync them, choose a Google Drive folder later: Sticky Notes menu → Change Sync Folder….")
        let docs = fm.urls(for: .documentDirectory, in: .userDomainMask).first?.path ?? NSHomeDirectory()
        return notesFolder(in: docs)
    }

    func chooseRoot(_ candidates: [DriveLocator.Candidate], intro: String) -> String? {
        let a = NSAlert()
        a.messageText = "Where should your notes sync?"
        a.informativeText = intro
        let shown = Array(candidates.prefix(3))
        for c in shown { a.addButton(withTitle: c.label) }
        a.addButton(withTitle: "Choose Folder…")
        a.addButton(withTitle: "Cancel")
        NSApp.activate(ignoringOtherApps: true)
        let choice = a.runModal().rawValue - NSApplication.ModalResponse.alertFirstButtonReturn.rawValue
        if choice >= 0 && choice < shown.count { return shown[choice].path }
        guard choice == shown.count else { return nil }

        let panel = NSOpenPanel()
        panel.canChooseDirectories = true
        panel.canChooseFiles = false
        panel.canCreateDirectories = true
        panel.prompt = "Choose"
        panel.message = "Choose your Google Drive folder (My Drive). Notes are kept in a StickyNotes folder inside it."
        if let first = shown.first { panel.directoryURL = URL(fileURLWithPath: first.path) }
        return panel.runModal() == .OK ? panel.url?.path : nil
    }

    func notesFolder(in root: String) -> String {
        (root as NSString).lastPathComponent == "StickyNotes" ? root : (root as NSString).appendingPathComponent("StickyNotes")
    }

    // The folder is only created when its parent exists, so a Drive that hasn't started yet
    // is waited for instead of being replaced by an empty local folder.
    func ensureFolder() -> Bool {
        var isDir: ObjCBool = false
        if fm.fileExists(atPath: folder, isDirectory: &isDir) { return isDir.boolValue }
        guard fm.fileExists(atPath: (folder as NSString).deletingLastPathComponent) else { return false }
        return (try? fm.createDirectory(atPath: folder, withIntermediateDirectories: false)) != nil
    }

    func path(_ id: String) -> String { (folder as NSString).appendingPathComponent(id + ".txt") }

    func stamp(_ path: String) -> String? {
        guard let a = try? fm.attributesOfItem(atPath: path) else { return nil }
        let time = (a[.modificationDate] as? Date)?.timeIntervalSince1970 ?? 0
        let size = (a[.size] as? NSNumber)?.int64Value ?? -1
        return "\(time)|\(size)"
    }

    func write(_ d: NoteData) -> Bool {
        guard ensureFolder() else { return false }
        let p = path(d.id)
        do {
            // Overwrite in place, never atomically: an atomic save writes into a temporary
            // "(A Document Being Saved By …)" folder and swaps the file in, which Google Drive
            // syncs as a new folder plus a deleted note.
            try Data(NoteFile.serialize(d).utf8).write(to: URL(fileURLWithPath: p))
        } catch {
            return false
        }
        if let st = stamp(p) { seen[d.id] = st }
        return true
    }

    // Reads every note file that changed since the last look; opens new notes, updates
    // changed ones and closes notes whose file was deleted (on the other computer).
    func scan() {
        guard ensureFolder(), let names = try? fm.contentsOfDirectory(atPath: folder) else { return }
        var present = Set<String>()
        for name in names where name.lowercased().hasSuffix(".txt") && !name.hasPrefix(".") {
            let id = String(name.dropLast(4))
            present.insert(id)
            let p = (folder as NSString).appendingPathComponent(name)
            guard let st = stamp(p), seen[id] != st else { continue }
            guard let content = try? String(contentsOfFile: p, encoding: .utf8) else { continue }  // try again next time
            seen[id] = st
            let d = NoteFile.parse(id: id, content: content)
            if let c = notes[id] { c.applyRemote(d) } else { open(d, activate: false) }
        }
        for (id, c) in notes where !present.contains(id) {
            if c.dirty {
                c.saveNow()  // edited here after it was deleted elsewhere: keep it
            } else {
                notes[id] = nil
                seen[id] = nil
                c.close()
            }
        }

        if !listedOnce {
            listedOnce = true
            if offerWelcome && notes.isEmpty { createNote(text: AppDelegate.welcome, color: "yellow", near: nil, activate: true) }
        }
    }

    // MARK: Notes

    @discardableResult
    func open(_ d: NoteData, activate: Bool, near: NoteController? = nil) -> NoteController {
        let frame: NSRect
        var top = false
        if near == nil, let p = local.placement(d.id), onScreen(p.rect) {
            frame = p.rect
            top = p.top
        } else {
            frame = defaultFrame(near: near)
        }
        let c = NoteController(app: self, data: d, frame: frame)
        c.onTop = top
        notes[d.id] = c
        if activate {
            NSApp.activate(ignoringOtherApps: true)
            c.window.makeKeyAndOrderFront(nil)
            c.window.makeFirstResponder(c.text)
            c.text.setSelectedRange(NSRange(location: (c.text.string as NSString).length, length: 0))
        } else {
            c.window.orderFront(nil)
        }
        c.window.invalidateShadow()
        c.ready = true
        placementChanged(c)
        return c
    }

    func defaultFrame(near: NoteController?) -> NSRect {
        let size = NSSize(width: 260, height: 260)
        if let n = near {
            let f = n.window.frame
            let r = NSRect(x: f.minX + 30, y: f.maxY - 30 - size.height, width: size.width, height: size.height)
            if let s = n.window.screen ?? NSScreen.main, s.visibleFrame.contains(r) { return r }
        }
        let vf = (NSScreen.main ?? NSScreen.screens.first)?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1280, height: 800)
        let step = CGFloat(cascade % 8) * 30
        cascade += 1
        return NSRect(x: vf.maxX - size.width - 40 - step, y: vf.maxY - size.height - 40 - step, width: size.width, height: size.height)
    }

    func onScreen(_ r: NSRect) -> Bool {
        NSScreen.screens.contains { s in
            let i = s.visibleFrame.intersection(r)
            return i.width >= 60 && i.height >= 40
        }
    }

    func placementChanged(_ c: NoteController) {
        guard notes[c.data.id] === c else { return }
        local.setPlacement(c.data.id, c.window.frame, c.onTop)
    }

    func createNote(text: String, color: String, title: String = "", near: NoteController?, activate: Bool) {
        var d = NoteData(id: NoteFile.newId())
        d.color = color
        d.title = title
        d.text = text
        d.modified = NoteFile.now()
        d.by = NoteFile.machine
        let saved = write(d)
        let c = open(d, activate: activate, near: near)
        if !saved { c.markDirty() }
    }

    func newNote(near: NoteController?) {
        createNote(text: "", color: near?.data.color ?? "yellow", near: near, activate: true)
    }

    // The note changed on the other computer while it was being edited here: keep the other
    // version as a new note next to it (this one is saved over the file a moment later).
    func conflictCopy(_ remote: NoteData, near: NoteController) {
        createNote(text: remote.text, color: remote.color, title: remote.title, near: near, activate: false)
    }

    func delete(_ c: NoteController) {
        if !c.text.string.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            let a = NSAlert()
            a.messageText = "Delete this note?"
            a.informativeText = "It moves to the Trash folder inside your notes folder."
            a.addButton(withTitle: "Delete")
            a.addButton(withTitle: "Cancel")
            guard a.runModal() == .alertFirstButtonReturn else { return }
        }
        let id = c.data.id
        let src = path(id)
        if fm.fileExists(atPath: src) {
            let trash = (folder as NSString).appendingPathComponent("Trash")
            do {
                try fm.createDirectory(atPath: trash, withIntermediateDirectories: true)
                try fm.moveItem(atPath: src, toPath: (trash as NSString).appendingPathComponent(NoteFile.trashName(id)))
            } catch {
                alert("Couldn't delete the note.", error.localizedDescription)
                return
            }
        }
        notes[id] = nil
        seen[id] = nil
        local.forget(id)
        c.close()
    }

    var keyNote: NoteController? { notes.values.first { $0.window.isKeyWindow } }

    func flushAll() {
        for c in notes.values where c.dirty { c.saveNow() }
    }

    // MARK: Menu commands

    @objc func showAll(_ sender: Any?) {
        if notes.isEmpty {
            newNote(near: nil)
            return
        }
        NSApp.activate(ignoringOtherApps: true)
        for c in notes.values { c.window.orderFront(nil) }
        (keyNote ?? notes.values.first)?.window.makeKey()
    }

    @objc func newNoteCommand(_ sender: Any?) { newNote(near: keyNote) }

    @objc func deleteNoteCommand(_ sender: Any?) {
        if let c = keyNote { delete(c) }
    }

    @objc func boldCommand(_ sender: Any?) { keyNote?.toggleBold() }
    @objc func checklistCommand(_ sender: Any?) { keyNote?.toggleChecklist() }
    @objc func inkCommand(_ sender: NSMenuItem) { keyNote?.setInk(sender.tag == 0 ? nil : Markup.inks[sender.tag - 1]) }
    @objc func titleCommand(_ sender: Any?) { keyNote?.view.beginTitleEdit() }

    @objc func openFolder(_ sender: Any?) {
        if ensureFolder() {
            NSWorkspace.shared.open(URL(fileURLWithPath: folder))
        } else {
            alert("The notes folder isn't available right now.", "\(folder)\n\nIs Google Drive running?")
        }
    }

    @objc func changeFolder(_ sender: Any?) {
        let intro = "Notes are kept in a StickyNotes folder inside the folder you choose. Choose the same Google Drive folder on all your computers.\n\nNow: \(folder)"
        guard let root = chooseRoot(DriveLocator.find(), intro: intro) else { return }
        let next = notesFolder(in: root)
        if next == folder { return }
        flushAll()
        for c in notes.values { c.close() }
        notes.removeAll()
        seen.removeAll()
        let old = folder
        folder = next
        if folderOverride == nil { local.folder = next }
        offerCopy(from: old)
        scan()
    }

    // Moving from a local folder to Drive (or between Drive accounts): bring the notes along.
    func offerCopy(from old: String) {
        let txt: (String) -> [String] = { dir in
            ((try? self.fm.contentsOfDirectory(atPath: dir)) ?? []).filter { $0.lowercased().hasSuffix(".txt") && !$0.hasPrefix(".") }
        }
        let oldNotes = txt(old)
        guard !oldNotes.isEmpty, ensureFolder(), txt(folder).isEmpty else { return }
        let a = NSAlert()
        a.messageText = "Copy your \(oldNotes.count) notes to the new folder?"
        a.informativeText = "From: \(old)"
        a.addButton(withTitle: "Copy")
        a.addButton(withTitle: "Don't Copy")
        guard a.runModal() == .alertFirstButtonReturn else { return }
        for name in oldNotes {
            let dst = (folder as NSString).appendingPathComponent(name)
            if fm.fileExists(atPath: dst) { continue }
            do {
                try fm.copyItem(atPath: (old as NSString).appendingPathComponent(name), toPath: dst)
            } catch {
                alert("Couldn't copy the notes.", error.localizedDescription)
                return
            }
        }
    }

    @objc func toggleLogin(_ item: NSMenuItem) {
        guard #available(macOS 13.0, *) else { return }
        do {
            if SMAppService.mainApp.status == .enabled {
                try SMAppService.mainApp.unregister()
            } else {
                try SMAppService.mainApp.register()
            }
        } catch {
            alert("Couldn't change Open at Login.", error.localizedDescription)
        }
        item.state = SMAppService.mainApp.status == .enabled ? .on : .off
    }

    @objc func showAbout(_ sender: Any?) {
        alert("Sticky Notes 1.2.1",
              "Notes folder:\n\(folder)\n\nEach note is a text file in this folder. Google Drive for desktop copies it to your other computers, where Sticky Notes for Mac or Windows shows it. Deleted notes go to the Trash folder inside it.\n\n© 2026 Mauricio Mendivelso. MIT License.\nProvided \"as is\", without warranty of any kind.")
    }

    func alert(_ title: String, _ info: String) {
        let a = NSAlert()
        a.messageText = title
        a.informativeText = info
        NSApp.activate(ignoringOtherApps: true)
        _ = a.runModal()
    }

    func buildMainMenu() {
        let main = NSMenu()

        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About Sticky Notes", action: #selector(showAbout(_:)), keyEquivalent: "").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Open Notes Folder", action: #selector(openFolder(_:)), keyEquivalent: "").target = self
        appMenu.addItem(withTitle: "Change Sync Folder…", action: #selector(changeFolder(_:)), keyEquivalent: "").target = self
        if #available(macOS 13.0, *) {
            let login = appMenu.addItem(withTitle: "Open at Login", action: #selector(toggleLogin(_:)), keyEquivalent: "")
            login.target = self
            login.state = SMAppService.mainApp.status == .enabled ? .on : .off
        }
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Hide Sticky Notes", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Quit Sticky Notes", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        add(appMenu, title: "Sticky Notes", to: main)

        let file = NSMenu()
        file.addItem(withTitle: "New Note", action: #selector(newNoteCommand(_:)), keyEquivalent: "n").target = self
        file.addItem(withTitle: "Show All Notes", action: #selector(showAll(_:)), keyEquivalent: "").target = self
        file.addItem(.separator())
        file.addItem(withTitle: "Delete Note", action: #selector(deleteNoteCommand(_:)), keyEquivalent: "").target = self
        add(file, title: "File", to: main)

        let edit = NSMenu()
        edit.addItem(withTitle: "Undo", action: Selector(("undo:")), keyEquivalent: "z")
        edit.addItem(withTitle: "Redo", action: Selector(("redo:")), keyEquivalent: "z").keyEquivalentModifierMask = [.command, .shift]
        edit.addItem(.separator())
        edit.addItem(withTitle: "Cut", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        edit.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        edit.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        edit.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        add(edit, title: "Edit", to: main)

        let format = NSMenu()
        format.addItem(withTitle: "Bold", action: #selector(boldCommand(_:)), keyEquivalent: "b").target = self
        let check = format.addItem(withTitle: "Checklist", action: #selector(checklistCommand(_:)), keyEquivalent: "l")
        check.keyEquivalentModifierMask = [.command, .shift]
        check.target = self
        let inks = NSMenu()
        for (i, label) in NoteColors.inkLabels.enumerated() {
            let item = inks.addItem(withTitle: label, action: #selector(inkCommand(_:)), keyEquivalent: "")
            item.tag = i
            item.target = self
            item.image = NoteColors.inkSwatch(i)
        }
        format.addItem(withTitle: "Text Color", action: nil, keyEquivalent: "").submenu = inks
        format.addItem(.separator())
        let title = format.addItem(withTitle: "Edit Title", action: #selector(titleCommand(_:)), keyEquivalent: "t")
        title.keyEquivalentModifierMask = [.command, .shift]
        title.target = self
        add(format, title: "Format", to: main)

        let windowMenu = NSMenu()
        windowMenu.addItem(withTitle: "Bring All to Front", action: #selector(NSApplication.arrangeInFront(_:)), keyEquivalent: "")
        add(windowMenu, title: "Window", to: main)
        NSApp.windowsMenu = windowMenu

        NSApp.mainMenu = main
    }

    private func add(_ menu: NSMenu, title: String, to main: NSMenu) {
        menu.title = title
        let item = NSMenuItem(title: title, action: nil, keyEquivalent: "")
        item.submenu = menu
        main.addItem(item)
    }
}

let args = CommandLine.arguments
if args.count == 2 && args[1] == "--selftest" {
    exit(selfTest())
}
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}

let app = NSApplication.shared
let delegate = AppDelegate()
if args.count == 3 && args[1] == "--folder" { delegate.folderOverride = args[2] }
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
