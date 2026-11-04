// Citorio for Mac: every so often a small window pops up with a quote, then hides again.
// It lives in the menu bar. The quotes are plain-text files in Documents/Citorio, one file per
// category, so new categories and quotes are added by adding or editing files.
// A tribute to Citorio 1.1 (1995-96) for Windows 3.1 by Javier Cámara Melgosa, whose
// collection of quotes it ships with. Twin of the Windows app in ../windows.
// Builds with swiftc (Xcode or the Command Line Tools); see build.sh.
import AppKit
import CoreText
import ServiceManagement
import UniformTypeIdentifiers

// MARK: - Quote files

struct Quote {
    let text: String
    let author: String
    let cat: Int
    var key: String { QuoteFile.hash(text) }
}

struct Category {
    let name: String
    let icons: [String]
    let file: URL
    var count: Int
}

enum QuoteFile {
    // UTF-8 (with or without BOM), UTF-16 with BOM, or else Windows-1252 (old Notepad "ANSI")
    static func decode(_ d: Data) -> String {
        let b = [UInt8](d.prefix(3))
        if b.count >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF {
            return String(data: d.dropFirst(3), encoding: .utf8) ?? String(data: d.dropFirst(3), encoding: .windowsCP1252) ?? ""
        }
        if b.count >= 2 && b[0] == 0xFF && b[1] == 0xFE { return String(data: d.dropFirst(2), encoding: .utf16LittleEndian) ?? "" }
        if b.count >= 2 && b[0] == 0xFE && b[1] == 0xFF { return String(data: d.dropFirst(2), encoding: .utf16BigEndian) ?? "" }
        return String(data: d, encoding: .utf8) ?? String(data: d, encoding: .windowsCP1252) ?? ""
    }

    static func isAuthorLine(_ s: String) -> Bool {
        let t = s.trimmingCharacters(in: .whitespaces)
        return t.hasPrefix("—") || t.hasPrefix("--")
    }

    static func authorText(_ s: String) -> String {
        var t = Substring(s.trimmingCharacters(in: .whitespaces))
        while let c = t.first, c == "—" || c == "-" { t = t.dropFirst() }
        return t.trimmingCharacters(in: .whitespaces)
    }

    // Format (same rules as the Windows app):
    //   Lines at the top starting with # are comments; "# name: X" names the category (else the file name)
    //   and "# icon: 😄" gives its icon (several, separated by spaces, are used in turn).
    //   Quotes are separated by lines with only %. A last line starting with — or -- is the author.
    //   A file without any % line has one quote per line, with the author after " — " or " -- ".
    static func parse(_ raw: String, fallbackName: String) -> (name: String, icons: [String], quotes: [(String, String)]) {
        let text = raw.replacingOccurrences(of: "\r\n", with: "\n").replacingOccurrences(of: "\r", with: "\n")
        let lines = text.components(separatedBy: "\n")
        var name = fallbackName, icons: [String] = []
        var i = 0
        while i < lines.count {
            let t = lines[i].trimmingCharacters(in: .whitespaces)
            if t.isEmpty { i += 1; continue }
            guard t.hasPrefix("#") else { break }
            let body = t.dropFirst().trimmingCharacters(in: .whitespaces)
            if let r = body.range(of: ":") {
                let key = body[..<r.lowerBound].trimmingCharacters(in: .whitespaces).lowercased()
                let val = body[r.upperBound...].trimmingCharacters(in: .whitespaces)
                if key == "name" && !val.isEmpty { name = val }
                if key == "icon" { icons = val.split(whereSeparator: { $0 == " " || $0 == "\t" }).map(String.init) }
            }
            i += 1
        }
        let body = Array(lines[i...])
        var quotes: [(String, String)] = []
        if body.contains(where: { $0.trimmingCharacters(in: .whitespaces) == "%" }) {
            var chunk: [String] = []
            func flush() {
                while let f = chunk.first, f.trimmingCharacters(in: .whitespaces).isEmpty { chunk.removeFirst() }
                while let l = chunk.last, l.trimmingCharacters(in: .whitespaces).isEmpty { chunk.removeLast() }
                if chunk.isEmpty { return }
                var author = ""
                if chunk.count > 1 && isAuthorLine(chunk[chunk.count - 1]) {
                    author = authorText(chunk.removeLast())
                    while let l = chunk.last, l.trimmingCharacters(in: .whitespaces).isEmpty { chunk.removeLast() }
                }
                quotes.append((chunk.joined(separator: "\n"), author))
                chunk = []
            }
            for l in body {
                if l.trimmingCharacters(in: .whitespaces) == "%" { flush() } else { chunk.append(l) }
            }
            flush()
        } else {
            for l in body {
                let t = l.trimmingCharacters(in: .whitespaces)
                if t.isEmpty { continue }
                var q = t, a = ""
                for sep in [" — ", " -- "] {
                    if let r = t.range(of: sep, options: .backwards) {
                        q = String(t[..<r.lowerBound]).trimmingCharacters(in: .whitespaces)
                        a = String(t[r.upperBound...]).trimmingCharacters(in: .whitespaces)
                        break
                    }
                }
                if !q.isEmpty { quotes.append((q, a)) }
            }
        }
        return (name, icons, quotes)
    }

    // FNV-1a of the text: remembers which quotes were shown, even if files are edited around them
    static func hash(_ s: String) -> String {
        var h: UInt32 = 2166136261
        for b in s.utf8 { h = (h ^ UInt32(b)) &* 16777619 }
        return String(format: "%08x", h)
    }
}

final class Store {
    let folder: URL
    var cats: [Category] = []
    var quotes: [Quote] = []

    init(folder: URL) { self.folder = folder }

    static var userFolder: URL {
        if let t = ProcessInfo.processInfo.environment["CITORIO_FOLDER"], !t.isEmpty { return URL(fileURLWithPath: t) }   // for testing
        return FileManager.default.urls(for: .documentDirectory, in: .userDomainMask)[0].appendingPathComponent("Citorio")
    }
    static var builtInFolder: URL? {
        if let r = Bundle.main.resourceURL?.appendingPathComponent("Quotes"), FileManager.default.fileExists(atPath: r.path) { return r }
        return nil
    }

