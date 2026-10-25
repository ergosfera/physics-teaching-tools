// Windows 95 look for macOS apps: skin, window chrome, in-window menus, scroll bars,
// dialog controls and message boxes. Shared by Notepad 95 and Paint 95; each app's build.sh
// compiles it together with the app's own main.swift.
import AppKit

// MARK: - Windows 95 skin

enum W95 {
    static func rgb(_ hex: Int) -> NSColor {
        NSColor(srgbRed: CGFloat((hex >> 16) & 0xff) / 255, green: CGFloat((hex >> 8) & 0xff) / 255,
                blue: CGFloat(hex & 0xff) / 255, alpha: 1)
    }
    static let face = rgb(0xc0c0c0), light = rgb(0xdfdfdf), white = rgb(0xffffff)
    static let shadow = rgb(0x808080), black = rgb(0x000000), navy = rgb(0x000080), teal = rgb(0x008080)
    static let yellow = rgb(0xffff00), red = rgb(0xff0000)

    /// Screen points per Windows 95 pixel. 1.5 makes the title bar as tall as a Mac one,
    /// and on a Retina screen each Win95 pixel is exactly 3 × 3 screen pixels, so lines stay sharp.
    static let scale: CGFloat = 1.5

    /// The app's 16 × 16 icon in pixel art, shown in title bars and the About box; set by the app at launch
    static var appIcon: [String] = []
    /// Title of message boxes ("Notepad", "Paint"); set by the app at launch
    static var appName = ""

    // MS Sans Serif 8 pt was the Windows 95 interface font; macOS ships its TrueType twin
    static let ui: NSFont = NSFont(name: "MicrosoftSansSerif", size: 11) ?? NSFont(name: "Tahoma", size: 11)
        ?? .systemFont(ofSize: 11)
    static let lineHeight = ceil(ui.ascender - ui.descender)

    static let palette: [Character: NSColor] = [
        "k": black, "w": white, "g": face, "l": light, "d": shadow, "b": navy, "t": teal, "y": yellow, "r": red,
        "u": rgb(0x0000ff), "G": rgb(0x008000), "c": rgb(0x00ffff), "m": rgb(0xff00ff),
    ]

    /// Windows 95 drew everything without anti-aliasing
    static func crisp() {
        guard let cg = NSGraphicsContext.current?.cgContext else { return }
        cg.setShouldAntialias(false)
        cg.setShouldSmoothFonts(false)
        cg.interpolationQuality = .none
    }

    static func fill(_ r: NSRect, _ c: NSColor) { c.setFill(); r.fill() }

    /// One-pixel frame: top and left in `tl`, bottom and right in `br` (flipped coordinates)
    static func edge(_ r: NSRect, _ tl: NSColor, _ br: NSColor) {
        fill(NSRect(x: r.minX, y: r.minY, width: r.width - 1, height: 1), tl)
        fill(NSRect(x: r.minX, y: r.minY, width: 1, height: r.height - 1), tl)
        fill(NSRect(x: r.minX, y: r.maxY - 1, width: r.width, height: 1), br)
        fill(NSRect(x: r.maxX - 1, y: r.minY, width: 1, height: r.height), br)
    }

    enum Bevel { case window, button, pressed, sunken }
    static func bevel(_ r: NSRect, _ b: Bevel) {
        let i = r.insetBy(dx: 1, dy: 1)
        switch b {
        case .window: edge(r, light, black); edge(i, white, shadow)
        case .button: edge(r, white, black); edge(i, light, shadow)
        case .pressed: edge(r, black, white); edge(i, shadow, light)
        case .sunken: edge(r, shadow, white); edge(i, black, light)
        }
    }

    /// Two etched lines, like a group box or a menu separator
    static func etchedLine(x: CGFloat, y: CGFloat, width: CGFloat) {
        fill(NSRect(x: x, y: y, width: width, height: 1), shadow)
        fill(NSRect(x: x, y: y + 1, width: width, height: 1), white)
    }

    static func width(_ s: String, _ font: NSFont = ui) -> CGFloat {
        ceil((s as NSString).size(withAttributes: [.font: font]).width)
    }

    static func text(_ s: String, at p: NSPoint, color: NSColor = black, bold: Bool = false) {
        let a: [NSAttributedString.Key: Any] = [.font: ui, .foregroundColor: color]
        (s as NSString).draw(at: p, withAttributes: a)
        // Windows emboldened bitmap fonts by drawing them twice, one pixel apart
        if bold { (s as NSString).draw(at: NSPoint(x: p.x + 1, y: p.y), withAttributes: a) }
    }

    /// "&File" -> ("File", 0): the letter after & is underlined and works as a shortcut
    static func mnemonic(_ s: String) -> (String, Int?) {
        guard let r = s.range(of: "&") else { return (s, nil) }
        let i = s.distance(from: s.startIndex, to: r.lowerBound)
        return (s.replacingCharacters(in: r, with: ""), i)
    }

    static func mnemonicKey(_ s: String) -> String? {
        let (t, i) = mnemonic(s)
        guard let i, i < t.count else { return nil }
        return String(t[t.index(t.startIndex, offsetBy: i)]).lowercased()
    }

    /// Draws a "&Label", underlining its mnemonic letter. Disabled labels are gray with a white shadow.
    static func label(_ s: String, at p: NSPoint, color: NSColor = black, disabled: Bool = false) {
        if disabled {
            label(s, at: NSPoint(x: p.x + 1, y: p.y + 1), color: white)
            label(s, at: p, color: shadow)
            return
        }
        let (t, i) = mnemonic(s)
        text(t, at: p, color: color)
        if let i, i < t.count {
            let prefix = String(t.prefix(i)), ch = String(t[t.index(t.startIndex, offsetBy: i)])
            let x = p.x + (prefix as NSString).size(withAttributes: [.font: ui]).width
            fill(NSRect(x: floor(x), y: p.y + round(ui.ascender) + 1, width: max(1, width(ch) - 1), height: 1), color)
        }
    }

