// Screen Ruler for macOS: up to three rulers and a protractor that float over everything on screen
// (twin of ../windows/Program.cs). Single-file AppKit app; build.sh makes Screen Ruler.app and a .dmg.
import AppKit

// MARK: - Dark Windows 95 skin (same tokens as the other Ergosfera apps)

enum Skin {
    static func rgb(_ r: Int, _ g: Int, _ b: Int, _ a: CGFloat = 1) -> NSColor {
        NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: a)
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
    static let lcdDim = rgb(0x3e, 0x62, 0x45)

    // The tools: a pale tint for each ruler, dark ticks, a red marker
    static let tints = [rgb(0xf6, 0xe7, 0xa1), rgb(0xbf, 0xe6, 0xf0), rgb(0xf3, 0xc9, 0xd9)]
    static let protractorTint = rgb(0xcd, 0xec, 0xcc)
    static let ink = rgb(0x1e, 0x1e, 0x22)
    static let marker = rgb(0xd0, 0x1c, 0x1c)

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

    static func size(_ s: String, _ font: NSFont) -> NSSize {
        (s as NSString).size(withAttributes: [.font: font])
    }

    static func draw(_ s: String, _ font: NSFont, _ color: NSColor, in r: NSRect, left: Bool = false, right: Bool = false) {
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color]
        let sz = (s as NSString).size(withAttributes: attrs)
        let x = left ? r.minX : right ? r.maxX - sz.width : r.midX - sz.width / 2
        (s as NSString).draw(at: NSPoint(x: x, y: r.midY - sz.height / 2), withAttributes: attrs)
    }
}

// MARK: - Units and tick marks (same rules as the Windows app; checked by --selftest)

enum Unit: String, CaseIterable {
    case cm, inch = "in", px, sm, skm, scm      // sm, skm, scm: scaled (calibrated) m, km and cm

    var scaled: Bool { self == .sm || self == .skm || self == .scm }
    var symbol: String {
        switch self {
        case .cm, .scm: return "cm"
        case .inch: return "in"
        case .px: return "px"
        case .sm: return "m"
        case .skm: return "km"
        }
    }
    var menuTitle: String {
        switch self {
        case .cm: return "Centimeters (cm)"
        case .inch: return "Inches (in)"
        case .px: return "Pixels (px)"
        case .sm: return "Meters, scaled (m)"
        case .skm: return "Kilometers, scaled (km)"
        case .scm: return "Centimeters, scaled (cm)"
        }
    }
    var meters: Double {
        switch self {
        case .sm: return 1
        case .skm: return 1000
        case .scm: return 0.01
        default: return 0
        }
    }
}

// Ticks every `step` units; every `major`-th is long and labeled, every `mid`-th medium,
// and for inches every `quarter`-th and `eighth`-th a little longer (0 = no such level).
struct TickPlan: Equatable {
    let step: Double, major: Int, mid: Int, quarter: Int, eighth: Int, decimals: Int
}

enum Measure {
    static let minMajor = 70.0     // least spacing between labels, in 96-dpi pixels

    // The smallest 1, 2 or 5 × 10^n that is at least `v`
    static func niceStep(_ v: Double) -> Double {
        let p = pow(10, floor(log10(v)))
        for m in [1.0, 2, 5, 10] where m * p >= v * (1 - 1e-9) { return m * p }
        return 10 * p
    }

    // `u` = screen units for one unit; `k` = screen units per 96-dpi pixel
    static func plan(_ unit: Unit, _ u: Double, _ k: Double) -> TickPlan {
        switch unit {
        case .cm:
            if 0.1 * u >= 3 * k { return TickPlan(step: 0.1, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 0) }
            return TickPlan(step: 0.5, major: 2, mid: 0, quarter: 0, eighth: 0, decimals: 0)
        case .inch:
            if u / 16 >= 3 * k { return TickPlan(step: 1.0 / 16, major: 16, mid: 8, quarter: 4, eighth: 2, decimals: 0) }
            return TickPlan(step: 1.0 / 8, major: 8, mid: 4, quarter: 2, eighth: 0, decimals: 0)
        default:
            // Labels at least 70 px apart, so tenths are always at least 7 px apart
            let big = niceStep(minMajor * k / u)
            let decimals = max(0, Int(-floor(log10(big) + 1e-9)))
            return TickPlan(step: big / 10, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: decimals)
        }
    }

    static func fixed(_ v: Double, _ decimals: Int) -> String { String(format: "%.\(decimals)f", v) }

    // A reading: px whole, cm 2 decimals, in 3 decimals, scaled units 4 significant figures
    static func format(_ v: Double, _ unit: Unit) -> String {
        let d: Int
        switch unit {
        case .px: d = 0
        case .cm: d = 2
        case .inch: d = 3
        default: d = v == 0 ? 2 : min(6, max(0, 3 - Int(floor(log10(abs(v))))))
        }
        return fixed(v, d) + " " + unit.symbol
    }

    static func angle(_ a: Double) -> String { fixed(a, 1) + "°" }

    static func norm180(_ a: Double) -> Double {
        var r = a.truncatingRemainder(dividingBy: 360)
        if r <= -180 { r += 360 }
        if r > 180 { r -= 360 }
        return r
    }

    // Angle between two directions, 0…180°
    static func between(_ a: Double, _ b: Double) -> Double { abs(norm180(a - b)) }

    // Where line p + t·d meets line q + s·e; returns (t, s), or nil if they're parallel
    static func cross(_ p: NSPoint, _ d: NSPoint, _ q: NSPoint, _ e: NSPoint) -> (Double, Double)? {
        let den = Double(d.x * e.y - d.y * e.x)
        if abs(den) < 1e-6 { return nil }
        let wx = Double(q.x - p.x), wy = Double(q.y - p.y)
        return ((wx * Double(e.y) - wy * Double(e.x)) / den, (wx * Double(d.y) - wy * Double(d.x)) / den)
    }

