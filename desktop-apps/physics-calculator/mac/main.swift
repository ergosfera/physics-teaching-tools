// Physics Calculator for macOS: the calculator panel of
// tools/teaching/notebook_calculator.html as a desktop app (twin of windows/App.cs).
// Engine.swift holds the math. build.sh compiles both into the app.
import AppKit

// MARK: - Classic Windows 95 skin (same tokens as the web page)

enum W95 {
    static func rgb(_ r: Int, _ g: Int, _ b: Int) -> NSColor {
        NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: 1)
    }
    static let face = rgb(0xc0, 0xc0, 0xc0)
    static let hi = NSColor.white
    static let lt = rgb(0xdf, 0xdf, 0xdf)
    static let sh = rgb(0x80, 0x80, 0x80)
    static let dk = NSColor.black
    static let text = NSColor.black
    static let dim = rgb(0x40, 0x40, 0x40)
    static let titleFrom = rgb(0x00, 0x00, 0x80)
    static let navy = rgb(0x00, 0x33, 0x99)
    static let green = rgb(0x00, 0x66, 0x22)
    static let amber = rgb(0x7a, 0x4a, 0x00)
    static let red = rgb(0xa0, 0x00, 0x00)
    static let go = rgb(0x0a, 0x7a, 0x35)
    static let emptyText = rgb(0xa8, 0xa8, 0xa8)
    static let placeholder = rgb(0x8a, 0x8a, 0x8a)

    static var fontCache: [String: NSFont] = [:]
    static func font(_ name: String, _ size: CGFloat, bold: Bool = false) -> NSFont {
        let key = "\(name)|\(size)|\(bold)"
        if let f = fontCache[key] { return f }
        var f = NSFont(name: name, size: size) ?? NSFont.systemFont(ofSize: size)
        if bold { f = NSFontManager.shared.convert(f, toHaveTrait: .boldFontMask) }
        fontCache[key] = f
        return f
    }

    // Views are flipped: minY is the top edge
    static func edge(_ r: NSRect, _ tl: NSColor, _ br: NSColor) {
        tl.setFill()
        NSRect(x: r.minX, y: r.minY, width: r.width, height: 1).fill()
        NSRect(x: r.minX, y: r.minY, width: 1, height: r.height).fill()
        br.setFill()
        NSRect(x: r.minX, y: r.maxY - 1, width: r.width, height: 1).fill()
        NSRect(x: r.maxX - 1, y: r.minY, width: 1, height: r.height).fill()
    }
    static func raised(_ r: NSRect) { edge(r, hi, dk); edge(r.insetBy(dx: 1, dy: 1), lt, sh) }
    static func sunken(_ r: NSRect) { edge(r, dk, hi); edge(r.insetBy(dx: 1, dy: 1), sh, lt) }

    enum Align { case left, center, right }

    static func width(_ s: String, _ font: NSFont) -> CGFloat {
        (s as NSString).size(withAttributes: [.font: font]).width
    }

    static func draw(_ s: String, _ font: NSFont, _ color: NSColor, in r: NSRect, align: Align = .center) {
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color]
        let size = (s as NSString).size(withAttributes: attrs)
        let x: CGFloat
        switch align {
        case .left: x = r.minX
        case .center: x = r.midX - size.width / 2
        case .right: x = r.maxX - size.width
        }
        (s as NSString).draw(at: NSPoint(x: x, y: r.midY - size.height / 2), withAttributes: attrs)
    }

    // Single line, cut with "…" when too wide
    static func drawTruncated(_ s: String, _ font: NSFont, _ color: NSColor, in r: NSRect) {
        let style = NSMutableParagraphStyle()
        style.lineBreakMode = .byTruncatingTail
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color, .paragraphStyle: style]
        let h = (s as NSString).size(withAttributes: attrs).height
        (s as NSString).draw(with: NSRect(x: r.minX, y: r.midY - h / 2, width: r.width, height: h),
                             options: [.usesLineFragmentOrigin, .truncatesLastVisibleLine], attributes: attrs)
    }
}

class FlippedView: NSView {
    override var isFlipped: Bool { true }
}

// MARK: - Windows 95 push button that never takes keyboard focus

final class W95Button: FlippedView {
    var title: String { didSet { needsDisplay = true } }
    var face: NSColor = W95.face { didSet { needsDisplay = true } }
    var fore: NSColor = W95.text { didSet { needsDisplay = true } }
    var font: NSFont = W95.font("Tahoma", 15, bold: true)
    var symbol: String? = nil { didSet { needsDisplay = true } }
    var active = false { didSet { needsDisplay = true } }
    var action: (() -> Void)?
    private var pressed = false { didSet { needsDisplay = true } }

    init(_ title: String, action: (() -> Void)? = nil) {
        self.title = title
        self.action = action
        super.init(frame: .zero)
    }
    required init?(coder: NSCoder) { fatalError("not used") }

    override var acceptsFirstResponder: Bool { false }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func resetCursorRects() { addCursorRect(bounds, cursor: .pointingHand) }

    override func mouseDown(with event: NSEvent) { pressed = true }
    override func mouseDragged(with event: NSEvent) { pressed = bounds.contains(convert(event.locationInWindow, from: nil)) }
    override func mouseUp(with event: NSEvent) {
        let inside = bounds.contains(convert(event.locationInWindow, from: nil))
        pressed = false
        if inside { action?() }
    }

    override func draw(_ dirtyRect: NSRect) {
        face.setFill()
        bounds.fill()
        let down = pressed || active
        if down { W95.sunken(bounds) } else { W95.raised(bounds) }
        let r = down ? bounds.offsetBy(dx: 1, dy: 1) : bounds
        if let name = symbol, let base = NSImage(systemSymbolName: name, accessibilityDescription: title)?
            .withSymbolConfiguration(.init(pointSize: 12, weight: .semibold)) {
            let color = fore
            let tinted = NSImage(size: base.size, flipped: false) { rect in
                base.draw(in: rect)
                color.set()
                rect.fill(using: .sourceAtop)
                return true
            }
            let s = base.size
            tinted.draw(in: NSRect(x: r.midX - s.width / 2, y: r.midY - s.height / 2, width: s.width, height: s.height),
                        from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true, hints: nil)
        } else {
            W95.draw(title, font, fore, in: r)
        }
    }
}

// MARK: - "= 20 m/s" with the unit in navy, shrinking to fit like the web display

final class ResultView: FlippedView {
    private enum Mode { case empty, value, error }
    private var mode = Mode.empty
    private var num = "", unit = "", err = ""

