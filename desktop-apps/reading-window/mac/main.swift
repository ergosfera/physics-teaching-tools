// Reading Window for macOS: shades the whole screen except a clear window that follows the pointer,
// to help focus while reading (twin of ../windows/Program.cs). Single-file AppKit app; build.sh makes
// Reading Window.app and a .dmg.
import AppKit

// MARK: - Dark Windows 95 skin (same tokens as the other Ergosfera apps)

enum Skin {
    static func rgb(_ r: Int, _ g: Int, _ b: Int, _ a: CGFloat = 1) -> NSColor {
        NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: a)
    }
    static func hex(_ s: String) -> NSColor? {
        guard s.count == 6, let v = Int(s, radix: 16) else { return nil }
        return rgb(v >> 16 & 255, v >> 8 & 255, v & 255)
    }
    static func hexString(_ c: NSColor) -> String {
        let s = c.usingColorSpace(.sRGB) ?? c
        func b(_ x: CGFloat) -> Int { max(0, min(255, Int((x * 255).rounded()))) }
        return String(format: "%02x%02x%02x", b(s.redComponent), b(s.greenComponent), b(s.blueComponent))
    }
    static let face = rgb(0x3b, 0x3b, 0x3b)
    static let hi = rgb(0x6e, 0x6e, 0x6e)
    static let lt = rgb(0x54, 0x54, 0x54)
    static let sh = rgb(0x1c, 0x1c, 0x1c)
    static let dk = NSColor.black
    static let text = rgb(0xe8, 0xe8, 0xe8)
    static let muted = rgb(0x9a, 0x9a, 0x9a)
    static let lcd = rgb(0x7f, 0xae, 0x86)
    static let lcdDigit = rgb(0x16, 0x32, 0x1c)
    static let accent = rgb(0x16, 0x3a, 0x70)

    // Shade colors: dark and calm, so the clear window stands out
    static let palette: [(String, String)] = [
        ("Black", "000000"), ("Charcoal", "2b2b2b"), ("Navy", "0b1f3a"), ("Deep blue", "1e3a8a"),
        ("Dark green", "0f3d2e"), ("Sepia", "3b2a1a"), ("Plum", "3b1d3f"), ("Warm gray", "57534e"),
    ]

    static func font(_ name: String, _ size: CGFloat, bold: Bool = false) -> NSFont {
        var f = NSFont(name: name, size: size) ?? NSFont.systemFont(ofSize: size)
        if bold { f = NSFontManager.shared.convert(f, toHaveTrait: .boldFontMask) }
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

    static func draw(_ s: String, _ font: NSFont, _ color: NSColor, in r: NSRect, left: Bool = false, right: Bool = false) {
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color]
        let sz = (s as NSString).size(withAttributes: attrs)
        let x = left ? r.minX : right ? r.maxX - sz.width : r.midX - sz.width / 2
        (s as NSString).draw(at: NSPoint(x: x, y: r.midY - sz.height / 2), withAttributes: attrs)
    }
}

// MARK: - The clear window's shape and geometry (same rules as the Windows app; checked by --selftest)

enum HoleShape: String, CaseIterable {
    case rect, rounded, ellipse, band

    var title: String {
        switch self {
        case .rect: return "Rect"
        case .rounded: return "Round"
        case .ellipse: return "Ellipse"
        case .band: return "Band"
        }
    }
}

enum Geometry {
    static let minW: CGFloat = 60, maxW: CGFloat = 3000, minH: CGFloat = 16, maxH: CGFloat = 2000
    static let grip: CGFloat = 10      // how far outside the window's edge you can grab it to resize

    // The clear window's bounding box; a band spans the whole width of its screen
    static func hole(center c: NSPoint, width w: CGFloat, height h: CGFloat, shape: HoleShape, screen: NSRect) -> NSRect {
        if shape == .band { return NSRect(x: screen.minX, y: c.y - h / 2, width: screen.width, height: h) }
        return NSRect(x: c.x - w / 2, y: c.y - h / 2, width: w, height: h)
    }

    static func cornerRadius(_ r: NSRect) -> CGFloat { min(24, min(r.width, r.height) / 4) }

    static func path(_ r: NSRect, _ shape: HoleShape) -> NSBezierPath {
        switch shape {
        case .rect, .band: return NSBezierPath(rect: r)
        case .rounded: let k = cornerRadius(r); return NSBezierPath(roundedRect: r, xRadius: k, yRadius: k)
        case .ellipse: return NSBezierPath(ovalIn: r)
        }
    }