    func listFiles() -> [URL] {
        let fm = FileManager.default
        guard let items = try? fm.contentsOfDirectory(at: folder, includingPropertiesForKeys: nil) else { return [] }
        return items.filter {
            $0.pathExtension.lowercased() == "txt" && !$0.lastPathComponent.hasPrefix("_") && !$0.lastPathComponent.hasPrefix(".")
        }.sorted { $0.lastPathComponent.localizedStandardCompare($1.lastPathComponent) == .orderedAscending }
    }

    func load() {
        var cs: [Category] = [], qs: [Quote] = []
        var seen = Set<String>()
        for f in listFiles() {
            guard let d = try? Data(contentsOf: f) else { continue }
            let p = QuoteFile.parse(QuoteFile.decode(d), fallbackName: f.deletingPathExtension().lastPathComponent)
            var name = p.name
            while seen.contains(name.lowercased()) { name += " (2)" }
            seen.insert(name.lowercased())
            let ci = cs.count
            cs.append(Category(name: name, icons: p.icons, file: f, count: p.quotes.count))
            for q in p.quotes { qs.append(Quote(text: q.0, author: q.1, cat: ci)) }
        }
        cats = cs
        quotes = qs
    }

    // Copies the lists that come with the app, named after their categories ("Ley de Murphy.txt")
    func installBuiltIns(overwrite: Bool) {
        let fm = FileManager.default
        try? fm.createDirectory(at: folder, withIntermediateDirectories: true)
        guard let src = Store.builtInFolder,
              let items = try? fm.contentsOfDirectory(at: src, includingPropertiesForKeys: nil) else { return }
        for f in items where f.pathExtension == "txt" {
            guard let d = try? Data(contentsOf: f) else { continue }
            let name = QuoteFile.parse(QuoteFile.decode(d), fallbackName: f.deletingPathExtension().lastPathComponent).name
            let dest = folder.appendingPathComponent(Store.safeFileName(name) + ".txt")
            if fm.fileExists(atPath: dest.path) && !overwrite { continue }
            try? d.write(to: dest)
        }
        let help = folder.appendingPathComponent("_How to add quotes.txt")
        if !fm.fileExists(atPath: help.path) { try? Store.helpText.data(using: .utf8)?.write(to: help) }
    }

    static func safeFileName(_ s: String) -> String {
        let bad = CharacterSet(charactersIn: "/\\:*?\"<>|")
        let t = s.components(separatedBy: bad).joined(separator: "-").trimmingCharacters(in: .whitespaces)
        return t.isEmpty ? "Quotes" : t
    }

    static func template(_ name: String) -> String {
        """
        # name: \(name)
        # icon: 💬
        # Write one quote after another, separated by a line with only %.
        # A last line starting with — (or --) is the author. Lines starting with # here at the top are notes.

        The first quote goes here.
        — Its author
        %
        A quote without an author.
        %
        A quote can take
        several lines.

        """
    }

    static let helpText = """
    CITORIO — HOW TO ADD QUOTES
    ===========================

    Every .txt file in this folder is a category of quotes. To add a category, add a file;
    to add quotes, edit a file in any text editor (TextEdit, Notepad…). Citorio reads the
    files again before each quote, so changes show up without restarting it.
    Files whose name starts with _ (like this one) are ignored.

    Inside a file:

      # name: Física
      # icon: 🔭
      Lines starting with # at the top are notes. "# name:" names the category (otherwise the
      file name is used) and "# icon:" chooses its icon. Several icons separated by spaces
      are used in turn.

      Then the quotes, separated by a line with only %:

        Si he visto más lejos es porque estoy sentado sobre los hombros de gigantes.
        — Isaac Newton
        %
        La imaginación es más importante que el conocimiento.
        — Albert Einstein
        %
        A quote can take
        several lines.

      A last line that starts with — (or --) is the author.

    A simple list also works: a file without any % line has one quote per line, with
    the author after " — " or " -- ", like

        La imaginación es más importante que el conocimiento. — Albert Einstein

    Save files as UTF-8 (the default in TextEdit and in Notepad on Windows 10 and 11).
    Turn categories on and off in Settings. "Restore built-in lists" in Settings puts back
    the lists Citorio came with.

    """
}

// MARK: - Drawing the quote (sizes in points)

enum Art {
    static let pad: CGFloat = 16, iconBox: CGFloat = 46, iconGap: CGFloat = 14, authorGap: CGFloat = 10

    static func quoteFont(_ family: String, _ size: CGFloat) -> NSFont {
        NSFontManager.shared.font(withFamily: family, traits: [], weight: 5, size: size) ?? NSFont(name: family, size: size)
            ?? NSFont(name: "Georgia", size: size) ?? NSFont.systemFont(ofSize: size)
    }
    static func authorFont(_ family: String, _ size: CGFloat) -> NSFont {
        let base = quoteFont(family, size * 0.78)
        return NSFontManager.shared.convert(base, toHaveTrait: .italicFontMask)
    }

    static func attrs(_ font: NSFont, _ color: NSColor, _ align: NSTextAlignment) -> [NSAttributedString.Key: Any] {
        let p = NSMutableParagraphStyle()
        p.alignment = align
        p.lineSpacing = font.pointSize * 0.18
        p.lineBreakMode = .byWordWrapping
        return [.font: font, .foregroundColor: color, .paragraphStyle: p]
    }

    static func textHeight(_ s: String, _ a: [NSAttributedString.Key: Any], _ w: CGFloat) -> CGFloat {
        if s.isEmpty { return 0 }
        let r = NSAttributedString(string: s, attributes: a).boundingRect(
            with: NSSize(width: w, height: 100000), options: [.usesLineFragmentOrigin, .usesFontLeading])
        return ceil(r.height)
    }

    struct Layout { var height: CGFloat; var size: CGFloat }

    // Height the quote needs at this width; shrinks the letters if it would be taller than maxH
    static func layout(_ q: Quote?, family: String, size: CGFloat, width: CGFloat, icon: Bool, maxH: CGFloat) -> Layout {
        var s = size
        while true {
            let h = height(q, family: family, size: s, width: width, icon: icon)
            if h <= maxH || s <= 10 { return Layout(height: min(h, maxH), size: s) }
            s -= 1
        }
    }