    static func selfTest() -> Int {
        var fails = 0
        func check(_ ok: Bool, _ what: String) { if !ok { fails += 1; print("FAIL: \(what)") } }
        func near(_ a: Double, _ b: Double) -> Bool { abs(a - b) < 1e-9 }
        check(near(niceStep(70), 100), "niceStep 70")
        check(near(niceStep(0.7), 1), "niceStep 0.7")
        check(near(niceStep(1.3), 2), "niceStep 1.3")
        check(near(niceStep(3), 5), "niceStep 3")
        check(near(niceStep(0.012), 0.02), "niceStep 0.012")
        check(near(niceStep(500), 500), "niceStep 500")
        check(plan(.cm, 37.8, 1) == TickPlan(step: 0.1, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 0), "plan cm")
        check(plan(.cm, 20, 1) == TickPlan(step: 0.5, major: 2, mid: 0, quarter: 0, eighth: 0, decimals: 0), "plan cm dense")
        check(plan(.inch, 96, 1) == TickPlan(step: 1.0 / 16, major: 16, mid: 8, quarter: 4, eighth: 2, decimals: 0), "plan in")
        check(plan(.px, 1, 1) == TickPlan(step: 10, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 0), "plan px")
        check(plan(.px, 0.5, 1) == TickPlan(step: 20, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 0), "plan px retina")
        check(plan(.skm, 30, 1) == TickPlan(step: 0.5, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 0), "plan km")
        check(plan(.sm, 1000, 1) == TickPlan(step: 0.01, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 1), "plan m")
        check(plan(.sm, 40, 1) == TickPlan(step: 0.2, major: 10, mid: 5, quarter: 0, eighth: 0, decimals: 0), "plan m 2")
        check(format(12.3456, .cm) == "12.35 cm", "format cm")
        check(format(3.14159, .inch) == "3.142 in", "format in")
        check(format(399.6, .px) == "400 px", "format px")
        check(format(1234.56, .skm) == "1235 km", "format km")
        check(format(0.012346, .sm) == "0.01235 m", "format m")
        check(format(5.4321, .scm) == "5.432 cm", "format scaled cm")
        check(format(0, .sm) == "0.00 m", "format zero")
        check(angle(23.44) == "23.4°", "angle")
        check(near(norm180(270), -90), "norm 270")
        check(near(norm180(-180), 180), "norm -180")
        check(near(norm180(-450), -90), "norm -450")
        check(near(between(10, 200), 170), "between 10 200")
        check(near(between(350, 20), 30), "between 350 20")
        if let c = cross(NSPoint(x: 0, y: 0), NSPoint(x: 1, y: 0), NSPoint(x: 5, y: -3), NSPoint(x: 0, y: 1)) {
            check(near(c.0, 5) && near(c.1, 3), "cross")
        } else { check(false, "cross") }
        check(cross(NSPoint(x: 0, y: 0), NSPoint(x: 1, y: 1), NSPoint(x: 0, y: 1), NSPoint(x: 2, y: 2)) == nil, "parallel")
        return fails
    }
}

// MARK: - Displays: real size (cm, in) and pixels

enum Screens {
    static func screen(at p: NSPoint) -> NSScreen {
        NSScreen.screens.first { $0.frame.contains(p) } ?? NSScreen.main ?? NSScreen.screens[0]
    }

    static func id(_ s: NSScreen) -> CGDirectDisplayID {
        (s.deviceDescription[NSDeviceDescriptionKey("NSScreenNumber")] as? NSNumber)?.uint32Value ?? CGMainDisplayID()
    }

    // Identifies a display (and its resolution) for the screen-size correction
    static func key(_ s: NSScreen) -> String {
        let d = id(s)
        return "\(CGDisplayVendorNumber(d))-\(CGDisplayModelNumber(d))-\(CGDisplaySerialNumber(d))-\(Int(s.frame.width))x\(Int(s.frame.height))"
    }

    // Points per millimeter as the display reports it, or nil if it doesn't (some projectors)
    static func reported(_ s: NSScreen) -> Double? {
        let mm = CGDisplayScreenSize(id(s))
        guard mm.width > 20 else { return nil }
        let v = Double(s.frame.width / mm.width)
        return v > 0.5 && v < 20 ? v : nil
    }

    static func fixKey(_ s: NSScreen) -> String { "sizeFix." + key(s) }
    static func fixed(_ s: NSScreen) -> Double? {
        let v = UserDefaults.standard.double(forKey: fixKey(s))
        return v > 0 ? v : nil
    }

    static func pointsPerMM(_ s: NSScreen) -> Double { fixed(s) ?? reported(s) ?? 110 / 25.4 }
}

// MARK: - Tools

enum Region { case none, body, marker, knob, grip, arm, rim }

final class ToolWindow: NSWindow {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

// A ruler or the protractor: drawn in its own coordinates (x along the baseline, y up),
// rotated by `angle` degrees counterclockwise about `origin` (screen points).
class Tool: NSObject {
    var origin = NSPoint.zero
    var angle = 0.0
    var window: ToolWindow!
    var view: ToolView!
    weak var app: AppDelegate!
    var grabbed: Region = .none
    var grabLocal = NSPoint.zero, grabAngle = 0.0, grabOrigin = NSPoint.zero, grabValue: CGFloat = 0

    var localBounds: NSRect { .zero }
    func hit(_ p: NSPoint) -> Region { .none }
    func drawLocal() {}
    var title: String { "" }
    var tint: NSColor { .white }

    var flipped: Bool { abs(Measure.norm180(angle)) > 90 }

    func toLocal(_ g: NSPoint) -> NSPoint {
        let r = angle * .pi / 180, c = CGFloat(cos(r)), s = CGFloat(sin(r))
        let dx = g.x - origin.x, dy = g.y - origin.y
        return NSPoint(x: dx * c + dy * s, y: -dx * s + dy * c)
    }

    func toGlobal(_ l: NSPoint) -> NSPoint {
        let r = angle * .pi / 180, c = CGFloat(cos(r)), s = CGFloat(sin(r))
        return NSPoint(x: origin.x + l.x * c - l.y * s, y: origin.y + l.x * s + l.y * c)
    }

    func makeWindow() {
        window = ToolWindow(contentRect: NSRect(x: 0, y: 0, width: 10, height: 10), styleMask: [.borderless],
                            backing: .buffered, defer: false)
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = false
        window.level = .floating
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.isReleasedWhenClosed = false
        window.acceptsMouseMovedEvents = true
        view = ToolView(tool: self)
        window.contentView = view
        layout()
        window.orderFrontRegardless()
    }

    func layout() {
        let b = localBounds
        let pts = [NSPoint(x: b.minX, y: b.minY), NSPoint(x: b.maxX, y: b.minY),
                   NSPoint(x: b.minX, y: b.maxY), NSPoint(x: b.maxX, y: b.maxY)].map(toGlobal)
        let x0 = pts.map { $0.x }.min()!, x1 = pts.map { $0.x }.max()!
        let y0 = pts.map { $0.y }.min()!, y1 = pts.map { $0.y }.max()!
        let f = NSRect(x: floor(x0) - 2, y: floor(y0) - 2, width: ceil(x1 - x0) + 5, height: ceil(y1 - y0) + 5)
        window.setFrame(f, display: false)
        view.needsDisplay = true
    }

    func close() { window.orderOut(nil); window.close() }

    // Text that stays upright when the tool is turned past 90°
    func text(_ s: String, _ font: NSFont, _ color: NSColor, at c: NSPoint, left: Bool = false) {
        let sz = Skin.size(s, font)
        NSGraphicsContext.saveGraphicsState()
        let t = NSAffineTransform()
        t.translateX(by: c.x, yBy: c.y)
        if flipped { t.rotate(byDegrees: 180) }
        t.concat()
        let x: CGFloat = left ? (flipped ? -sz.width : 0) : -sz.width / 2
        (s as NSString).draw(at: NSPoint(x: x, y: -sz.height / 2), withAttributes: [.font: font, .foregroundColor: color])
        NSGraphicsContext.restoreGraphicsState()
    }