    func showEmpty() { mode = .empty; needsDisplay = true }
    func showValue(_ n: String, _ u: String) { mode = .value; num = n; unit = u; needsDisplay = true }
    func showError(_ m: String) { mode = .error; err = m; needsDisplay = true }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.white.setFill()
        bounds.fill()
        switch mode {
        case .empty:
            W95.draw("—", W95.font("Courier New", 40, bold: true), W95.emptyText, in: bounds, align: .right)
        case .error:
            let style = NSMutableParagraphStyle()
            style.alignment = .right
            let attrs: [NSAttributedString.Key: Any] = [.font: W95.font("Courier New", 17, bold: true),
                                                        .foregroundColor: W95.red, .paragraphStyle: style]
            let text = "⚠ " + err as NSString
            let h = text.boundingRect(with: NSSize(width: bounds.width, height: 1000), options: [.usesLineFragmentOrigin],
                                      attributes: attrs).height
            text.draw(with: NSRect(x: 0, y: max(0, (bounds.height - h) / 2), width: bounds.width, height: h),
                      options: [.usesLineFragmentOrigin], attributes: attrs)
        case .value:
            let main = "= " + num
            let tight = unit == "°"
            var font = W95.font("Courier New", 40, bold: true)
            var wMain: CGFloat = 0, wUnit: CGFloat = 0, gap: CGFloat = 0
            var size: CGFloat = 40
            while size >= 16 {
                font = W95.font("Courier New", size, bold: true)
                wMain = W95.width(main, font)
                wUnit = unit.isEmpty ? 0 : W95.width(unit, font)
                gap = unit.isEmpty || tight ? 0 : 0.3 * size
                if wMain + gap + wUnit <= bounds.width { break }
                size -= 1.5
            }
            let x = bounds.width - (wMain + gap + wUnit)
            W95.draw(main, font, W95.text, in: NSRect(x: x, y: 0, width: wMain, height: bounds.height), align: .left)
            if !unit.isEmpty {
                W95.draw(unit, font, W95.navy, in: NSRect(x: x + wMain + gap, y: 0, width: wUnit, height: bounds.height), align: .left)
            }
        }
    }
}

// MARK: - History tape, newest first; click a line to reuse its result

struct HistItem { let expr: String; let out: String; let insert: String }

final class HistoryDoc: FlippedView {
    var items: [HistItem] = []
    var picked: ((HistItem) -> Void)?
    private var hover = -1
    private let rowH: CGFloat = 18
    private let mono = W95.font("Courier New", 13)
    private let monoBold = W95.font("Courier New", 13, bold: true)
    private let plain = W95.font("Tahoma", 12.5)
    private let code = W95.font("Courier New", 13.5, bold: true)

    // Hint shown while the tape is empty: plain words and code samples
    private static let hint: [[String]] = [
        ["Numbers can carry units: ", "`3 m/s × 2 s"],
        ["Convert with “to”: ", "`72 km/h to m/s", ", ", "`25 °C to °F"],
        ["Constants: ", "`G M_Earth / R_Earth^2", " · find any with ", "`⌘K"],
        ["½mv²: ", "`1/2 × 2 kg × (3 m/s)^2", " · ", "`\\hbar", " types ħ"]]

    private struct Word { let text: String; let isCode: Bool; let rect: NSRect }

    private func layoutHint(_ width: CGFloat) -> [Word] {
        var words: [Word] = []
        let pad: CGFloat = 5, lineH: CGFloat = 19
        var x = pad, y: CGFloat = 2
        for line in HistoryDoc.hint {
            for seg in line {
                let isCode = seg.hasPrefix("`")
                let text = isCode ? String(seg.dropFirst()) : seg
                let f = isCode ? code : plain
                // split after each space, keeping the space with its word
                var pieces: [String] = []
                var cur = ""
                for ch in text {
                    cur.append(ch)
                    if ch == " " { pieces.append(cur); cur = "" }
                }
                if !cur.isEmpty { pieces.append(cur) }
                for piece in pieces {
                    let w = W95.width(piece, f)
                    if x + w > width - pad && x > pad { x = pad; y += lineH }
                    words.append(Word(text: piece, isCode: isCode, rect: NSRect(x: x, y: y, width: w + 2, height: lineH)))
                    x += w
                }
            }
            x = pad
            y += lineH
        }
        return words
    }

    func contentHeight(_ width: CGFloat) -> CGFloat {
        if items.isEmpty { return (layoutHint(width).last?.rect.maxY ?? 0) + 4 }
        return CGFloat(items.count) * rowH + 6
    }

    override func draw(_ dirtyRect: NSRect) {
        NSColor.white.setFill()
        bounds.fill()
        if items.isEmpty {
            for w in layoutHint(bounds.width) {
                W95.draw(w.text, w.isCode ? code : plain, w.isCode ? W95.navy : W95.dim, in: w.rect, align: .left)
            }
            return
        }
        for i in 0..<items.count {
            let item = items[items.count - 1 - i]
            let row = NSRect(x: 3, y: 3 + CGFloat(i) * rowH, width: bounds.width - 6, height: rowH)
            if !row.intersects(dirtyRect) { continue }
            let hot = i == hover
            if hot { W95.titleFrom.setFill(); row.fill() }
            let res = "= " + item.out
            let wRes = W95.width(res, monoBold)
            let resRect = NSRect(x: row.maxX - 5 - wRes, y: row.minY, width: wRes, height: rowH)
            W95.drawTruncated(item.expr, mono, hot ? .white : W95.dim,
                              in: NSRect(x: row.minX + 5, y: row.minY, width: max(0, resRect.minX - row.minX - 15), height: rowH))
            W95.draw(res, monoBold, hot ? .white : W95.text, in: resRect, align: .left)
        }
    }

    private func rowAt(_ p: NSPoint) -> Int {
        if items.isEmpty { return -1 }
        let i = Int(floor((p.y - 3) / rowH))
        return i >= 0 && i < items.count ? i : -1
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        for t in trackingAreas { removeTrackingArea(t) }
        addTrackingArea(NSTrackingArea(rect: bounds, options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                                       owner: self, userInfo: nil))
    }

    override func mouseMoved(with event: NSEvent) {
        let h = rowAt(convert(event.locationInWindow, from: nil))
        if h != hover {
            hover = h
            toolTip = h >= 0 ? "Click to use this result" : nil
            needsDisplay = true
        }
    }
    override func mouseExited(with event: NSEvent) { hover = -1; needsDisplay = true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func mouseDown(with event: NSEvent) {}
    override func mouseUp(with event: NSEvent) {
        let i = rowAt(convert(event.locationInWindow, from: nil))
        if i >= 0 { picked?(items[items.count - 1 - i]) }
    }
}

// MARK: - Constants pad: a search bar, then one raised button per constant, grouped
// The search takes its keys from the expression box (CalcView.searchKey), so the
// focus never moves: ⌘K, type, ↑ ↓, Return.

final class ConstDoc: FlippedView {
    var picked: ((String) -> Void)?
    var searchClicked: (() -> Void)?
    private(set) var query: String?          // nil: not searching
    private(set) var items = Engine.CONSTANTS
    private(set) var pick = -1
    private var pressed = -1
    private enum Entry { case header(String), row(Int) }
    private var entries: [(Entry, NSRect)] = []
    private let pad: CGFloat = 8, barH: CGFloat = 30, headH: CGFloat = 22, rowH: CGFloat = 52, gap: CGFloat = 4