    static func height(_ q: Quote?, family: String, size: CGFloat, width: CGFloat, icon: Bool) -> CGFloat {
        guard let q = q else { return 2 * pad + iconBox }
        let tw = width - 2 * pad - (icon ? iconBox + iconGap : 0)
        var h = textHeight(q.text, attrs(quoteFont(family, size), .black, .left), tw)
        if !q.author.isEmpty { h += authorGap + textHeight(q.author, attrs(authorFont(family, size), .black, .right), tw) }
        return ceil(max(h, icon ? iconBox : 0) + 2 * pad)
    }

    // Draws in a flipped (y-down) coordinate system
    static func draw(_ q: Quote?, iconText: String, family: String, size: CGFloat, in rect: NSRect, icon: Bool, dark: Bool) {
        let fg = dark ? NSColor(white: 0.93, alpha: 1) : NSColor(white: 0.08, alpha: 1)
        let au = dark ? NSColor(srgbRed: 0.62, green: 0.74, blue: 1, alpha: 1) : NSColor(srgbRed: 0, green: 0, blue: 0.5, alpha: 1)
        var x = rect.minX + pad
        if icon {
            let box = NSRect(x: x, y: rect.minY + pad, width: iconBox, height: iconBox)
            (dark ? NSColor(white: 1, alpha: 0.08) : NSColor(white: 0, alpha: 0.05)).setFill()
            NSBezierPath(roundedRect: box, xRadius: 9, yRadius: 9).fill()
            let ia: [NSAttributedString.Key: Any] = [.font: NSFont.systemFont(ofSize: 30)]
            let isz = NSAttributedString(string: iconText, attributes: ia).size()
            NSAttributedString(string: iconText, attributes: ia).draw(at: NSPoint(x: box.midX - isz.width / 2, y: box.midY - isz.height / 2))
            x += iconBox + iconGap
        }
        guard let q = q else { return }
        let tw = rect.maxX - pad - x
        let qa = attrs(quoteFont(family, size), fg, .left)
        let qh = textHeight(q.text, qa, tw)
        NSAttributedString(string: q.text, attributes: qa).draw(with: NSRect(x: x, y: rect.minY + pad, width: tw, height: qh + 2),
                                                                options: [.usesLineFragmentOrigin, .usesFontLeading])
        if !q.author.isEmpty {
            let aa = attrs(authorFont(family, size), au, .right)
            let ah = textHeight("— " + q.author, aa, tw)
            NSAttributedString(string: "— " + q.author, attributes: aa).draw(
                with: NSRect(x: x, y: rect.minY + pad + qh + authorGap, width: tw, height: ah + 2),
                options: [.usesLineFragmentOrigin, .usesFontLeading])
        }
    }

    // --render out.png quotesDir: reads the lists, checks them and draws one quote
    static func render(_ path: String, folder: String) -> Bool {
        let store = Store(folder: URL(fileURLWithPath: folder))
        store.load()
        print("Categories: \(store.cats.count), quotes: \(store.quotes.count)")
        for c in store.cats { print("  \(c.name): \(c.count) \(c.icons.first ?? "")") }
        guard !store.quotes.isEmpty else { return false }
        let q = store.quotes.first(where: { !$0.author.isEmpty && $0.text.count > 120 }) ?? store.quotes[0]
        let w: CGFloat = 460, scale: CGFloat = 2
        let h = height(q, family: "Georgia", size: 18, width: w, icon: true)
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(w * scale), pixelsHigh: Int(h * scale),
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0),
              let ctx = NSGraphicsContext(bitmapImageRep: rep) else { return false }
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = ctx
        let g = ctx.cgContext
        g.translateBy(x: 0, y: h * scale)
        g.scaleBy(x: scale, y: -scale)
        NSGraphicsContext.current = NSGraphicsContext(cgContext: g, flipped: true)
        NSColor.white.setFill()
        NSRect(x: 0, y: 0, width: w, height: h).fill()
        draw(q, iconText: store.cats[q.cat].icons.first ?? "📜", family: "Georgia", size: 18,
             in: NSRect(x: 0, y: 0, width: w, height: h), icon: true, dark: false)
        NSGraphicsContext.restoreGraphicsState()
        guard let data = rep.representation(using: .png, properties: [:]) else { return false }
        return (try? data.write(to: URL(fileURLWithPath: path))) != nil
    }
}

// MARK: - App icon (used by build.sh: Citorio --write-iconset <dir>)

enum IconArt {
    // A white speech bubble with a big serif quotation mark, on a navy rounded square
    static func draw(_ g: CGContext, _ s: CGFloat) {
        let inset = s * 0.098
        let body = CGRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        g.addPath(CGPath(roundedRect: body, cornerWidth: body.width * 0.225, cornerHeight: body.width * 0.225, transform: nil))
        g.setFillColor(CGColor(srgbRed: 0, green: 0, blue: 0x80 / 255.0, alpha: 1))
        g.fillPath()
        let b = CGRect(x: s * 0.2, y: s * 0.34, width: s * 0.6, height: s * 0.42)
        let bubble = CGMutablePath()
        bubble.addRoundedRect(in: b, cornerWidth: s * 0.1, cornerHeight: s * 0.1)
        bubble.move(to: CGPoint(x: s * 0.32, y: b.minY + 1))
        bubble.addLine(to: CGPoint(x: s * 0.28, y: s * 0.22))
        bubble.addLine(to: CGPoint(x: s * 0.46, y: b.minY + 1))
        bubble.closeSubpath()
        g.addPath(bubble)
        g.setFillColor(CGColor(gray: 1, alpha: 1))
        g.fillPath()
        let font = CTFontCreateWithName("Georgia-Bold" as CFString, s * 0.5, nil)
        var ch: [UniChar] = Array("\u{201C}".utf16)
        var glyph = CGGlyph(0)
        CTFontGetGlyphsForCharacters(font, &ch, &glyph, 1)
        if let p = CTFontCreatePathForGlyph(font, glyph, nil) {
            let pb = p.boundingBoxOfPath
            var m = CGAffineTransform(translationX: b.midX - pb.midX, y: b.midY - pb.midY)
            if let moved = p.copy(using: &m) {
                g.addPath(moved)
                g.setFillColor(CGColor(srgbRed: 0, green: 0, blue: 0x80 / 255.0, alpha: 1))
                g.fillPath()
            }
        }
    }