    /// Focus rectangle: every other pixel
    static func dotted(_ r: NSRect) {
        for x in stride(from: r.minX, to: r.maxX, by: 2) {
            fill(NSRect(x: x, y: r.minY, width: 1, height: 1), black)
            fill(NSRect(x: x, y: r.maxY - 1, width: 1, height: 1), black)
        }
        for y in stride(from: r.minY, to: r.maxY, by: 2) {
            fill(NSRect(x: r.minX, y: y, width: 1, height: 1), black)
            fill(NSRect(x: r.maxX - 1, y: y, width: 1, height: 1), black)
        }
    }

    /// Pixel art from rows of palette letters ("." is transparent); `color` paints every pixel one color
    static func pixels(_ rows: [String], at o: NSPoint, scale: CGFloat = 1, color: NSColor? = nil) {
        for (y, row) in rows.enumerated() {
            for (x, ch) in row.enumerated() where ch != "." {
                (color ?? palette[ch] ?? black).setFill()
                NSRect(x: o.x + CGFloat(x) * scale, y: o.y + CGFloat(y) * scale, width: scale, height: scale).fill()
            }
        }
    }

    static let check = ["......k", ".....kk", "k...kkk", "kk.kkk.", "kkkkk..", ".kkk...", "..k...."]

    // Scroll bar track: white and gray pixels in a checkerboard
    static let dither: NSColor = {
        let img = NSImage(size: NSSize(width: 2, height: 2), flipped: false) { _ in
            face.setFill(); NSRect(x: 0, y: 0, width: 2, height: 2).fill()
            white.setFill(); NSRect(x: 0, y: 0, width: 1, height: 1).fill(); NSRect(x: 1, y: 1, width: 1, height: 1).fill()
            return true
        }
        return NSColor(patternImage: img)
    }()
}

class FlippedView: NSView {
    override var isFlipped: Bool { true }
}

// MARK: - Window chrome

final class Win95Window: NSWindow {
    var acceptsMain = true
    var onEnter: (() -> Void)?
    var onEscape: (() -> Void)?
    var onKey: ((String) -> Bool)?
    private lazy var editor: NSTextView = {
        let t = NSTextView()
        t.isFieldEditor = true
        t.insertionPointColor = W95.black
        t.selectedTextAttributes = [.backgroundColor: W95.navy, .foregroundColor: W95.white]
        return t
    }()

    init(contentRect: NSRect, minimizable: Bool) {
        super.init(contentRect: contentRect, styleMask: minimizable ? [.borderless, .miniaturizable] : [.borderless],
                   backing: .buffered, defer: false)
        hasShadow = false
        isOpaque = true
        backgroundColor = W95.face
        appearance = NSAppearance(named: .aqua)
        isReleasedWhenClosed = false
        acceptsMouseMovedEvents = true
        autorecalculatesKeyViewLoop = true
    }

    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { acceptsMain }

    override func fieldEditor(_ createFlag: Bool, for object: Any?) -> NSText? {
        object is NSTextField ? editor : super.fieldEditor(createFlag, for: object)
    }

    override func becomeKey() { super.becomeKey(); (contentView as? ChromeView)?.titleBar.active = true }
    override func resignKey() { super.resignKey(); (contentView as? ChromeView)?.titleBar.active = false }

    override func keyDown(with e: NSEvent) {
        if let k = e.charactersIgnoringModifiers?.lowercased(), e.modifierFlags.intersection([.command, .control]).isEmpty,
           onKey?(k) == true { return }
        switch e.keyCode {
        case 36, 76: if let f = onEnter { f() } else { super.keyDown(with: e) }
        case 53: if let f = onEscape { f() } else { super.keyDown(with: e) }
        default: super.keyDown(with: e)
        }
    }

    override func cancelOperation(_ sender: Any?) { onEscape?() }
}

/// The window's whole content: raised gray frame, title bar, and a client area below it.
/// Everything inside is laid out in Windows 95 pixels and magnified by W95.scale.
final class ChromeView: FlippedView {
    static let border: CGFloat = 4, top: CGFloat = 23
    let titleBar: TitleBarView
    let client = FlippedView()
    let resizable: Bool
    var minSize = NSSize(width: 240 * W95.scale, height: 140 * W95.scale)   // in screen points
    var onResize: (() -> Void)?

    /// Window size in screen points for a client area given in Windows 95 pixels
    static func windowSize(forClient s: NSSize) -> NSSize {
        NSSize(width: (s.width + 2 * border) * W95.scale, height: (s.height + top + border) * W95.scale)
    }