    func setQuery(_ q: String?) {
        query = q
        items = Engine.findConstants(q ?? "")
        pick = (q ?? "").trimmingCharacters(in: .whitespaces).isEmpty || items.isEmpty ? -1 : 0
        layoutEntries()
    }

    func movePick(_ step: Int) {
        if items.isEmpty { return }
        pick = max(0, min(items.count - 1, pick + step))
        needsDisplay = true
        if let r = rowRect(pick) { scrollToVisible(r.insetBy(dx: 0, dy: -gap)) }
    }

    var pickedConstant: Constant? { items.isEmpty ? nil : items[max(pick, 0)] }

    private func layoutEntries() {
        entries = []
        var y = pad + barH + gap
        var group: String? = nil
        let grouped = (query ?? "").trimmingCharacters(in: .whitespaces).isEmpty
        for (i, c) in items.enumerated() {
            if grouped && c.group != group {
                group = c.group
                entries.append((.header(c.group), NSRect(x: pad, y: y, width: 0, height: headH)))
                y += headH
            }
            entries.append((.row(i), NSRect(x: pad, y: y, width: 0, height: rowH)))
            y += rowH + gap
        }
        needsDisplay = true
    }

    func contentHeight() -> CGFloat {
        if entries.isEmpty && !items.isEmpty { layoutEntries() }
        return (entries.last.map { $0.1.maxY } ?? pad + barH + gap + 20) + pad
    }

    private func full(_ r: NSRect) -> NSRect { NSRect(x: r.minX, y: r.minY, width: bounds.width - 2 * pad, height: r.height) }
    private func rowRect(_ i: Int) -> NSRect? {
        for (e, r) in entries { if case .row(let j) = e, j == i { return full(r) } }
        return nil
    }
    private var barRect: NSRect { NSRect(x: pad, y: pad, width: bounds.width - 2 * pad, height: barH) }

    private static func pretty(_ v: String) -> String {
        v.replacingOccurrences(of: "e(-?\\d+)", with: " ×10^$1", options: .regularExpression)
    }

    // The textbook symbol: italic letter, upright subscript after "_"
    static func symbol(_ sym: String, _ size: CGFloat, _ color: NSColor) -> NSAttributedString {
        let italic = NSFont(name: "TimesNewRomanPS-ItalicMT", size: size)
            ?? NSFontManager.shared.convert(NSFont.systemFont(ofSize: size), toHaveTrait: .italicFontMask)
        let upright = NSFont(name: "Times New Roman", size: size * 0.6) ?? NSFont.systemFont(ofSize: size * 0.6)
        let parts = sym.split(separator: "_", maxSplits: 1).map(String.init)
        let out = NSMutableAttributedString(string: parts[0], attributes: [.font: italic, .foregroundColor: color])
        if parts.count > 1 {
            out.append(NSAttributedString(string: parts[1], attributes: [
                .font: upright, .foregroundColor: color, .baselineOffset: -size * 0.2]))
        }
        return out
    }

    private lazy var leftW: CGFloat = {
        let code = W95.font("Courier New", 11, bold: true)
        return Engine.CONSTANTS.map { max(ConstDoc.symbol($0.sym, 24, W95.navy).size().width, W95.width($0.typed, code)) }
            .max()! + 14
    }()

    override func draw(_ dirtyRect: NSRect) {
        W95.face.setFill()
        bounds.fill()

        // Search bar
        let bar = barRect
        NSColor.white.setFill()
        bar.fill()
        W95.sunken(bar)
        let tr = bar.insetBy(dx: 8, dy: 0)
        if let q = query {
            let f = W95.font("Courier New", 15, bold: true)
            W95.draw(q, f, W95.text, in: tr, align: .left)
            W95.text.setFill()
            NSRect(x: tr.minX + W95.width(q, f) + 1, y: bar.minY + 7, width: 2, height: barH - 14).fill()
            if q.isEmpty {
                W95.draw("type a name, symbol or word · ↑ ↓ · Return", W95.font("Tahoma", 11.5), W95.placeholder,
                         in: tr.offsetBy(dx: 8, dy: 0), align: .left)
            }
        } else {
            W95.draw("Search constants… (⌘K)", W95.font("Tahoma", 12), W95.placeholder, in: tr, align: .left)
        }

        if items.isEmpty {
            W95.draw("No constant matches.", W95.font("Tahoma", 12), W95.dim,
                     in: NSRect(x: pad + 2, y: bar.maxY + gap, width: bar.width, height: 20), align: .left)
        }
        let code = W95.font("Courier New", 11, bold: true)
        for (e, r0) in entries {
            let r = full(r0)
            if !r.intersects(dirtyRect) { continue }
            switch e {
            case .header(let g):
                W95.draw(g.uppercased(), W95.font("Tahoma", 11, bold: true), W95.dim,
                         in: NSRect(x: r.minX + 2, y: r.minY + 4, width: r.width, height: r.height - 4), align: .left)
            case .row(let i):
                let c = items[i]
                let down = i == pressed
                if down { W95.sunken(r) } else { W95.raised(r) }
                if i == pick { W95.navy.setStroke(); NSBezierPath(rect: r.insetBy(dx: 3.5, dy: 3.5)).stroke() }
                let d: CGFloat = down ? 1 : 0
                let x = r.minX + 10 + d
                ConstDoc.symbol(c.sym, 24, W95.navy).draw(at: NSPoint(x: x, y: r.minY + 3 + d))
                W95.draw(c.typed, code, W95.dim, in: NSRect(x: x, y: r.minY + 33 + d, width: leftW, height: 14), align: .left)
                let x2 = x + leftW, w2 = r.maxX - x2 - 6
                W95.drawTruncated((ConstDoc.pretty(c.value) + " " + c.unit).trimmingCharacters(in: .whitespaces),
                                  W95.font("Courier New", 13.5, bold: true), W95.text,
                                  in: NSRect(x: x2, y: r.minY + 6 + d, width: w2, height: 20))
                W95.drawTruncated(c.desc, W95.font("Tahoma", 12), W95.dim,
                                  in: NSRect(x: x2, y: r.minY + 28 + d, width: w2, height: 18))
            }
        }
    }

    private func rowAt(_ p: NSPoint) -> Int {
        for (e, r) in entries { if case .row(let i) = e, full(r).contains(p) { return i } }
        return -1
    }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }
    override func resetCursorRects() {
        addCursorRect(barRect, cursor: .iBeam)
        for (e, r) in entries { if case .row = e { addCursorRect(full(r), cursor: .pointingHand) } }
    }
    override func mouseDown(with event: NSEvent) {
        let p = convert(event.locationInWindow, from: nil)
        if barRect.contains(p) { searchClicked?(); return }
        pressed = rowAt(p)
        needsDisplay = true
    }
    override func mouseUp(with event: NSEvent) {
        let hit = rowAt(convert(event.locationInWindow, from: nil))
        let was = pressed
        pressed = -1
        needsDisplay = true
        if was >= 0 && hit == was { picked?(items[was].typed) }
    }
}