    // Which edges of the box p is just outside of (or on): -1 left/bottom, +1 right/top, 0 neither.
    // nil if p isn't near the box at all, or is inside it.
    static func edge(_ p: NSPoint, _ r: NSRect, band: Bool) -> (Int, Int)? {
        let g = grip
        guard r.insetBy(dx: -g, dy: -g).contains(p), !r.insetBy(dx: 2, dy: 2).contains(p) else { return nil }
        var dx = 0, dy = 0
        if !band {
            if p.x < r.minX + 2 { dx = -1 } else if p.x > r.maxX - 2 { dx = 1 }
        }
        if p.y < r.minY + 2 { dy = -1 } else if p.y > r.maxY - 2 { dy = 1 }
        return dx == 0 && dy == 0 ? nil : (dx, dy)
    }

    static func clampW(_ w: CGFloat) -> CGFloat { min(maxW, max(minW, w)) }
    static func clampH(_ h: CGFloat) -> CGFloat { min(maxH, max(minH, h)) }
    static func percent(_ v: Double) -> String { "\(Int((v * 100).rounded()))%" }

    static func selfTest() -> Int {
        var fails = 0
        func check(_ ok: Bool, _ what: String) { if !ok { fails += 1; print("FAIL: \(what)") } }
        let screen = NSRect(x: 0, y: 0, width: 1000, height: 800)
        check(hole(center: NSPoint(x: 500, y: 400), width: 300, height: 100, shape: .rect, screen: screen) ==
              NSRect(x: 350, y: 350, width: 300, height: 100), "hole rect")
        check(hole(center: NSPoint(x: 500, y: 400), width: 300, height: 100, shape: .band, screen: screen) ==
              NSRect(x: 0, y: 350, width: 1000, height: 100), "hole band")
        check(cornerRadius(NSRect(x: 0, y: 0, width: 300, height: 100)) == 24, "corner big")
        check(cornerRadius(NSRect(x: 0, y: 0, width: 300, height: 40)) == 10, "corner small")
        let r = NSRect(x: 100, y: 100, width: 200, height: 100)
        check(edge(NSPoint(x: 95, y: 150), r, band: false).map { $0 == (-1, 0) } ?? false, "edge left")
        check(edge(NSPoint(x: 305, y: 205), r, band: false).map { $0 == (1, 1) } ?? false, "edge corner")
        check(edge(NSPoint(x: 305, y: 150), r, band: true) == nil, "band has no side edges")
        check(edge(NSPoint(x: 200, y: 150), r, band: false) == nil, "inside")
        check(edge(NSPoint(x: 50, y: 150), r, band: false) == nil, "far")
        check(clampW(10) == minW && clampW(5000) == maxW && clampH(100) == 100, "clamp")
        check(percent(0.6) == "60%" && percent(0.955) == "96%", "percent")
        return fails
    }
}

// MARK: - Settings

final class Settings {
    private let d = UserDefaults.standard

    init() {
        d.register(defaults: ["width": 640.0, "height": 120.0, "shape": "rounded", "darkness": 0.6,
                              "color": "000000", "enabled": true])
    }

    var width: CGFloat { get { Geometry.clampW(CGFloat(d.double(forKey: "width"))) } set { d.set(Double(newValue), forKey: "width") } }
    var height: CGFloat { get { Geometry.clampH(CGFloat(d.double(forKey: "height"))) } set { d.set(Double(newValue), forKey: "height") } }
    var shape: HoleShape {
        get { HoleShape(rawValue: d.string(forKey: "shape") ?? "") ?? .rounded }
        set { d.set(newValue.rawValue, forKey: "shape") }
    }
    var darkness: Double { get { min(0.95, max(0.1, d.double(forKey: "darkness"))) } set { d.set(newValue, forKey: "darkness") } }
    var color: NSColor {
        get { Skin.hex(d.string(forKey: "color") ?? "") ?? .black }
        set { d.set(Skin.hexString(newValue), forKey: "color") }
    }
    var enabled: Bool { get { d.bool(forKey: "enabled") } set { d.set(newValue, forKey: "enabled") } }
}

// MARK: - The shade: one see-through window per screen, dark everywhere but the clear window