    init(frame: NSRect, title: String, kinds: [CaptionButton.Kind], resizable: Bool, showIcon: Bool) {
        titleBar = TitleBarView(frame: NSRect(x: 4, y: 4, width: frame.width / W95.scale - 8, height: 18), title: title,
                                kinds: kinds, showIcon: showIcon)
        self.resizable = resizable
        super.init(frame: frame)
        autoresizesSubviews = false
        setBoundsSize(NSSize(width: frame.width / W95.scale, height: frame.height / W95.scale))
        addSubview(titleBar)
        addSubview(client)
        layoutParts()
    }
    required init?(coder: NSCoder) { fatalError() }

    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        setBoundsSize(NSSize(width: newSize.width / W95.scale, height: newSize.height / W95.scale))
        layoutParts()
    }

    private func layoutParts() {
        let w = bounds.width, h = bounds.height
        titleBar.frame = NSRect(x: 4, y: 4, width: w - 8, height: 18)
        client.frame = NSRect(x: 4, y: ChromeView.top, width: w - 8, height: h - ChromeView.top - 4)
    }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        W95.bevel(bounds, .window)
    }

    // Resizing from the frame, since the window has no system frame
    private struct Edges { var left = false, right = false, top = false, bottom = false }

    private func edges(at p: NSPoint) -> Edges {
        let b = ChromeView.border, c: CGFloat = 16
        var e = Edges()
        let nearL = p.x < c, nearR = p.x > bounds.width - c, nearT = p.y < c, nearB = p.y > bounds.height - c
        if p.x < b { e.left = true; e.top = nearT; e.bottom = nearB }
        if p.x > bounds.width - b { e.right = true; e.top = nearT; e.bottom = nearB }
        if p.y < b { e.top = true; e.left = nearL; e.right = nearR }
        if p.y > bounds.height - b { e.bottom = true; e.left = nearL; e.right = nearR }
        return e
    }

    override func resetCursorRects() {
        guard resizable else { return }
        let w = bounds.width, h = bounds.height, b = ChromeView.border, c: CGFloat = 16
        func add(_ r: NSRect, _ pos: Int) { addCursorRect(r, cursor: ChromeView.cursor(pos)) }
        add(NSRect(x: c, y: 0, width: w - 2 * c, height: b), 1)            // top
        add(NSRect(x: c, y: h - b, width: w - 2 * c, height: b), 1)        // bottom
        add(NSRect(x: 0, y: c, width: b, height: h - 2 * c), 0)            // left
        add(NSRect(x: w - b, y: c, width: b, height: h - 2 * c), 0)        // right
        add(NSRect(x: 0, y: 0, width: c, height: b), 2); add(NSRect(x: 0, y: 0, width: b, height: c), 2)
        add(NSRect(x: w - c, y: h - b, width: c, height: b), 2); add(NSRect(x: w - b, y: h - c, width: b, height: c), 2)
        add(NSRect(x: w - c, y: 0, width: c, height: b), 3); add(NSRect(x: w - b, y: 0, width: b, height: c), 3)
        add(NSRect(x: 0, y: h - b, width: c, height: b), 3); add(NSRect(x: 0, y: h - c, width: b, height: c), 3)
    }

    /// 0 left-right, 1 up-down, 2 top-left/bottom-right diagonal, 3 top-right/bottom-left diagonal
    private static func cursor(_ kind: Int) -> NSCursor {
        if #available(macOS 15.0, *) {
            switch kind {
            case 0: return .frameResize(position: .right, directions: .all)
            case 1: return .frameResize(position: .bottom, directions: .all)
            case 2: return .frameResize(position: .bottomRight, directions: .all)
            default: return .frameResize(position: .bottomLeft, directions: .all)
            }
        }
        switch kind {
        case 0: return .resizeLeftRight
        case 1: return .resizeUpDown
        default: return .crosshair
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard resizable, let win = window else { return }
        let e = edges(at: convert(event.locationInWindow, from: nil))
        guard e.left || e.right || e.top || e.bottom else { return }
        let start = NSEvent.mouseLocation, f0 = win.frame
        while let ev = win.nextEvent(matching: [.leftMouseDragged, .leftMouseUp]), ev.type != .leftMouseUp {
            let p = NSEvent.mouseLocation, dx = p.x - start.x, dy = p.y - start.y
            var f = f0
            if e.right { f.size.width = max(minSize.width, f0.width + dx) }
            if e.left { f.size.width = max(minSize.width, f0.width - dx); f.origin.x = f0.maxX - f.width }
            if e.top { f.size.height = max(minSize.height, f0.height + dy) }
            if e.bottom { f.size.height = max(minSize.height, f0.height - dy); f.origin.y = f0.maxY - f.height }
            win.setFrame(f, display: true)
        }
        onResize?()
    }
}

final class TitleBarView: FlippedView {
    var title: String { didSet { needsDisplay = true } }
    var active = true { didSet { needsDisplay = true } }
    var maximized = false { didSet { buttons.forEach { $0.restore = maximized } } }
    var onButton: ((CaptionButton.Kind) -> Void)?
    var onDoubleClick: (() -> Void)?
    private let showIcon: Bool
    private var buttons: [CaptionButton] = []

    init(frame: NSRect, title: String, kinds: [CaptionButton.Kind], showIcon: Bool) {
        self.title = title
        self.showIcon = showIcon
        super.init(frame: frame)
        var x = frame.width - 2
        for kind in kinds.reversed() {
            x -= 16
            let b = CaptionButton(frame: NSRect(x: x, y: 2, width: 16, height: 14), kind: kind)
            b.autoresizingMask = [.minXMargin]
            b.action = { [weak self] in self?.onButton?(kind) }
            addSubview(b)
            buttons.append(b)
            if kind == .close { x -= 2 }   // Windows 95 leaves a gap before the close button
        }
    }
    required init?(coder: NSCoder) { fatalError() }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, active ? W95.navy : W95.shadow)
        var x: CGFloat = 3
        if showIcon { W95.pixels(W95.appIcon, at: NSPoint(x: 2, y: 1)); x = 21 }
        let limit = (buttons.map { $0.frame.minX }.min() ?? bounds.width) - 4 - x
        var t = title
        while t.count > 1 && W95.width(t) > limit { t = String(t.dropLast(t.hasSuffix("...") ? 4 : 1)) + "..." }
        W95.text(t, at: NSPoint(x: x, y: floor((bounds.height - W95.lineHeight) / 2)),
                 color: active ? W95.white : W95.face, bold: true)
    }

    override func mouseDown(with event: NSEvent) {
        if event.clickCount == 2, let f = onDoubleClick { f(); return }
        window?.performDrag(with: event)
    }
}

final class CaptionButton: NSView {
    enum Kind { case minimize, maximize, close }
    let kind: Kind
    var restore = false { didSet { needsDisplay = true } }
    var action: () -> Void = {}
    private var pressed = false

    init(frame: NSRect, kind: Kind) { self.kind = kind; super.init(frame: frame) }
    required init?(coder: NSCoder) { fatalError() }
    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    private static let glyphs: [Kind: [String]] = [
        .close: ["kk....kk", ".kk..kk.", "..kkkk..", "...kk...", "..kkkk..", ".kk..kk.", "kk....kk"],
        .minimize: [".......", ".......", ".......", ".......", ".......", ".......", "kkkkkk.", "kkkkkk."],
        .maximize: ["kkkkkkkkk", "kkkkkkkkk", "k.......k", "k.......k", "k.......k", "k.......k", "k.......k",
                    "k.......k", "kkkkkkkkk"],
    ]
    private static let restoreGlyph = ["..kkkkkk", "..kkkkkk", "..k....k", "kkkkkk.k", "kkkkkk.k", "k....kkk", "k....k..",
                                       "k....k..", "kkkkkk.."]

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        W95.bevel(bounds, pressed ? .pressed : .button)
        let g = kind == .maximize && restore ? CaptionButton.restoreGlyph : CaptionButton.glyphs[kind]!
        let w = CGFloat(g[0].count), h = CGFloat(g.count), off: CGFloat = pressed ? 1 : 0
        W95.pixels(g, at: NSPoint(x: floor((bounds.width - w) / 2) + off, y: floor((bounds.height - h) / 2) + off))
    }

    override func mouseDown(with event: NSEvent) {
        pressed = true; display()
        while let e = window?.nextEvent(matching: [.leftMouseDragged, .leftMouseUp]) {
            let inside = bounds.contains(convert(e.locationInWindow, from: nil))
            if e.type == .leftMouseUp {
                pressed = false; display()
                if inside { action() }
                return
            }
            if inside != pressed { pressed = inside; display() }
        }
    }
}