// A scroll view around a flipped document view; `bevel` draws the sunken white frame
final class ScrollBox: FlippedView {
    let scrollView = NSScrollView()
    let doc: FlippedView
    let bevel: Bool
    var contentHeight: (CGFloat) -> CGFloat = { _ in 0 }

    init(doc: FlippedView, bevel: Bool, background: NSColor) {
        self.doc = doc
        self.bevel = bevel
        super.init(frame: .zero)
        scrollView.hasVerticalScroller = true
        scrollView.autohidesScrollers = true
        scrollView.borderType = .noBorder
        scrollView.drawsBackground = true
        scrollView.backgroundColor = background
        scrollView.documentView = doc
        addSubview(scrollView)
    }
    required init?(coder: NSCoder) { fatalError("not used") }

    override func draw(_ dirtyRect: NSRect) {
        if bevel {
            NSColor.white.setFill()
            bounds.fill()
            W95.sunken(bounds)
        }
    }

    func relayout() {
        let inset: CGFloat = bevel ? 2 : 0
        scrollView.frame = bounds.insetBy(dx: inset, dy: inset)
        let w = scrollView.contentSize.width
        doc.frame = NSRect(x: 0, y: 0, width: w, height: max(contentHeight(w), scrollView.contentSize.height))
        doc.needsDisplay = true
        window?.invalidateCursorRects(for: doc)
    }

    func scrollToTop() { doc.scroll(.zero) }

    override func resizeSubviews(withOldSize oldSize: NSSize) { relayout() }
}

// MARK: - A 4 × 10 grid of keys

final class PadView: FlippedView {
    private var cells: [(W95Button, Int, Int, Int)] = []

    func place(_ b: W95Button, _ row: Int, _ col: Int, _ span: Int = 1) {
        cells.append((b, row, col, span))
        addSubview(b)
    }

    override func draw(_ dirtyRect: NSRect) { W95.face.setFill(); bounds.fill() }

    override func resizeSubviews(withOldSize oldSize: NSSize) {
        let pad: CGFloat = 8, gap: CGFloat = 4
        let cw = (bounds.width - 2 * pad - 3 * gap) / 4
        let ch = (bounds.height - 2 * pad - 9 * gap) / 10
        for (b, row, col, span) in cells {
            let x = (pad + CGFloat(col) * (cw + gap)).rounded()
            let y = (pad + CGFloat(row) * (ch + gap)).rounded()
            let x2 = (pad + CGFloat(col + span) * (cw + gap) - gap).rounded()
            let y2 = (pad + CGFloat(row + 1) * (ch + gap) - gap).rounded()
            b.frame = NSRect(x: x, y: y, width: x2 - x, height: y2 - y)
        }
    }
}

// MARK: - Expression box: a text field whose field editor reports keys first

final class ExprEditor: NSTextView {
    var keyHandler: ((NSEvent) -> Bool)?
    override func keyDown(with event: NSEvent) {
        if let h = keyHandler, h(event) { return }
        super.keyDown(with: event)
    }
}

// MARK: - Settings

final class Prefs {
    private let d = UserDefaults.standard
    init() { d.register(defaults: ["angleMode": "deg", "dec": -1, "sig": -1, "pow10": false, "topMost": false]) }
    var angleMode: String { get { d.string(forKey: "angleMode") ?? "deg" } set { d.set(newValue, forKey: "angleMode") } }
    var dec: Int? { get { let v = d.integer(forKey: "dec"); return v >= 0 && v <= 10 ? v : nil } set { d.set(newValue ?? -1, forKey: "dec") } }
    var sig: Int? { get { let v = d.integer(forKey: "sig"); return v >= 1 && v <= 8 ? v : nil } set { d.set(newValue ?? -1, forKey: "sig") } }
    var pow10: Bool { get { d.bool(forKey: "pow10") } set { d.set(newValue, forKey: "pow10") } }
    var topMost: Bool { get { d.bool(forKey: "topMost") } set { d.set(newValue, forKey: "topMost") } }
}

// MARK: - The calculator window content

final class CalcView: FlippedView, NSTextFieldDelegate {
    let engine = Engine()
    let prefs = Prefs()

    let titleLabel = NSTextField(labelWithString: "PHYSICS CALCULATOR")
    let btnPin = W95Button("Always on top")
    let btnAbout = W95Button("?")
    let btnDeg = W95Button("DEG")
    let btnRad = W95Button("RAD")
    let display = FlippedView()
    let expr = NSTextField()
    let editor = ExprEditor()
    let result = ResultView()
    let ans = NSTextField(labelWithString: "ANS = 0")
    let lblDec = NSTextField(labelWithString: "Dec")
    let lblSig = NSTextField(labelWithString: "Sig. fig.")
    let decSel = NSPopUpButton()
    let sigSel = NSPopUpButton()
    let btnPow10 = W95Button("×10^")
    let histDoc = HistoryDoc()
    lazy var hist = ScrollBox(doc: histDoc, bevel: true, background: .white)
    let tabs = [W95Button("Keys"), W95Button("Units"), W95Button("Constants")]
    let keysPad = PadView()
    let unitsPad = PadView()
    let constDoc = ConstDoc()
    lazy var constPad = ScrollBox(doc: constDoc, bevel: false, background: W95.face)

    var resultShown = false
    var lastResult: CalcResult?
    var histIdx = 0
    static let continues = try! NSRegularExpression(pattern: "^\\s*([-+−×÷*/^·]|to\\b)")
    static let letterStart = try! NSRegularExpression(pattern: "^[\\p{L}°]")
    static let joinEnd = try! NSRegularExpression(pattern: "[\\p{L}0-9)°⁰-⁹²³¹]$")

    static func test(_ re: NSRegularExpression, _ s: String) -> Bool {
        re.firstMatch(in: s, range: NSRange(location: 0, length: (s as NSString).length)) != nil
    }

    init() {
        super.init(frame: NSRect(x: 0, y: 0, width: 380, height: 720))
        buildHeader()
        buildDisplay()
        buildPads()
        for v in [display, hist, keysPad, unitsPad, constPad] as [NSView] { addSubview(v) }
        for t in tabs { addSubview(t) }
        applyPrefs()
        renderHistory()
        showAns()
    }
    required init?(coder: NSCoder) { fatalError("not used") }

    override func draw(_ dirtyRect: NSRect) {
        W95.face.setFill()
        bounds.fill()
        W95.sh.setFill()
        NSRect(x: 0, y: 38, width: bounds.width, height: 1).fill()
        W95.lt.setFill()
        NSRect(x: 0, y: 39, width: bounds.width, height: 1).fill()
    }

    // MARK: Building