final class ShadeWindow: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
}

final class ShadeView: NSView {
    weak var app: AppDelegate!
    var resizing: (Int, Int)?
    var downAt = NSPoint.zero
    var tracking: NSTrackingArea?

    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func updateTrackingAreas() {
        if let t = tracking { removeTrackingArea(t) }
        tracking = NSTrackingArea(rect: bounds, options: [.mouseMoved, .activeAlways, .inVisibleRect], owner: self)
        addTrackingArea(tracking!)
        super.updateTrackingAreas()
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let w = window else { return }
        // Start from clear: the shade is see-through, so painting over the old shade would darken it
        NSColor.clear.set()
        dirtyRect.fill(using: .copy)
        let p = NSBezierPath(rect: bounds)
        let hole = app.hole.offsetBy(dx: -w.frame.minX, dy: -w.frame.minY)
        p.append(Geometry.path(hole, app.cfg.shape))
        p.windingRule = .evenOdd
        app.cfg.color.withAlphaComponent(CGFloat(app.cfg.darkness)).setFill()
        p.fill()
    }

    func cursor(for e: (Int, Int)?) -> NSCursor {
        guard let e = e else { return .pointingHand }
        if e.0 != 0 && e.1 == 0 { return .resizeLeftRight }
        if e.0 == 0 { return .resizeUpDown }
        return .crosshair
    }

    override func mouseMoved(with e: NSEvent) {
        cursor(for: Geometry.edge(NSEvent.mouseLocation, app.hole, band: app.cfg.shape == .band)).set()
    }

    // While held: drag just outside the clear window's edge to resize it; click anywhere else in the shade to grab it
    override func mouseDown(with e: NSEvent) {
        let g = NSEvent.mouseLocation
        downAt = g
        resizing = Geometry.edge(g, app.hole, band: app.cfg.shape == .band)
        if resizing == nil { app.grab() }
    }

    override func mouseDragged(with e: NSEvent) {
        guard let (dx, dy) = resizing else { return }
        let g = NSEvent.mouseLocation, c = app.center
        if dx != 0 { app.cfg.width = Geometry.clampW(2 * abs(g.x - c.x)) }
        if dy != 0 { app.cfg.height = Geometry.clampH(2 * abs(g.y - c.y)) }
        app.holeChanged()
        app.panel.needsDisplay = true
    }

    override func mouseUp(with e: NSEvent) { resizing = nil }

    override func scrollWheel(with e: NSEvent) {}      // the shade doesn't scroll; the page under the clear window does
}

// MARK: - The panel

final class PanelView: NSView {
    enum Hit: Equatable { case none, onOff, about, shape(HoleShape), color, slider(Int) }

    weak var app: AppDelegate!
    var pressed: Hit = .none
    static let width: CGFloat = 380, height: CGFloat = 228

    let fButton = Skin.font("Tahoma", 12, bold: true)
    let fLabel = Skin.font("Tahoma", 12, bold: true)
    let fStatus = Skin.font("Tahoma", 11.5, bold: true)

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    var rcOnOff: NSRect { NSRect(x: 8, y: 8, width: 104, height: 26) }
    var rcAbout: NSRect { NSRect(x: bounds.width - 34, y: 8, width: 26, height: 26) }
    var rcStatus: NSRect { NSRect(x: 118, y: 8, width: bounds.width - 118 - 40, height: 26) }
    func rcShape(_ i: Int) -> NSRect { NSRect(x: 8 + CGFloat(i) * 92, y: 42, width: 86, height: 26) }
    func rcSliderRow(_ i: Int) -> NSRect { NSRect(x: 8, y: 78 + CGFloat(i) * 34, width: bounds.width - 16, height: 28) }
    func rcTrack(_ i: Int) -> NSRect { let r = rcSliderRow(i); return NSRect(x: r.minX + 88, y: r.midY - 4, width: r.width - 88 - 70, height: 8) }
    var rcColor: NSRect { NSRect(x: 8, y: 184, width: 160, height: 26) }

    // Sliders: 0 darkness, 1 width, 2 height
    func sliderValue(_ i: Int) -> Double {
        switch i {
        case 0: return (app.cfg.darkness - 0.1) / 0.85
        case 1: return Double((app.cfg.width - Geometry.minW) / (Geometry.maxW - Geometry.minW))
        default: return Double((app.cfg.height - Geometry.minH) / (Geometry.maxH - Geometry.minH))
        }
    }