    static func png(_ size: Int) -> Data? {
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size,
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0),
              let ctx = NSGraphicsContext(bitmapImageRep: rep) else { return nil }
        draw(ctx.cgContext, CGFloat(size))
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

// MARK: - The quote window

final class QuoteView: NSView {
    weak var app: AppDelegate?
    override var isFlipped: Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        guard let app = app else { return }
        let dark = effectiveAppearance.bestMatch(from: [.darkAqua, .aqua]) == .darkAqua
        (dark ? NSColor(white: 0.13, alpha: 1) : NSColor.white).setFill()
        bounds.fill()
        Art.draw(app.current ?? app.emptyQuote(), iconText: app.currentIcon, family: app.fontFamily, size: app.shownSize,
                 in: bounds, icon: app.showIcons && app.current != nil, dark: dark)
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        trackingAreas.forEach { removeTrackingArea($0) }
        addTrackingArea(NSTrackingArea(rect: .zero, options: [.mouseEnteredAndExited, .activeAlways, .inVisibleRect], owner: self))
    }
    override func mouseEntered(with event: NSEvent) { app?.hovering = true }
    override func mouseExited(with event: NSEvent) { app?.hovering = false }
    override func mouseDown(with event: NSEvent) { if event.clickCount == 2 { app?.nextQuote() } }
    override func menu(for event: NSEvent) -> NSMenu? { app?.statusMenu }
}