    func buildHeader() {
        titleLabel.font = W95.font("Tahoma", 12, bold: true)
        titleLabel.textColor = W95.text
        addSubview(titleLabel)

        btnPin.action = { [weak self] in self?.setTopMost(!(self?.prefs.topMost ?? false)) }
        btnAbout.font = W95.font("Tahoma", 12.5, bold: true)
        btnAbout.toolTip = "About Physics Calculator"
        btnAbout.action = { [weak self] in self?.showAbout() }
        for (b, mode) in [(btnDeg, "deg"), (btnRad, "rad")] {
            b.font = W95.font("Tahoma", 12.5, bold: true)
            b.action = { [weak self] in self?.setAngleMode(mode) }
        }
        btnDeg.toolTip = "Plain numbers in sin/cos/tan are degrees"
        btnRad.toolTip = "Plain numbers in sin/cos/tan are radians"
        for b in [btnPin, btnAbout, btnDeg, btnRad] { addSubview(b) }
    }

    func buildDisplay() {
        expr.isBordered = false
        expr.drawsBackground = true
        expr.backgroundColor = .white
        expr.textColor = W95.text
        expr.font = W95.font("Courier New", 26, bold: true)
        expr.alignment = .right
        expr.focusRingType = .none
        expr.usesSingleLineMode = true
        expr.cell?.isScrollable = true
        expr.cell?.wraps = false
        expr.placeholderAttributedString = NSAttributedString(string: "e.g. 72 km/h to m/s", attributes: [
            .font: W95.font("Courier New", 16), .foregroundColor: W95.placeholder])
        expr.toolTip = "Numbers can carry units: 3 m/s × 2 s. Convert with “to”: 72 km/h to m/s. ↑/↓ recall earlier lines. " +
            "⌘K finds a constant; \\hbar types ħ."
        editor.isFieldEditor = true
        editor.keyHandler = { [weak self] e in self?.handleKey(e) ?? false }
        expr.delegate = self

        ans.font = W95.font("Courier New", 14)
        ans.textColor = W95.dim
        ans.alignment = .right
        for l in [lblDec, lblSig] {
            l.font = W95.font("Tahoma", 12)
            l.textColor = W95.dim
        }
        decSel.addItems(withTitles: ["auto"] + (0...10).map { String($0) })
        sigSel.addItems(withTitles: ["auto"] + (1...8).map { String($0) })
        for p in [decSel, sigSel] {
            p.controlSize = .small
            p.font = W95.font("Tahoma", 12)
            p.target = self
            p.refusesFirstResponder = true
        }
        decSel.action = #selector(decChanged)
        sigSel.action = #selector(sigChanged)

        btnPow10.font = W95.font("Courier New", 12.5, bold: true)
        btnPow10.toolTip = "Toggle ×10^ notation"
        btnPow10.action = { [weak self] in
            guard let self = self else { return }
            self.engine.pow10.toggle()
            self.prefs.pow10 = self.engine.pow10
            self.updatePow10Button()
            self.refreshShown()
            self.focusExpr()
        }

        for v in [expr, result, ans, lblDec, decSel, lblSig, sigSel, btnPow10] as [NSView] { display.addSubview(v) }
        histDoc.picked = { [weak self] item in if !item.insert.isEmpty { self?.insertAt(item.insert) } }
        hist.contentHeight = { [weak self] w in self?.histDoc.contentHeight(w) ?? 0 }
    }

    func key(_ text: String, _ what: String, _ kind: String) -> W95Button {
        let b = W95Button(text)
        switch kind {
        case "fn": b.face = W95.green; b.fore = .white
        case "op": b.face = W95.navy; b.fore = .white
        case "spec": b.face = W95.amber; b.fore = .white
        case "ctrl": b.face = W95.red; b.fore = .white
        case "eq": b.face = W95.go; b.fore = .white; b.font = W95.font("Tahoma", 17, bold: true)
        case "unit": b.face = .white; b.fore = W95.navy; b.font = W95.font("Courier New", 15, bold: true)
        default: b.face = .white; b.fore = .black
        }
        if what == " to " { b.toolTip = "Convert the result, e.g. 72 km/h to m/s" }
        b.action = { [weak self] in
            if what.hasPrefix("!") { self?.doAction(String(what.dropFirst())) } else { self?.insertAt(what) }
        }
        return b
    }

    func buildPads() {
        // [text, insert or !action, kind]
        let keys: [[String]] = [
            ["sin", "sin(", "fn"], ["cos", "cos(", "fn"], ["tan", "tan(", "fn"], ["⌫", "!back", "ctrl"],
            ["sin⁻¹", "asin(", "fn"], ["cos⁻¹", "acos(", "fn"], ["tan⁻¹", "atan(", "fn"], ["AC", "!clear", "ctrl"],
            ["log", "log(", "fn"], ["ln", "ln(", "fn"], ["√", "sqrt(", "fn"], ["x²", "^2", "fn"],
            ["π", "π", "spec"], ["e", "e", "spec"], ["×10^", "*10^", "spec"], ["xʸ", "^", "op"],
            ["(", "(", "op"], [")", ")", "op"], ["to", " to ", "spec"], ["ANS", "ANS", "spec"],
            ["7", "7", "num"], ["8", "8", "num"], ["9", "9", "num"], ["÷", "÷", "op"],
            ["4", "4", "num"], ["5", "5", "num"], ["6", "6", "num"], ["×", "×", "op"],
            ["1", "1", "num"], ["2", "2", "num"], ["3", "3", "num"], ["−", "−", "op"],
            ["±", "!negate", "op"], ["0", "0", "num"], [".", ".", "num"], ["+", "+", "op"]]
        for (i, k) in keys.enumerated() { keysPad.place(key(k[0], k[1], k[2]), i / 4, i % 4) }
        keysPad.place(key("= (Enter)", "!eq", "eq"), 9, 0, 4)

        let units = ["m", "cm", "mm", "km", "s", "min", "h", "ms", "kg", "g", "N", "J", "W", "Pa", "Hz", "kWh",
                     "m/s", "km/h", "m/s²", "eV", "A", "V", "Ω", "C", "K", "°C", "°", "rad", "L", "atm", "bar", "mol",
                     "·", "/", "²", "³"]
        for (i, u) in units.enumerated() {
            let op = ["·", "/", "²", "³"].contains(u)
            unitsPad.place(key(u, u, op ? "op" : "unit"), i / 4, i % 4)
        }
        unitsPad.place(key("to", " to ", "spec"), 9, 0, 2)
        unitsPad.place(key("⌫", "!back", "ctrl"), 9, 2)
        unitsPad.place(key("=", "!eq", "eq"), 9, 3)

        constDoc.picked = { [weak self] name in self?.closeConstSearch(); self?.insertAt(name) }
        constDoc.searchClicked = { [weak self] in self?.openConstSearch() }
        constPad.contentHeight = { [weak self] _ in self?.constDoc.contentHeight() ?? 0 }

        for (i, t) in tabs.enumerated() {
            t.font = W95.font("Tahoma", 12.5, bold: true)
            t.action = { [weak self] in self?.showPad(i); self?.focusExpr() }
            t.toolTip = ["F1", "F2", "F3 or ⌘K: search the constants"][i]
        }
        showPad(0)
    }