    func setSlider(_ i: Int, _ f: Double) {
        let f = min(1, max(0, f))
        switch i {
        case 0: app.cfg.darkness = ((0.1 + 0.85 * f) * 100).rounded() / 100
        case 1: app.cfg.width = Geometry.clampW((Geometry.minW + CGFloat(f) * (Geometry.maxW - Geometry.minW)).rounded())
        default: app.cfg.height = Geometry.clampH((Geometry.minH + CGFloat(f) * (Geometry.maxH - Geometry.minH)).rounded())
        }
        if i == 0 { app.redrawAll() } else { app.holeChanged() }
    }

    func sliderText(_ i: Int) -> String {
        switch i {
        case 0: return Geometry.percent(app.cfg.darkness)
        case 1: return app.cfg.shape == .band ? "full" : "\(Int(app.cfg.width)) pt"
        default: return "\(Int(app.cfg.height)) pt"
        }
    }

    func hit(_ p: NSPoint) -> Hit {
        if rcOnOff.contains(p) { return .onOff }
        if rcAbout.contains(p) { return .about }
        for (i, s) in HoleShape.allCases.enumerated() where rcShape(i).contains(p) { return .shape(s) }
        for i in 0..<3 where rcSliderRow(i).insetBy(dx: 0, dy: 2).contains(p) && p.x >= rcTrack(i).minX - 8 && p.x <= rcTrack(i).maxX + 8 {
            return i == 1 && app.cfg.shape == .band ? .none : .slider(i)
        }
        if rcColor.contains(p) { return .color }
        return .none
    }

    override func mouseDown(with e: NSEvent) {
        let p = convert(e.locationInWindow, from: nil)
        pressed = hit(p)
        if case .slider(let i) = pressed { setSlider(i, Double((p.x - rcTrack(i).minX) / rcTrack(i).width)) }
        if pressed == .color {
            app.colorMenu().popUp(positioning: nil, at: NSPoint(x: rcColor.minX, y: rcColor.maxY + 2), in: self)
            pressed = .none
        }
        needsDisplay = true
    }

    override func mouseDragged(with e: NSEvent) {
        guard case .slider(let i) = pressed else { return }
        let p = convert(e.locationInWindow, from: nil)
        setSlider(i, Double((p.x - rcTrack(i).minX) / rcTrack(i).width))
        needsDisplay = true
    }

    override func mouseUp(with e: NSEvent) {
        let h = hit(convert(e.locationInWindow, from: nil)), was = pressed
        pressed = .none
        needsDisplay = true
        guard h == was else { return }
        switch h {
        case .onOff: app.toggleShade()
        case .about: app.showAbout()
        case .shape(let s): app.cfg.shape = s; app.holeChanged()
        default: break
        }
    }

    override func scrollWheel(with e: NSEvent) {
        // Scroll over a slider to fine-tune it
        let p = convert(e.locationInWindow, from: nil)
        guard case .slider(let i) = hit(p) else { return }
        let step = i == 0 ? 0.01 / 0.85 : 1 / Double(i == 1 ? Geometry.maxW - Geometry.minW : Geometry.maxH - Geometry.minH) * 10
        setSlider(i, sliderValue(i) + (e.scrollingDeltaY > 0 ? step : -step))
        needsDisplay = true
    }

    func button(_ r: NSRect, down: Bool, _ label: String, enabled: Bool = true, left: Bool = false) {
        Skin.face.setFill()
        r.fill()
        if down { Skin.sunken(r) } else { Skin.raised(r) }
        let t = down ? r.offsetBy(dx: 1, dy: 1) : r
        Skin.draw(label, fButton, enabled ? Skin.text : Skin.muted, in: left ? t.insetBy(dx: 8, dy: 0) : t, left: left)
    }