// MARK: - Menus (drawn like Windows 95, inside the window)

struct MenuItem95 {
    let title: String, shortcut: String, isSeparator: Bool
    let enabled: () -> Bool, checked: () -> Bool, run: () -> Void

    init(_ title: String, _ shortcut: String = "", enabled: @escaping () -> Bool = { true },
         checked: @escaping () -> Bool = { false }, run: @escaping () -> Void) {
        self.title = title; self.shortcut = shortcut; isSeparator = false
        self.enabled = enabled; self.checked = checked; self.run = run
    }
    private init() {
        title = ""; shortcut = ""; isSeparator = true
        enabled = { false }; checked = { false }; run = {}
    }
    static let separator = MenuItem95()
}

final class PopupView: FlippedView {
    let items: [MenuItem95]
    let enabled: [Bool]
    private let checked: [Bool]
    private let rows: [NSRect]
    private let keyX: CGFloat
    var hover: Int? { didSet { if hover != oldValue { needsDisplay = true } } }

    init(items: [MenuItem95]) {
        self.items = items
        enabled = items.map { !$0.isSeparator && $0.enabled() }
        checked = items.map { $0.checked() }
        let textW = items.map { W95.width(W95.mnemonic($0.title).0) }.max() ?? 0
        let keyW = items.map { W95.width($0.shortcut) }.max() ?? 0
        let width = 3 + 18 + textW + (keyW > 0 ? 24 + keyW : 0) + 18 + 3
        keyX = 3 + 18 + textW + 24
        var y: CGFloat = 3, r: [NSRect] = []
        for item in items {
            let h: CGFloat = item.isSeparator ? 8 : 17
            r.append(NSRect(x: 3, y: y, width: width - 6, height: h))
            y += h
        }
        rows = r
        super.init(frame: NSRect(x: 0, y: 0, width: width, height: y + 3))
    }
    required init?(coder: NSCoder) { fatalError() }

    func index(at p: NSPoint) -> Int? {
        rows.firstIndex { $0.contains(p) }.flatMap { items[$0].isSeparator ? nil : $0 }
    }

    func move(_ step: Int) {
        let n = items.count
        var i = hover ?? (step > 0 ? -1 : n)
        for _ in 0..<n {
            i = (i + step + n) % n
            if !items[i].isSeparator { hover = i; return }
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        W95.bevel(bounds, .window)
        for (i, item) in items.enumerated() {
            let r = rows[i]
            if item.isSeparator { W95.etchedLine(x: r.minX - 2 + 3, y: r.minY + 3, width: r.width - 2); continue }
            let hot = hover == i
            if hot { W95.fill(r, W95.navy) }
            let color = hot ? W95.white : W95.black
            let ty = r.minY + floor((r.height - W95.lineHeight) / 2)
            if checked[i] { W95.pixels(W95.check, at: NSPoint(x: r.minX + 6, y: r.minY + 5), color: enabled[i] ? color : W95.shadow) }
            if !enabled[i] {
                if hot { W95.label(item.title, at: NSPoint(x: r.minX + 18, y: ty), color: W95.shadow) }
                else { W95.label(item.title, at: NSPoint(x: r.minX + 18, y: ty), disabled: true) }
            } else {
                W95.label(item.title, at: NSPoint(x: r.minX + 18, y: ty), color: color)
            }
            if !item.shortcut.isEmpty {
                let p = NSPoint(x: keyX, y: ty)
                if enabled[i] || hot { W95.text(item.shortcut, at: p, color: enabled[i] ? color : W95.shadow) }
                else { W95.text(item.shortcut, at: NSPoint(x: p.x + 1, y: p.y + 1), color: W95.white); W95.text(item.shortcut, at: p, color: W95.shadow) }
            }
        }
    }
}

final class PopupPanel: NSPanel {
    /// Size on screen of a menu laid out in Windows 95 pixels
    static func size(of view: PopupView) -> NSSize {
        NSSize(width: view.frame.width * W95.scale, height: view.frame.height * W95.scale)
    }

    init(view: PopupView, topLeft: NSPoint) {
        let natural = view.frame.size, size = PopupPanel.size(of: view)
        super.init(contentRect: NSRect(x: topLeft.x, y: topLeft.y - size.height, width: size.width, height: size.height),
                   styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        defer { view.setBoundsSize(natural) }
        level = .popUpMenu
        hasShadow = false
        isOpaque = true
        isReleasedWhenClosed = false
        acceptsMouseMovedEvents = true
        appearance = NSAppearance(named: .aqua)
        contentView = view
    }
    override var canBecomeKey: Bool { false }
}

final class MenuBarView: FlippedView {
    struct Menu { let title: String; let items: () -> [MenuItem95] }
    var menus: [Menu] = []
    weak var tracker: MenuTracker?
    var highlighted: Int? { didSet { needsDisplay = true } }

    func titleRect(_ i: Int) -> NSRect {
        var x: CGFloat = 0
        for j in 0..<i { x += W95.width(W95.mnemonic(menus[j].title).0) + 14 }
        return NSRect(x: x, y: 1, width: W95.width(W95.mnemonic(menus[i].title).0) + 14, height: bounds.height - 2)
    }

    func index(at p: NSPoint) -> Int? { menus.indices.first { titleRect($0).contains(p) } }

    func index(atScreen p: NSPoint) -> Int? {
        guard let w = window else { return nil }
        return index(at: convert(w.convertPoint(fromScreen: p), from: nil))
    }

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        for i in menus.indices {
            let r = titleRect(i), hot = highlighted == i
            if hot { W95.fill(r, W95.navy) }
            W95.label(menus[i].title, at: NSPoint(x: r.minX + 7, y: r.minY + floor((r.height - W95.lineHeight) / 2)),
                      color: hot ? W95.white : W95.black)
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard let i = index(at: convert(event.locationInWindow, from: nil)), let tracker else { return }
        if tracker.openIndex == i { tracker.close() } else { tracker.openBar(i, keyboard: false) }
    }
}

/// Runs an open menu: while one is open, it sees every mouse and key event in the app first
final class MenuTracker {
    weak var window: NSWindow?
    weak var bar: MenuBarView?
    private(set) var openIndex: Int?
    private var popup: PopupPanel?
    private var monitor: Any?