    func showPad(_ idx: Int) {
        for (i, t) in tabs.enumerated() {
            t.active = i == idx
            t.fore = i == idx ? W95.navy : W95.text
        }
        if idx != 2 && constDoc.query != nil { constDoc.setQuery(nil); constPad.relayout() }
        keysPad.isHidden = idx != 0
        unitsPad.isHidden = idx != 1
        constPad.isHidden = idx != 2
        if idx == 2 { constPad.relayout() }
    }

    // MARK: Layout

    override func resizeSubviews(withOldSize oldSize: NSSize) { doLayout() }

    func doLayout() {
        let W = bounds.width, H = bounds.height, m: CGFloat = 8

        // Header: title on the left; pin, about, DEG/RAD on the right
        let hh: CGFloat = 40, bh: CGFloat = 26, by = (hh - bh) / 2
        var x = W - 10
        btnRad.frame = NSRect(x: x - 44, y: by, width: 44, height: bh); x -= 44
        btnDeg.frame = NSRect(x: x - 44, y: by, width: 44, height: bh); x -= 44 + 8
        btnAbout.frame = NSRect(x: x - 26, y: by, width: 26, height: bh); x -= 26 + 3
        btnPin.frame = NSRect(x: x - 26, y: by, width: 26, height: bh); x -= 26
        titleLabel.sizeToFit()
        titleLabel.frame = NSRect(x: 10, y: (hh - 2 - titleLabel.frame.height) / 2, width: max(0, x - 14), height: titleLabel.frame.height)

        // Display
        var y = hh + m
        let dx: CGFloat = 12, dw = W - 2 * m
        let exprH: CGFloat = 32, resH: CGFloat = 50, ansH: CGFloat = 18, optH: CGFloat = 26
        let dh = 10 + exprH + 3 + resH + 3 + ansH + 6 + optH + 8
        display.frame = NSRect(x: m, y: y, width: dw, height: dh)
        var iy: CGFloat = 10
        expr.frame = NSRect(x: dx, y: iy, width: dw - 2 * dx, height: exprH); iy += exprH + 3
        result.frame = NSRect(x: dx, y: iy, width: dw - 2 * dx, height: resH); iy += resH + 3
        ans.frame = NSRect(x: dx, y: iy, width: dw - 2 * dx, height: ansH); iy += ansH + 6
        var ox = dx
        for (label, popup) in [(lblDec, decSel), (lblSig, sigSel)] {
            label.sizeToFit()
            label.frame.origin = NSPoint(x: ox, y: iy + (optH - label.frame.height) / 2)
            ox += label.frame.width + 6
            popup.frame = NSRect(x: ox, y: iy + 1, width: 64, height: optH - 2)
            ox += 64 + 10
        }
        btnPow10.frame = NSRect(x: ox, y: iy, width: 52, height: optH)
        y += dh

        // History tape
        y += 6
        hist.frame = NSRect(x: m, y: y, width: W - 2 * m, height: 92)
        hist.relayout()
        y += 92

        // Pad tabs and pads
        y += m
        let tw = ((W - 2 * m) / 3).rounded(.down)
        for (i, t) in tabs.enumerated() {
            t.frame = NSRect(x: m + CGFloat(i) * tw, y: y, width: i == 2 ? W - 2 * m - 2 * tw : tw, height: 26)
        }
        y += 26
        for pad in [keysPad, unitsPad, constPad] as [NSView] {
            pad.frame = NSRect(x: 0, y: y, width: W, height: max(0, H - y))
        }
        constPad.relayout()
        needsDisplay = true
    }

    // MARK: Settings

    func applyPrefs() {
        engine.angleMode = prefs.angleMode
        engine.decPlaces = prefs.dec
        engine.sigFigs = prefs.dec == nil ? prefs.sig : nil
        engine.pow10 = prefs.pow10
        decSel.selectItem(at: engine.decPlaces.map { $0 + 1 } ?? 0)
        sigSel.selectItem(at: engine.sigFigs ?? 0)
        updatePow10Button()
        updateModeButtons()
    }

    func updatePow10Button() {
        btnPow10.active = engine.pow10
        btnPow10.fore = engine.pow10 ? W95.navy : W95.text
    }

    func setTopMost(_ on: Bool) {
        prefs.topMost = on
        window?.level = on ? .floating : .normal
        btnPin.symbol = on ? "pin.fill" : "pin"
        btnPin.active = on
        btnPin.toolTip = on ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"
        (NSApp.delegate as? AppDelegate)?.topMostItem?.state = on ? .on : .off
        focusExpr()
    }

    func setAngleMode(_ mode: String) {
        engine.angleMode = mode
        prefs.angleMode = mode
        updateModeButtons()
        refreshShown()
        focusExpr()
    }

    func updateModeButtons() {
        btnDeg.active = engine.angleMode == "deg"
        btnRad.active = engine.angleMode == "rad"
    }

    @objc func decChanged() {
        let i = decSel.indexOfSelectedItem
        engine.decPlaces = i <= 0 ? nil : i - 1
        if engine.decPlaces != nil { engine.sigFigs = nil; sigSel.selectItem(at: 0) }
        prefs.dec = engine.decPlaces
        prefs.sig = engine.sigFigs
        refreshShown()
        focusExpr()
    }

    @objc func sigChanged() {
        let i = sigSel.indexOfSelectedItem
        engine.sigFigs = i <= 0 ? nil : i
        if engine.sigFigs != nil { engine.decPlaces = nil; decSel.selectItem(at: 0) }
        prefs.dec = engine.decPlaces
        prefs.sig = engine.sigFigs
        refreshShown()
        focusExpr()
    }