    // A round knob with a circular arrow: drag it to turn the tool
    func drawKnob(_ c: NSPoint, _ r: CGFloat) {
        let disc = NSBezierPath(ovalIn: NSRect(x: c.x - r, y: c.y - r, width: 2 * r, height: 2 * r))
        Skin.rgb(255, 255, 255, 0.85).setFill()
        disc.fill()
        Skin.ink.setStroke()
        disc.lineWidth = 1
        disc.stroke()
        let a = NSBezierPath()
        a.appendArc(withCenter: c, radius: r * 0.55, startAngle: 120, endAngle: 400)
        a.lineWidth = 1.6
        a.stroke()
        let e = 400.0 * .pi / 180, tip = NSPoint(x: c.x + r * 0.55 * CGFloat(cos(e)), y: c.y + r * 0.55 * CGFloat(sin(e)))
        let head = NSBezierPath()
        head.move(to: NSPoint(x: tip.x - r * 0.38, y: tip.y + r * 0.05))
        head.line(to: tip)
        head.line(to: NSPoint(x: tip.x - r * 0.05, y: tip.y + r * 0.4))
        head.lineWidth = 1.6
        head.stroke()
    }

    // MARK: Dragging (global points, so the window can move under the mouse)

    func begin(_ region: Region, _ g: NSPoint) {
        grabbed = region
        grabLocal = toLocal(g)
        grabAngle = angle
        grabOrigin = origin
    }

    func drag(_ g: NSPoint, shift: Bool) {
        switch grabbed {
        case .body:
            // Keep the grabbed point under the mouse
            let r = angle * .pi / 180, c = CGFloat(cos(r)), s = CGFloat(sin(r))
            origin = NSPoint(x: g.x - (grabLocal.x * c - grabLocal.y * s), y: g.y - (grabLocal.x * s + grabLocal.y * c))
        case .knob:
            let now = atan2(Double(g.y - origin.y), Double(g.x - origin.x)) * 180 / .pi
            let then = atan2(Double(grabLocal.y), Double(grabLocal.x)) * 180 / .pi
            var a = Measure.norm180(now - then)
            if shift { a = (a / 15).rounded() * 15 }
            angle = a
        default: break
        }
        layout()
    }

    func nudge(dx: CGFloat, dy: CGFloat, turn: Double) {
        origin.x += dx
        origin.y += dy
        angle = Measure.norm180(((angle + turn) * 10).rounded() / 10)
        layout()
        app.changed()
    }

    func serialize() -> String { "" }
    func readouts() -> [String] { [] }