    override func draw(_ dirtyRect: NSRect) {
        Skin.face.setFill()
        bounds.fill()
        button(rcOnOff, down: app.cfg.enabled || pressed == .onOff, app.cfg.enabled ? "Shade: On" : "Shade: Off")
        button(rcAbout, down: pressed == .about, "?")

        let st = rcStatus.insetBy(dx: 2, dy: 2)
        Skin.lcd.setFill(); st.fill(); Skin.sunken(st)
        let status = !app.cfg.enabled ? "Shade is off" : app.following ? "Following · click to hold" : "Held · click the shade to grab"
        Skin.draw(status, fStatus, Skin.lcdDigit, in: st.insetBy(dx: 6, dy: 0), left: true)

        for (i, s) in HoleShape.allCases.enumerated() {
            button(rcShape(i), down: app.cfg.shape == s || pressed == .shape(s), s.title)
        }

        for (i, name) in ["Darkness", "Width", "Height"].enumerated() {
            let row = rcSliderRow(i)
            let on = !(i == 1 && app.cfg.shape == .band)
            Skin.draw(name, fLabel, on ? Skin.text : Skin.muted, in: NSRect(x: row.minX, y: row.minY, width: 84, height: row.height), left: true)
            let t = rcTrack(i)
            Skin.sh.setFill(); t.fill(); Skin.sunken(t)
            if on {
                let x = t.minX + CGFloat(sliderValue(i)) * t.width
                let fillR = NSRect(x: t.minX + 2, y: t.minY + 2, width: max(0, x - t.minX - 2), height: t.height - 4)
                Skin.accent.setFill(); fillR.fill()
                let thumb = NSRect(x: x - 6, y: row.midY - 10, width: 12, height: 20)
                Skin.face.setFill(); thumb.fill(); Skin.raised(thumb)
            }
            Skin.draw(sliderText(i), fLabel, on ? Skin.text : Skin.muted,
                      in: NSRect(x: t.maxX + 8, y: row.minY, width: row.maxX - t.maxX - 8, height: row.height), right: true)
        }

        button(rcColor, down: pressed == .color, "Color", left: true)
        let sw = NSRect(x: rcColor.maxX - 44, y: rcColor.midY - 7, width: 22, height: 14)
        app.cfg.color.setFill(); sw.fill()
        Skin.text.setStroke(); NSBezierPath(rect: sw.insetBy(dx: 0.5, dy: 0.5)).stroke()
        Skin.draw("▾", fButton, Skin.text, in: NSRect(x: rcColor.maxX - 18, y: rcColor.minY, width: 12, height: rcColor.height))
        Skin.draw("Drag just outside its edge to resize while held", Skin.font("Tahoma", 10.5), Skin.muted,
                  in: NSRect(x: rcColor.maxX + 10, y: rcColor.minY, width: bounds.width - rcColor.maxX - 18, height: rcColor.height), left: true)
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

// MARK: - App icon (used by build.sh: ReadingWindow --write-iconset <dir>)

enum IconArt {
    // Lines of text, all shaded but the ones in a clear window
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        Skin.rgb(0xf4, 0xf1, 0xea).setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()
        let lineH = s * 0.045
        Skin.rgb(0x55, 0x55, 0x5c).setFill()
        for i in 0..<6 {
            let y = body.maxY - s * 0.17 - CGFloat(i) * s * 0.11
            let w = body.width * (i % 3 == 2 ? 0.5 : 0.72)
            NSBezierPath(roundedRect: NSRect(x: body.minX + s * 0.1, y: y, width: w, height: lineH), xRadius: lineH / 2, yRadius: lineH / 2).fill()
        }
        let hole = NSRect(x: body.minX + s * 0.05, y: body.midY - s * 0.09, width: body.width - s * 0.1, height: s * 0.17)
        let shade = NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225)
        shade.append(NSBezierPath(roundedRect: hole, xRadius: s * 0.03, yRadius: s * 0.03))
        shade.windingRule = .evenOdd
        Skin.rgb(0x0b, 0x1f, 0x3a, 0.78).setFill()
        shade.fill()
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
    let cfg = Settings()
    var shades: [ShadeWindow] = []
    var panelWindow: NSWindow!
    var panel: PanelView!
    var poll: Timer?
    var center = NSPoint.zero
    var following = true
    var clickMonitor: Any?
    var lastHole = NSRect.zero

    var hole: NSRect {
        let s = NSScreen.screens.first { $0.frame.contains(center) } ?? NSScreen.main ?? NSScreen.screens[0]
        return Geometry.hole(center: center, width: cfg.width, height: cfg.height, shape: cfg.shape, screen: s.frame)
    }

    func applicationDidFinishLaunching(_ note: Notification) {
        panel = PanelView(frame: NSRect(x: 0, y: 0, width: PanelView.width, height: PanelView.height))
        panel.app = self
        panelWindow = NSWindow(contentRect: panel.frame, styleMask: [.titled, .closable, .miniaturizable],
                               backing: .buffered, defer: false)
        panelWindow.title = "Reading Window"
        panelWindow.contentView = panel
        panelWindow.delegate = self
        panelWindow.isReleasedWhenClosed = false
        panelWindow.level = NSWindow.Level(rawValue: NSWindow.Level.statusBar.rawValue + 1)     // above the shade
        panelWindow.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        buildMenu()
        if !panelWindow.setFrameUsingName("ReadingWindowPanel") {
            let v = NSScreen.main?.visibleFrame ?? .zero
            panelWindow.setFrameTopLeftPoint(NSPoint(x: v.maxX - PanelView.width - 40, y: v.maxY - 40))
        }
        panelWindow.setFrameAutosaveName("ReadingWindowPanel")

        center = NSEvent.mouseLocation
        buildShades()
        NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification, object: nil,
                                               queue: .main) { [weak self] _ in self?.buildShades() }
        panelWindow.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)