final class QuotePanel: NSPanel {
    override var canBecomeKey: Bool { true }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate, NSMenuDelegate {
    let d = UserDefaults.standard
    var store = Store(folder: Store.userFolder)
    var panel: QuotePanel!
    var quoteView: QuoteView!
    var bar: NSView!
    var pauseButton: NSButton!
    var statusItem: NSStatusItem!
    let statusMenu = NSMenu()
    var pauseItem: NSMenuItem!, loginItem: NSMenuItem!
    var settings: SettingsController?

    var current: Quote?
    var currentIcon = "📜"
    var shownSize: CGFloat = 18
    var visible = false, paused = false, hovering = false
    var phaseEnd = Date(), phaseLen: TimeInterval = 1
    var iconTurn = [String: Int]()
    var resizing = false
    let barH: CGFloat = 36

    // Settings
    var minShow: Int { get { d.object(forKey: "minShow") == nil ? 10 : d.integer(forKey: "minShow") } set { d.set(newValue, forKey: "minShow") } }
    var maxShow: Int { get { d.object(forKey: "maxShow") == nil ? 40 : d.integer(forKey: "maxShow") } set { d.set(newValue, forKey: "maxShow") } }
    var hideFor: Int { get { d.object(forKey: "hideFor") == nil ? 60 : d.integer(forKey: "hideFor") } set { d.set(newValue, forKey: "hideFor") } }
    var fontFamily: String { get { d.string(forKey: "font") ?? "Georgia" } set { d.set(newValue, forKey: "font") } }
    var fontSize: CGFloat { get { d.object(forKey: "size") == nil ? 18 : CGFloat(d.double(forKey: "size")) } set { d.set(Double(newValue), forKey: "size") } }
    var showIcons: Bool { get { d.object(forKey: "icons") == nil ? true : d.bool(forKey: "icons") } set { d.set(newValue, forKey: "icons") } }
    var sound: Bool { get { d.object(forKey: "sound") == nil ? true : d.bool(forKey: "sound") } set { d.set(newValue, forKey: "sound") } }
    var stayVisible: Bool { get { d.bool(forKey: "stayVisible") } set { d.set(newValue, forKey: "stayVisible") } }
    var off: Set<String> {
        get { Set(d.stringArray(forKey: "off") ?? []) }
        set { d.set(Array(newValue).sorted(), forKey: "off") }
    }
    var history: [String] { get { d.stringArray(forKey: "history") ?? [] } set { d.set(Array(newValue.suffix(3000)), forKey: "history") } }

    func applicationDidFinishLaunching(_ notification: Notification) {
        if !FileManager.default.fileExists(atPath: store.folder.path) { store.installBuiltIns(overwrite: false) }
        store.load()

        let w = d.object(forKey: "width") == nil ? 460 : CGFloat(d.double(forKey: "width"))
        panel = QuotePanel(contentRect: NSRect(x: 0, y: 0, width: w, height: 160),
                           styleMask: [.titled, .closable, .resizable, .nonactivatingPanel, .utilityWindow],
                           backing: .buffered, defer: false)
        panel.title = "Citorio"
        panel.level = .floating
        panel.hidesOnDeactivate = false
        panel.isFloatingPanel = true
        panel.becomesKeyOnlyIfNeeded = true
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        panel.delegate = self
        panel.isReleasedWhenClosed = false

        let content = NSView(frame: NSRect(x: 0, y: 0, width: w, height: 160))
        quoteView = QuoteView(frame: NSRect(x: 0, y: barH, width: w, height: 160 - barH))
        quoteView.app = self
        quoteView.autoresizingMask = [.width, .height]
        content.addSubview(quoteView)
        bar = NSView(frame: NSRect(x: 0, y: 0, width: w, height: barH))
        bar.autoresizingMask = [.width]
        content.addSubview(bar)
        let buttons: [(String, Selector, String)] = [
            ("Next", #selector(nextQuote), "Show another quote"),
            ("Later", #selector(later), "Hide; show this same quote next time"),
            ("Pause", #selector(togglePause), "Stop or restart the timer"),
            ("Copy", #selector(copyQuote), "Copy the quote"),
        ]
        var x: CGFloat = 8
        for (t, sel, tip) in buttons {
            let b = NSButton(title: t, target: self, action: sel)
            b.bezelStyle = .rounded
            b.controlSize = .small
            b.font = NSFont.systemFont(ofSize: NSFont.smallSystemFontSize)
            b.toolTip = tip
            b.sizeToFit()
            b.frame = NSRect(x: x, y: 6, width: max(64, b.frame.width), height: 24)
            x += b.frame.width + 4
            bar.addSubview(b)
            if t == "Pause" { pauseButton = b }
        }
        let gear = NSButton(title: "Settings…", target: self, action: #selector(openSettings))
        gear.bezelStyle = .rounded
        gear.controlSize = .small
        gear.font = NSFont.systemFont(ofSize: NSFont.smallSystemFontSize)
        gear.sizeToFit()
        gear.frame = NSRect(x: w - gear.frame.width - 8, y: 6, width: gear.frame.width, height: 24)
        gear.autoresizingMask = [.minXMargin]
        bar.addSubview(gear)
        panel.contentView = content
        panel.contentMinSize = NSSize(width: x + gear.frame.width + 16, height: 80)

        buildStatusItem()
        show(pick(), sound: false)

        Timer.scheduledTimer(withTimeInterval: 0.25, repeats: true) { [weak self] _ in self?.tick() }
    }

    // Opening Citorio again while it runs shows a quote, like the original (handy when the menu bar is full)
    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showNow()
        return false
    }

    // ── Picking ──

    func pool() -> [Int] {
        let o = off
        return store.quotes.indices.filter { !o.contains(store.cats[store.quotes[$0].cat].name) }
    }

    // Not one of the last 60% shown, like the original
    func pick() -> Quote? {
        store.load()
        if let p = d.string(forKey: "pending") {
            d.removeObject(forKey: "pending")
            if let q = store.quotes.first(where: { $0.key == p }) { return q }
        }
        let p = pool()
        if p.isEmpty { return nil }
        let mem = Int(Double(p.count) * 0.6)
        let recent = Set(history.suffix(mem))
        let fresh = p.filter { !recent.contains(store.quotes[$0].key) && store.quotes[$0].key != current?.key }
        let from = fresh.isEmpty ? p : fresh
        return store.quotes[from.randomElement()!]
    }

    func showDuration(_ q: Quote?) -> TimeInterval {
        guard let q = q else { return TimeInterval(maxShow) }
        let len = Double(q.text.count + q.author.count)
        let f = min(1, max(0, (len - 40) / 560))
        let lo = Double(min(minShow, maxShow)), hi = Double(max(minShow, maxShow))
        return lo + (hi - lo) * f
    }

    // ── Showing and hiding ──

    func show(_ q: Quote?, sound playSound: Bool) {
        current = q
        if let q = q {
            let c = store.cats[q.cat]
            if c.icons.isEmpty { currentIcon = "📜" }
            else { let n = iconTurn[c.name, default: 0]; currentIcon = c.icons[n % c.icons.count]; iconTurn[c.name] = n + 1 }
            var h = history
            h.append(q.key)
            history = h
        }
        relayout(keepTop: true)
        quoteView.needsDisplay = true
        if !visible {
            panel.orderFrontRegardless()
            visible = true
        }
        if playSound && sound { NSSound(named: "Glass")?.play() }
        startPhase(showDuration(q))
        updateTitle()
    }

    func emptyQuote() -> Quote {
        Quote(text: store.quotes.isEmpty
              ? "There are no quotes in Documents/Citorio. Open Settings to add a category or restore the built-in lists."
              : "All categories are turned off. Turn some on in Settings.", author: "", cat: -1)
    }

    func relayout(keepTop: Bool) {
        guard let screen = panel.screen ?? NSScreen.main else { return }
        let w = panel.frame.width
        let vf = screen.visibleFrame
        let titleH = panel.frame.height - panel.contentLayoutRect.height
        let q = current ?? emptyQuote()
        let lay = Art.layout(q, family: fontFamily, size: fontSize, width: w, icon: showIcons && current != nil,
                             maxH: vf.height * 0.8 - barH - titleH)
        shownSize = lay.size
        let h = lay.height + barH + titleH
        // Where it was left (the top edge stays put as the height changes); the first time, top right
        var x = vf.maxX - w - 16, top = vf.maxY - 12
        if !keepTop { x = panel.frame.minX; top = panel.frame.maxY }
        else if d.object(forKey: "x") != nil { x = CGFloat(d.double(forKey: "x")); top = CGFloat(d.double(forKey: "top")) }
        let home = NSScreen.screens.first(where: { $0.visibleFrame.insetBy(dx: -2, dy: -2).contains(NSPoint(x: x + 20, y: top - 10)) })?.visibleFrame
        if home == nil { x = vf.maxX - w - 16; top = vf.maxY - 12 }
        let sf = home ?? vf
        top = min(top, sf.maxY)
        if top - h < sf.minY { top = min(sf.maxY, sf.minY + h) }
        resizing = true
        panel.contentMinSize = NSSize(width: panel.contentMinSize.width, height: h - titleH)
        panel.contentMaxSize = NSSize(width: 4000, height: h - titleH)
        panel.setFrame(NSRect(x: x, y: top - h, width: w, height: h), display: true)
        resizing = false
        quoteView.frame = NSRect(x: 0, y: barH, width: w, height: h - titleH - barH)
    }

    func hide() {
        if stayVisible { show(pick(), sound: true); return }
        panel.orderOut(nil)
        visible = false
        startPhase(TimeInterval(hideFor))
        updateTitle()
    }

    func startPhase(_ s: TimeInterval) { phaseLen = max(1, s); phaseEnd = Date().addingTimeInterval(phaseLen) }

    func tick() {
        let holding = paused || (visible && (hovering || settings?.window?.isVisible == true))
        if holding { phaseEnd = phaseEnd.addingTimeInterval(0.25); return }
        if Date() >= phaseEnd {
            if visible { hide() } else { show(pick(), sound: true) }
        }
    }

    func updateTitle() {
        panel.title = paused ? "[Citorio]" : "Citorio"
        pauseButton.title = paused ? "Resume" : "Pause"
        pauseItem?.title = paused ? "Resume" : "Pause"
        statusItem?.button?.appearsDisabled = paused
    }

    // ── Actions ──

    @objc func nextQuote() { show(pick(), sound: false) }

    @objc func showNow() {
        if visible { panel.orderFrontRegardless(); startPhase(showDuration(current)) } else { show(pick(), sound: false) }
    }

    @objc func later() {
        if let q = current { d.set(q.key, forKey: "pending") }
        panel.orderOut(nil)
        visible = false
        startPhase(TimeInterval(hideFor))
        updateTitle()
    }

    @objc func togglePause() {
        paused.toggle()
        if !paused { startPhase(visible ? showDuration(current) : TimeInterval(hideFor)) }
        updateTitle()
    }

    @objc func copyQuote() {
        guard let q = current else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(q.text + (q.author.isEmpty ? "" : "\n— " + q.author), forType: .string)
    }

    @objc func openSettings() {
        if settings == nil { settings = SettingsController(app: self) }
        settings!.refresh()
        NSApp.activate(ignoringOtherApps: true)
        settings!.window?.center()
        settings!.window?.makeKeyAndOrderFront(nil)
    }

    @objc func openFolder() {
        if !FileManager.default.fileExists(atPath: store.folder.path) { store.installBuiltIns(overwrite: false) }
        NSWorkspace.shared.open(store.folder)
    }

    @objc func about() {
        let a = NSAlert()
        a.messageText = "Citorio 1.0"
        a.informativeText = """
        Every so often a small window shows a quote, then hides again.

        A tribute to Citorio 1.1 (1995–96) for Windows 3.1 by Javier Cámara Melgosa, \
        itself based on Pundit (1990) by Jeff Bienstadt. The \(store.quotes.count) quotes \
        that come with it are the ones he collected and translated; this program is new.

        Your quotes are in Documents/Citorio: one .txt file per category.

        © 2026 Mauricio Mendivelso. MIT License.
        """
        NSApp.activate(ignoringOtherApps: true)
        a.runModal()
    }

    @objc func toggleLogin() {
        guard #available(macOS 13, *) else { return }
        do {
            if SMAppService.mainApp.status == .enabled { try SMAppService.mainApp.unregister() }
            else { try SMAppService.mainApp.register() }
        } catch {
            let a = NSAlert()
            a.messageText = "Couldn't change Open at Login"
            a.informativeText = "Move Citorio into Applications and try again.\n\n\(error.localizedDescription)"
            NSApp.activate(ignoringOtherApps: true)
            a.runModal()
        }
    }

    @objc func quit() { NSApp.terminate(nil) }

    // ── Menu bar item ──

    func buildStatusItem() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let img = NSImage(systemSymbolName: "quote.bubble", accessibilityDescription: "Citorio") {
            img.isTemplate = true
            statusItem.button?.image = img
        } else {
            statusItem.button?.title = "❝"
        }
        statusMenu.delegate = self
        statusMenu.addItem(withTitle: "Show a Quote Now", action: #selector(showNow), keyEquivalent: "").target = self
        statusMenu.addItem(withTitle: "Next Quote", action: #selector(nextQuote), keyEquivalent: "").target = self
        pauseItem = statusMenu.addItem(withTitle: "Pause", action: #selector(togglePause), keyEquivalent: "")
        pauseItem.target = self
        statusMenu.addItem(withTitle: "Copy Quote", action: #selector(copyQuote), keyEquivalent: "").target = self
        statusMenu.addItem(.separator())
        statusMenu.addItem(withTitle: "Settings…", action: #selector(openSettings), keyEquivalent: ",").target = self
        statusMenu.addItem(withTitle: "Open Quotes Folder", action: #selector(openFolder), keyEquivalent: "").target = self
        loginItem = statusMenu.addItem(withTitle: "Open at Login", action: #selector(toggleLogin), keyEquivalent: "")
        loginItem.target = self
        if #unavailable(macOS 13) { loginItem.isHidden = true }
        statusMenu.addItem(withTitle: "About Citorio", action: #selector(about), keyEquivalent: "").target = self
        statusMenu.addItem(.separator())
        statusMenu.addItem(withTitle: "Quit Citorio", action: #selector(quit), keyEquivalent: "q").target = self
        statusItem.menu = statusMenu
    }

    func menuWillOpen(_ menu: NSMenu) {
        if #available(macOS 13, *) { loginItem.state = SMAppService.mainApp.status == .enabled ? .on : .off }
    }

    // ── Window ──

    func windowShouldClose(_ sender: NSWindow) -> Bool {
        // Closing only hides it until the next quote; quit from the menu bar
        panel.orderOut(nil)
        visible = false
        startPhase(TimeInterval(hideFor))
        updateTitle()
        return false
    }

    func windowDidResize(_ notification: Notification) {
        if resizing { return }
        d.set(Double(panel.frame.width), forKey: "width")
        relayout(keepTop: false)
        savePosition()
        quoteView.needsDisplay = true
    }

    func windowDidMove(_ notification: Notification) { if !resizing { savePosition() } }

    func savePosition() {
        d.set(Double(panel.frame.minX), forKey: "x")
        d.set(Double(panel.frame.maxY), forKey: "top")
    }

    func settingsChanged() {
        if let c = current, off.contains(store.cats.indices.contains(c.cat) ? store.cats[c.cat].name : "") {
            show(pick(), sound: false)
        } else {
            relayout(keepTop: false)
            quoteView.needsDisplay = true
        }
    }
}

// MARK: - Settings window

final class SettingsController: NSWindowController, NSWindowDelegate {
    unowned let app: AppDelegate
    var minField = NSTextField(), maxField = NSTextField(), hideField = NSTextField(), sizeField = NSTextField()
    var fontPopup = NSPopUpButton()
    var iconsBox: NSButton!, soundBox: NSButton!, stayBox: NSButton!
    var list = NSStackView()
    var countLabel = NSTextField(labelWithString: "")

    init(app: AppDelegate) {
        self.app = app
        let w = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 560, height: 640),
                         styleMask: [.titled, .closable], backing: .buffered, defer: false)
        w.title = "Citorio Settings"
        w.isReleasedWhenClosed = false
        super.init(window: w)
        w.delegate = self
        build()
    }
    required init?(coder: NSCoder) { fatalError() }

    @discardableResult func label(_ s: String, _ x: CGFloat, _ y: CGFloat, bold: Bool = false, size: CGFloat = 13) -> NSTextField {
        let l = NSTextField(labelWithString: s)
        l.font = bold ? NSFont.boldSystemFont(ofSize: size) : NSFont.systemFont(ofSize: size)
        l.sizeToFit()
        l.frame.origin = NSPoint(x: x, y: y)
        window!.contentView!.addSubview(l)
        return l
    }

    func numberField(_ f: NSTextField, _ x: CGFloat, _ y: CGFloat) {
        f.frame = NSRect(x: x, y: y - 3, width: 60, height: 22)
        f.alignment = .right
        f.target = self
        f.action = #selector(saveNumbers)
        window!.contentView!.addSubview(f)
        label("seconds", x + 66, y)
    }

    func checkbox(_ title: String, _ x: CGFloat, _ y: CGFloat, _ sel: Selector) -> NSButton {
        let b = NSButton(checkboxWithTitle: title, target: self, action: sel)
        b.frame.origin = NSPoint(x: x, y: y)
        b.sizeToFit()
        window!.contentView!.addSubview(b)
        return b
    }

    @discardableResult func button(_ title: String, _ x: CGFloat, _ y: CGFloat, _ sel: Selector) -> NSButton {
        let b = NSButton(title: title, target: self, action: sel)
        b.bezelStyle = .rounded
        b.sizeToFit()
        b.frame.origin = NSPoint(x: x, y: y)
        window!.contentView!.addSubview(b)
        return b
    }

    func build() {
        let v = window!.contentView!
        var y: CGFloat = 604
        label("Timing", 20, y, bold: true)
        y -= 30; label("Show each quote at least", 36, y); numberField(minField, 240, y)
        y -= 28; label("and at most", 36, y); numberField(maxField, 240, y)
        y -= 28; label("Hide between quotes for", 36, y); numberField(hideField, 240, y)
        y -= 22; label("Longer quotes stay longer. Moving the mouse over a quote holds it.", 36, y, size: 11).textColor = .secondaryLabelColor

        y -= 34; label("Appearance", 20, y, bold: true)
        y -= 32; label("Font", 36, y)
        fontPopup.frame = NSRect(x: 80, y: y - 5, width: 240, height: 26)
        let families = NSFontManager.shared.availableFontFamilies
        let serifs = ["Georgia", "Times New Roman", "Palatino", "Baskerville", "Hoefler Text", "Iowan Old Style",
                      "Charter", "Book Antiqua", "Garamond", "Didot", "Cochin", "New York"].filter { families.contains($0) }
        fontPopup.addItems(withTitles: serifs)
        fontPopup.menu?.addItem(.separator())
        for f in families where !serifs.contains(f) && !f.hasPrefix(".") { fontPopup.addItem(withTitle: f) }
        fontPopup.target = self
        fontPopup.action = #selector(saveFont)
        v.addSubview(fontPopup)
        label("Size", 340, y)
        sizeField.frame = NSRect(x: 376, y: y - 3, width: 50, height: 22)
        sizeField.alignment = .right
        sizeField.target = self
        sizeField.action = #selector(saveFont)
        v.addSubview(sizeField)
        label("pt", 432, y)
        y -= 30; iconsBox = checkbox("Show the category icon", 36, y, #selector(saveChecks))
        y -= 24; soundBox = checkbox("Play a sound when a quote appears", 36, y, #selector(saveChecks))
        y -= 24; stayBox = checkbox("Never hide: go straight to the next quote (for a projector)", 36, y, #selector(saveChecks))

        y -= 36; label("Categories", 20, y, bold: true)
        countLabel.frame = NSRect(x: 120, y: y, width: 300, height: 17)
        countLabel.textColor = .secondaryLabelColor
        v.addSubview(countLabel)
        let scrollH: CGFloat = y - 110
        let scroll = NSScrollView(frame: NSRect(x: 36, y: 100, width: 488, height: scrollH - 8))
        scroll.hasVerticalScroller = true
        scroll.borderType = .bezelBorder
        list.orientation = .vertical
        list.alignment = .leading
        list.spacing = 2
        list.edgeInsets = NSEdgeInsets(top: 6, left: 8, bottom: 6, right: 8)
        let doc = FlippedView(frame: NSRect(x: 0, y: 0, width: 470, height: 10))
        doc.addSubview(list)
        scroll.documentView = doc
        v.addSubview(scroll)
        let b1 = button("New Category…", 30, 62, #selector(newCategory))
        let b2 = button("Import List…", b1.frame.maxX + 4, 62, #selector(importList))
        let b3 = button("Open Folder", b2.frame.maxX + 4, 62, #selector(openFolder))
        _ = button("Restore Built-in Lists…", b3.frame.maxX + 4, 62, #selector(restore))
        label("Each category is a .txt file in Documents/Citorio (see “_How to add quotes.txt” there).", 36, 40, size: 11).textColor = .secondaryLabelColor
        let done = button("Done", 0, 8, #selector(closeIt))
        done.keyEquivalent = "\r"
        done.frame.origin.x = 560 - done.frame.width - 20
    }

    func refresh() {
        minField.integerValue = app.minShow
        maxField.integerValue = app.maxShow
        hideField.integerValue = app.hideFor
        sizeField.integerValue = Int(app.fontSize)
        if fontPopup.item(withTitle: app.fontFamily) == nil { fontPopup.insertItem(withTitle: app.fontFamily, at: 0) }
        fontPopup.selectItem(withTitle: app.fontFamily)
        iconsBox.state = app.showIcons ? .on : .off
        soundBox.state = app.sound ? .on : .off
        stayBox.state = app.stayVisible ? .on : .off
        app.store.load()
        list.arrangedSubviews.forEach { $0.removeFromSuperview() }
        let off = app.off
        for (i, c) in app.store.cats.enumerated() {
            let row = NSStackView()
            row.orientation = .horizontal
            row.spacing = 8
            let cb = NSButton(checkboxWithTitle: "\(c.icons.first ?? "📜")  \(c.name)  (\(c.count))", target: self, action: #selector(toggleCat(_:)))
            cb.tag = i
            cb.state = off.contains(c.name) ? .off : .on
            cb.widthAnchor.constraint(equalToConstant: 360).isActive = true
            let edit = NSButton(title: "Edit", target: self, action: #selector(editCat(_:)))
            edit.bezelStyle = .rounded
            edit.controlSize = .small
            edit.tag = i
            row.addArrangedSubview(cb)
            row.addArrangedSubview(edit)
            list.addArrangedSubview(row)
        }
        if app.store.cats.isEmpty { list.addArrangedSubview(NSTextField(labelWithString: "No lists yet. Add a category, import a list or restore the built-in lists.")) }
        list.layoutSubtreeIfNeeded()
        let size = list.fittingSize
        list.frame = NSRect(x: 0, y: 0, width: max(470, size.width), height: size.height)
        list.superview?.frame = list.frame
        updateCount()
    }

    func updateCount() {
        let off = app.off
        let on = app.store.cats.filter { !off.contains($0.name) }.reduce(0) { $0 + $1.count }
        countLabel.stringValue = "\(on) of \(app.store.quotes.count) quotes turned on"
    }

    @objc func saveNumbers() {
        app.minShow = max(2, minField.integerValue)
        app.maxShow = max(2, maxField.integerValue)
        app.hideFor = max(0, hideField.integerValue)
        minField.integerValue = app.minShow; maxField.integerValue = app.maxShow; hideField.integerValue = app.hideFor
    }

    @objc func saveFont() {
        if let t = fontPopup.titleOfSelectedItem { app.fontFamily = t }
        app.fontSize = CGFloat(min(72, max(9, sizeField.integerValue)))
        sizeField.integerValue = Int(app.fontSize)
        app.settingsChanged()
    }

    @objc func saveChecks() {
        app.showIcons = iconsBox.state == .on
        app.sound = soundBox.state == .on
        app.stayVisible = stayBox.state == .on
        app.settingsChanged()
    }

    @objc func toggleCat(_ sender: NSButton) {
        guard app.store.cats.indices.contains(sender.tag) else { return }
        var o = app.off
        let name = app.store.cats[sender.tag].name
        if sender.state == .on { o.remove(name) } else { o.insert(name) }
        app.off = o
        updateCount()
        app.settingsChanged()
    }

    @objc func editCat(_ sender: NSButton) {
        guard app.store.cats.indices.contains(sender.tag) else { return }
        NSWorkspace.shared.open(app.store.cats[sender.tag].file)
    }

    @objc func openFolder() { app.openFolder() }

    @objc func newCategory() {
        let a = NSAlert()
        a.messageText = "New category"
        a.informativeText = "Name of the new category. Citorio creates a file for it and opens it so you can write the quotes."
        let f = NSTextField(frame: NSRect(x: 0, y: 0, width: 260, height: 24))
        a.accessoryView = f
        a.addButton(withTitle: "Create")
        a.addButton(withTitle: "Cancel")
        a.window.initialFirstResponder = f
        guard a.runModal() == .alertFirstButtonReturn else { return }
        let name = f.stringValue.trimmingCharacters(in: .whitespaces)
        if name.isEmpty { return }
        try? FileManager.default.createDirectory(at: app.store.folder, withIntermediateDirectories: true)
        let url = app.store.folder.appendingPathComponent(Store.safeFileName(name) + ".txt")
        if !FileManager.default.fileExists(atPath: url.path) {
            try? Store.template(name).data(using: .utf8)?.write(to: url)
        }
        refresh()
        NSWorkspace.shared.open(url)
    }

    @objc func importList() {
        let p = NSOpenPanel()
        p.allowedContentTypes = [.plainText, .text]
        p.allowsMultipleSelection = true
        p.message = "Choose text files with quotes. Each file becomes a category."
        guard p.runModal() == .OK else { return }
        let fm = FileManager.default
        try? fm.createDirectory(at: app.store.folder, withIntermediateDirectories: true)
        var added: [String] = []
        for src in p.urls {
            guard let data = try? Data(contentsOf: src) else { continue }
            let parsed = QuoteFile.parse(QuoteFile.decode(data), fallbackName: src.deletingPathExtension().lastPathComponent)
            var dest = app.store.folder.appendingPathComponent(Store.safeFileName(src.deletingPathExtension().lastPathComponent) + ".txt")
            if fm.fileExists(atPath: dest.path) {
                let a = NSAlert()
                a.messageText = "“\(dest.lastPathComponent)” already exists"
                a.addButton(withTitle: "Replace")
                a.addButton(withTitle: "Keep Both")
                a.addButton(withTitle: "Skip")
                let r = a.runModal()
                if r == .alertThirdButtonReturn { continue }
                if r == .alertSecondButtonReturn {
                    var n = 2
                    while fm.fileExists(atPath: dest.path) {
                        dest = app.store.folder.appendingPathComponent(Store.safeFileName(src.deletingPathExtension().lastPathComponent) + " \(n).txt")
                        n += 1
                    }
                }
            }
            // Saved as UTF-8 so it reads the same everywhere
            try? QuoteFile.decode(data).data(using: .utf8)?.write(to: dest)
            added.append("\(parsed.name): \(parsed.quotes.count) quotes")
        }
        refresh()
        if !added.isEmpty {
            let a = NSAlert()
            a.messageText = "Imported"
            a.informativeText = added.joined(separator: "\n")
            a.runModal()
        }
    }

    @objc func restore() {
        let a = NSAlert()
        a.messageText = "Restore the built-in lists?"
        a.informativeText = "This puts back the lists Citorio came with. Changes you made to those files are replaced; your own categories are kept."
        a.addButton(withTitle: "Restore")
        a.addButton(withTitle: "Cancel")
        guard a.runModal() == .alertFirstButtonReturn else { return }
        app.store.installBuiltIns(overwrite: true)
        refresh()
        app.settingsChanged()
    }

    @objc func closeIt() { saveNumbers(); saveFont(); window?.close() }

    func windowWillClose(_ notification: Notification) { saveNumbers() }
}

final class FlippedView: NSView { override var isFlipped: Bool { true } }

// MARK: - Start

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}
if args.count == 4 && args[1] == "--render" {
    exit(Art.render(args[2], folder: args[3]) ? 0 : 1)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