    func openBar(_ i: Int, keyboard: Bool) {
        guard let bar, let w = bar.window else { return }
        let r = w.convertToScreen(bar.convert(bar.titleRect(i), to: nil))
        show(bar.menus[i].items(), topLeft: NSPoint(x: r.minX, y: r.minY), hoverFirst: keyboard)
        openIndex = i
        bar.highlighted = i
    }

    func openContext(_ items: [MenuItem95], at p: NSPoint) {
        show(items, topLeft: NSPoint(x: p.x + 1, y: p.y - 1), hoverFirst: false)
        openIndex = nil
        bar?.highlighted = nil
    }

    private func show(_ items: [MenuItem95], topLeft: NSPoint, hoverFirst: Bool) {
        closePopup()
        let v = PopupView(items: items)
        var tl = topLeft
        let size = PopupPanel.size(of: v)
        if let s = (window?.screen ?? NSScreen.main)?.visibleFrame {
            tl.x = max(s.minX, min(tl.x, s.maxX - size.width))
            tl.y = min(s.maxY, max(tl.y, s.minY + size.height))
        }
        let p = PopupPanel(view: v, topLeft: tl)
        if hoverFirst { v.move(1) }
        window?.addChildWindow(p, ordered: .above)
        p.orderFront(nil)
        popup = p
        if monitor == nil {
            monitor = NSEvent.addLocalMonitorForEvents(matching: [.mouseMoved, .leftMouseDown, .leftMouseUp, .leftMouseDragged,
                                                                  .rightMouseDown, .rightMouseUp, .rightMouseDragged,
                                                                  .keyDown, .scrollWheel]) { [weak self] e in
                guard let self else { return e }
                return self.handle(e)
            }
        }
    }

    func close() {
        closePopup()
        openIndex = nil
        bar?.highlighted = nil
        if let m = monitor { NSEvent.removeMonitor(m); monitor = nil }
    }

    private func closePopup() {
        guard let p = popup else { return }
        p.parent?.removeChildWindow(p)
        p.orderOut(nil)
        popup = nil
    }

    private func handle(_ e: NSEvent) -> NSEvent? {
        guard let popup, let view = popup.contentView as? PopupView else { return e }
        let p = NSEvent.mouseLocation
        let inPopup = popup.frame.contains(p)
        let local = view.convert(popup.convertPoint(fromScreen: p), from: nil)
        let barIndex = bar?.index(atScreen: p)
        switch e.type {
        case .mouseMoved, .leftMouseDragged, .rightMouseDragged:
            if inPopup { view.hover = view.index(at: local) }
            else if let i = barIndex, openIndex != nil, i != openIndex { openBar(i, keyboard: false) }
            else { view.hover = nil }
        case .leftMouseDown, .rightMouseDown:
            if inPopup { break }
            if let i = barIndex, openIndex != nil {
                if i == openIndex { close() } else { openBar(i, keyboard: false) }
            } else {
                close()   // a click outside only closes the menu, like Windows
            }
        case .leftMouseUp, .rightMouseUp:
            if inPopup, let i = view.index(at: local) { activate(view, i) }
        case .keyDown:
            if e.modifierFlags.contains(.command) { close(); return e }
            switch e.keyCode {
            case 53, 109: close()                         // Esc, F10
            case 126: view.move(-1)                       // up
            case 125: view.move(1)                        // down
            case 123, 124:                                // left, right
                if let o = openIndex, let bar {
                    let n = bar.menus.count
                    openBar((o + (e.keyCode == 123 ? n - 1 : 1)) % n, keyboard: true)
                }
            case 36, 76, 49: if let h = view.hover { activate(view, h) }
            default:
                if let k = e.charactersIgnoringModifiers?.lowercased(),
                   let i = view.items.indices.first(where: { view.enabled[$0] && W95.mnemonicKey(view.items[$0].title) == k }) {
                    activate(view, i)
                }
            }
        default: break
        }
        return nil
    }

    private func activate(_ view: PopupView, _ i: Int) {
        guard view.enabled[i] else { return }
        let item = view.items[i]
        close()
        DispatchQueue.main.async { item.run() }
    }
}

// MARK: - Scroll bars

final class Win95Scroller: NSScroller {
    override class var isCompatibleWithOverlayScrollers: Bool { false }
    override class func scrollerWidth(for controlSize: NSControl.ControlSize, scrollerStyle: NSScroller.Style) -> CGFloat { 16 }
    override var isFlipped: Bool { true }

    private enum Part { case dec, inc, pageDec, pageInc, thumb, none }
    private var pressed: Part = .none
    private var vertical: Bool { bounds.height >= bounds.width }
    private var length: CGFloat { vertical ? bounds.height : bounds.width }
    private var arrow: CGFloat { min(16, floor(length / 2)) }
    private var usable: Bool { isEnabled && knobProportion < 1 && length > 2 * arrow + 8 }

    private var thumb: (pos: CGFloat, len: CGFloat) {
        let track = length - 2 * arrow
        let len = max(8, floor(track * knobProportion))
        return (arrow + floor((track - len) * CGFloat(min(max(doubleValue, 0), 1))), len)
    }

    private func rect(_ start: CGFloat, _ len: CGFloat) -> NSRect {
        vertical ? NSRect(x: 0, y: start, width: bounds.width, height: len) : NSRect(x: start, y: 0, width: len, height: bounds.height)
    }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.dither.setFill()
        bounds.fill()
        drawArrow(rect(0, arrow), forward: false, down: pressed == .dec)
        drawArrow(rect(length - arrow, arrow), forward: true, down: pressed == .inc)
        if usable {
            let t = rect(thumb.pos, thumb.len)
            W95.fill(t, W95.face)
            W95.bevel(t, .button)
        }
    }