        poll = Timer.scheduledTimer(withTimeInterval: 1.0 / 60, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(poll!, forMode: .common)

        // While following, the shade lets every click through to the page; a click in another app holds the window.
        // (Clicks in this app's own panel don't, and need no permission to watch.)
        clickMonitor = NSEvent.addGlobalMonitorForEvents(matching: [.leftMouseDown]) { [weak self] _ in self?.hold() }
    }

    // One shade window over each screen, above everything but the panel (and menus)
    func buildShades() {
        for w in shades { w.orderOut(nil); w.close() }
        shades = NSScreen.screens.map { s in
            let w = ShadeWindow(contentRect: s.frame, styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
            w.isOpaque = false
            w.backgroundColor = .clear
            w.hasShadow = false
            w.level = .statusBar
            w.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
            w.isReleasedWhenClosed = false
            w.ignoresMouseEvents = true
            w.hidesOnDeactivate = false
            let v = ShadeView(frame: NSRect(origin: .zero, size: s.frame.size))
            v.app = self
            w.contentView = v
            w.setFrame(s.frame, display: false)
            return w
        }
        updateVisibility()
    }

    func updateVisibility() {
        for w in shades { if cfg.enabled { w.orderFrontRegardless() } else { w.orderOut(nil) } }
        holeChanged(force: true)
    }

    var mouseOverPanel: Bool { panelWindow.isVisible && panelWindow.frame.contains(NSEvent.mouseLocation) }

    // A click holds the window where it is (the click also reaches the page below)
    func hold() {
        guard cfg.enabled, following, !mouseOverPanel else { return }
        following = false
        center = NSEvent.mouseLocation
        holeChanged()
    }

    func tick() {
        guard cfg.enabled else { return }
        let g = NSEvent.mouseLocation
        if following {
            // Follow the pointer, but not onto the panel, so its controls can be used
            if !mouseOverPanel && g != center { center = g; holeChanged() }
            for w in shades where !w.ignoresMouseEvents { w.ignoresMouseEvents = true }
        } else {
            // Held: the shade catches clicks (to grab or resize); the clear window lets them through
            let inHole = Geometry.path(hole, cfg.shape).contains(g) && Geometry.edge(g, hole, band: cfg.shape == .band) == nil
            for w in shades {
                let dragging = (w.contentView as? ShadeView)?.resizing != nil
                let catchIt = dragging || !inHole
                if w.ignoresMouseEvents == catchIt { w.ignoresMouseEvents = !catchIt }
            }
        }
    }

    func grab() {
        following = true
        center = NSEvent.mouseLocation
        for w in shades { w.ignoresMouseEvents = true }
        holeChanged()
        panel.needsDisplay = true
    }

    // Redraw only what changed: the old and the new clear window
    func holeChanged(force: Bool = false) {
        let h = hole
        for w in shades {
            guard let v = w.contentView else { continue }
            if force {
                v.needsDisplay = true
            } else {
                let dirty = lastHole.union(h).insetBy(dx: -4, dy: -4).offsetBy(dx: -w.frame.minX, dy: -w.frame.minY)
                if dirty.intersects(v.bounds) { v.setNeedsDisplay(dirty) }
            }
        }
        lastHole = h
        panel.needsDisplay = true
    }

    @objc func toggleShade() {
        cfg.enabled.toggle()
        following = true
        updateVisibility()
    }

    func redrawAll() { for w in shades { w.contentView?.needsDisplay = true }; panel.needsDisplay = true }

    func colorMenu() -> NSMenu {
        let m = NSMenu()
        let current = Skin.hexString(cfg.color)
        for (title, hex) in Skin.palette {
            let it = m.addItem(withTitle: title, action: #selector(pickColor(_:)), keyEquivalent: "")
            it.target = self
            it.representedObject = hex
            it.state = hex == current ? .on : .off
            it.image = NSImage(size: NSSize(width: 12, height: 12), flipped: false) { r in
                Skin.hex(hex)!.setFill(); r.fill()
                NSColor(white: 0.6, alpha: 1).setStroke(); NSBezierPath(rect: r.insetBy(dx: 0.5, dy: 0.5)).stroke()
                return true
            }
        }
        m.addItem(.separator())
        m.addItem(withTitle: "Other Color…", action: #selector(otherColor), keyEquivalent: "").target = self
        return m
    }

    @objc func pickColor(_ sender: NSMenuItem) {
        guard let hex = sender.representedObject as? String, let c = Skin.hex(hex) else { return }
        cfg.color = c
        redrawAll()
    }

    @objc func otherColor() {
        let p = NSColorPanel.shared
        p.showsAlpha = false
        p.color = cfg.color
        p.setTarget(self)
        p.setAction(#selector(colorPanelChanged(_:)))
        p.level = NSWindow.Level(rawValue: NSWindow.Level.statusBar.rawValue + 2)
        NSApp.activate(ignoringOtherApps: true)
        p.orderFront(nil)
    }

    @objc func colorPanelChanged(_ sender: NSColorPanel) {
        cfg.color = sender.color.usingColorSpace(.sRGB) ?? sender.color
        redrawAll()
    }

    @objc func setShape(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String, let s = HoleShape(rawValue: raw) else { return }
        cfg.shape = s
        holeChanged(force: true)
    }

    @objc func showPanel() { panelWindow.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }

    @objc func showAbout() {
        let credits = NSMutableAttributedString(
            string: "Free software, provided \"as is\", without warranty of any kind.\n" +
                    "This app collects no personal data and never connects to the internet. " +
                    "It doesn't capture or record the screen.\n\n",
            attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.labelColor])
        credits.append(NSAttributedString(string: License.text,
            attributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]))
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Reading Window",
            .applicationVersion: "1.0",
            .version: "",
            .credits: credits,
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): "© 2026 Mauricio Mendivelso · MIT License",
        ])
        // The About window must show above the shade
        for w in NSApp.windows where w.className.contains("About") { w.level = NSWindow.Level(rawValue: NSWindow.Level.statusBar.rawValue + 2) }
    }

    func buildMenu() {
        let main = NSMenu()

        let appItem = NSMenuItem()
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About Reading Window", action: #selector(showAbout), keyEquivalent: "").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Hide Reading Window", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Quit Reading Window", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu
        main.addItem(appItem)

        let shadeItem = NSMenuItem()
        let shade = NSMenu(title: "Shade")
        shade.addItem(withTitle: "Shade On/Off", action: #selector(toggleShade), keyEquivalent: "s").target = self
        shade.addItem(.separator())
        for (i, s) in HoleShape.allCases.enumerated() {
            let it = shade.addItem(withTitle: s.title, action: #selector(setShape(_:)), keyEquivalent: "\(i + 1)")
            it.target = self
            it.representedObject = s.rawValue
        }
        shadeItem.submenu = shade
        main.addItem(shadeItem)

        let winItem = NSMenuItem()
        let win = NSMenu(title: "Window")
        win.addItem(withTitle: "Show Panel", action: #selector(showPanel), keyEquivalent: "0").target = self
        win.addItem(withTitle: "Minimize", action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        winItem.submenu = win
        main.addItem(winItem)

        NSApp.mainMenu = main
    }

    // Closing the panel quits, and takes the shade away
    func windowWillClose(_ notification: Notification) {
        if (notification.object as? NSWindow) === panelWindow { NSApp.terminate(nil) }
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showPanel()
        return true
    }
}

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}
if args.count == 2 && args[1] == "--selftest" {
    let fails = Geometry.selfTest()
    print(fails == 0 ? "Self-test passed" : "Self-test: \(fails) failed")
    exit(Int32(fails))
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