    @objc func showAbout() {
        let credits = NSMutableAttributedString(
            string: "Free software, provided \"as is\", without warranty of any kind. Check important results independently.\n" +
                    "This app collects no personal data and never connects to the internet.\n\n",
            attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.labelColor])
        credits.append(NSAttributedString(string: License.text,
            attributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]))
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Physics Calculator",
            .applicationVersion: "1.0",
            .version: "",
            .credits: credits,
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): "© 2026 Mauricio Mendivelso · MIT License",
        ])
    }

    // MARK: Expression box helpers

    var exprText: String {
        get { (expr.currentEditor() as? NSTextView)?.string ?? expr.stringValue }
        set {
            if let ed = expr.currentEditor() as? NSTextView { ed.string = newValue } else { expr.stringValue = newValue }
        }
    }

    var selection: NSRange {
        get { (expr.currentEditor() as? NSTextView)?.selectedRange() ?? NSRange(location: (exprText as NSString).length, length: 0) }
        set { (expr.currentEditor() as? NSTextView)?.setSelectedRange(newValue) }
    }

    func focusExpr() {
        guard let w = window else { return }
        if expr.currentEditor() == nil {
            w.makeFirstResponder(expr)
            selection = NSRange(location: (exprText as NSString).length, length: 0)
        }
    }

    func caretToEnd() { selection = NSRange(location: (exprText as NSString).length, length: 0) }

    // MARK: Calculator behaviour (as in the web page)

    func showAns() { ans.stringValue = "ANS = " + Engine.joinOut(engine.formatQ(engine.lastAns)) }

    func renderHistory() {
        hist.relayout()
        hist.scrollToTop()
    }

    func resetCalcDisplay() {
        exprText = ""
        result.showEmpty()
        resultShown = false
    }

    func commit() {
        let raw = exprText
        if raw.trimmingCharacters(in: .whitespaces).isEmpty { return }
        let r: CalcResult
        do { r = try engine.evaluate(raw) }
        catch let e as CalcError { result.showError(e.message); return }
        catch { result.showError("syntax error"); return }
        engine.lastAns = r.q
        lastResult = r
        showAns()
        let o = engine.formatResult(r)
        result.showValue(o.0, o.1)
        histDoc.items.append(HistItem(expr: raw.trimmingCharacters(in: .whitespacesAndNewlines),
                                      out: Engine.joinOut(o), insert: Engine.reinsertText(r)))
        if histDoc.items.count > 50 { histDoc.items.removeFirst() }
        histIdx = histDoc.items.count
        renderHistory()
        resultShown = true
        // expression stays visible so the user can see what they calculated
    }

    // Re-render after a format or angle-mode change
    func refreshShown() {
        showAns()
        if resultShown, let r = lastResult { let o = engine.formatResult(r); result.showValue(o.0, o.1) }
    }

    // MARK: Constants search (⌘K or F3)

    @objc func openConstSearch() {
        showPad(2)
        focusExpr()
        constDoc.setQuery("")
        constPad.relayout()
        constPad.scrollToTop()
    }

    func closeConstSearch() {
        if constDoc.query == nil { return }
        constDoc.setQuery(nil)
        constPad.relayout()
    }

    @objc func showKeysPad() { closeConstSearch(); showPad(0); focusExpr() }
    @objc func showUnitsPad() { closeConstSearch(); showPad(1); focusExpr() }

    // While searching, the expression box's keys go to the search
    func searchKey(_ e: NSEvent) -> Bool {
        guard let q = constDoc.query else { return false }
        switch e.keyCode {
        case 53: closeConstSearch()                                 // escape
        case 36, 76:                                                // return, enter
            let c = constDoc.pickedConstant
            closeConstSearch()
            if let c = c { insertAt(c.typed) }
        case 126, 125: constDoc.movePick(e.keyCode == 125 ? 1 : -1) // up, down
        case 51:                                                    // delete
            if q.isEmpty { closeConstSearch() } else { constDoc.setQuery(String(q.dropLast())); constPad.relayout() }
        default:
            guard let chars = e.characters, let scalar = chars.unicodeScalars.first,
                  !CharacterSet.controlCharacters.contains(scalar), !(0xF700...0xF8FF).contains(scalar.value) else { return true }
            constDoc.setQuery(q + chars)
            constPad.relayout()
            constPad.scrollToTop()
        }
        return true
    }

    // \hbar becomes ħ as soon as it is typed
    func controlTextDidChange(_ obj: Notification) {
        let v = exprText
        guard v.contains("\\") else { return }
        let at = min(selection.location, (v as NSString).length)
        let head = Engine.fromLatex((v as NSString).substring(to: at)), tail = Engine.fromLatex((v as NSString).substring(from: at))
        if head + tail == v { return }
        exprText = head + tail
        selection = NSRange(location: (head as NSString).length, length: 0)
    }

    func handleKey(_ e: NSEvent) -> Bool {
        let mods = e.modifierFlags.intersection([.command, .control])
        if !mods.isEmpty { return false }
        if searchKey(e) { return true }
        switch e.keyCode {
        case 36, 76: commit(); return true                  // return, enter
        case 53: resetCalcDisplay(); return true            // escape
        case 126, 125:                                      // up, down
            if histDoc.items.isEmpty { return false }
            histIdx = max(0, min(histDoc.items.count, histIdx + (e.keyCode == 126 ? -1 : 1)))
            exprText = histIdx < histDoc.items.count ? histDoc.items[histIdx].expr : ""
            caretToEnd()
            result.showEmpty()
            resultShown = false
            return true
        default: break
        }
        guard resultShown else { return false }
        // After a result, a new key starts fresh; an operator carries on from ANS
        if e.keyCode == 51 || e.keyCode == 117 {            // delete, forward delete
            resetCalcDisplay()
            return false
        }
        guard let chars = e.characters, let scalar = chars.unicodeScalars.first,
              !CharacterSet.controlCharacters.contains(scalar), !(0xF700...0xF8FF).contains(scalar.value) else { return false }
        exprText = CalcView.test(CalcView.continues, chars) ? "ANS" : ""
        caretToEnd()
        result.showEmpty()
        resultShown = false
        return false
    }

    func insertAt(_ textIn: String) {
        focusExpr()
        var text = textIn
        var s: Int, f: Int
        if resultShown {
            resetCalcDisplay()
            if CalcView.test(CalcView.continues, text) { exprText = "ANS" }
            s = (exprText as NSString).length
            f = s
        } else {
            let sel = selection
            s = sel.location
            f = sel.location + sel.length
        }
        let cur = exprText as NSString
        let before = cur.substring(to: s)
        // Keep units and constants apart from what comes before: "3 m s", not "3ms"
        if CalcView.test(CalcView.letterStart, text) && CalcView.test(CalcView.joinEnd, before) { text = " " + text }
        exprText = before + text + cur.substring(from: f)
        selection = NSRange(location: s + (text as NSString).length, length: 0)
    }

    func doAction(_ act: String) {
        focusExpr()
        switch act {
        case "back":
            if resultShown { resetCalcDisplay(); return }
            let sel = selection
            let cur = exprText as NSString
            if sel.length > 0 {
                exprText = cur.replacingCharacters(in: sel, with: "")
                selection = NSRange(location: sel.location, length: 0)
            } else if sel.location > 0 {
                exprText = cur.replacingCharacters(in: NSRange(location: sel.location - 1, length: 1), with: "")
                selection = NSRange(location: sel.location - 1, length: 0)
            }
        case "clear":
            resetCalcDisplay()
        case "eq":
            commit()
        case "negate":
            if resultShown { resetCalcDisplay() }
            let v = exprText.trimmingCharacters(in: .whitespacesAndNewlines)
            exprText = v.hasPrefix("-") ? String(v.dropFirst()) : (v.isEmpty ? "" : "-(" + v + ")")
            caretToEnd()
        default:
            break
        }
    }
}

// MARK: - License (same text as desktop-apps/LICENSE)