    private func drawArrow(_ r: NSRect, forward: Bool, down: Bool) {
        W95.fill(r, W95.face)
        if down { W95.edge(r, W95.shadow, W95.shadow) } else { W95.bevel(r, .button) }
        let off: CGFloat = down ? 1 : 0
        func glyph(_ dx: CGFloat, _ color: NSColor) {
            for i in 0..<4 {
                let k = CGFloat(forward ? 3 - i : i), w = CGFloat(2 * i + 1)
                let cross = vertical ? r.minX + floor((r.width - 7) / 2) + 3 - CGFloat(i)
                                     : r.minY + floor((r.height - 7) / 2) + 3 - CGFloat(i)
                let along = (vertical ? r.minY + floor((r.height - 4) / 2) : r.minX + floor((r.width - 4) / 2)) + k
                let px = vertical ? NSRect(x: cross + off + dx, y: along + off + dx, width: w, height: 1)
                                  : NSRect(x: along + off + dx, y: cross + off + dx, width: 1, height: w)
                W95.fill(px, color)
            }
        }
        if usable { glyph(0, W95.black) } else { glyph(1, W95.white); glyph(0, W95.shadow) }
    }

    private func part(at p: NSPoint) -> Part {
        let pos = vertical ? p.y : p.x
        if pos < arrow { return .dec }
        if pos >= length - arrow { return .inc }
        let t = thumb
        if pos < t.pos { return .pageDec }
        if pos >= t.pos + t.len { return .pageInc }
        return .thumb
    }

    private func scroll(by d: CGFloat? = nil, toFraction f: CGFloat? = nil) {
        guard let sv = enclosingScrollView, let doc = sv.documentView else { return }
        let clip = sv.contentView
        var o = clip.bounds.origin
        let maxPos = vertical ? max(0, doc.frame.height - clip.bounds.height) : max(0, doc.frame.width - clip.bounds.width)
        var pos = vertical ? o.y : o.x
        if let d { pos += d }
        if let f { pos = f * maxPos }
        pos = min(max(0, pos), maxPos)
        if vertical { o.y = pos } else { o.x = pos }
        clip.scroll(to: o)
        sv.reflectScrolledClipView(clip)
    }

    private func step(_ p: Part) {
        guard let sv = enclosingScrollView else { return }
        let line = vertical ? sv.verticalLineScroll : sv.horizontalLineScroll
        let page = max(line, (vertical ? sv.contentView.bounds.height : sv.contentView.bounds.width) - line)
        switch p {
        case .dec: scroll(by: -line)
        case .inc: scroll(by: line)
        case .pageDec: scroll(by: -page)
        case .pageInc: scroll(by: page)
        default: break
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard usable, let win = window else { return }
        let start = convert(event.locationInWindow, from: nil)
        let hit = part(at: start)
        if hit == .thumb {
            let v0 = CGFloat(doubleValue), travel = length - 2 * arrow - thumb.len
            while let e = win.nextEvent(matching: [.leftMouseDragged, .leftMouseUp]), e.type != .leftMouseUp {
                let p = convert(e.locationInWindow, from: nil)
                let d = vertical ? p.y - start.y : p.x - start.x
                scroll(toFraction: travel > 0 ? min(max(v0 + d / travel, 0), 1) : 0)
                win.displayIfNeeded()
            }
            return
        }
        pressed = hit
        step(hit)
        needsDisplay = true
        win.displayIfNeeded()
        NSEvent.startPeriodicEvents(afterDelay: 0.35, withPeriod: 0.05)
        defer { NSEvent.stopPeriodicEvents(); pressed = .none; needsDisplay = true }
        while let e = win.nextEvent(matching: [.leftMouseUp, .leftMouseDragged, .periodic]), e.type != .leftMouseUp {
            let now = part(at: convert(win.mouseLocationOutsideOfEventStream, from: nil))
            if e.type == .periodic && now == hit { step(hit) }
            pressed = now == hit ? hit : .none
            needsDisplay = true
            win.displayIfNeeded()
        }
    }
}

// MARK: - Dialog controls

class W95Control: FlippedView {
    var isEnabled = true { didSet { needsDisplay = true } }
    var focused: Bool { window?.isKeyWindow == true && window?.firstResponder === self }
    override var acceptsFirstResponder: Bool { isEnabled }
    override func becomeFirstResponder() -> Bool { superview?.subviews.forEach { $0.needsDisplay = true }; return true }
    override func resignFirstResponder() -> Bool { superview?.subviews.forEach { $0.needsDisplay = true }; return true }
    func activate() {}

    override func keyDown(with e: NSEvent) {
        switch e.keyCode {
        case 49: if isEnabled { activate() }
        case 48: e.modifierFlags.contains(.shift) ? window?.selectPreviousKeyView(nil) : window?.selectNextKeyView(nil)
        default: super.keyDown(with: e)
        }
    }

    /// Press feedback while the mouse is down; `released(inside:)` runs on mouse-up
    func track(_ event: NSEvent, pressed: (Bool) -> Void) -> Bool {
        guard isEnabled else { return false }
        window?.makeFirstResponder(self)
        pressed(true); display()
        while let e = window?.nextEvent(matching: [.leftMouseDragged, .leftMouseUp]) {
            let inside = bounds.contains(convert(e.locationInWindow, from: nil))
            pressed(e.type == .leftMouseUp ? false : inside); display()
            if e.type == .leftMouseUp { return inside }
        }
        return false
    }
}

final class W95Button: W95Control {
    let title: String
    var isDefault = false { didSet { needsDisplay = true } }
    var action: () -> Void
    private var down = false

    init(_ title: String, frame: NSRect, action: @escaping () -> Void) {
        self.title = title; self.action = action
        super.init(frame: frame)
    }
    required init?(coder: NSCoder) { fatalError() }

    override func activate() { action() }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        // The focused button wears the thick black border; otherwise the default button does
        let focusOnButton = window?.isKeyWindow == true && window?.firstResponder is W95Button
        var r = bounds
        if focused || (isDefault && !focusOnButton) { W95.edge(r, W95.black, W95.black); r = r.insetBy(dx: 1, dy: 1) }
        if down { W95.edge(r, W95.shadow, W95.shadow) } else { W95.bevel(r, .button) }
        let t = W95.mnemonic(title).0, off: CGFloat = down ? 1 : 0
        W95.label(title, at: NSPoint(x: floor((bounds.width - W95.width(t)) / 2) + off,
                                     y: floor((bounds.height - W95.lineHeight) / 2) + off), disabled: !isEnabled)
        if focused { W95.dotted(bounds.insetBy(dx: 4, dy: 4)) }
    }