    func baseMenu(_ m: NSMenu) {
        m.addItem(.separator())
        let r0 = m.addItem(withTitle: "Turn to 0°", action: #selector(turnFlat), keyEquivalent: "")
        r0.target = self
        let r90 = m.addItem(withTitle: "Turn to 90°", action: #selector(turnUp), keyEquivalent: "")
        r90.target = self
        m.addItem(.separator())
        m.addItem(withTitle: "New Ruler", action: #selector(AppDelegate.addRuler), keyEquivalent: "").target = app
        m.addItem(withTitle: "New Protractor", action: #selector(AppDelegate.addProtractor), keyEquivalent: "").target = app
        m.addItem(withTitle: "Remove \(title)", action: #selector(remove), keyEquivalent: "").target = self
        m.addItem(.separator())
        m.addItem(withTitle: "Show Panel", action: #selector(AppDelegate.showPanel), keyEquivalent: "").target = app
        m.addItem(withTitle: "About Screen Ruler", action: #selector(AppDelegate.showAbout), keyEquivalent: "").target = app
        m.addItem(withTitle: "Quit Screen Ruler", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "")
    }

    func menu() -> NSMenu { let m = NSMenu(); baseMenu(m); return m }

    @objc func turnFlat() { angle = 0; layout(); app.changed() }
    @objc func turnUp() { angle = 90; layout(); app.changed() }
    @objc func remove() { app.remove(self) }
}

final class ToolView: NSView {
    unowned let tool: Tool
    var tracking: NSTrackingArea?

    init(tool: Tool) {
        self.tool = tool
        super.init(frame: .zero)
    }
    required init?(coder: NSCoder) { fatalError() }

    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func updateTrackingAreas() {
        if let t = tracking { removeTrackingArea(t) }
        tracking = NSTrackingArea(rect: bounds, options: [.mouseMoved, .activeAlways, .inVisibleRect], owner: self)
        addTrackingArea(tracking!)
        super.updateTrackingAreas()
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let w = window else { return }
        NSGraphicsContext.saveGraphicsState()
        let t = NSAffineTransform()
        t.translateX(by: tool.origin.x - w.frame.minX, yBy: tool.origin.y - w.frame.minY)
        t.rotate(byDegrees: CGFloat(tool.angle))
        t.concat()
        tool.drawLocal()
        NSGraphicsContext.restoreGraphicsState()
    }

    func cursor(_ r: Region) -> NSCursor {
        switch r {
        case .body: return tool.grabbed == .body ? .closedHand : .openHand
        case .none: return .arrow
        default: return .pointingHand
        }
    }

    override func mouseMoved(with e: NSEvent) {
        cursor(tool.hit(tool.toLocal(NSEvent.mouseLocation))).set()
    }

    override func mouseDown(with e: NSEvent) {
        let g = NSEvent.mouseLocation
        let r = tool.hit(tool.toLocal(g))
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(self)
        guard r != .none else { return }
        tool.begin(r, g)
        cursor(r).set()
    }

    override func mouseDragged(with e: NSEvent) {
        guard tool.grabbed != .none else { return }
        tool.drag(NSEvent.mouseLocation, shift: e.modifierFlags.contains(.shift))
        tool.app.changed(save: false)
    }

    override func mouseUp(with e: NSEvent) {
        guard tool.grabbed != .none else { return }
        tool.grabbed = .none
        tool.app.changed()
        cursor(tool.hit(tool.toLocal(NSEvent.mouseLocation))).set()
    }

    override func menu(for event: NSEvent) -> NSMenu? { tool.menu() }

    override func keyDown(with e: NSEvent) {
        let big: CGFloat = e.modifierFlags.contains(.shift) ? 10 : 1
        let turn = e.modifierFlags.contains(.option)
        let step = e.modifierFlags.contains(.shift) ? 1.0 : 0.1
        switch e.keyCode {
        case 123: turn ? tool.nudge(dx: 0, dy: 0, turn: step) : tool.nudge(dx: -big, dy: 0, turn: 0)    // ←
        case 124: turn ? tool.nudge(dx: 0, dy: 0, turn: -step) : tool.nudge(dx: big, dy: 0, turn: 0)    // →
        case 125: tool.nudge(dx: 0, dy: -big, turn: 0)                                                   // ↓
        case 126: tool.nudge(dx: 0, dy: big, turn: 0)                                                    // ↑
        case 51, 117: tool.remove()                                                                      // delete
        default: super.keyDown(with: e)
        }
    }
}

// MARK: Ruler

final class Ruler: Tool {
    static let height: CGFloat = 72, zeroMargin: CGFloat = 16, endMargin: CGFloat = 52, tagRoom: CGFloat = 26
    static let minLength: CGFloat = 80, maxLength: CGFloat = 3000

    let index: Int
    var length: CGFloat = 400
    var marker: CGFloat = 200
    var unit: Unit = .cm
    var metersPerPoint = 0.0        // set with Set Scale…; 0 = not set yet
    var crossTags: [(CGFloat, String)] = []

    init(index: Int) { self.index = index }

    override var title: String { "Ruler \(index + 1)" }
    override var tint: NSColor { Skin.tints[index] }

    var body: NSRect {
        NSRect(x: -Ruler.zeroMargin, y: -Ruler.height, width: length + Ruler.zeroMargin + Ruler.endMargin, height: Ruler.height)
    }
    override var localBounds: NSRect {
        var b = body
        b.size.height += Ruler.tagRoom
        return b
    }
    var knob: NSPoint { NSPoint(x: length + 24, y: -Ruler.height / 2) }

    var screen: NSScreen { Screens.screen(at: origin) }

    // Screen points for one unit
    var unitPoints: Double {
        let s = screen
        switch unit {
        case .cm: return Screens.pointsPerMM(s) * 10
        case .inch: return Screens.pointsPerMM(s) * 25.4
        case .px: return 1 / Double(s.backingScaleFactor)
        default: return metersPerPoint > 0 ? unit.meters / metersPerPoint : Screens.pointsPerMM(s) * 10
        }
    }

    var reading: String { Measure.format(Double(marker) / unitPoints, unit) }

    override func hit(_ p: NSPoint) -> Region {
        guard body.contains(p) else { return .none }
        if abs(p.x - marker) <= 6 && p.x <= length + 6 { return .marker }
        if hypot(p.x - knob.x, p.y - knob.y) <= 15 { return .knob }
        if p.x >= length + 40 { return .grip }
        return .body
    }

    override func begin(_ region: Region, _ g: NSPoint) {
        super.begin(region, g)
        grabValue = region == .grip ? length - grabLocal.x : marker - grabLocal.x
    }

    override func drag(_ g: NSPoint, shift: Bool) {
        let x = toLocal(g).x
        switch grabbed {
        case .marker:
            var m = x + grabValue
            if shift {      // snap to the nearest tick
                let step = Measure.plan(unit, unitPoints, 1).step * unitPoints
                m = CGFloat((Double(m) / step).rounded() * step)
            }
            marker = min(length, max(0, m))
            view.needsDisplay = true
        case .grip:
            length = min(Ruler.maxLength, max(Ruler.minLength, x + grabValue))
            marker = min(marker, length)
            layout()
        default: super.drag(g, shift: shift)
        }
    }

    override func drawLocal() {
        let b = body
        let shape = NSBezierPath(roundedRect: b, xRadius: 4, yRadius: 4)
        tint.withAlphaComponent(0.84).setFill()
        shape.fill()
        Skin.ink.withAlphaComponent(0.55).setStroke()
        shape.lineWidth = 1
        shape.stroke()

        // The measuring edge and its ticks
        let u = unitPoints
        let plan = Measure.plan(unit, u, 1)
        let ticks = NSBezierPath()
        ticks.lineWidth = 1
        let n = Int(floor(Double(length) / (plan.step * u) + 1e-6))
        let fLabel = Skin.font("Helvetica", 11)
        for i in 0...max(0, n) {
            let x = CGFloat(Double(i) * plan.step * u)
            let h: CGFloat
            if i % plan.major == 0 { h = 22 }
            else if plan.mid > 0 && i % plan.mid == 0 { h = 15 }
            else if plan.quarter > 0 && i % plan.quarter == 0 { h = 11 }
            else if plan.eighth > 0 && i % plan.eighth == 0 { h = 8 }
            else { h = 6 }
            ticks.move(to: NSPoint(x: x, y: 0))
            ticks.line(to: NSPoint(x: x, y: -h))
            if i % plan.major == 0 {
                let v = Double(i) * plan.step
                text(Measure.fixed(v, plan.decimals), fLabel, Skin.ink, at: NSPoint(x: x, y: -31))
            }
        }
        Skin.ink.setStroke()
        ticks.stroke()
        let edgeLine = NSBezierPath()
        edgeLine.move(to: NSPoint(x: b.minX + 2, y: -0.5))
        edgeLine.line(to: NSPoint(x: b.maxX - 2, y: -0.5))
        edgeLine.lineWidth = 1
        edgeLine.stroke()

        // Unit and angle, near the zero end
        let fInfo = Skin.font("Helvetica", 11, bold: true)
        let info = (unit.scaled ? unit.symbol + " (scaled)" : unit.symbol) + "   ∠ " + Measure.angle(Measure.norm180(angle))
        text(info, fInfo, Skin.ink.withAlphaComponent(0.8), at: NSPoint(x: flipped ? length : 4, y: -62), left: true)

        // The red marker: a line across the ruler and the reading from 0
        Skin.marker.setStroke()
        let line = NSBezierPath()
        line.move(to: NSPoint(x: marker, y: 0))
        line.line(to: NSPoint(x: marker, y: -Ruler.height + 1))
        line.lineWidth = 1.5
        line.stroke()
        Skin.marker.setFill()
        let tri = NSBezierPath()
        tri.move(to: NSPoint(x: marker - 6, y: -Ruler.height + 1))
        tri.line(to: NSPoint(x: marker + 6, y: -Ruler.height + 1))
        tri.line(to: NSPoint(x: marker, y: -Ruler.height + 9))
        tri.close()
        tri.fill()
        let fRead = Skin.font("Menlo", 11, bold: true)
        let r = reading, w = Skin.size(r, fRead).width
        let right = marker + 8 + w <= length + Ruler.endMargin - 4
        let tag = NSRect(x: right ? marker + 5 : marker - 9 - w, y: -54, width: w + 4, height: 15)
        Skin.rgb(255, 255, 255, 0.8).setFill()
        NSBezierPath(roundedRect: tag, xRadius: 2, yRadius: 2).fill()
        text(r, fRead, Skin.marker, at: NSPoint(x: tag.midX, y: tag.midY))

        drawKnob(knob, 12)

        // Grip at the far end: drag to make the ruler longer or shorter
        Skin.ink.withAlphaComponent(0.5).setStroke()
        let grip = NSBezierPath()
        for dx: CGFloat in [43, 46, 49] {
            grip.move(to: NSPoint(x: length + dx, y: -Ruler.height / 2 - 12))
            grip.line(to: NSPoint(x: length + dx, y: -Ruler.height / 2 + 12))
        }
        grip.lineWidth = 1
        grip.stroke()

        // Where another ruler's edge crosses this one, the angle between them
        let fTag = Skin.font("Helvetica", 11, bold: true)
        for (x, s) in crossTags {
            let w = Skin.size(s, fTag).width + 8
            let box = NSRect(x: x - w / 2, y: 4, width: w, height: 17)
            Skin.rgb(255, 255, 255, 0.92).setFill()
            let p = NSBezierPath(roundedRect: box, xRadius: 3, yRadius: 3)
            p.fill()
            Skin.ink.setStroke()
            p.lineWidth = 1
            p.stroke()
            text(s, fTag, Skin.ink, at: NSPoint(x: box.midX, y: box.midY))
        }
    }

    override func menu() -> NSMenu {
        let m = NSMenu()
        for u in Unit.allCases {
            if u == .sm { m.addItem(.separator()) }
            let it = m.addItem(withTitle: u.menuTitle, action: #selector(pickUnit(_:)), keyEquivalent: "")
            it.target = self
            it.representedObject = u.rawValue
            it.state = u == unit ? .on : .off
        }
        m.addItem(.separator())
        m.addItem(withTitle: "Set Scale…", action: #selector(setScaleFromMenu), keyEquivalent: "").target = self
        m.addItem(withTitle: "Correct Screen Size…", action: #selector(correctSize), keyEquivalent: "").target = self
        if Screens.fixed(screen) != nil {
            m.addItem(withTitle: "Use the Display's Reported Size", action: #selector(resetSize), keyEquivalent: "").target = self
        }
        baseMenu(m)
        return m
    }

    @objc func pickUnit(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String, let u = Unit(rawValue: raw) else { return }
        if u.scaled && metersPerPoint <= 0 { askScale(unit: u); return }
        unit = u
        view.needsDisplay = true
        app.changed()
    }

    @objc func setScaleFromMenu() { askScale(unit: unit.scaled ? unit : .sm) }

    func askScale(unit want: Unit) {
        guard marker >= 10 else {
            app.message("Move the red marker first",
                        "Lay \(title) along a length you know (a map's scale bar, or an object of known size in a photo), " +
                        "with 0 at one end. Then drag the red marker to the other end and choose Set Scale… again.")
            return
        }
        let units: [Unit] = [.sm, .skm, .scm]
        let px = Measure.format(Double(marker) * Double(screen.backingScaleFactor), .px)
        guard let (v, i) = app.askLength(title: "Set the scale of \(title)",
                                         text: "From 0 to the red marker is \(px) on the screen. How long is it in reality?",
                                         units: units.map { $0.symbol }, selected: units.firstIndex(of: want) ?? 0) else { return }
        metersPerPoint = v * units[i].meters / Double(marker)
        unit = units[i]
        view.needsDisplay = true
        app.changed()
    }

    @objc func correctSize() {
        guard marker >= 10 else {
            app.message("Move the red marker first",
                        "Hold a real ruler (or a bank card, 8.56 cm wide) against the screen, along \(title). " +
                        "Drag the red marker to a length you can read on it, then choose Correct Screen Size… again.")
            return
        }
        let units = ["cm", "in"]
        let px = Measure.format(Double(marker) * Double(screen.backingScaleFactor), .px)
        guard let (v, i) = app.askLength(title: "Correct the screen size",
                                         text: "Hold a real ruler against the screen. From 0 to the red marker is \(px). " +
                                               "How long is it on your real ruler?",
                                         units: units, selected: unit == .inch ? 1 : 0) else { return }
        let mm = v * (i == 0 ? 10 : 25.4)
        UserDefaults.standard.set(Double(marker) / mm, forKey: Screens.fixKey(screen))
        app.changed()
    }

    @objc func resetSize() {
        UserDefaults.standard.removeObject(forKey: Screens.fixKey(screen))
        app.changed()
    }

    override func serialize() -> String {
        [Double(origin.x), Double(origin.y), angle, Double(length), Double(marker)].map { String($0) }.joined(separator: ",") +
            ",\(unit.rawValue),\(metersPerPoint)"
    }

    static func parse(_ s: String, index: Int) -> Ruler? {
        let f = s.split(separator: ",").map(String.init)
        guard f.count == 7, let x = Double(f[0]), let y = Double(f[1]), let a = Double(f[2]),
              let l = Double(f[3]), let m = Double(f[4]), let u = Unit(rawValue: f[5]), let k = Double(f[6]) else { return nil }
        let r = Ruler(index: index)
        r.origin = NSPoint(x: x, y: y)
        r.angle = Measure.norm180(a)
        r.length = min(maxLength, max(minLength, CGFloat(l)))
        r.marker = min(r.length, max(0, CGFloat(m)))
        r.metersPerPoint = max(0, k)
        r.unit = u.scaled && r.metersPerPoint <= 0 ? .cm : u
        return r
    }

    override func readouts() -> [String] { [reading, "∠ " + Measure.angle(Measure.norm180(angle))] }
}

// MARK: Protractor

final class Protractor: Tool {
    static let bar: CGFloat = 26, minRadius: CGFloat = 70
    var radius: CGFloat = 150
    var arm = 60.0          // degrees from the baseline, 0…180

    override var title: String { "Protractor" }
    override var tint: NSColor { Skin.protractorTint }

    var armLength: CGFloat { radius + 36 }
    var knob: NSPoint { NSPoint(x: radius + 24, y: -Protractor.bar / 2) }
    var armEnd: NSPoint {
        let r = arm * .pi / 180
        return NSPoint(x: armLength * CGFloat(cos(r)), y: armLength * CGFloat(sin(r)))
    }

    override var localBounds: NSRect {
        NSRect(x: -radius - 48, y: -Protractor.bar - 2, width: 2 * radius + 96, height: radius + 50 + Protractor.bar)
    }

    override func hit(_ p: NSPoint) -> Region {
        let e = armEnd
        if hypot(p.x - e.x, p.y - e.y) <= 11 { return .arm }
        let len = hypot(e.x, e.y), t = (p.x * e.x + p.y * e.y) / len
        if t > 20 && t <= len && abs(p.x * e.y - p.y * e.x) / len <= 5 { return .arm }
        if hypot(p.x - knob.x, p.y - knob.y) <= 14 { return .knob }
        let r = hypot(p.x, p.y)
        if p.y >= 0 && r <= radius { return r >= radius - 8 ? .rim : .body }
        if p.y < 0 && p.y >= -Protractor.bar && p.x >= -radius - 8 && p.x <= radius + 40 { return .body }
        return .none
    }

    override func begin(_ region: Region, _ g: NSPoint) {
        super.begin(region, g)
        grabValue = radius - hypot(grabLocal.x, grabLocal.y)
    }

    override func drag(_ g: NSPoint, shift: Bool) {
        let p = toLocal(g)
        switch grabbed {
        case .arm:
            var a = atan2(Double(p.y), Double(p.x)) * 180 / .pi
            if a < -90 { a = 180 } else if a < 0 { a = 0 }
            if shift { a = (a / 5).rounded() * 5 }
            arm = min(180, max(0, a))
            view.needsDisplay = true
        case .rim:
            radius = min(700, max(Protractor.minRadius, hypot(p.x, p.y) + grabValue))
            layout()
        default: super.drag(g, shift: shift)
        }
    }

    override func drawLocal() {
        let R = radius
        let shape = NSBezierPath()
        shape.move(to: NSPoint(x: radius, y: 0))
        shape.appendArc(withCenter: .zero, radius: R, startAngle: 0, endAngle: 180)
        shape.close()
        let base = NSBezierPath(roundedRect: NSRect(x: -R - 8, y: -Protractor.bar, width: 2 * R + 48, height: Protractor.bar),
                                xRadius: 4, yRadius: 4)
        shape.append(base)
        shape.windingRule = .nonZero
        tint.withAlphaComponent(0.8).setFill()
        shape.fill()
        Skin.ink.withAlphaComponent(0.55).setStroke()
        shape.lineWidth = 1
        shape.stroke()

        // Degree ticks around the rim, with the outer scale counterclockwise and the inner one clockwise
        let ticks = NSBezierPath()
        ticks.lineWidth = 1
        let fOuter = Skin.font("Helvetica", 10), fInner = Skin.font("Helvetica", 8.5)
        for d in 0...180 {
            let r = Double(d) * .pi / 180, c = CGFloat(cos(r)), s = CGFloat(sin(r))
            let h: CGFloat = d % 10 == 0 ? 15 : d % 5 == 0 ? 10 : 6
            ticks.move(to: NSPoint(x: R * c, y: R * s))
            ticks.line(to: NSPoint(x: (R - h) * c, y: (R - h) * s))
            if d % 10 == 0 {
                text("\(d)", fOuter, Skin.ink, at: NSPoint(x: (R - 25) * c, y: (R - 25) * s))
                if R >= 110 && d % 180 != 0 {
                    text("\(180 - d)", fInner, Skin.ink.withAlphaComponent(0.55), at: NSPoint(x: (R - 40) * c, y: (R - 40) * s))
                }
            }
        }
        // Baseline and center cross
        ticks.move(to: NSPoint(x: -R, y: 0)); ticks.line(to: NSPoint(x: R, y: 0))
        ticks.move(to: NSPoint(x: 0, y: -8)); ticks.line(to: NSPoint(x: 0, y: 8))
        Skin.ink.setStroke()
        ticks.stroke()

        // The arm, from the center, and the angle it makes with the baseline
        let arc = NSBezierPath()
        arc.appendArc(withCenter: .zero, radius: 30, startAngle: 0, endAngle: CGFloat(arm))
        arc.lineWidth = 2
        Skin.marker.withAlphaComponent(0.7).setStroke()
        arc.stroke()
        let e = armEnd
        let armLine = NSBezierPath()
        armLine.move(to: .zero)
        armLine.line(to: e)
        armLine.lineWidth = 1.5
        Skin.marker.setStroke()
        armLine.stroke()
        Skin.marker.withAlphaComponent(0.85).setFill()
        NSBezierPath(ovalIn: NSRect(x: e.x - 8, y: e.y - 8, width: 16, height: 16)).fill()
        Skin.ink.setFill()
        NSBezierPath(ovalIn: NSRect(x: -2.5, y: -2.5, width: 5, height: 5)).fill()

        let fBig = Skin.font("Menlo", 15, bold: true)
        let label = Measure.angle(arm)
        let mid = (arm < 90 ? arm + 90 : arm - 90) * .pi / 180     // away from the arm
        let at = NSPoint(x: R * 0.36 * CGFloat(cos(mid)), y: max(R * 0.18, R * 0.36 * CGFloat(sin(mid))))
        let w = Skin.size(label, fBig).width + 8
        Skin.rgb(255, 255, 255, 0.8).setFill()
        NSBezierPath(roundedRect: NSRect(x: at.x - w / 2, y: at.y - 10, width: w, height: 20), xRadius: 3, yRadius: 3).fill()
        text(label, fBig, Skin.marker, at: at)

        text("∠ " + Measure.angle(Measure.norm180(angle)), Skin.font("Helvetica", 10, bold: true),
             Skin.ink.withAlphaComponent(0.75), at: NSPoint(x: -R * 0.55, y: -Protractor.bar / 2))
        drawKnob(knob, 11)
    }

    override func serialize() -> String {
        [Double(origin.x), Double(origin.y), angle, Double(radius), arm].map { String($0) }.joined(separator: ",")
    }

    static func parse(_ s: String) -> Protractor? {
        let f = s.split(separator: ",").compactMap { Double($0) }
        guard f.count == 5 else { return nil }
        let p = Protractor()
        p.origin = NSPoint(x: f[0], y: f[1])
        p.angle = Measure.norm180(f[2])
        p.radius = max(minRadius, CGFloat(f[3]))
        p.arm = min(180, max(0, f[4]))
        return p
    }

    override func readouts() -> [String] { [Measure.angle(arm), "∠ " + Measure.angle(Measure.norm180(angle))] }
}

// MARK: - The panel: add tools and read every measurement

final class PanelView: NSView {
    enum Hit: Equatable { case none, addRuler, addProtractor, about, row(Int), close(Int) }
    struct Row { let tool: Tool?; let name: String; let values: [String]; let tint: NSColor? }

    weak var app: AppDelegate!
    var rows: [Row] = []
    var pressed: Hit = .none
    static let width: CGFloat = 340, rowH: CGFloat = 22, top: CGFloat = 44

    let fButton = Skin.font("Tahoma", 12, bold: true)
    let fName = Skin.font("Tahoma", 12, bold: true)
    let fValue = Skin.font("Menlo", 12, bold: true)

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    var rcRuler: NSRect { NSRect(x: 8, y: 8, width: 92, height: 26) }
    var rcProtractor: NSRect { NSRect(x: 106, y: 8, width: 112, height: 26) }
    var rcAbout: NSRect { NSRect(x: bounds.width - 34, y: 8, width: 26, height: 26) }
    var rcLcd: NSRect { NSRect(x: 8, y: PanelView.top, width: bounds.width - 16, height: CGFloat(max(1, rows.count)) * PanelView.rowH + 8) }
    func rcRow(_ i: Int) -> NSRect { NSRect(x: rcLcd.minX + 4, y: rcLcd.minY + 4 + CGFloat(i) * PanelView.rowH, width: rcLcd.width - 8, height: PanelView.rowH) }
    func rcClose(_ i: Int) -> NSRect { let r = rcRow(i); return NSRect(x: r.maxX - 20, y: r.minY + 2, width: 18, height: 18) }

    var wantedHeight: CGFloat { PanelView.top + CGFloat(max(1, rows.count)) * PanelView.rowH + 8 + 8 }

    func hit(_ p: NSPoint) -> Hit {
        if rcRuler.contains(p) { return .addRuler }
        if rcProtractor.contains(p) { return .addProtractor }
        if rcAbout.contains(p) { return .about }
        for i in rows.indices where rcRow(i).contains(p) {
            if rows[i].tool != nil && rcClose(i).contains(p) { return .close(i) }
            return rows[i].tool != nil ? .row(i) : .none
        }
        return .none
    }

    func enabled(_ h: Hit) -> Bool {
        switch h {
        case .addRuler: return app.rulers.count < 3
        case .addProtractor: return app.protractor == nil
        default: return true
        }
    }

    override func mouseDown(with e: NSEvent) {
        let h = hit(convert(e.locationInWindow, from: nil))
        pressed = enabled(h) ? h : .none
        needsDisplay = true
    }

    override func mouseUp(with e: NSEvent) {
        let h = hit(convert(e.locationInWindow, from: nil)), was = pressed
        pressed = .none
        needsDisplay = true
        guard h == was else { return }
        switch h {
        case .addRuler: app.addRuler()
        case .addProtractor: app.addProtractor()
        case .about: app.showAbout()
        case .row(let i): if let t = rows[i].tool { t.window.orderFrontRegardless(); t.window.makeKey() }
        case .close(let i): if let t = rows[i].tool { app.remove(t) }
        case .none: break
        }
    }

    func button(_ r: NSRect, _ h: Hit, _ label: String) {
        let down = pressed == h
        Skin.face.setFill()
        r.fill()
        if down { Skin.sunken(r) } else { Skin.raised(r) }
        Skin.draw(label, fButton, enabled(h) ? Skin.text : Skin.muted, in: down ? r.offsetBy(dx: 1, dy: 1) : r)
    }

    override func draw(_ dirtyRect: NSRect) {
        Skin.face.setFill()
        bounds.fill()
        button(rcRuler, .addRuler, "+ Ruler")
        button(rcProtractor, .addProtractor, "+ Protractor")
        button(rcAbout, .about, "?")

        let lcd = rcLcd
        Skin.lcd.setFill()
        lcd.fill()
        Skin.sunken(lcd)
        if rows.isEmpty {
            Skin.draw("Add a ruler or a protractor", fName, Skin.lcdDim, in: rcRow(0))
            return
        }
        for (i, row) in rows.enumerated() {
            let r = rcRow(i)
            var x = r.minX + 4
            if let t = row.tint {
                let sw = NSRect(x: x, y: r.midY - 5, width: 10, height: 10)
                t.setFill(); sw.fill()
                Skin.lcdDigit.setStroke(); NSBezierPath(rect: sw.insetBy(dx: 0.5, dy: 0.5)).stroke()
                x += 16
            } else { x += 16 }
            Skin.draw(row.name, fName, Skin.lcdDigit, in: NSRect(x: x, y: r.minY, width: 120, height: r.height), left: true)
            let right = r.maxX - (row.tool != nil ? 26 : 4)
            if row.values.count > 1 {
                Skin.draw(row.values[1], fValue, Skin.lcdDigit, in: NSRect(x: right - 80, y: r.minY, width: 80, height: r.height), right: true)
            }
            if let v = row.values.first {
                let w: CGFloat = row.values.count > 1 ? 88 : 80
                Skin.draw(v, fValue, Skin.lcdDigit, in: NSRect(x: right - 80 - 10 - w, y: r.minY, width: w + 8, height: r.height),
                          right: row.values.count > 1)
            }
            if row.tool != nil {
                Skin.draw("×", Skin.font("Tahoma", 15, bold: true), pressed == .close(i) ? Skin.marker : Skin.lcdDim, in: rcClose(i))
            }
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

// MARK: - App icon (used by build.sh: ScreenRuler --write-iconset <dir>)

enum IconArt {
    // A yellow ruler, tilted, with a red marker
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        Skin.face.setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()

        NSGraphicsContext.saveGraphicsState()
        let t = NSAffineTransform()
        t.translateX(by: s / 2, yBy: s / 2)
        t.rotate(byDegrees: 30)
        t.concat()
        let w = s * 0.86, h = s * 0.26
        let strip = NSRect(x: -w / 2, y: -h / 2, width: w, height: h)
        Skin.tints[0].setFill()
        NSBezierPath(roundedRect: strip, xRadius: s * 0.02, yRadius: s * 0.02).fill()
        let ticks = NSBezierPath()
        let n = 12
        for i in 0...n {
            let x = strip.minX + s * 0.05 + CGFloat(i) * (w - s * 0.1) / CGFloat(n)
            let th = i % 4 == 0 ? h * 0.55 : i % 2 == 0 ? h * 0.38 : h * 0.24
            ticks.move(to: NSPoint(x: x, y: strip.maxY))
            ticks.line(to: NSPoint(x: x, y: strip.maxY - th))
        }
        ticks.lineWidth = max(1, s * 0.012)
        Skin.ink.setStroke()
        ticks.stroke()
        let m = NSBezierPath()
        m.move(to: NSPoint(x: s * 0.1, y: strip.maxY))
        m.line(to: NSPoint(x: s * 0.1, y: strip.minY))
        m.lineWidth = max(1.5, s * 0.022)
        Skin.marker.setStroke()
        m.stroke()
        NSGraphicsContext.restoreGraphicsState()
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

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate, NSMenuItemValidation {
    var rulers: [Ruler] = []
    var protractor: Protractor?
    var panelWindow: NSWindow!
    var panel: PanelView!
    var poll: Timer?
    let d = UserDefaults.standard

    func applicationDidFinishLaunching(_ note: Notification) {
        panel = PanelView(frame: NSRect(x: 0, y: 0, width: PanelView.width, height: 120))
        panel.app = self
        panelWindow = NSWindow(contentRect: panel.frame, styleMask: [.titled, .closable, .miniaturizable],
                               backing: .buffered, defer: false)
        panelWindow.title = "Screen Ruler"
        panelWindow.contentView = panel
        panelWindow.delegate = self
        panelWindow.isReleasedWhenClosed = false
        panelWindow.level = .floating
        buildMenu()

        for i in 0..<3 {
            if let s = d.string(forKey: "ruler.\(i)"), let r = Ruler.parse(s, index: i) { attach(r) }
        }
        if let s = d.string(forKey: "protractor"), let p = Protractor.parse(s) { attach(p) }
        if rulers.isEmpty && protractor == nil { addRuler() }
        for t in tools where !NSScreen.screens.contains(where: { $0.frame.contains(t.origin) }) { place(t) }

        refreshPanel()
        if !panelWindow.setFrameUsingName("ScreenRulerPanel") {
            let v = NSScreen.main?.visibleFrame ?? .zero
            panelWindow.setFrameTopLeftPoint(NSPoint(x: v.maxX - PanelView.width - 40, y: v.maxY - 40))
        }
        panelWindow.setFrameAutosaveName("ScreenRulerPanel")
        refreshPanel()
        panelWindow.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)

        // Clicks pass through the empty parts of each tool's window to whatever is underneath
        poll = Timer.scheduledTimer(withTimeInterval: 1.0 / 30, repeats: true) { [weak self] _ in self?.updatePassThrough() }
    }

    var tools: [Tool] { rulers.map { $0 as Tool } + (protractor.map { [$0 as Tool] } ?? []) }

    func updatePassThrough() {
        let g = NSEvent.mouseLocation
        for t in tools {
            let over = t.grabbed != .none || t.hit(t.toLocal(g)) != .none
            if t.window.ignoresMouseEvents == over { t.window.ignoresMouseEvents = !over }
        }
    }

    func attach(_ t: Tool) {
        t.app = self
        if let r = t as? Ruler { rulers.append(r); rulers.sort { $0.index < $1.index } }
        if let p = t as? Protractor { protractor = p }
        t.makeWindow()
    }

    // A new tool in the middle of the screen with the mouse, a little apart from the others
    func place(_ t: Tool) {
        let v = Screens.screen(at: NSEvent.mouseLocation).visibleFrame
        if let r = t as? Ruler {
            r.length = min(600, floor(v.width * 0.45))
            r.marker = floor(r.length / 2)
            r.origin = NSPoint(x: v.midX - r.length / 2, y: v.midY + 120 - CGFloat(r.index) * 110)
        } else if let p = t as? Protractor {
            p.origin = NSPoint(x: v.midX, y: max(v.minY + 50, v.midY - 120 - p.radius - 30))
        }
        t.angle = 0
        t.layout()
    }

    @objc func addRuler() {
        guard rulers.count < 3, let i = (0..<3).first(where: { i in !rulers.contains { $0.index == i } }) else { return }
        let r = Ruler(index: i)
        if let other = rulers.first { r.unit = other.unit; r.metersPerPoint = other.metersPerPoint }
        attach(r)
        place(r)
        changed()
    }

    @objc func addProtractor() {
        guard protractor == nil else { return }
        let p = Protractor()
        attach(p)
        place(p)
        changed()
    }

    func remove(_ t: Tool) {
        t.close()
        if let r = t as? Ruler { rulers.removeAll { $0 === r }; d.removeObject(forKey: "ruler.\(r.index)") }
        if t === protractor { protractor = nil; d.removeObject(forKey: "protractor") }
        changed()
    }

    @objc func showPanel() { panelWindow.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }

    // Something moved or changed: update the crossing angles and the panel, and remember it
    func changed(save: Bool = true) {
        updateCrossings()
        refreshPanel()
        guard save else { return }
        for r in rulers { d.set(r.serialize(), forKey: "ruler.\(r.index)") }
        if let p = protractor { d.set(p.serialize(), forKey: "protractor") }
    }

    func updateCrossings() {
        var tags = [Int: [(CGFloat, String)]]()
        for (i, a) in rulers.enumerated() {
            for b in rulers[(i + 1)...] {
                let da = a.toGlobal(NSPoint(x: 1, y: 0)), db = b.toGlobal(NSPoint(x: 1, y: 0))
                let ea = NSPoint(x: da.x - a.origin.x, y: da.y - a.origin.y), eb = NSPoint(x: db.x - b.origin.x, y: db.y - b.origin.y)
                guard let (ta, tb) = Measure.cross(a.origin, ea, b.origin, eb),
                      ta >= -12, ta <= Double(a.length) + 12, tb >= -12, tb <= Double(b.length) + 12 else { continue }
                let s = Measure.angle(Measure.between(a.angle, b.angle))
                tags[a.index, default: []].append((CGFloat(ta), s))
            }
        }
        for r in rulers {
            let new = tags[r.index] ?? []
            if new.count != r.crossTags.count || zip(new, r.crossTags).contains(where: { $0.0 != $1.0 || $0.1 != $1.1 }) {
                r.crossTags = new
                r.view.needsDisplay = true
            }
        }
    }

    func refreshPanel() {
        var rows: [PanelView.Row] = []
        for r in rulers { rows.append(.init(tool: r, name: r.title, values: r.readouts(), tint: r.tint)) }
        for (i, a) in rulers.enumerated() {
            for b in rulers[(i + 1)...] {
                rows.append(.init(tool: nil, name: "Ruler \(a.index + 1) ∠ Ruler \(b.index + 1)",
                                  values: [Measure.angle(Measure.between(a.angle, b.angle))], tint: nil))
            }
        }
        if let p = protractor { rows.append(.init(tool: p, name: p.title, values: p.readouts(), tint: p.tint)) }
        panel.rows = rows
        let h = panel.wantedHeight
        if abs(panelWindow.contentLayoutRect.height - h) > 0.5 {
            let top = panelWindow.frame.maxY
            var f = panelWindow.frameRect(forContentRect: NSRect(x: 0, y: 0, width: PanelView.width, height: h))
            f.origin = NSPoint(x: panelWindow.frame.minX, y: top - f.height)
            panelWindow.setFrame(f, display: true)
        }
        panel.needsDisplay = true
    }

    func message(_ title: String, _ text: String) {
        NSApp.activate(ignoringOtherApps: true)
        let a = NSAlert()
        a.messageText = title
        a.informativeText = text
        a.runModal()
    }

    // Asks for a length and its unit; returns (value, unit index), or nil if canceled
    func askLength(title: String, text: String, units: [String], selected: Int) -> (Double, Int)? {
        NSApp.activate(ignoringOtherApps: true)
        let a = NSAlert()
        a.messageText = title
        a.informativeText = text
        a.addButton(withTitle: "OK")
        a.addButton(withTitle: "Cancel")
        let box = NSView(frame: NSRect(x: 0, y: 0, width: 220, height: 26))
        let field = NSTextField(frame: NSRect(x: 0, y: 1, width: 120, height: 24))
        field.placeholderString = "Length"
        let pop = NSPopUpButton(frame: NSRect(x: 128, y: 0, width: 90, height: 26), pullsDown: false)
        pop.addItems(withTitles: units)
        pop.selectItem(at: selected)
        box.addSubview(field)
        box.addSubview(pop)
        a.accessoryView = box
        a.window.initialFirstResponder = field
        while true {
            guard a.runModal() == .alertFirstButtonReturn else { return nil }
            let s = field.stringValue.trimmingCharacters(in: .whitespaces).replacingOccurrences(of: ",", with: ".")
            if let v = Double(s), v > 0, v.isFinite { return (v, pop.indexOfSelectedItem) }
            a.informativeText = text + "\n\nType a number greater than 0."
        }
    }

    @objc func showAbout() {
        let credits = NSMutableAttributedString(
            string: "Free software, provided \"as is\", without warranty of any kind. " +
                    "Don't rely on its measurements where an error could cause harm or loss.\n" +
                    "This app collects no personal data and never connects to the internet.\n\n",
            attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.labelColor])
        credits.append(NSAttributedString(string: License.text,
            attributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]))
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Screen Ruler",
            .applicationVersion: "1.0",
            .version: "",
            .credits: credits,
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): "© 2026 Mauricio Mendivelso · MIT License",
        ])
    }

    func buildMenu() {
        let main = NSMenu()

        let appItem = NSMenuItem()
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About Screen Ruler", action: #selector(showAbout), keyEquivalent: "").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Hide Screen Ruler", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Quit Screen Ruler", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu
        main.addItem(appItem)

        let toolsItem = NSMenuItem()
        let toolsMenu = NSMenu(title: "Tools")
        toolsMenu.addItem(withTitle: "New Ruler", action: #selector(addRuler), keyEquivalent: "n").target = self
        let np = toolsMenu.addItem(withTitle: "New Protractor", action: #selector(addProtractor), keyEquivalent: "n")
        np.keyEquivalentModifierMask = [.command, .shift]
        np.target = self
        toolsItem.submenu = toolsMenu
        main.addItem(toolsItem)

        let winItem = NSMenuItem()
        let win = NSMenu(title: "Window")
        win.addItem(withTitle: "Show Panel", action: #selector(showPanel), keyEquivalent: "0").target = self
        win.addItem(withTitle: "Minimize", action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        winItem.submenu = win
        main.addItem(winItem)

        NSApp.mainMenu = main
    }

    func validateMenuItem(_ item: NSMenuItem) -> Bool {
        if item.action == #selector(addRuler) { return rulers.count < 3 }
        if item.action == #selector(addProtractor) { return protractor == nil }
        return true
    }

    // Closing the panel quits; the tools go with it and come back where they were next time
    func windowWillClose(_ notification: Notification) {
        if (notification.object as? NSWindow) === panelWindow { NSApp.terminate(nil) }
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        showPanel()
        return true
    }

    func applicationWillTerminate(_ notification: Notification) { changed() }
}

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}
if args.count == 2 && args[1] == "--selftest" {
    let fails = Measure.selfTest()
    print(fails == 0 ? "Self-test passed" : "Self-test: \(fails) failed")
    exit(Int32(fails))
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
