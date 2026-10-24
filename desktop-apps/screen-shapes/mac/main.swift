// Screen Shapes for macOS: lines, rectangles, circles and guide lines that float over everything on screen
// and measure what's under them (twin of ../windows/Program.cs). Single-file AppKit app; build.sh makes
// Screen Shapes.app and a .dmg. The shapes come from the Image Measure web tool (tools/physics/lab-tools).
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
    static let lcdDim = rgb(0x3e, 0x62, 0x45)
    static let ink = rgb(0x1e, 0x1e, 0x22)
    static let marker = rgb(0xd0, 0x1c, 0x1c)

    // Shape colors: the Image Measure web tool's, then a few more
    static let palette: [(String, String)] = [
        ("Sky", "38bdf8"), ("Violet", "a78bfa"), ("Green", "34d399"), ("Pink", "f472b6"), ("Orange", "fb923c"),
        ("Yellow", "facc15"), ("Red", "ef4444"), ("Blue", "3b82f6"), ("Black", "1e1e22"), ("White", "ffffff"),
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

// MARK: - Units (same rules as the Windows app and Screen Ruler; checked by --selftest)

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

enum Measure {
    static func fixed(_ v: Double, _ decimals: Int) -> String { String(format: "%.\(decimals)f", v) }

    // Decimals for a reading: px whole, cm 2, in 3, scaled units 4 significant figures
    static func decimals(_ v: Double, _ unit: Unit) -> Int {
        switch unit {
        case .px: return 0
        case .cm: return 2
        case .inch: return 3
        default: return v == 0 ? 2 : min(6, max(0, 3 - Int(floor(log10(abs(v))))))
        }
    }

    static func number(_ v: Double, _ unit: Unit) -> String { fixed(v, decimals(v, unit)) }
    static func format(_ v: Double, _ unit: Unit) -> String { number(v, unit) + " " + unit.symbol }
    static func formatArea(_ v: Double, _ unit: Unit) -> String { number(v, unit) + " " + unit.symbol + "²" }
    static func angle(_ a: Double) -> String { fixed(a, 1) + "°" }

    static func norm180(_ a: Double) -> Double {
        var r = a.truncatingRemainder(dividingBy: 360)
        if r <= -180 { r += 360 }
        if r > 180 { r -= 360 }
        return r
    }

    // Distance from p to the segment ab
    static func segmentDistance(_ p: NSPoint, _ a: NSPoint, _ b: NSPoint) -> CGFloat {
        let dx = b.x - a.x, dy = b.y - a.y, len2 = dx * dx + dy * dy
        let t = len2 == 0 ? 0 : max(0, min(1, ((p.x - a.x) * dx + (p.y - a.y) * dy) / len2))
        return hypot(p.x - (a.x + t * dx), p.y - (a.y + t * dy))
    }

    static func selfTest() -> Int {
        var fails = 0
        func check(_ ok: Bool, _ what: String) { if !ok { fails += 1; print("FAIL: \(what)") } }
        func near(_ a: Double, _ b: Double) -> Bool { abs(a - b) < 1e-9 }
        check(format(12.3456, .cm) == "12.35 cm", "format cm")
        check(format(3.14159, .inch) == "3.142 in", "format in")
        check(format(399.6, .px) == "400 px", "format px")
        check(format(1234.56, .skm) == "1235 km", "format km")
        check(format(0.012346, .sm) == "0.01235 m", "format m")
        check(format(5.4321, .scm) == "5.432 cm", "format scaled cm")
        check(format(0, .sm) == "0.00 m", "format zero")
        check(formatArea(12.3456, .cm) == "12.35 cm²", "area cm")
        check(formatArea(1234.6, .px) == "1235 px²", "area px")
        check(formatArea(0.5, .skm) == "0.5000 km²", "area km")
        check(angle(23.44) == "23.4°", "angle")
        check(near(norm180(270), -90), "norm 270")
        check(near(norm180(-180), 180), "norm -180")
        check(near(Double(segmentDistance(NSPoint(x: 5, y: 3), NSPoint(x: 0, y: 0), NSPoint(x: 10, y: 0))), 3), "segment middle")
        check(near(Double(segmentDistance(NSPoint(x: -3, y: 4), NSPoint(x: 0, y: 0), NSPoint(x: 10, y: 0))), 5), "segment end")
        check(near(Double(segmentDistance(NSPoint(x: 1, y: 1), NSPoint(x: 2, y: 2), NSPoint(x: 2, y: 2))), sqrt(2)), "segment point")
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

// MARK: - Shapes

enum Kind: String, CaseIterable {
    case line, rect, circle, hline, vline

    var title: String {
        switch self {
        case .line: return "Line"
        case .rect: return "Rect"
        case .circle: return "Circle"
        case .hline: return "H-line"
        case .vline: return "V-line"
        }
    }
    var defaultColor: String {
        switch self {
        case .line: return "38bdf8"
        case .rect: return "a78bfa"
        case .circle: return "34d399"
        case .hline, .vline: return "f472b6"
        }
    }
    var fillable: Bool { self == .rect || self == .circle }
}

enum Part: Equatable { case none, body, handle(Int) }

// What a shape measures, in screen points (lengths) or points² (areas)
enum Dim { case length, area, angle }
struct Quantity { let name: String; let value: Double; let dim: Dim }

final class ShapeWindow: NSWindow {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { false }
}

// One shape in its own see-through window. Points are global screen points (y up):
//   line: a, b = ends · rect: a, b = opposite corners · circle: a = center, b = a point on the rim
//   hline: a = handle (a.y is the line) · vline: a = handle (a.x is the line)
final class Shape: NSObject {
    let kind: Kind
    let number: Int
    var a = NSPoint.zero, b = NSPoint.zero
    var color: NSColor
    var fill = true
    var gap: Double?              // H/V lines: distance to the nearest line of the same kind above / to the left
    var window: ShapeWindow!
    var view: ShapeView!
    weak var app: AppDelegate!
    var grabbed: Part = .none
    var grabMouse = NSPoint.zero, grabA = NSPoint.zero, grabB = NSPoint.zero, grabFixed = NSPoint.zero

    static let handleR: CGFloat = 5, near: CGFloat = 6
    let fLabel = Skin.font("Helvetica", 11, bold: true)

    init(kind: Kind, number: Int) {
        self.kind = kind
        self.number = number
        self.color = Skin.hex(kind.defaultColor)!
    }

    var name: String { "\(kind.title) \(number)" }
    var screen: NSScreen { Screens.screen(at: a) }
    var radius: CGFloat { hypot(b.x - a.x, b.y - a.y) }
    var box: NSRect { NSRect(x: min(a.x, b.x), y: min(a.y, b.y), width: abs(b.x - a.x), height: abs(b.y - a.y)) }

    var corners: [NSPoint] {
        let r = box
        return [NSPoint(x: r.minX, y: r.maxY), NSPoint(x: r.maxX, y: r.maxY), NSPoint(x: r.maxX, y: r.minY), NSPoint(x: r.minX, y: r.minY)]
    }

    var handles: [NSPoint] {
        switch kind {
        case .line, .circle: return [a, b]
        case .rect: return corners
        case .hline, .vline: return [a]
        }
    }

    // The area the shape itself covers (not its label)
    var outline: NSRect {
        switch kind {
        case .line, .rect: return box
        case .circle: let r = radius; return NSRect(x: a.x - r, y: a.y - r, width: 2 * r, height: 2 * r)
        case .hline: let f = screen.frame; return NSRect(x: f.minX, y: a.y, width: f.width, height: 0)
        case .vline: let f = screen.frame; return NSRect(x: a.x, y: f.minY, width: 0, height: f.height)
        }
    }

    // MARK: Measurements

    func quantities() -> [Quantity] {
        switch kind {
        case .line:
            let ang = atan2(Double(b.y - a.y), Double(b.x - a.x)) * 180 / .pi
            return [Quantity(name: "length", value: Double(hypot(b.x - a.x, b.y - a.y)), dim: .length),
                    Quantity(name: "angle", value: Measure.norm180(ang), dim: .angle)]
        case .rect:
            let r = box
            return [Quantity(name: "width", value: Double(r.width), dim: .length),
                    Quantity(name: "height", value: Double(r.height), dim: .length),
                    Quantity(name: "area", value: Double(r.width * r.height), dim: .area)]
        case .circle:
            let r = Double(radius)
            return [Quantity(name: "radius", value: r, dim: .length),
                    Quantity(name: "area", value: .pi * r * r, dim: .area)]
        case .hline:
            var q = [Quantity(name: "y", value: Double(screen.frame.maxY - a.y), dim: .length)]
            if let g = gap { q.append(Quantity(name: "gap", value: g, dim: .length)) }
            return q
        case .vline:
            var q = [Quantity(name: "x", value: Double(a.x - screen.frame.minX), dim: .length)]
            if let g = gap { q.append(Quantity(name: "gap", value: g, dim: .length)) }
            return q
        }
    }

    // Short readings for the label and the panel
    func readouts() -> [String] {
        let u = app.unitPoints(at: a), unit = app.unit
        let q = quantities()
        func len(_ v: Double) -> String { Measure.format(v / u, unit) }
        func area(_ v: Double) -> String { Measure.formatArea(v / (u * u), unit) }
        switch kind {
        case .line: return [len(q[0].value), "∠ " + Measure.angle(q[1].value)]
        case .rect:
            return [Measure.number(q[0].value / u, unit) + " × " + len(q[1].value), "A " + area(q[2].value)]
        case .circle: return ["r " + len(q[0].value), "A " + area(q[1].value)]
        case .hline, .vline:
            var s = [(kind == .hline ? "y " : "x ") + len(q[0].value)]
            if q.count > 1 { s.append("Δ " + len(q[1].value)) }
            return s
        }
    }

    var labelText: String { ([name] + readouts()).joined(separator: "   ") }

    // MARK: Layout

    var labelRect: NSRect {
        let sz = Skin.size(labelText, fLabel)
        let w = sz.width + 12, h: CGFloat = 18
        let c: NSPoint
        switch kind {
        case .line: c = NSPoint(x: (a.x + b.x) / 2, y: max(a.y, b.y) + 16)
        case .rect: c = NSPoint(x: box.midX, y: box.maxY + 16)
        case .circle: c = NSPoint(x: a.x, y: a.y + radius + 16)
        case .hline: c = NSPoint(x: a.x + 14 + w / 2, y: a.y + 15)
        case .vline: c = NSPoint(x: a.x + 12 + w / 2, y: a.y + 16)
        }
        return NSRect(x: c.x - w / 2, y: c.y - h / 2, width: w, height: h)
    }

    func makeWindow() {
        window = ShapeWindow(contentRect: NSRect(x: 0, y: 0, width: 10, height: 10), styleMask: [.borderless],
                             backing: .buffered, defer: false)
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = false
        window.level = .floating
        window.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary]
        window.isReleasedWhenClosed = false
        window.acceptsMouseMovedEvents = true
        view = ShapeView(shape: self)
        window.contentView = view
        layout()
        window.orderFrontRegardless()
    }

    func layout() {
        let f = outline.insetBy(dx: -12, dy: -12).union(labelRect.insetBy(dx: -2, dy: -2)).integral
        window.setFrame(f, display: false)
        view.needsDisplay = true
    }

    func close() { window.orderOut(nil); window.close() }

    // MARK: Hit testing (global points)

    // The part under p, and whether it's only the inside of a filled shape (a weak hit,
    // which gives way to another shape's outline, handle or label at the same point)
    func hit(_ p: NSPoint) -> Part { hitTest(p).0 }

    func hitTest(_ p: NSPoint) -> (Part, weak: Bool) {
        let part = strongHit(p)
        if part != .none { return (part, false) }
        guard fill && kind.fillable else { return (.none, false) }
        let inside = kind == .rect ? box.contains(p) : hypot(p.x - a.x, p.y - a.y) < radius
        return (inside ? .body : .none, true)
    }

    func strongHit(_ p: NSPoint) -> Part {
        for (i, h) in handles.enumerated() where hypot(p.x - h.x, p.y - h.y) <= Shape.handleR + 4 {
            if kind == .circle && i == 0 || kind == .hline || kind == .vline { return .body }
            return .handle(i)
        }
        if labelRect.contains(p) { return .body }
        let n = Shape.near
        switch kind {
        case .line:
            return Measure.segmentDistance(p, a, b) <= n ? .body : .none
        case .rect:
            let r = box
            if r.insetBy(dx: -n, dy: -n).contains(p) && !r.insetBy(dx: n, dy: n).contains(p) { return .body }
        case .circle:
            let d = hypot(p.x - a.x, p.y - a.y)
            if abs(d - radius) <= n { return .body }
        case .hline:
            if abs(p.y - a.y) <= n && screen.frame.contains(NSPoint(x: p.x, y: a.y)) { return .body }
        case .vline:
            if abs(p.x - a.x) <= n && screen.frame.contains(NSPoint(x: a.x, y: p.y)) { return .body }
        }
        return .none
    }

    // MARK: Dragging

    func begin(_ part: Part, _ g: NSPoint) {
        grabbed = part
        grabMouse = g
        grabA = a
        grabB = b
        if kind == .rect, case .handle(let i) = part { grabFixed = corners[(i + 2) % 4] }
    }

    func drag(_ g: NSPoint, shift: Bool) {
        let dx = g.x - grabMouse.x, dy = g.y - grabMouse.y
        switch grabbed {
        case .body:
            a = NSPoint(x: grabA.x + dx, y: grabA.y + dy)
            b = NSPoint(x: grabB.x + dx, y: grabB.y + dy)
        case .handle(let i):
            let p = NSPoint(x: (i == 0 ? grabA : grabB).x + dx, y: (i == 0 ? grabA : grabB).y + dy)
            switch kind {
            case .line:
                var q = p
                if shift {      // 15° steps about the other end, keeping the length
                    let o = i == 0 ? b : a
                    let len = hypot(q.x - o.x, q.y - o.y)
                    let ang = (atan2(Double(q.y - o.y), Double(q.x - o.x)) * 180 / .pi / 15).rounded() * 15 * .pi / 180
                    q = NSPoint(x: o.x + len * CGFloat(cos(ang)), y: o.y + len * CGFloat(sin(ang)))
                }
                if i == 0 { a = q } else { b = q }
            case .rect:
                var q = NSPoint(x: grabCorner(i).x + dx, y: grabCorner(i).y + dy)
                if shift {      // a square
                    let s = max(abs(q.x - grabFixed.x), abs(q.y - grabFixed.y))
                    q = NSPoint(x: grabFixed.x + (q.x < grabFixed.x ? -s : s), y: grabFixed.y + (q.y < grabFixed.y ? -s : s))
                }
                a = grabFixed
                b = q
            case .circle:
                b = p
            default: break
            }
        case .none: break
        }
        layout()
    }

    // Where corner i was when the drag began
    func grabCorner(_ i: Int) -> NSPoint {
        let r = NSRect(x: min(grabA.x, grabB.x), y: min(grabA.y, grabB.y), width: abs(grabB.x - grabA.x), height: abs(grabB.y - grabA.y))
        return [NSPoint(x: r.minX, y: r.maxY), NSPoint(x: r.maxX, y: r.maxY), NSPoint(x: r.maxX, y: r.minY), NSPoint(x: r.minX, y: r.minY)][i]
    }

    func nudge(_ dx: CGFloat, _ dy: CGFloat) {
        a.x += dx; a.y += dy; b.x += dx; b.y += dy
        layout()
        app.changed()
    }

    // MARK: Drawing (global points; the view shifts the origin)

    func stroke(_ p: NSBezierPath, dashed: Bool = false) {
        p.lineJoinStyle = .round
        p.lineCapStyle = .round
        if dashed { p.setLineDash([5, 4], count: 2, phase: 0) }
        NSColor(white: 0, alpha: 0.35).setStroke()      // a dark edge, so any color shows on any background
        p.lineWidth = 4
        p.stroke()
        color.setStroke()
        p.lineWidth = 2
        p.stroke()
    }

    func drawLocal() {
        switch kind {
        case .line:
            let p = NSBezierPath(); p.move(to: a); p.line(to: b); stroke(p)
        case .rect:
            let p = NSBezierPath(rect: box)
            if fill { color.withAlphaComponent(0.14).setFill(); p.fill() }
            stroke(p)
        case .circle:
            let p = NSBezierPath(ovalIn: outline)
            if fill { color.withAlphaComponent(0.14).setFill(); p.fill() }
            stroke(p)
            let r = NSBezierPath(); r.move(to: a); r.line(to: b)
            color.setStroke(); r.lineWidth = 1; r.setLineDash([5, 4], count: 2, phase: 0); r.stroke()
        case .hline:
            let f = screen.frame
            let p = NSBezierPath(); p.move(to: NSPoint(x: f.minX, y: a.y)); p.line(to: NSPoint(x: f.maxX, y: a.y)); stroke(p)
        case .vline:
            let f = screen.frame
            let p = NSBezierPath(); p.move(to: NSPoint(x: a.x, y: f.minY)); p.line(to: NSPoint(x: a.x, y: f.maxY)); stroke(p)
        }
        for h in handles {
            let r = Shape.handleR
            let d = NSBezierPath(ovalIn: NSRect(x: h.x - r, y: h.y - r, width: 2 * r, height: 2 * r))
            NSColor(white: 1, alpha: 0.95).setFill()
            d.fill()
            color.setStroke()
            d.lineWidth = 2
            d.stroke()
        }
        let lr = labelRect
        let bg = NSBezierPath(roundedRect: lr, xRadius: 4, yRadius: 4)
        NSColor(white: 1, alpha: 0.9).setFill()
        bg.fill()
        color.setStroke()
        bg.lineWidth = 1.5
        bg.stroke()
        Skin.draw(labelText, fLabel, Skin.ink, in: lr)
    }

    // MARK: Menu

    func menu() -> NSMenu {
        let m = NSMenu()
        let colors = NSMenuItem(title: "Color", action: nil, keyEquivalent: "")
        let cm = NSMenu()
        let current = Skin.hexString(color)
        for (title, hex) in Skin.palette {
            let it = cm.addItem(withTitle: title, action: #selector(pickColor(_:)), keyEquivalent: "")
            it.target = self
            it.representedObject = hex
            it.state = hex == current ? .on : .off
            it.image = swatch(Skin.hex(hex)!)
        }
        cm.addItem(.separator())
        cm.addItem(withTitle: "Other Color…", action: #selector(otherColor), keyEquivalent: "").target = self
        colors.submenu = cm
        m.addItem(colors)
        if kind.fillable {
            let f = m.addItem(withTitle: "Fill", action: #selector(toggleFill), keyEquivalent: "")
            f.target = self
            f.state = fill ? .on : .off
        }
        m.addItem(.separator())
        m.addItem(app.unitsItem())
        if kind == .line {
            m.addItem(withTitle: "Set Scale from This Line…", action: #selector(setScale), keyEquivalent: "").target = self
            m.addItem(withTitle: "Correct Screen Size from This Line…", action: #selector(correctSize), keyEquivalent: "").target = self
        }
        if Screens.fixed(screen) != nil {
            m.addItem(withTitle: "Use the Display's Reported Size", action: #selector(resetSize), keyEquivalent: "").target = self
        }
        m.addItem(.separator())
        m.addItem(withTitle: "Duplicate \(name)", action: #selector(duplicate), keyEquivalent: "").target = self
        m.addItem(withTitle: "Remove \(name)", action: #selector(remove), keyEquivalent: "").target = self
        m.addItem(.separator())
        m.addItem(withTitle: "Show Panel", action: #selector(AppDelegate.showPanel), keyEquivalent: "").target = app
        m.addItem(withTitle: "About Screen Shapes", action: #selector(AppDelegate.showAbout), keyEquivalent: "").target = app
        m.addItem(withTitle: "Quit Screen Shapes", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "")
        return m
    }

    func swatch(_ c: NSColor) -> NSImage {
        NSImage(size: NSSize(width: 12, height: 12), flipped: false) { r in
            c.setFill()
            NSBezierPath(roundedRect: r.insetBy(dx: 0.5, dy: 0.5), xRadius: 2, yRadius: 2).fill()
            NSColor(white: 0, alpha: 0.4).setStroke()
            NSBezierPath(roundedRect: r.insetBy(dx: 0.5, dy: 0.5), xRadius: 2, yRadius: 2).stroke()
            return true
        }
    }

    @objc func pickColor(_ sender: NSMenuItem) {
        guard let hex = sender.representedObject as? String, let c = Skin.hex(hex) else { return }
        color = c
        view.needsDisplay = true
        app.changed()
    }

    @objc func otherColor() {
        let p = NSColorPanel.shared
        p.showsAlpha = false
        p.color = color
        p.setTarget(self)
        p.setAction(#selector(colorPanelChanged(_:)))
        p.level = .modalPanel
        NSApp.activate(ignoringOtherApps: true)
        p.orderFront(nil)
    }

    @objc func colorPanelChanged(_ sender: NSColorPanel) {
        color = sender.color.usingColorSpace(.sRGB) ?? sender.color
        view.needsDisplay = true
        app.changed()
    }

    @objc func toggleFill() { fill.toggle(); view.needsDisplay = true; app.changed() }

    @objc func setScale() {
        let len = Double(hypot(b.x - a.x, b.y - a.y))
        guard len >= 10 else { return }
        let units: [Unit] = [.sm, .skm, .scm]
        let px = Measure.format(len * Double(screen.backingScaleFactor), .px)
        guard let (v, i) = app.askLength(title: "Set the scale",
                                         text: "Lay \(name) along a length you know (a map's scale bar, or an object of known size " +
                                               "in a photo). It is \(px) long on the screen. How long is it in reality?",
                                         units: units.map { $0.symbol }, selected: app.unit.scaled ? units.firstIndex(of: app.unit) ?? 0 : 0)
        else { return }
        app.metersPerPoint = v * units[i].meters / len
        app.unit = units[i]
        app.changed()
    }

    @objc func correctSize() {
        let len = Double(hypot(b.x - a.x, b.y - a.y))
        guard len >= 10 else { return }
        let px = Measure.format(len * Double(screen.backingScaleFactor), .px)
        guard let (v, i) = app.askLength(title: "Correct the screen size",
                                         text: "Hold a real ruler (or a bank card, 8.56 cm wide) against the screen along \(name). " +
                                               "It is \(px) long on the screen. How long is it on your real ruler?",
                                         units: ["cm", "in"], selected: app.unit == .inch ? 1 : 0) else { return }
        UserDefaults.standard.set(len / (v * (i == 0 ? 10 : 25.4)), forKey: Screens.fixKey(screen))
        app.changed()
    }

    @objc func resetSize() {
        UserDefaults.standard.removeObject(forKey: Screens.fixKey(screen))
        app.changed()
    }

    @objc func duplicate() { app.duplicate(self) }
    @objc func remove() { app.remove(self) }

    // MARK: Saving

    func serialize() -> String {
        ([kind.rawValue, "\(number)"] + [a.x, a.y, b.x, b.y].map { String(Double($0)) } + [Skin.hexString(color), fill ? "1" : "0"])
            .joined(separator: ",")
    }

    static func parse(_ s: String) -> Shape? {
        let f = s.split(separator: ",").map(String.init)
        guard f.count == 8, let k = Kind(rawValue: f[0]), let n = Int(f[1]), let ax = Double(f[2]), let ay = Double(f[3]),
              let bx = Double(f[4]), let by = Double(f[5]), let c = Skin.hex(f[6]) else { return nil }
        let sh = Shape(kind: k, number: n)
        sh.a = NSPoint(x: ax, y: ay)
        sh.b = NSPoint(x: bx, y: by)
        sh.color = c
        sh.fill = f[7] == "1"
        return sh
    }
}

final class ShapeView: NSView {
    unowned let shape: Shape
    var tracking: NSTrackingArea?

    init(shape: Shape) {
        self.shape = shape
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
        t.translateX(by: -w.frame.minX, yBy: -w.frame.minY)
        t.concat()
        shape.drawLocal()
        NSGraphicsContext.restoreGraphicsState()
    }

    func cursor(_ p: Part) -> NSCursor {
        switch p {
        case .none: return .arrow
        case .body:
            if shape.kind == .hline { return .resizeUpDown }
            if shape.kind == .vline { return .resizeLeftRight }
            return shape.grabbed == .body ? .closedHand : .openHand
        case .handle: return .crosshair
        }
    }

    override func mouseMoved(with e: NSEvent) { cursor(shape.hit(NSEvent.mouseLocation)).set() }

    override func mouseDown(with e: NSEvent) {
        let g = NSEvent.mouseLocation
        let p = shape.hit(g)
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(self)
        guard p != .none else { return }
        shape.begin(p, g)
        cursor(p).set()
    }

    override func mouseDragged(with e: NSEvent) {
        guard shape.grabbed != .none else { return }
        shape.drag(NSEvent.mouseLocation, shift: e.modifierFlags.contains(.shift))
        shape.app.changed(save: false)
    }

    override func mouseUp(with e: NSEvent) {
        guard shape.grabbed != .none else { return }
        shape.grabbed = .none
        shape.app.changed()
        cursor(shape.hit(NSEvent.mouseLocation)).set()
    }

    override func menu(for event: NSEvent) -> NSMenu? { shape.menu() }

    override func keyDown(with e: NSEvent) {
        let step: CGFloat = e.modifierFlags.contains(.shift) ? 10 : 1
        switch e.keyCode {
        case 123: shape.nudge(-step, 0)      // ←
        case 124: shape.nudge(step, 0)       // →
        case 125: shape.nudge(0, -step)      // ↓
        case 126: shape.nudge(0, step)       // ↑
        case 51, 117: shape.remove()         // delete
        default: super.keyDown(with: e)
        }
    }
}

// MARK: - The panel: add shapes, choose units and read every measurement

final class PanelView: NSView {
    enum Hit: Equatable { case none, add(Kind), about, units, copy, clear, row(Int), close(Int) }

    weak var app: AppDelegate!
    var pressed: Hit = .none
    static let width: CGFloat = 460, rowH: CGFloat = 22, top: CGFloat = 78

    let fButton = Skin.font("Tahoma", 12, bold: true)
    let fName = Skin.font("Tahoma", 12, bold: true)
    let fValue = Skin.font("Menlo", 12, bold: true)

    override var isFlipped: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    static let addButtons: [(Kind, String, CGFloat)] = [
        (.line, "+ Line", 70), (.rect, "+ Rect", 70), (.circle, "+ Circle", 80), (.hline, "+ H-line", 80), (.vline, "+ V-line", 80)]

    func rcAdd(_ k: Kind) -> NSRect {
        var x: CGFloat = 8
        for (kind, _, w) in PanelView.addButtons {
            if kind == k { return NSRect(x: x, y: 8, width: w, height: 26) }
            x += w + 6
        }
        return .zero
    }
    var rcAbout: NSRect { NSRect(x: bounds.width - 34, y: 8, width: 26, height: 26) }
    var rcUnits: NSRect { NSRect(x: 8, y: 40, width: 190, height: 26) }
    var rcCopy: NSRect { NSRect(x: bounds.width - 8 - 70 - 6 - 84, y: 40, width: 70, height: 26) }
    var rcClear: NSRect { NSRect(x: bounds.width - 8 - 84, y: 40, width: 84, height: 26) }
    var rcLcd: NSRect { NSRect(x: 8, y: PanelView.top, width: bounds.width - 16, height: CGFloat(max(1, app.shapes.count)) * PanelView.rowH + 8) }
    func rcRow(_ i: Int) -> NSRect { NSRect(x: rcLcd.minX + 4, y: rcLcd.minY + 4 + CGFloat(i) * PanelView.rowH, width: rcLcd.width - 8, height: PanelView.rowH) }
    func rcClose(_ i: Int) -> NSRect { let r = rcRow(i); return NSRect(x: r.maxX - 20, y: r.minY + 2, width: 18, height: 18) }

    var wantedHeight: CGFloat { PanelView.top + CGFloat(max(1, app.shapes.count)) * PanelView.rowH + 8 + 8 }

    func hit(_ p: NSPoint) -> Hit {
        for (k, _, _) in PanelView.addButtons where rcAdd(k).contains(p) { return .add(k) }
        if rcAbout.contains(p) { return .about }
        if rcUnits.contains(p) { return .units }
        if rcCopy.contains(p) { return .copy }
        if rcClear.contains(p) { return .clear }
        for i in app.shapes.indices where rcRow(i).contains(p) { return rcClose(i).contains(p) ? .close(i) : .row(i) }
        return .none
    }

    func enabled(_ h: Hit) -> Bool {
        switch h {
        case .add: return app.shapes.count < AppDelegate.maxShapes
        case .copy, .clear: return !app.shapes.isEmpty
        default: return true
        }
    }

    override func mouseDown(with e: NSEvent) {
        let h = hit(convert(e.locationInWindow, from: nil))
        pressed = enabled(h) ? h : .none
        needsDisplay = true
        if pressed == .units {      // a pop-up menu under the button
            let r = rcUnits
            app.unitsMenu().popUp(positioning: nil, at: NSPoint(x: r.minX, y: r.maxY + 2), in: self)
            pressed = .none
            needsDisplay = true
        }
    }

    override func mouseUp(with e: NSEvent) {
        let h = hit(convert(e.locationInWindow, from: nil)), was = pressed
        pressed = .none
        needsDisplay = true
        guard h == was else { return }
        switch h {
        case .add(let k): app.add(k)
        case .about: app.showAbout()
        case .copy: app.copyTable()
        case .clear: app.clearAll()
        case .row(let i): let s = app.shapes[i]; s.window.orderFrontRegardless(); s.window.makeKey()
        case .close(let i): app.remove(app.shapes[i])
        case .units, .none: break
        }
    }

    func button(_ r: NSRect, _ h: Hit, _ label: String, left: Bool = false) {
        let down = pressed == h
        Skin.face.setFill()
        r.fill()
        if down { Skin.sunken(r) } else { Skin.raised(r) }
        let t = down ? r.offsetBy(dx: 1, dy: 1) : r
        Skin.draw(label, fButton, enabled(h) ? Skin.text : Skin.muted, in: left ? t.insetBy(dx: 8, dy: 0) : t, left: left)
    }

    override func draw(_ dirtyRect: NSRect) {
        Skin.face.setFill()
        bounds.fill()
        for (k, label, _) in PanelView.addButtons { button(rcAdd(k), .add(k), label) }
        button(rcAbout, .about, "?")
        button(rcUnits, .units, "Units: " + app.unit.symbol + (app.unit.scaled ? " (scaled)" : "") + "  ▾", left: true)
        button(rcCopy, .copy, "Copy")
        button(rcClear, .clear, "Clear all")

        let lcd = rcLcd
        Skin.lcd.setFill()
        lcd.fill()
        Skin.sunken(lcd)
        if app.shapes.isEmpty {
            Skin.draw("Add a line or a shape", fName, Skin.lcdDim, in: rcRow(0))
            return
        }
        for (i, s) in app.shapes.enumerated() {
            let r = rcRow(i)
            let sw = NSRect(x: r.minX + 4, y: r.midY - 5, width: 10, height: 10)
            s.color.setFill(); sw.fill()
            Skin.lcdDigit.setStroke(); NSBezierPath(rect: sw.insetBy(dx: 0.5, dy: 0.5)).stroke()
            Skin.draw(s.name, fName, Skin.lcdDigit, in: NSRect(x: r.minX + 20, y: r.minY, width: 90, height: r.height), left: true)
            Skin.draw(s.readouts().joined(separator: "   "), fValue, Skin.lcdDigit,
                      in: NSRect(x: r.minX + 100, y: r.minY, width: r.width - 100 - 26, height: r.height), right: true)
            Skin.draw("×", Skin.font("Tahoma", 15, bold: true), pressed == .close(i) ? Skin.marker : Skin.lcdDim, in: rcClose(i))
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

// MARK: - App icon (used by build.sh: ScreenShapes --write-iconset <dir>)

enum IconArt {
    // A violet rectangle, a green circle and a sky-blue line
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        Skin.face.setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()
        let w = max(1.5, s * 0.035)
        func shape(_ p: NSBezierPath, _ hex: String, fill: Bool) {
            let c = Skin.hex(hex)!
            if fill { c.withAlphaComponent(0.3).setFill(); p.fill() }
            c.setStroke()
            p.lineWidth = w
            p.stroke()
        }
        shape(NSBezierPath(rect: NSRect(x: s * 0.22, y: s * 0.42, width: s * 0.38, height: s * 0.32)), "a78bfa", fill: true)
        shape(NSBezierPath(ovalIn: NSRect(x: s * 0.44, y: s * 0.24, width: s * 0.34, height: s * 0.34)), "34d399", fill: true)
        let l = NSBezierPath()
        l.move(to: NSPoint(x: s * 0.2, y: s * 0.24))
        l.line(to: NSPoint(x: s * 0.8, y: s * 0.78))
        l.lineCapStyle = .round
        shape(l, "38bdf8", fill: false)
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
    static let maxShapes = 20
    var shapes: [Shape] = []
    var panelWindow: NSWindow!
    var panel: PanelView!
    var poll: Timer?
    let d = UserDefaults.standard

    // One unit and one scale for every shape
    var unit: Unit {
        get { let u = Unit(rawValue: d.string(forKey: "unit") ?? "") ?? .cm; return u.scaled && metersPerPoint <= 0 ? .cm : u }
        set { d.set(newValue.rawValue, forKey: "unit") }
    }
    var metersPerPoint: Double {
        get { max(0, d.double(forKey: "metersPerPoint")) }
        set { d.set(newValue, forKey: "metersPerPoint") }
    }

    // Screen points for one unit near p
    func unitPoints(at p: NSPoint) -> Double {
        let s = Screens.screen(at: p)
        switch unit {
        case .cm: return Screens.pointsPerMM(s) * 10
        case .inch: return Screens.pointsPerMM(s) * 25.4
        case .px: return 1 / Double(s.backingScaleFactor)
        default: return unit.meters / metersPerPoint
        }
    }

    func applicationDidFinishLaunching(_ note: Notification) {
        panel = PanelView(frame: NSRect(x: 0, y: 0, width: PanelView.width, height: 140))
        panel.app = self
        panelWindow = NSWindow(contentRect: panel.frame, styleMask: [.titled, .closable, .miniaturizable],
                               backing: .buffered, defer: false)
        panelWindow.title = "Screen Shapes"
        panelWindow.contentView = panel
        panelWindow.delegate = self
        panelWindow.isReleasedWhenClosed = false
        panelWindow.level = .floating
        buildMenu()

        for s in d.stringArray(forKey: "shapes") ?? [] {
            guard shapes.count < AppDelegate.maxShapes, let sh = Shape.parse(s) else { continue }
            attach(sh)
            if !NSScreen.screens.contains(where: { $0.frame.contains(sh.a) }) { place(sh) }
        }

        changed(save: false)
        if !panelWindow.setFrameUsingName("ScreenShapesPanel") {
            let v = NSScreen.main?.visibleFrame ?? .zero
            panelWindow.setFrameTopLeftPoint(NSPoint(x: v.maxX - PanelView.width - 40, y: v.maxY - 40))
        }
        panelWindow.setFrameAutosaveName("ScreenShapesPanel")
        refreshPanel()
        panelWindow.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)

        // Clicks pass through the empty parts of each shape's window to whatever is underneath
        poll = Timer.scheduledTimer(withTimeInterval: 1.0 / 30, repeats: true) { [weak self] _ in self?.updatePassThrough() }
    }

    func updatePassThrough() {
        let g = NSEvent.mouseLocation
        let hits = shapes.map { $0.hitTest(g) }
        let strong = hits.contains { $0.0 != .none && !$0.weak }
        for (s, h) in zip(shapes, hits) {
            let over = s.grabbed != .none || (h.0 != .none && (!h.weak || !strong))
            if s.window.ignoresMouseEvents == over { s.window.ignoresMouseEvents = !over }
        }
    }

    func attach(_ s: Shape) {
        s.app = self
        shapes.append(s)
        s.makeWindow()
    }

    func nextNumber(_ k: Kind) -> Int { (shapes.filter { $0.kind == k }.map { $0.number }.max() ?? 0) + 1 }

    // A new shape on the screen with the mouse: each kind has its own spot, and another
    // of the same kind goes a little further along, so new shapes don't pile up
    func place(_ s: Shape) {
        let v = Screens.screen(at: NSEvent.mouseLocation).visibleFrame
        let n = CGFloat((shapes.filter { $0.kind == s.kind && $0 !== s }.count) % 5)
        let c = NSPoint(x: v.midX, y: v.midY)
        switch s.kind {
        case .line:
            s.a = NSPoint(x: c.x - 150, y: c.y + 170 - 40 * n); s.b = NSPoint(x: c.x + 150, y: s.a.y)
        case .rect:
            s.a = NSPoint(x: c.x - 330 + 30 * n, y: c.y - 90 - 30 * n); s.b = NSPoint(x: s.a.x + 220, y: s.a.y + 150)
        case .circle:
            s.a = NSPoint(x: c.x + 220 + 30 * n, y: c.y - 15 - 30 * n); s.b = NSPoint(x: s.a.x + 90, y: s.a.y)
        case .hline:
            s.a = NSPoint(x: c.x - 40, y: c.y - 170 - 40 * n); s.b = s.a
        case .vline:
            s.a = NSPoint(x: c.x - 420 - 40 * n, y: c.y - 230 + 26 * n); s.b = s.a
        }
        s.layout()
    }

    @objc func addLine() { add(.line) }
    @objc func addRect() { add(.rect) }
    @objc func addCircle() { add(.circle) }
    @objc func addHLine() { add(.hline) }
    @objc func addVLine() { add(.vline) }

    func add(_ k: Kind) {
        guard shapes.count < AppDelegate.maxShapes else { return }
        let s = Shape(kind: k, number: nextNumber(k))
        attach(s)
        place(s)
        changed()
    }

    func duplicate(_ o: Shape) {
        guard shapes.count < AppDelegate.maxShapes else { return }
        let s = Shape(kind: o.kind, number: nextNumber(o.kind))
        s.color = o.color
        s.fill = o.fill
        s.a = NSPoint(x: o.a.x + 24, y: o.a.y - 24)
        s.b = NSPoint(x: o.b.x + 24, y: o.b.y - 24)
        attach(s)
        changed()
    }

    func remove(_ s: Shape) {
        s.close()
        shapes.removeAll { $0 === s }
        changed()
    }

    @objc func clearAll() {
        guard !shapes.isEmpty else { return }
        NSApp.activate(ignoringOtherApps: true)
        let a = NSAlert()
        a.messageText = "Remove all \(shapes.count) shapes?"
        a.addButton(withTitle: "Remove All")
        a.addButton(withTitle: "Cancel")
        guard a.runModal() == .alertFirstButtonReturn else { return }
        for s in shapes { s.close() }
        shapes.removeAll()
        changed()
    }

    @objc func showPanel() { panelWindow.makeKeyAndOrderFront(nil); NSApp.activate(ignoringOtherApps: true) }

    // Something moved or changed: update the gaps and the panel, and remember it
    func changed(save: Bool = true) {
        updateGaps()
        for s in shapes { s.layout() }
        refreshPanel()
        guard save else { return }
        d.set(shapes.map { $0.serialize() }, forKey: "shapes")
    }

    // Each H-line's gap to the nearest one above it, each V-line's to the nearest one on its left
    func updateGaps() {
        for s in shapes where s.kind == .hline {
            let above = shapes.filter { $0.kind == .hline && $0.a.y > s.a.y }.map { $0.a.y }.min()
            s.gap = above.map { Double($0 - s.a.y) }
        }
        for s in shapes where s.kind == .vline {
            let left = shapes.filter { $0.kind == .vline && $0.a.x < s.a.x }.map { $0.a.x }.max()
            s.gap = left.map { Double(s.a.x - $0) }
        }
    }

    func refreshPanel() {
        let h = panel.wantedHeight
        if abs(panelWindow.contentLayoutRect.height - h) > 0.5 {
            let top = panelWindow.frame.maxY
            var f = panelWindow.frameRect(forContentRect: NSRect(x: 0, y: 0, width: PanelView.width, height: h))
            f.origin = NSPoint(x: panelWindow.frame.minX, y: top - f.height)
            panelWindow.setFrame(f, display: true)
        }
        panel.needsDisplay = true
    }

    // The table, tab-separated, ready to paste into a spreadsheet
    @objc func copyTable() {
        var lines = ["Shape\tQuantity\tValue\tUnit"]
        for s in shapes {
            let u = unitPoints(at: s.a)
            for q in s.quantities() {
                switch q.dim {
                case .length: lines.append("\(s.name)\t\(q.name)\t\(Measure.number(q.value / u, unit))\t\(unit.symbol)")
                case .area: lines.append("\(s.name)\t\(q.name)\t\(Measure.number(q.value / (u * u), unit))\t\(unit.symbol)²")
                case .angle: lines.append("\(s.name)\t\(q.name)\t\(Measure.fixed(q.value, 1))\t°")
                }
            }
        }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(lines.joined(separator: "\n") + "\n", forType: .string)
    }

    // MARK: Units

    func unitsMenu() -> NSMenu {
        let m = NSMenu()
        for u in Unit.allCases {
            if u == .sm { m.addItem(.separator()) }
            let it = m.addItem(withTitle: u.menuTitle, action: #selector(pickUnit(_:)), keyEquivalent: "")
            it.target = self
            it.representedObject = u.rawValue
            it.state = u == unit ? .on : .off
        }
        return m
    }

    func unitsItem() -> NSMenuItem {
        let it = NSMenuItem(title: "Units", action: nil, keyEquivalent: "")
        it.submenu = unitsMenu()
        return it
    }

    @objc func pickUnit(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String, let u = Unit(rawValue: raw) else { return }
        if u.scaled && metersPerPoint <= 0 {
            message("Set a scale first",
                    "Add a line (+ Line) and lay it along a length you know, such as a map's scale bar or an object of known " +
                    "size in a photo. Then right-click the line and choose Set Scale from This Line….")
            return
        }
        unit = u
        changed()
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
            .applicationName: "Screen Shapes",
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
        appMenu.addItem(withTitle: "About Screen Shapes", action: #selector(showAbout), keyEquivalent: "").target = self
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Hide Screen Shapes", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Quit Screen Shapes", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu
        main.addItem(appItem)

        let shapesItem = NSMenuItem()
        let shapesMenu = NSMenu(title: "Shapes")
        shapesMenu.addItem(withTitle: "New Line", action: #selector(addLine), keyEquivalent: "1").target = self
        shapesMenu.addItem(withTitle: "New Rectangle", action: #selector(addRect), keyEquivalent: "2").target = self
        shapesMenu.addItem(withTitle: "New Circle", action: #selector(addCircle), keyEquivalent: "3").target = self
        shapesMenu.addItem(withTitle: "New Horizontal Line", action: #selector(addHLine), keyEquivalent: "4").target = self
        shapesMenu.addItem(withTitle: "New Vertical Line", action: #selector(addVLine), keyEquivalent: "5").target = self
        shapesMenu.addItem(.separator())
        let copy = shapesMenu.addItem(withTitle: "Copy Table", action: #selector(copyTable), keyEquivalent: "c")
        copy.keyEquivalentModifierMask = [.command, .shift]
        copy.target = self
        shapesMenu.addItem(withTitle: "Remove All…", action: #selector(clearAll), keyEquivalent: "").target = self
        shapesItem.submenu = shapesMenu
        main.addItem(shapesItem)

        let winItem = NSMenuItem()
        let win = NSMenu(title: "Window")
        win.addItem(withTitle: "Show Panel", action: #selector(showPanel), keyEquivalent: "0").target = self
        win.addItem(withTitle: "Minimize", action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        winItem.submenu = win
        main.addItem(winItem)

        NSApp.mainMenu = main
    }

    func validateMenuItem(_ item: NSMenuItem) -> Bool {
        switch item.action {
        case #selector(addLine), #selector(addRect), #selector(addCircle), #selector(addHLine), #selector(addVLine):
            return shapes.count < AppDelegate.maxShapes
        case #selector(copyTable), #selector(clearAll):
            return !shapes.isEmpty
        default:
            return true
        }
    }

    // Closing the panel quits; the shapes go with it and come back where they were next time
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