    override func mouseDown(with event: NSEvent) {
        if track(event, pressed: { down = $0 }) { action() }
    }
}

final class W95Check: W95Control {
    let title: String
    var isOn: Bool { didSet { needsDisplay = true } }
    var onChange: (Bool) -> Void = { _ in }

    init(_ title: String, frame: NSRect, isOn: Bool) {
        self.title = title; self.isOn = isOn
        super.init(frame: frame)
    }
    required init?(coder: NSCoder) { fatalError() }

    override func activate() { isOn.toggle(); onChange(isOn) }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        let box = NSRect(x: 0, y: floor((bounds.height - 13) / 2), width: 13, height: 13)
        W95.fill(box, W95.white)
        W95.bevel(box, .sunken)
        if isOn { W95.pixels(W95.check, at: NSPoint(x: box.minX + 3, y: box.minY + 3)) }
        let ty = floor((bounds.height - W95.lineHeight) / 2)
        W95.label(title, at: NSPoint(x: 19, y: ty), disabled: !isEnabled)
        if focused { W95.dotted(NSRect(x: 17, y: ty - 1, width: W95.width(W95.mnemonic(title).0) + 4, height: W95.lineHeight + 2)) }
    }

    override func mouseDown(with event: NSEvent) {
        if track(event, pressed: { _ in }) { activate() }
    }
}

final class W95Radio: W95Control {
    let title: String
    var isOn: Bool { didSet { needsDisplay = true } }
    var group: [W95Radio] = []
    var onSelect: () -> Void = {}

    private static let ring = ["....dddd....", "..ddkkkkdd..", ".dkkwwwwllw.", ".dkwwwwwwlw.", "dkwwwwwwwwlw", "dkwwwwwwwwlw",
                               "dkwwwwwwwwlw", "dkwwwwwwwwlw", ".dkwwwwwwlw.", ".dllwwwwllw.", "..wwllllww..", "....wwww...."]
    private static let dot = [".kk.", "kkkk", "kkkk", ".kk."]

    init(_ title: String, frame: NSRect, isOn: Bool) {
        self.title = title; self.isOn = isOn
        super.init(frame: frame)
    }
    required init?(coder: NSCoder) { fatalError() }

    override func activate() {
        group.forEach { $0.isOn = $0 === self }
        onSelect()
    }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        let y = floor((bounds.height - 12) / 2)
        W95.pixels(W95Radio.ring, at: NSPoint(x: 0, y: y))
        if isOn { W95.pixels(W95Radio.dot, at: NSPoint(x: 4, y: y + 4)) }
        let ty = floor((bounds.height - W95.lineHeight) / 2)
        W95.label(title, at: NSPoint(x: 17, y: ty), disabled: !isEnabled)
        if focused { W95.dotted(NSRect(x: 15, y: ty - 1, width: W95.width(W95.mnemonic(title).0) + 4, height: W95.lineHeight + 2)) }
    }

    override func mouseDown(with event: NSEvent) {
        if track(event, pressed: { _ in }) { activate() }
    }
}

final class W95Label: FlippedView {
    let title: String
    init(_ title: String, frame: NSRect) { self.title = title; super.init(frame: frame) }
    required init?(coder: NSCoder) { fatalError() }
    override func draw(_ dirtyRect: NSRect) { W95.crisp(); W95.label(title, at: .zero) }
}

final class W95Group: FlippedView {
    let title: String
    init(_ title: String, frame: NSRect) { self.title = title; super.init(frame: frame) }
    required init?(coder: NSCoder) { fatalError() }
    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        let top = floor(W95.lineHeight / 2)
        let r = NSRect(x: 0, y: top, width: bounds.width - 1, height: bounds.height - top - 1)
        W95.edge(r, W95.shadow, W95.shadow)
        W95.edge(r.offsetBy(dx: 1, dy: 1), W95.white, W95.white)
        W95.fill(NSRect(x: r.minX + 1, y: r.minY + 1, width: r.width - 2, height: 1), W95.white)
        W95.fill(NSRect(x: 6, y: 0, width: W95.width(W95.mnemonic(title).0) + 4, height: W95.lineHeight), W95.face)
        W95.label(title, at: NSPoint(x: 8, y: 0))
    }
}

/// Sunken white box with a text field inside
final class W95Field: FlippedView {
    let field = NSTextField()
    override init(frame: NSRect) {
        super.init(frame: frame)
        field.frame = NSRect(x: 3, y: floor((frame.height - 17) / 2), width: frame.width - 6, height: 17)
        field.autoresizingMask = [.width]
        field.isBordered = false
        field.isBezeled = false
        field.drawsBackground = false
        field.focusRingType = .none
        field.font = W95.ui
        field.textColor = W95.black
        field.cell?.usesSingleLineMode = true
        field.cell?.isScrollable = true
        field.cell?.wraps = false
        addSubview(field)
    }
    required init?(coder: NSCoder) { fatalError() }
    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.white)
        W95.bevel(bounds, .sunken)
    }
}

final class TextBlock: FlippedView {
    let text: String
    init(_ text: String, frame: NSRect) { self.text = text; super.init(frame: frame) }
    required init?(coder: NSCoder) { fatalError() }
    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        (text as NSString).draw(in: bounds, withAttributes: [.font: W95.ui, .foregroundColor: W95.black])
    }
}

// MARK: - Message boxes

enum MBIcon { case warning, info, error, app }