enum License {
    static let text = """
    MIT License

    Copyright (c) 2026 Mauricio Mendivelso

    Permission is hereby granted, free of charge, to any person obtaining a copy \
    of this software and associated documentation files (the "Software"), to deal \
    in the Software without restriction, including without limitation the rights \
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell \
    copies of the Software, and to permit persons to whom the Software is \
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all \
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR \
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, \
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE \
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER \
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, \
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE \
    SOFTWARE.
    """
}

// MARK: - App icon (used by build.sh: PhysicsCalculator --write-iconset <dir>)

enum IconArt {
    // A little Windows 95 calculator: grey body, white display, colored keys (drawn y-up)
    static func draw(_ s: CGFloat) {
        func rect(_ x: CGFloat, _ top: CGFloat, _ w: CGFloat, _ h: CGFloat) -> NSRect {
            NSRect(x: x, y: s - top - h, width: w, height: h)
        }
        func bevel(_ r: NSRect, _ t: CGFloat, raised: Bool) {
            (raised ? NSColor.white : W95.sh).setFill()
            NSRect(x: r.minX, y: r.maxY - t, width: r.width, height: t).fill()
            NSRect(x: r.minX, y: r.minY, width: t, height: r.height).fill()
            (raised ? W95.rgb(0x50, 0x50, 0x50) : NSColor.white).setFill()
            NSRect(x: r.minX, y: r.minY, width: r.width, height: t).fill()
            NSRect(x: r.maxX - t, y: r.minY, width: t, height: r.height).fill()
        }
        let t = max(1, (s / 40).rounded())
        let body = rect((s * 0.17).rounded(), (s * 0.08).rounded(), (s * 0.66).rounded(), (s * 0.84).rounded())
        W95.face.setFill()
        body.fill()
        bevel(body, t, raised: true)
        let pad = (s * 0.07).rounded()
        let disp = NSRect(x: body.minX + pad, y: body.maxY - pad - (s * 0.18).rounded(), width: body.width - 2 * pad, height: (s * 0.18).rounded())
        NSColor.white.setFill()
        disp.fill()
        bevel(disp, t, raised: false)
        W95.navy.setFill()
        NSRect(x: disp.maxX - disp.width * 0.45, y: disp.minY + disp.height * 0.38, width: disp.width * 0.3, height: max(t, disp.height * 0.22)).fill()
        let colors: [[NSColor]] = [[W95.green, W95.green, W95.red], [.white, .white, W95.navy], [.white, .white, W95.navy]]
        let gap = max(1, (s * 0.03).rounded())
        let top = disp.minY - pad * 0.8
        let kw = (disp.width - 2 * gap) / 3, kh = (top - (body.minY + pad) - 2 * gap) / 3
        for r in 0..<3 {
            for c in 0..<3 {
                let k = NSRect(x: disp.minX + CGFloat(c) * (kw + gap), y: top - CGFloat(r + 1) * kh - CGFloat(r) * gap, width: kw, height: kh)
                colors[r][c].setFill()
                k.fill()
                bevel(k, max(1, t / 2), raised: true)
            }
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

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    var window: NSWindow!
    var calc: CalcView!
    var topMostItem: NSMenuItem?

    func applicationDidFinishLaunching(_ note: Notification) {
        calc = CalcView()
        window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 380, height: 720),
                          styleMask: [.titled, .closable, .miniaturizable, .resizable], backing: .buffered, defer: false)
        window.title = "Physics Calculator"
        window.contentView = calc
        window.contentMinSize = NSSize(width: 320, height: 600)
        window.delegate = self
        window.isReleasedWhenClosed = false
        if !window.setFrameUsingName("PhysicsCalculator"), let screen = NSScreen.main {
            let vf = screen.visibleFrame
            var frame = window.frame
            frame.size.height = min(frame.height, vf.height - 20)
            frame.origin = NSPoint(x: vf.maxX - frame.width - 24, y: vf.midY - frame.height / 2)
            window.setFrame(frame, display: false)
        }
        window.setFrameAutosaveName("PhysicsCalculator")
        buildMenu()
        calc.doLayout()
        window.makeKeyAndOrderFront(nil)
        calc.setTopMost(calc.prefs.topMost)
        calc.focusExpr()
        NSApp.activate(ignoringOtherApps: true)
    }

    // The expression box gets a field editor that reports keys first
    func windowWillReturnFieldEditor(_ sender: NSWindow, to client: Any?) -> Any? {
        if let field = client as? NSTextField, field === calc?.expr { return calc.editor }
        return nil
    }

    func buildMenu() {
        let main = NSMenu()

        let appItem = NSMenuItem()
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About Physics Calculator", action: #selector(CalcView.showAbout), keyEquivalent: "").target = calc
        appMenu.addItem(.separator())
        let top = appMenu.addItem(withTitle: "Always on Top", action: #selector(toggleTopMost), keyEquivalent: "")
        top.target = self
        topMostItem = top
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Hide Physics Calculator", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Quit Physics Calculator", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu
        main.addItem(appItem)

        // Standard editing commands so ⌘C, ⌘V, ⌘A… work in the expression box
        let editItem = NSMenuItem()
        let edit = NSMenu(title: "Edit")
        edit.addItem(withTitle: "Undo", action: Selector(("undo:")), keyEquivalent: "z")
        let redo = edit.addItem(withTitle: "Redo", action: Selector(("redo:")), keyEquivalent: "z")
        redo.keyEquivalentModifierMask = [.command, .shift]
        edit.addItem(.separator())
        edit.addItem(withTitle: "Cut", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        edit.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        edit.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        edit.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        editItem.submenu = edit
        main.addItem(editItem)

        // The pads from the keyboard: F1 F2 F3, and ⌘K to find a constant
        let padItem = NSMenuItem()
        let padMenu = NSMenu(title: "Pad")
        for (title, sel, key) in [("Keys", #selector(CalcView.showKeysPad), NSF1FunctionKey),
                                  ("Units", #selector(CalcView.showUnitsPad), NSF2FunctionKey),
                                  ("Constants", #selector(CalcView.openConstSearch), NSF3FunctionKey)] {
            let item = padMenu.addItem(withTitle: title, action: sel, keyEquivalent: String(Character(UnicodeScalar(key)!)))
            item.keyEquivalentModifierMask = []
            item.target = calc
        }
        padMenu.addItem(.separator())
        padMenu.addItem(withTitle: "Find Constant…", action: #selector(CalcView.openConstSearch), keyEquivalent: "k").target = calc
        padItem.submenu = padMenu
        main.addItem(padItem)

        let winItem = NSMenuItem()
        let win = NSMenu(title: "Window")
        win.addItem(withTitle: "Minimize", action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        winItem.submenu = win
        main.addItem(winItem)

        NSApp.mainMenu = main
    }

    @objc func toggleTopMost() { calc.setTopMost(!calc.prefs.topMost) }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}
if args.count == 3 && args[1] == "--selftest" {
    exit(runSelfTest(args[2]))
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