final class IconView: FlippedView {
    let icon: MBIcon
    init(_ icon: MBIcon, frame: NSRect) { self.icon = icon; super.init(frame: frame) }
    required init?(coder: NSCoder) { fatalError() }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        func stroke(_ p: NSBezierPath, _ fill: NSColor) { fill.setFill(); p.fill(); W95.black.setStroke(); p.lineWidth = 1; p.stroke() }
        switch icon {
        case .warning:
            let p = NSBezierPath()
            p.move(to: NSPoint(x: 16, y: 1.5)); p.line(to: NSPoint(x: 30.5, y: 29.5)); p.line(to: NSPoint(x: 1.5, y: 29.5)); p.close()
            stroke(p, W95.yellow)
            W95.fill(NSRect(x: 14, y: 10, width: 4, height: 11), W95.black)
            W95.fill(NSRect(x: 14, y: 23, width: 4, height: 4), W95.black)
        case .info:
            stroke(NSBezierPath(ovalIn: NSRect(x: 1.5, y: 1.5, width: 29, height: 29)), W95.white)
            W95.fill(NSRect(x: 14, y: 6, width: 4, height: 4), W95.navy)
            W95.fill(NSRect(x: 14, y: 12, width: 4, height: 12), W95.navy)
            W95.fill(NSRect(x: 12, y: 12, width: 2, height: 2), W95.navy)
            W95.fill(NSRect(x: 11, y: 24, width: 10, height: 2), W95.navy)
        case .error:
            stroke(NSBezierPath(ovalIn: NSRect(x: 1.5, y: 1.5, width: 29, height: 29)), W95.red)
            let x = NSBezierPath()
            x.move(to: NSPoint(x: 10, y: 10)); x.line(to: NSPoint(x: 22, y: 22))
            x.move(to: NSPoint(x: 22, y: 10)); x.line(to: NSPoint(x: 10, y: 22))
            x.lineWidth = 3; W95.white.setStroke(); x.stroke()
        case .app:
            W95.pixels(W95.appIcon, at: .zero, scale: 2)
        }
    }
}

/// A modal Windows 95 message box. Returns the index of the button pressed.
@discardableResult
func messageBox(_ text: String, title: String = W95.appName, icon: MBIcon, buttons: [String] = ["OK"],
                defaultIndex: Int = 0, cancelIndex: Int? = nil, over parent: NSWindow?) -> Int {
    let maxW: CGFloat = 330
    let size = (text as NSString).boundingRect(with: NSSize(width: maxW, height: 4000), options: [.usesLineFragmentOrigin],
                                               attributes: [.font: W95.ui]).size
    let tw = ceil(size.width) + 2, th = ceil(size.height)
    let bw: CGFloat = 75, bh: CGFloat = 23, gap: CGFloat = 6
    let rowW = bw * CGFloat(buttons.count) + gap * CGFloat(buttons.count - 1)
    let textX: CGFloat = 12 + 32 + 14
    let clientW = max(textX + tw + 16, rowW + 24, 200)
    let textY = 14 + max(0, floor((32 - th) / 2))
    let buttonY = 14 + max(32, th + textY - 14) + 16
    let client = NSSize(width: clientW, height: buttonY + bh + 10)

    let wsize = ChromeView.windowSize(forClient: client)
    let win = Win95Window(contentRect: NSRect(origin: .zero, size: wsize), minimizable: false)
    win.acceptsMain = false
    win.isExcludedFromWindowsMenu = true
    win.level = .modalPanel
    let chrome = ChromeView(frame: NSRect(origin: .zero, size: wsize), title: title, kinds: [.close], resizable: false, showIcon: false)
    win.contentView = chrome
    func stop(_ i: Int) { NSApp.stopModal(withCode: NSApplication.ModalResponse(rawValue: 1000 + i)) }
    let cancel = cancelIndex ?? (buttons.count == 1 ? 0 : nil)
    chrome.titleBar.onButton = { _ in stop(cancel ?? buttons.count - 1) }
    chrome.client.addSubview(IconView(icon, frame: NSRect(x: 12, y: 14, width: 32, height: 32)))
    chrome.client.addSubview(TextBlock(text, frame: NSRect(x: textX, y: textY, width: tw, height: th)))
    var views: [W95Button] = []
    for (i, title) in buttons.enumerated() {
        let b = W95Button(title, frame: NSRect(x: floor((clientW - rowW) / 2) + (bw + gap) * CGFloat(i), y: buttonY, width: bw, height: bh)) { stop(i) }
        b.isDefault = i == defaultIndex
        chrome.client.addSubview(b)
        views.append(b)
    }
    win.onEnter = { stop((win.firstResponder as? W95Button).flatMap { b in views.firstIndex { $0 === b } } ?? defaultIndex) }
    win.onEscape = { if let c = cancel { stop(c) } }
    win.onKey = { k in
        guard let i = buttons.indices.first(where: { W95.mnemonicKey(buttons[$0]) == k }) else { return false }
        stop(i); return true
    }
    if let p = parent, p.isVisible {
        win.setFrameOrigin(NSPoint(x: round(p.frame.midX - wsize.width / 2), y: round(p.frame.midY - wsize.height / 2 + 40)))
    } else {
        win.center()
    }
    win.makeKeyAndOrderFront(nil)
    win.makeFirstResponder(views[defaultIndex])
    NSSound.beep()
    let r = NSApp.runModal(for: win).rawValue - 1000
    win.orderOut(nil)
    parent?.makeKeyAndOrderFront(nil)
    return r
}

// MARK: - App icon (written by each app's build.sh with --write-iconset)

enum W95Icon {
    /// 16 × 16 pixel art, magnified on a teal Windows 95 desktop square
    static func draw(_ art: [String], size s: CGFloat) {
        guard let cg = NSGraphicsContext.current?.cgContext else { return }
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        W95.teal.setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()
        let scale = max(1, floor(s * 0.78 / 16)), o = floor((s - 16 * scale) / 2)
        cg.saveGState()
        cg.translateBy(x: 0, y: s)
        cg.scaleBy(x: 1, y: -1)   // pixel rows run top to bottom
        cg.setShouldAntialias(false)
        W95.pixels(art, at: NSPoint(x: o, y: o + scale / 2), scale: scale)
        cg.restoreGState()
    }

    static func png(_ art: [String], size: Int) -> Data? {
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: size, pixelsHigh: size,
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0) else { return nil }
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
        draw(art, size: CGFloat(size))
        NSGraphicsContext.restoreGraphicsState()
        return rep.representation(using: .png, properties: [:])
    }

    static func writeIconset(_ art: [String], to dir: String) throws {
        try FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
        for base in [16, 32, 128, 256, 512] {
            for scale in [1, 2] {
                let name = scale == 1 ? "icon_\(base)x\(base).png" : "icon_\(base)x\(base)@2x.png"
                guard let data = png(art, size: base * scale) else { continue }
                try data.write(to: URL(fileURLWithPath: dir).appendingPathComponent(name))
            }
        }
    }
}
