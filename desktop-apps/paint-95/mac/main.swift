// Paint 95 for macOS: a bitmap painter that looks and works like the Windows 95 Paint.
// AppKit app; the Windows 95 look comes from ../../win95-kit/Win95Kit.swift. build.sh makes "Paint 95.app" and a .dmg.
import AppKit
import UniformTypeIdentifiers

// MARK: - Pixels

struct RGBA: Equatable {
    var r: UInt8, g: UInt8, b: UInt8

    init(_ r: UInt8, _ g: UInt8, _ b: UInt8) { self.r = r; self.g = g; self.b = b }
    init(hex: Int) { self.init(UInt8((hex >> 16) & 255), UInt8((hex >> 8) & 255), UInt8(hex & 255)) }
    init(u32 v: UInt32) { self.init(UInt8(v & 255), UInt8((v >> 8) & 255), UInt8((v >> 16) & 255)) }
    init(_ c: NSColor) {
        let s = c.usingColorSpace(.sRGB) ?? .black
        func q(_ v: CGFloat) -> UInt8 { UInt8(max(0, min(255, (v * 255).rounded()))) }
        self.init(q(s.redComponent), q(s.greenComponent), q(s.blueComponent))
    }

    /// The pixel as stored in memory (RGBA bytes, little-endian), always opaque
    var u32: UInt32 { UInt32(r) | UInt32(g) << 8 | UInt32(b) << 16 | 0xFF00_0000 }
    var cg: CGColor { CGColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: 1) }
    var ns: NSColor { NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: 1) }

    static let white = RGBA(255, 255, 255), black = RGBA(0, 0, 0)
}

/// An RGBA image you can draw on. Coordinates run from the top-left pixel, like Windows.
final class Bitmap {
    let width: Int, height: Int
    let ctx: CGContext
    static let space = CGColorSpace(name: CGColorSpace.sRGB)!

    init(width: Int, height: Int, fill: RGBA?) {
        self.width = max(1, width)
        self.height = max(1, height)
        ctx = CGContext(data: nil, width: self.width, height: self.height, bitsPerComponent: 8, bytesPerRow: self.width * 4,
                        space: Bitmap.space, bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue)!
        ctx.translateBy(x: 0, y: CGFloat(self.height))
        ctx.scaleBy(x: 1, y: -1)
        ctx.setShouldAntialias(false)
        ctx.setShouldSmoothFonts(false)
        ctx.interpolationQuality = .none
        if let fill { self.fill(CGRect(x: 0, y: 0, width: self.width, height: self.height), fill) }
    }

    convenience init(width: Int, height: Int, data: Data) {
        self.init(width: width, height: height, fill: nil)
        data.withUnsafeBytes { _ = memcpy(ctx.data!, $0.baseAddress!, min($0.count, width * height * 4)) }
    }

    /// Any image, flattened onto `background` (pictures have no transparency here, as in Windows 95)
    convenience init(image: CGImage, background: RGBA) {
        self.init(width: image.width, height: image.height, fill: background)
        draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
    }

    var px: UnsafeMutablePointer<UInt32> { ctx.data!.assumingMemoryBound(to: UInt32.self) }
    var data: Data { Data(bytes: ctx.data!, count: width * height * 4) }

    func restore(_ d: Data) { d.withUnsafeBytes { _ = memcpy(ctx.data!, $0.baseAddress!, min($0.count, width * height * 4)) } }
    func contains(_ x: Int, _ y: Int) -> Bool { x >= 0 && y >= 0 && x < width && y < height }
    func get(_ x: Int, _ y: Int) -> UInt32 { px[y * width + x] }
    func plot(_ x: Int, _ y: Int, _ c: UInt32) { if contains(x, y) { px[y * width + x] = c } }
    func image() -> CGImage { ctx.makeImage()! }

    func fill(_ r: CGRect, _ c: RGBA) { ctx.setFillColor(c.cg); ctx.fill(r) }

    /// Draws a CGImage right side up into the top-left-origin coordinates
    func draw(_ img: CGImage, in r: CGRect) {
        ctx.saveGState()
        ctx.translateBy(x: r.minX, y: r.maxY)
        ctx.scaleBy(x: 1, y: -1)
        ctx.draw(img, in: CGRect(origin: .zero, size: r.size))
        ctx.restoreGState()
    }

    func copy() -> Bitmap { Bitmap(width: width, height: height, data: data) }

    func crop(x: Int, y: Int, width w: Int, height h: Int) -> Bitmap {
        let b = Bitmap(width: w, height: h, fill: nil)
        for row in 0..<h { memcpy(b.px + row * w, px + (y + row) * width + x, w * 4) }
        return b
    }

    /// A new bitmap of the given size with this one drawn through `t` on a `fill` background
    func transformed(width w: Int, height h: Int, fill: RGBA?, _ t: CGAffineTransform) -> Bitmap {
        let b = Bitmap(width: w, height: h, fill: fill)
        b.ctx.saveGState()
        b.ctx.concatenate(t)
        b.draw(image(), in: CGRect(x: 0, y: 0, width: width, height: height))
        b.ctx.restoreGState()
        return b
    }

    /// Bresenham line, one pixel wide
    func line(_ x0: Int, _ y0: Int, _ x1: Int, _ y1: Int, _ c: UInt32) {
        Bitmap.walk(x0, y0, x1, y1) { plot($0, $1, c) }
    }

    static func walk(_ x0: Int, _ y0: Int, _ x1: Int, _ y1: Int, _ f: (Int, Int) -> Void) {
        var x = x0, y = y0
        let dx = abs(x1 - x0), dy = -abs(y1 - y0), sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1
        var err = dx + dy
        while true {
            f(x, y)
            if x == x1 && y == y1 { return }
            let e2 = 2 * err
            if e2 >= dy { err += dy; x += sx }
            if e2 <= dx { err += dx; y += sy }
        }
    }

    /// Flood fill of the area of one exact color, like Fill With Color in Windows 95
    func floodFill(_ x: Int, _ y: Int, _ c: UInt32) {
        guard contains(x, y) else { return }
        let target = get(x, y)
        guard target != c else { return }
        var stack = [(x, y)]
        while let (sx, sy) = stack.popLast() {
            var l = sx
            while l > 0 && get(l - 1, sy) == target { l -= 1 }
            var r = sx
            while r < width - 1 && get(r + 1, sy) == target { r += 1 }
            guard get(sx, sy) == target else { continue }
            var upOpen = false, downOpen = false
            for i in l...r {
                px[sy * width + i] = c
                if sy > 0 {
                    let t = get(i, sy - 1) == target
                    if t && !upOpen { stack.append((i, sy - 1)) }
                    upOpen = t
                }
                if sy < height - 1 {
                    let t = get(i, sy + 1) == target
                    if t && !downOpen { stack.append((i, sy + 1)) }
                    downOpen = t
                }
            }
        }
    }

    func invert() {
        for i in 0..<(width * height) where px[i] >> 24 != 0 { px[i] = (~px[i] & 0x00FF_FFFF) | (px[i] & 0xFF00_0000) }
    }

    /// Copy with every pixel of color `c` made transparent (Draw Transparent)
    func clearing(_ c: UInt32) -> Bitmap {
        let b = copy()
        for i in 0..<(width * height) where b.px[i] == c { b.px[i] = 0 }
        return b
    }

    /// Makes every pixel outside `path` transparent (free-form selections)
    func mask(_ path: CGPath) {
        guard let m = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8, bytesPerRow: width,
                                space: CGColorSpaceCreateDeviceGray(), bitmapInfo: CGImageAlphaInfo.none.rawValue) else { return }
        m.translateBy(x: 0, y: CGFloat(height))
        m.scaleBy(x: 1, y: -1)
        m.setShouldAntialias(false)
        m.setFillColor(gray: 1, alpha: 1)
        m.addPath(path)
        m.fillPath()
        let mp = m.data!.assumingMemoryBound(to: UInt8.self)
        for i in 0..<(width * height) where mp[i] < 128 { px[i] = 0 }
    }

    func encoded(as type: UTType) -> Data? {
        let rep = NSBitmapImageRep(cgImage: image())
        switch type {
        case .bmp: return rep.representation(using: .bmp, properties: [:])
        case .jpeg: return rep.representation(using: .jpeg, properties: [.compressionFactor: 0.92])
        case .gif: return rep.representation(using: .gif, properties: [:])
        case .tiff: return rep.representation(using: .tiff, properties: [:])
        default: return rep.representation(using: .png, properties: [:])
        }
    }

    static func load(_ url: URL) throws -> Bitmap {
        let data = try Data(contentsOf: url)
        guard let rep = NSBitmapImageRep(data: data) ?? NSImage(data: data).flatMap({ img in
            img.tiffRepresentation.flatMap { NSBitmapImageRep(data: $0) } }),
              let cg = rep.cgImage else {
            throw NSError(domain: "Paint", code: 1, userInfo: [NSLocalizedDescriptionKey: "This is not a picture Paint can read."])
        }
        return Bitmap(image: cg, background: .white)
    }
}

// MARK: - Tools

enum Tool: Int, CaseIterable {
    case freeSelect, select, eraser, fill, pick, magnifier, pencil, brush, airbrush, text, line, curve, rect, polygon, ellipse, roundRect

    var hint: String {
        switch self {
        case .freeSelect: return "Selects a free-form part of the picture to move, copy, or edit."
        case .select: return "Selects a rectangular part of the picture to move, copy, or edit."
        case .eraser: return "Erases a portion of the picture, using the selected eraser shape."
        case .fill: return "Fills an area with the current drawing color."
        case .pick: return "Picks up a color from the picture for drawing."
        case .magnifier: return "Changes the magnification."
        case .pencil: return "Draws a free-form line one pixel wide."
        case .brush: return "Draws using a brush with the selected shape and size."
        case .airbrush: return "Draws using an airbrush of the selected size."
        case .text: return "Inserts text into the picture."
        case .line: return "Draws a straight line with the selected line width."
        case .curve: return "Draws a curved line with the selected line width."
        case .rect: return "Draws a rectangle with the selected fill style."
        case .polygon: return "Draws a polygon with the selected fill style."
        case .ellipse: return "Draws an ellipse with the selected fill style."
        case .roundRect: return "Draws a rounded rectangle with the selected fill style."
        }
    }

    var isShape: Bool { [.rect, .polygon, .ellipse, .roundRect].contains(self) }
    var usesWidth: Bool { [.line, .curve].contains(self) || isShape }
}

enum Art {
    static func pad(_ rows: [String]) -> [String] {
        rows.map { $0.padding(toLength: 16, withPad: ".", startingAt: 0) } + Array(repeating: "................", count: max(0, 16 - rows.count))
    }

    static let icons: [Tool: [String]] = [
        .freeSelect: pad(["", "....k.k", "...k....k.k", "..k.........k", "...........k", "..k.......k", "...........k",
                          "..k..........k", "...k", "....k.......k", ".....k.k...k", "..........k", ".........k", "........k"]),
        .select: pad(["", "", "..k.k.k.k.k.k", "", "..k.........k", "", "..k.........k", "", "..k.........k", "",
                      "..k.........k", "", "..k.k.k.k.k.k"]),
        .eraser: pad(["", "", "", ".......kkkkkkk", "......kyyyyyykk", ".....kyyyyyykwk", "....kyyyyyykwwk", "...kyyyyyykwwk",
                      "..kkkkkkkkwwk", "..kwwwwwwkwk", "..kwwwwwwkk", "..kkkkkkkk"]),
        .fill: pad(["", "......kk", ".....k..k", ".....k.kkk", "....k.kwwwk", "...k.kwwwwwk", "..kkkwwwwwwwk", ".kuukwwwwwwwwk",
                    ".kuuukwwwwwwk", ".kuuu.kwwwwk", ".kuuu..kwwk", ".kuu....kk", "..u", "..u"]),
        .pick: pad(["", "...........kkk", "..........kkkkk", "..........kkkkk", ".........kkkkk", "........kwkkk", ".......kwwwk",
                    "......kwwwk", ".....kwwwk", "....kwwwk", "...kwwwk", "..kwwwk", "..kwwk", ".kkkk", ".k"]),
        .magnifier: pad(["", "....kkkk", "...kwwwwk", "..kwwlwwwk", "..kwlwwwwk", "..kwwwwwwk", "..kwwwwwwk", "...kwwwwk",
                         "....kkkkkk", "........kkk", ".........kkk", "..........kkk", "...........kk"]),
        .pencil: pad(["", "............kk", "...........kddk", "..........kyykk", ".........kyyyk", "........kyyyk", ".......kyyyk",
                      "......kyyyk", ".....kyyyk", "....kyyyk", "...kwyyk", "...kwwk", "..kkkk", "..kk"]),
        .brush: pad(["", "............kk", "...........kddk", "..........kddk", ".........kddk", "........kddk", ".......kddk",
                     "......kkkk", ".....kllk", "....kllk", "...kkkk", "..kbbk", ".kbbbk", ".kbbk", ".kk"]),
        .airbrush: pad(["", "..k.k", "k.k..k", ".k.k", "k.k.k.kk", ".k..kkkkk", "....kwwwk", "....kdddk", "...kwwwwwk",
                        "...kwwwwwk", "...kwbbbwk", "...kwbbbwk", "...kwwwwwk", "...kwwwwwk", "...kkkkkkk"]),
        .text: pad(["", "", "......kk", "......kk", ".....kkkk", ".....k.kk", "....kk..kk", "....k...kk", "...kkkkkkkk",
                    "...k.....kk", "..kk......kk", "..k.......kk", ".kkk.....kkkk"]),
        .line: pad(["", "", "..k", "...k", "....k", ".....k", "......k", ".......k", "........k", ".........k", "..........k",
                    "...........k", "............k", ".............k"]),
        .curve: pad(["", "", "..kk", "....k", ".....k", ".....k", "......k", "......k", ".......k", ".......k", "........k",
                     "........k", ".........k", "..........kk"]),
        .rect: pad(["", "", "", "..kkkkkkkkkkkk", "..k..........k", "..k..........k", "..k..........k", "..k..........k",
                    "..k..........k", "..k..........k", "..k..........k", "..kkkkkkkkkkkk"]),
        .polygon: pad(["", "", "...kkkkkk", "...k.....k", "...k......k", "...k.......k", "...k........k", "...k.....kkkk",
                       "...k.....k", "...k.....k", "...k.....k", "...kkkkkkk"]),
        .ellipse: pad(["", "", "", ".....kkkkkk", "...kk......kk", "..k..........k", "..k..........k", "..k..........k",
                       "...kk......kk", ".....kkkkkk"]),
        .roundRect: pad(["", "", "", "....kkkkkkkk", "...k........k", "..k..........k", "..k..........k", "..k..........k",
                         "..k..........k", "..k..........k", "...k........k", "....kkkkkkkk"]),
    ]

    // 16 × 16 painter's palette with a brush, for the title bar and the app icon
    static let appIcon = [
        "................",
        "....kkkkkk......",
        "..kkwwwwwwkk....",
        ".kwwrrwwuuwwk...",
        ".kwwrrwwuuwwwk..",
        "kwwwwwwwwwwwwk..",
        "kwyywwwkkwwwwk..",
        "kwyywwk..kwGGk..",
        "kwwwwwk..kwGGk..",
        ".kwwwwwkkwwwwk..",
        ".kwwwwwwwwwwk...",
        "..kkwwwwwwkkkk..",
        "....kkkkkk..kbk.",
        "............kbbk",
        ".............kbk",
        "..............k.",
    ]

    /// The 28 colors of the Windows 95 Paint color box
    static let palette: [RGBA] = [
        0x000000, 0x808080, 0x800000, 0x808000, 0x008000, 0x008080, 0x000080, 0x800080, 0x808040, 0x004040, 0x0080FF, 0x004080, 0x4000FF, 0x804000,
        0xFFFFFF, 0xC0C0C0, 0xFF0000, 0xFFFF00, 0x00FF00, 0x00FFFF, 0x0000FF, 0xFF00FF, 0xFFFF80, 0x00FF80, 0x80FFFF, 0x8080FF, 0xFF0080, 0xFF8040,
    ].map { RGBA(hex: $0) }

    static func cursor(_ art: [String], hot: NSPoint) -> NSCursor {
        let img = NSImage(size: NSSize(width: 16, height: 16), flipped: true) { _ in
            W95.crisp(); W95.pixels(art, at: .zero); return true
        }
        return NSCursor(image: img, hotSpot: hot)
    }
    static let pencilCursor = cursor(icons[.pencil]!, hot: NSPoint(x: 2, y: 13))
    static let fillCursor = cursor(icons[.fill]!, hot: NSPoint(x: 2, y: 13))
    static let pickCursor = cursor(icons[.pick]!, hot: NSPoint(x: 1, y: 14))
    static let zoomCursor = cursor(icons[.magnifier]!, hot: NSPoint(x: 5, y: 5))
}

/// Settings of the tool options box under the tools
struct ToolOptions {
    var eraserSize = 1          // 4, 6, 8, 10 pixels
    var brush = 1               // 0–2 round, 3–5 square, 6–8 "/", 9–11 "\" (large, medium, small)
    var airSize = 0             // radius 4, 8, 12
    var lineWidth = 1           // 1–5
    var fillStyle = 0           // outline, outline + fill, fill
    var transparent = false     // selection and text: let the background color show through
    static let eraserPixels = [4, 6, 8, 10]
    static let airRadius = [4, 8, 12]
    static let brushSizes = [7, 4, 1, 8, 5, 2, 8, 5, 2, 8, 5, 2]
}

/// A selection lifted off the picture: it floats until it's dropped somewhere
final class Floating {
    var bitmap: Bitmap
    var x: Int, y: Int
    var path: CGPath?        // free-form outline, in the selection's own pixels
    var lifted: Bool         // the area under it has already been cleared to the background color

    init(bitmap: Bitmap, x: Int, y: Int, path: CGPath?, lifted: Bool) {
        self.bitmap = bitmap; self.x = x; self.y = y; self.path = path; self.lifted = lifted
    }
    var rect: CGRect { CGRect(x: x, y: y, width: bitmap.width, height: bitmap.height) }
}

// MARK: - Tool box, color box and status bar

final class ToolBoxView: FlippedView {
    weak var doc: PaintDoc?
    static let width: CGFloat = 56
    private func buttonRect(_ t: Tool) -> NSRect {
        NSRect(x: 3 + CGFloat(t.rawValue % 2) * 25, y: 4 + CGFloat(t.rawValue / 2) * 25, width: 25, height: 25)
    }
    private var optionsRect: NSRect { NSRect(x: 6, y: 4 + 8 * 25 + 6, width: 44, height: 66) }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        guard let doc else { return }
        for t in Tool.allCases {
            let r = buttonRect(t), on = doc.tool == t
            if on { W95.dither.setFill(); r.fill(); W95.bevel(r, .pressed) } else { W95.fill(r, W95.face); W95.bevel(r, .button) }
            W95.pixels(Art.icons[t]!, at: NSPoint(x: r.minX + 4 + (on ? 1 : 0), y: r.minY + 4 + (on ? 1 : 0)))
        }
        let o = optionsRect
        W95.edge(o, W95.shadow, W95.white)
        drawOptions(in: o.insetBy(dx: 1, dy: 1))
    }

    /// Option rows for the current tool: (rect, isSelected, draw)
    private func optionItems(in o: NSRect) -> [(NSRect, Bool, (NSRect, NSColor) -> Void)] {
        guard let doc else { return [] }
        let opt = doc.options
        func rows(_ n: Int) -> [NSRect] {
            let h = floor((o.height - 4) / CGFloat(n))
            return (0..<n).map { NSRect(x: o.minX + 2, y: o.minY + 2 + CGFloat($0) * h, width: o.width - 4, height: h) }
        }
        switch doc.tool {
        case .select, .freeSelect, .text:
            return rows(2).enumerated().map { i, r in
                (r, opt.transparent == (i == 1), { r, _ in
                    let box = NSRect(x: r.midX - 12, y: r.midY - 8, width: 24, height: 16)
                    if i == 0 { W95.fill(box, W95.white); W95.edge(box, W95.black, W95.black) }
                    W95.fill(NSRect(x: box.minX + 4, y: box.minY + 4, width: 8, height: 8), W95.red)
                    W95.fill(NSRect(x: box.minX + 10, y: box.minY + 7, width: 9, height: 6), W95.navy)
                })
            }
        case .eraser:
            return rows(4).enumerated().map { i, r in
                (r, opt.eraserSize == i, { r, c in
                    let s = CGFloat(ToolOptions.eraserPixels[i])
                    W95.fill(NSRect(x: floor(r.midX - s / 2), y: floor(r.midY - s / 2), width: s, height: s), c)
                })
            }
        case .magnifier:
            let z = [1, 2, 6, 8]
            return rows(4).enumerated().map { i, r in
                (r, doc.zoom == CGFloat(z[i]), { r, c in
                    W95.text("\(z[i])x", at: NSPoint(x: r.midX - 7, y: r.midY - W95.lineHeight / 2), color: c)
                })
            }
        case .brush:
            var items: [(NSRect, Bool, (NSRect, NSColor) -> Void)] = []
            let cw = floor((o.width - 4) / 3), ch = floor((o.height - 4) / 4)
            for i in 0..<12 {
                let r = NSRect(x: o.minX + 2 + CGFloat(i % 3) * cw, y: o.minY + 2 + CGFloat(i / 3) * ch, width: cw, height: ch)
                items.append((r, opt.brush == i, { r, c in
                    let s = CGFloat(ToolOptions.brushSizes[i]), cx = floor(r.midX), cy = floor(r.midY)
                    switch i / 3 {
                    case 0:
                        c.setFill(); NSBezierPath(ovalIn: NSRect(x: cx - floor(s / 2), y: cy - floor(s / 2), width: s, height: s)).fill()
                    case 1: W95.fill(NSRect(x: cx - floor(s / 2), y: cy - floor(s / 2), width: s, height: s), c)
                    default:
                        let n = Int(s)
                        for k in 0..<n {
                            let dx = CGFloat(k) - floor(s / 2)
                            W95.fill(NSRect(x: cx + dx, y: i / 3 == 2 ? cy - dx : cy + dx, width: 1, height: 1), c)
                        }
                    }
                }))
            }
            return items
        case .airbrush:
            let rs = [NSRect(x: o.minX + 2, y: o.minY + 2, width: 20, height: 28), NSRect(x: o.minX + 21, y: o.minY + 2, width: 20, height: 28),
                      NSRect(x: o.minX + 2, y: o.minY + 30, width: o.width - 4, height: o.height - 32)]
            return rs.enumerated().map { i, r in
                (r, opt.airSize == i, { r, c in
                    var g = SystemRandomNumberGenerator()
                    let rad = CGFloat(ToolOptions.airRadius[i])
                    for _ in 0..<Int(rad * 3) {
                        let a = CGFloat.random(in: 0..<(2 * .pi), using: &g), d = rad * sqrt(CGFloat.random(in: 0...1, using: &g))
                        W95.fill(NSRect(x: floor(r.midX + cos(a) * d), y: floor(r.midY + sin(a) * d), width: 1, height: 1), c)
                    }
                })
            }
        case .line, .curve:
            return rows(5).enumerated().map { i, r in
                (r, opt.lineWidth == i + 1, { r, c in
                    W95.fill(NSRect(x: r.minX + 4, y: floor(r.midY - CGFloat(i + 1) / 2), width: r.width - 8, height: CGFloat(i + 1)), c)
                })
            }
        case .rect, .polygon, .ellipse, .roundRect:
            return rows(3).enumerated().map { i, r in
                (r, opt.fillStyle == i, { r, c in
                    let box = NSRect(x: r.minX + 5, y: r.midY - 5, width: r.width - 10, height: 10)
                    if i == 2 { W95.fill(box, W95.shadow) } else {
                        if i == 1 { W95.fill(box, W95.shadow) }
                        W95.edge(box, c, c)
                    }
                })
            }
        default:
            return []
        }
    }

    private func drawOptions(in o: NSRect) {
        for (r, on, draw) in optionItems(in: o) {
            if on { W95.fill(r, W95.navy) }
            draw(r, on ? W95.white : W95.black)
        }
    }

    override func mouseDown(with event: NSEvent) {
        guard let doc else { return }
        let p = convert(event.locationInWindow, from: nil)
        if let t = Tool.allCases.first(where: { buttonRect($0).contains(p) }) { doc.setTool(t); return }
        let items = optionItems(in: optionsRect.insetBy(dx: 1, dy: 1))
        guard let i = items.firstIndex(where: { $0.0.contains(p) }) else { return }
        switch doc.tool {
        case .select, .freeSelect, .text: doc.options.transparent = i == 1; doc.canvas.needsDisplay = true
        case .eraser: doc.options.eraserSize = i
        case .magnifier: doc.setZoom(CGFloat([1, 2, 6, 8][i]), around: nil)
        case .brush: doc.options.brush = i
        case .airbrush: doc.options.airSize = i
        case .line, .curve: doc.options.lineWidth = i + 1
        default: doc.options.fillStyle = i
        }
        doc.canvas.updateCursor()
        needsDisplay = true
    }
}

final class ColorBoxView: FlippedView {
    weak var doc: PaintDoc?
    private var lastCell: Int?
    static let height: CGFloat = 44

    private func cellRect(_ i: Int) -> NSRect {
        NSRect(x: 38 + CGFloat(i % 14) * 16, y: 6 + CGFloat(i / 14) * 16, width: 16, height: 16)
    }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        guard let doc else { return }
        // Current colors: drawing color in front, background color behind
        let box = NSRect(x: 4, y: 6, width: 32, height: 32)
        W95.dither.setFill(); box.fill()
        W95.bevel(box, .sunken)
        let back = NSRect(x: 16, y: 18, width: 13, height: 13), front = NSRect(x: 9, y: 11, width: 13, height: 13)
        W95.fill(back, doc.bg.ns); W95.bevel(back, .button); W95.fill(back.insetBy(dx: 2, dy: 2), doc.bg.ns)
        W95.fill(front, doc.fg.ns); W95.bevel(front, .button); W95.fill(front.insetBy(dx: 2, dy: 2), doc.fg.ns)
        for (i, c) in doc.palette.enumerated() {
            let r = cellRect(i)
            W95.bevel(r, .sunken)
            W95.fill(r.insetBy(dx: 2, dy: 2), c.ns)
        }
    }

    private func pick(_ event: NSEvent, background: Bool) {
        guard let doc else { return }
        let p = convert(event.locationInWindow, from: nil)
        guard let i = doc.palette.indices.first(where: { cellRect($0).contains(p) }) else { return }
        // A double-click on one color edits it
        defer { lastCell = i }
        if event.clickCount == 2 && !background && lastCell == i { doc.editColor(i); return }
        if background { doc.bg = doc.palette[i] } else { doc.fg = doc.palette[i] }
    }

    override func mouseDown(with event: NSEvent) {
        if event.modifierFlags.contains(.control) { pick(event, background: true) } else { pick(event, background: false) }
    }
    override func rightMouseDown(with event: NSEvent) { pick(event, background: true) }
}

final class StatusBarView: FlippedView {
    static let height: CGFloat = 20
    var text = "" { didSet { if text != oldValue { needsDisplay = true } } }
    var position = "" { didSet { if position != oldValue { needsDisplay = true } } }
    var size = "" { didSet { if size != oldValue { needsDisplay = true } } }

    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        let w = bounds.width, paneY: CGFloat = 3, paneH = bounds.height - 5
        let panes = [(NSRect(x: 2, y: paneY, width: max(40, w - 4 - 2 * 92), height: paneH), text),
                     (NSRect(x: w - 2 - 2 * 92 + 2, y: paneY, width: 90, height: paneH), position),
                     (NSRect(x: w - 2 - 92 + 2, y: paneY, width: 90, height: paneH), size)]
        for (r, s) in panes {
            W95.edge(r, W95.shadow, W95.white)
            NSGraphicsContext.saveGraphicsState()
            NSBezierPath(rect: r.insetBy(dx: 1, dy: 1)).addClip()
            W95.text(s, at: NSPoint(x: r.minX + 4, y: r.minY + floor((r.height - W95.lineHeight) / 2)))
            NSGraphicsContext.restoreGraphicsState()
        }
    }
}

final class SunkenView: FlippedView {
    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        W95.bevel(bounds, .sunken)
    }
}

/// Plain gray square for the corner between the two scroll bars
final class FaceView: FlippedView {
    override func draw(_ dirtyRect: NSRect) { W95.fill(bounds, W95.face) }
}

// MARK: - Canvas

final class PaintTextView: NSTextView {
    weak var doc: PaintDoc?
    override func changeFont(_ sender: Any?) { doc?.changeTextFont(sender) }
}

final class CanvasView: FlippedView {
    weak var doc: PaintDoc?
    static let margin: CGFloat = 3
    private var tracking: NSTrackingArea?
    private enum Handle { case right, bottom, corner }
    private var handleDrag: (Handle, CGSize)?

    /// Window units per picture pixel: zoom 1 shows one pixel per screen point
    var unit: CGFloat { (doc?.zoom ?? 1) / W95.scale }

    func imageRect() -> NSRect {
        guard let b = doc?.bitmap else { return .zero }
        return NSRect(x: CanvasView.margin, y: CanvasView.margin, width: CGFloat(b.width) * unit, height: CGFloat(b.height) * unit)
    }

    func toImage(_ p: NSPoint) -> CGPoint {
        CGPoint(x: (p.x - CanvasView.margin) / unit, y: (p.y - CanvasView.margin) / unit)
    }
    func fromImage(_ r: CGRect) -> NSRect {
        NSRect(x: CanvasView.margin + r.minX * unit, y: CanvasView.margin + r.minY * unit, width: r.width * unit, height: r.height * unit)
    }

    private func handleRects() -> [(Handle, NSRect)] {
        let r = imageRect(), h: CGFloat = 3
        return [(.right, NSRect(x: r.maxX, y: floor(r.midY - h / 2), width: h, height: h)),
                (.bottom, NSRect(x: floor(r.midX - h / 2), y: r.maxY, width: h, height: h)),
                (.corner, NSRect(x: r.maxX, y: r.maxY, width: h, height: h))]
    }

    func updateSize() {
        let r = imageRect()
        setFrameSize(NSSize(width: r.maxX + 10, height: r.maxY + 10))
        needsDisplay = true
        updateCursor()
    }

    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func updateTrackingAreas() {
        if let t = tracking { removeTrackingArea(t) }
        tracking = NSTrackingArea(rect: bounds, options: [.mouseMoved, .mouseEnteredAndExited, .activeInKeyWindow, .inVisibleRect],
                                  owner: self, userInfo: nil)
        addTrackingArea(tracking!)
        super.updateTrackingAreas()
    }

    func updateCursor() { window?.invalidateCursorRects(for: self) }

    override func resetCursorRects() {
        guard let doc else { return }
        addCursorRect(imageRect(), cursor: doc.cursor(unitsPerPixel: unit))
        for (h, r) in handleRects() {
            addCursorRect(r.insetBy(dx: -1, dy: -1), cursor: h == .right ? .resizeLeftRight : h == .bottom ? .resizeUpDown : .crosshair)
        }
    }

    override func draw(_ dirtyRect: NSRect) {
        guard let doc, let cg = NSGraphicsContext.current?.cgContext else { return }
        let r = imageRect()
        cg.interpolationQuality = .none
        cg.setShouldAntialias(false)
        func put(_ img: CGImage, _ rect: NSRect) {
            cg.saveGState()
            cg.translateBy(x: rect.minX, y: rect.maxY)
            cg.scaleBy(x: 1, y: -1)
            cg.draw(img, in: CGRect(origin: .zero, size: rect.size))
            cg.restoreGState()
        }
        put(doc.bitmap.image(), r)
        if let f = doc.floating {
            put(doc.floatingImage(f), fromImage(f.rect))
        }
        // Grid at large magnifications
        if doc.showGrid && doc.zoom >= 4 {
            let path = NSBezierPath()
            for x in 1..<doc.bitmap.width { let gx = r.minX + CGFloat(x) * unit; path.move(to: NSPoint(x: gx, y: r.minY)); path.line(to: NSPoint(x: gx, y: r.maxY)) }
            for y in 1..<doc.bitmap.height { let gy = r.minY + CGFloat(y) * unit; path.move(to: NSPoint(x: r.minX, y: gy)); path.line(to: NSPoint(x: r.maxX, y: gy)) }
            path.lineWidth = 0.5 / W95.scale
            W95.shadow.setStroke()
            path.stroke()
        }
        // Resize handles
        for (_, h) in handleRects() { W95.fill(h, W95.navy) }
        // Overlays: selection outline, free-form trail, text box, canvas resize preview
        let dash: [CGFloat] = [2 / W95.scale, 2 / W95.scale]
        func dashed(_ p: NSBezierPath) {
            p.lineWidth = 1 / W95.scale
            W95.white.setStroke(); p.stroke()
            p.setLineDash(dash, count: 2, phase: 0)
            W95.navy.setStroke(); p.stroke()
        }
        if let f = doc.floating { dashed(NSBezierPath(rect: fromImage(f.rect).insetBy(dx: -0.5, dy: -0.5))) }
        if let box = doc.dragBox { dashed(NSBezierPath(rect: fromImage(box))) }
        if doc.freeTrail.count > 1 {
            let p = NSBezierPath()
            p.move(to: NSPoint(x: CanvasView.margin + (doc.freeTrail[0].x + 0.5) * unit, y: CanvasView.margin + (doc.freeTrail[0].y + 0.5) * unit))
            for q in doc.freeTrail.dropFirst() { p.line(to: NSPoint(x: CanvasView.margin + (q.x + 0.5) * unit, y: CanvasView.margin + (q.y + 0.5) * unit)) }
            p.lineWidth = 1 / W95.scale
            W95.black.setStroke(); p.stroke()
        }
        if let (_, size) = handleDrag {
            dashed(NSBezierPath(rect: NSRect(x: r.minX, y: r.minY, width: size.width * unit, height: size.height * unit)))
        }
    }

    // Mouse: left draws with the drawing color, right with the background color
    private func begin(_ e: NSEvent, right: Bool) {
        guard let doc else { return }
        window?.makeFirstResponder(self)
        let p = convert(e.locationInWindow, from: nil)
        if !right, let (h, _) = handleRects().first(where: { $0.1.insetBy(dx: -1, dy: -1).contains(p) }) {
            doc.finishPending()
            handleDrag = (h, CGSize(width: doc.bitmap.width, height: doc.bitmap.height))
            return
        }
        doc.mouseDown(toImage(p), right: right, event: e)
    }
    private func move(_ e: NSEvent) {
        guard let doc else { return }
        let p = convert(e.locationInWindow, from: nil)
        if let (h, _) = handleDrag {
            let ip = toImage(p)
            let w = h == .bottom ? CGFloat(doc.bitmap.width) : max(1, ip.x.rounded())
            let ht = h == .right ? CGFloat(doc.bitmap.height) : max(1, ip.y.rounded())
            handleDrag = (h, CGSize(width: min(8000, w), height: min(8000, ht)))
            doc.status.size = "\(Int(w))x\(Int(ht))"
            needsDisplay = true
            return
        }
        doc.mouseDragged(toImage(p), event: e)
        autoscroll(with: e)
    }
    private func end(_ e: NSEvent) {
        guard let doc else { return }
        if let (_, size) = handleDrag {
            handleDrag = nil
            doc.resizeImage(width: Int(size.width), height: Int(size.height))
            return
        }
        doc.mouseUp(toImage(convert(e.locationInWindow, from: nil)), event: e)
    }

    override func mouseDown(with e: NSEvent) { begin(e, right: e.modifierFlags.contains(.control)) }
    override func mouseDragged(with e: NSEvent) { move(e) }
    override func mouseUp(with e: NSEvent) { end(e) }
    override func rightMouseDown(with e: NSEvent) { begin(e, right: true) }
    override func rightMouseDragged(with e: NSEvent) { move(e) }
    override func rightMouseUp(with e: NSEvent) { end(e) }
    override func menu(for event: NSEvent) -> NSMenu? { nil }

    override func mouseMoved(with e: NSEvent) {
        guard let doc else { return }
        let p = toImage(convert(e.locationInWindow, from: nil))
        let x = Int(floor(p.x)), y = Int(floor(p.y))
        doc.status.position = doc.bitmap.contains(x, y) ? "\(x),\(y)" : ""
        doc.mouseMoved(p)
    }
    override func mouseExited(with event: NSEvent) { doc?.status.position = "" }

    override func keyDown(with e: NSEvent) {
        guard let doc else { return }
        switch e.keyCode {
        case 51, 117: doc.clearSelection()                       // Delete, forward delete
        case 53: doc.cancelPending()                             // Esc
        case 36, 76: doc.finishPending()                         // Return
        case 123: doc.nudge(-1, 0)
        case 124: doc.nudge(1, 0)
        case 125: doc.nudge(0, 1)
        case 126: doc.nudge(0, -1)
        case 109: doc.tracker.openBar(0, keyboard: true)        // F10
        default: super.keyDown(with: e)
        }
    }

    @objc func changeFont(_ sender: Any?) { doc?.changeTextFont(sender) }
}

// MARK: - Dialogs

/// Sends Return and Esc typed in a dialog's text fields to the dialog
final class DialogFieldDelegate: NSObject, NSTextFieldDelegate {
    weak var window: Win95Window?
    func control(_ control: NSControl, textView: NSTextView, doCommandBy sel: Selector) -> Bool {
        if sel == #selector(NSResponder.insertNewline(_:)) { window?.onEnter?(); return true }
        if sel == #selector(NSResponder.cancelOperation(_:)) { window?.onEscape?(); return true }
        return false
    }
}

/// A modal Windows 95 dialog with OK and Cancel. `build` adds the controls and returns a check
/// that runs on OK (returning false keeps the dialog open).
func runDialog(title: String, client: NSSize, over parent: NSWindow?, build: (NSView, DialogFieldDelegate) -> () -> Bool) -> Bool {
    let size = ChromeView.windowSize(forClient: client)
    let win = Win95Window(contentRect: NSRect(origin: .zero, size: size), minimizable: false)
    win.acceptsMain = false
    win.isExcludedFromWindowsMenu = true
    win.level = .modalPanel
    let chrome = ChromeView(frame: NSRect(origin: .zero, size: size), title: title, kinds: [.close], resizable: false, showIcon: false)
    win.contentView = chrome
    let fields = DialogFieldDelegate()
    fields.window = win
    let validate = build(chrome.client, fields)
    let ok = W95Button("OK", frame: NSRect(x: client.width - 83, y: 8, width: 75, height: 23)) {
        if validate() { NSApp.stopModal(withCode: .OK) }
    }
    ok.isDefault = true
    let cancel = W95Button("Cancel", frame: NSRect(x: client.width - 83, y: 36, width: 75, height: 23)) { NSApp.stopModal(withCode: .cancel) }
    chrome.client.addSubview(ok)
    chrome.client.addSubview(cancel)
    chrome.titleBar.onButton = { _ in NSApp.stopModal(withCode: .cancel) }
    win.onEnter = { if let b = win.firstResponder as? W95Button { b.action() } else { ok.action() } }
    win.onEscape = { NSApp.stopModal(withCode: .cancel) }
    if let p = parent, p.isVisible {
        win.setFrameOrigin(NSPoint(x: round(p.frame.midX - size.width / 2), y: round(p.frame.midY - size.height / 2 + 40)))
    } else {
        win.center()
    }
    win.makeKeyAndOrderFront(nil)
    if let f = chrome.client.subviews.compactMap({ $0 as? W95Field }).first {
        win.makeFirstResponder(f.field)
        f.field.selectText(nil)
    } else {
        win.makeFirstResponder(ok)
    }
    let r = NSApp.runModal(for: win)
    win.orderOut(nil)
    parent?.makeKeyAndOrderFront(nil)
    return withExtendedLifetime(fields) { r == .OK }
}

func numberField(_ v: Int, frame: NSRect, _ d: DialogFieldDelegate) -> W95Field {
    let f = W95Field(frame: frame)
    f.field.stringValue = "\(v)"
    f.field.delegate = d
    return f
}

// MARK: - Paint window

final class PaintDoc: NSObject, NSWindowDelegate {
    let window: Win95Window
    let chrome: ChromeView
    let menuBar = MenuBarView()
    let tracker = MenuTracker()
    let toolBox = ToolBoxView()
    let colorBox = ColorBoxView()
    let status = StatusBarView()
    let workspace = SunkenView()
    let scroll = NSScrollView()
    let canvas = CanvasView()
    private let corner = FaceView()

    var bitmap: Bitmap
    var url: URL?
    var fileType: UTType = .png
    var dirty = false { didSet { if dirty != oldValue { updateTitle() } } }

    var tool: Tool = .pencil
    private var previousTool: Tool = .pencil
    var options = ToolOptions()
    var fg = RGBA.black { didSet { colorBox.needsDisplay = true; textView?.textColor = fg.ns } }
    var bg = RGBA.white { didSet { colorBox.needsDisplay = true; updateTextBackground(); canvas.needsDisplay = true } }
    var palette = Art.palette { didSet { colorBox.needsDisplay = true } }
    var zoom: CGFloat = 1
    var showGrid = false
    var showTools = true, showColors = true, showStatus = true
    static var textFont = NSFont(name: "Arial", size: 14) ?? NSFont.systemFont(ofSize: 14)

    // Work in progress
    var floating: Floating?
    var dragBox: CGRect?
    var freeTrail: [CGPoint] = []
    private var strokeBase: Data?
    private var start = CGPoint.zero, last = CGPoint.zero
    private var right = false
    private var moving: (dx: CGFloat, dy: CGFloat)?
    private var polygon: [CGPoint] = []
    private var curve: (a: CGPoint, b: CGPoint, c1: CGPoint, c2: CGPoint, stage: Int)?
    private var airTimer: Timer?
    private var textView: PaintTextView?
    private var textRect = CGRect.zero
    private var editingColor: Int?
    private var floatCache: (key: String, image: CGImage)?

    private struct Snapshot { let width: Int, height: Int, data: Data }
    private var undoStack: [Snapshot] = [], redoStack: [Snapshot] = []

    var displayName: String { url?.lastPathComponent ?? "untitled" }

    init(frame: NSRect) {
        bitmap = Bitmap(width: 640, height: 480, fill: .white)
        window = Win95Window(contentRect: frame, minimizable: true)
        chrome = ChromeView(frame: NSRect(origin: .zero, size: frame.size), title: "untitled - Paint",
                            kinds: [.minimize, .maximize, .close], resizable: true, showIcon: true)
        super.init()
        window.contentView = chrome
        window.delegate = self
        chrome.minSize = NSSize(width: 420 * W95.scale / 1.5, height: 380)

        let c = chrome.client
        menuBar.autoresizingMask = [.width]
        toolBox.autoresizingMask = [.height]
        colorBox.autoresizingMask = [.width, .minYMargin]
        status.autoresizingMask = [.width, .minYMargin]
        workspace.autoresizingMask = [.width, .height]
        [menuBar, toolBox, workspace, colorBox, status].forEach { c.addSubview($0) }
        toolBox.doc = self
        colorBox.doc = self
        canvas.doc = self

        scroll.autoresizingMask = [.width, .height]
        scroll.verticalScroller = Win95Scroller()
        scroll.horizontalScroller = Win95Scroller()
        scroll.scrollerStyle = .legacy
        scroll.hasVerticalScroller = true
        scroll.hasHorizontalScroller = true
        scroll.autohidesScrollers = false
        scroll.borderType = .noBorder
        scroll.drawsBackground = false
        scroll.contentView.drawsBackground = true
        scroll.contentView.backgroundColor = W95.shadow
        scroll.verticalLineScroll = 16
        scroll.horizontalLineScroll = 16
        scroll.documentView = canvas
        workspace.addSubview(scroll)
        workspace.addSubview(corner)
        layoutPanels()
        canvas.updateSize()

        tracker.window = window
        tracker.bar = menuBar
        menuBar.tracker = tracker
        menuBar.menus = [
            .init(title: "&File") { [weak self] in self?.fileItems() ?? [] },
            .init(title: "&Edit") { [weak self] in self?.editItems() ?? [] },
            .init(title: "&View") { [weak self] in self?.viewItems() ?? [] },
            .init(title: "&Image") { [weak self] in self?.imageItems() ?? [] },
            .init(title: "&Options") { [weak self] in self?.optionItems() ?? [] },
            .init(title: "&Help") { [MenuItem95("&About Paint 95") { AppDelegate.shared.about() }] },
        ]
        chrome.titleBar.onButton = { [weak self] kind in
            guard let self else { return }
            switch kind {
            case .close: self.close()
            case .minimize: self.window.miniaturize(nil)
            case .maximize: self.toggleMaximize()
            }
        }
        chrome.titleBar.onDoubleClick = { [weak self] in self?.toggleMaximize() }
        chrome.onResize = { [weak self] in self?.restoreFrame = nil; self?.chrome.titleBar.maximized = false }
        status.text = tool.hint
        updateTitle()
        window.makeFirstResponder(canvas)
    }

    func layoutPanels() {
        let c = chrome.client.bounds
        let top: CGFloat = 19
        var bottom = c.height
        menuBar.frame = NSRect(x: 0, y: 0, width: c.width, height: 19)
        status.isHidden = !showStatus
        if showStatus { status.frame = NSRect(x: 0, y: bottom - StatusBarView.height, width: c.width, height: StatusBarView.height); bottom -= StatusBarView.height }
        colorBox.isHidden = !showColors
        if showColors { colorBox.frame = NSRect(x: 0, y: bottom - ColorBoxView.height, width: c.width, height: ColorBoxView.height); bottom -= ColorBoxView.height }
        toolBox.isHidden = !showTools
        let left = showTools ? ToolBoxView.width : 0
        if showTools { toolBox.frame = NSRect(x: 0, y: top, width: left, height: bottom - top) }
        workspace.frame = NSRect(x: left, y: top, width: c.width - left, height: bottom - top)
        scroll.frame = workspace.bounds.insetBy(dx: 2, dy: 2)
        corner.frame = NSRect(x: scroll.frame.maxX - 16, y: scroll.frame.maxY - 16, width: 16, height: 16)
        corner.autoresizingMask = [.minXMargin, .minYMargin]
    }

    // MARK: Menus

    private func fileItems() -> [MenuItem95] {
        [MenuItem95("&New", "Cmd+N") { [weak self] in self?.newImage() },
         MenuItem95("&Open...", "Cmd+O") { [weak self] in self?.openImage() },
         MenuItem95("&Save", "Cmd+S") { [weak self] in self?.save() },
         MenuItem95("Save &As...", "Shift+Cmd+S") { [weak self] in self?.saveAs() },
         .separator,
         MenuItem95("&Print...", "Cmd+P") { [weak self] in self?.printImage() },
         .separator,
         MenuItem95("Set As &Desktop Picture") { [weak self] in self?.setAsDesktopPicture() },
         .separator,
         MenuItem95("E&xit", "Cmd+W") { [weak self] in self?.close() }]
    }

    private func editItems() -> [MenuItem95] {
        let hasSel = { [weak self] in self?.floating != nil }
        return [MenuItem95("&Undo", "Cmd+Z", enabled: { [weak self] in !(self?.undoStack.isEmpty ?? true) }) { [weak self] in self?.undo() },
                MenuItem95("&Repeat", "Shift+Cmd+Z", enabled: { [weak self] in !(self?.redoStack.isEmpty ?? true) }) { [weak self] in self?.redo() },
                .separator,
                MenuItem95("Cu&t", "Cmd+X", enabled: hasSel) { [weak self] in self?.cut() },
                MenuItem95("&Copy", "Cmd+C", enabled: hasSel) { [weak self] in self?.copySelection() },
                MenuItem95("&Paste", "Cmd+V", enabled: { NSImage(pasteboard: .general) != nil }) { [weak self] in self?.paste() },
                MenuItem95("C&lear Selection", "Del", enabled: hasSel) { [weak self] in self?.clearSelection() },
                MenuItem95("Select &All", "Cmd+A") { [weak self] in self?.selectAll() },
                .separator,
                MenuItem95("C&opy To...", enabled: hasSel) { [weak self] in self?.copyTo() },
                MenuItem95("Paste &From...") { [weak self] in self?.pasteFrom() }]
    }

    private func viewItems() -> [MenuItem95] {
        [MenuItem95("&Tool Box", "Cmd+T", checked: { [weak self] in self?.showTools ?? false }) { [weak self] in self?.togglePanel(0) },
         MenuItem95("&Color Box", "Cmd+L", checked: { [weak self] in self?.showColors ?? false }) { [weak self] in self?.togglePanel(1) },
         MenuItem95("&Status Bar", checked: { [weak self] in self?.showStatus ?? false }) { [weak self] in self?.togglePanel(2) },
         .separator,
         MenuItem95("&Normal Size", "Cmd+0", checked: { [weak self] in self?.zoom == 1 }) { [weak self] in self?.setZoom(1, around: nil) },
         MenuItem95("&Large Size", "Cmd+4", checked: { [weak self] in self?.zoom == 4 }) { [weak self] in self?.setZoom(4, around: nil) },
         MenuItem95("Zoom &In", "Cmd++") { [weak self] in self?.zoomStep(1) },
         MenuItem95("Zoom O&ut", "Cmd+-") { [weak self] in self?.zoomStep(-1) },
         MenuItem95("Show &Grid", "Cmd+G", enabled: { [weak self] in (self?.zoom ?? 1) >= 4 },
                    checked: { [weak self] in self?.showGrid ?? false }) { [weak self] in self?.toggleGrid() },
         .separator,
         MenuItem95("&View Bitmap", "Cmd+F") { [weak self] in self?.viewBitmap() },
         MenuItem95("Te&xt Font...") { [weak self] in self?.showFonts() }]
    }

    private func imageItems() -> [MenuItem95] {
        [MenuItem95("&Flip/Rotate...", "Cmd+R") { [weak self] in self?.flipRotate() },
         MenuItem95("&Stretch/Skew...", "Opt+Cmd+W") { [weak self] in self?.stretchSkew() },
         MenuItem95("&Invert Colors", "Cmd+I") { [weak self] in self?.invertColors() },
         MenuItem95("&Attributes...", "Cmd+E") { [weak self] in self?.attributes() },
         MenuItem95("&Clear Image", "Shift+Cmd+N", enabled: { [weak self] in self?.floating == nil }) { [weak self] in self?.clearImage() }]
    }

    private func optionItems() -> [MenuItem95] {
        [MenuItem95("&Edit Colors...") { [weak self] in
            guard let self else { return }
            self.editColor(self.palette.firstIndex(of: self.fg) ?? 0) },
         MenuItem95("&Draw Opaque", checked: { [weak self] in !(self?.options.transparent ?? false) }) { [weak self] in
            guard let self else { return }
            self.options.transparent.toggle(); self.toolBox.needsDisplay = true; self.canvas.needsDisplay = true }]
    }

    // MARK: State

    func updateTitle() {
        let t = "\(displayName) - Paint"
        chrome.titleBar.title = t
        window.title = t
        window.isDocumentEdited = dirty
    }

    func setTool(_ t: Tool) {
        finishPending()
        if t != .pick { previousTool = t }
        tool = t
        status.text = t.hint
        toolBox.needsDisplay = true
        canvas.updateCursor()
    }

    func cursor(unitsPerPixel u: CGFloat) -> NSCursor {
        switch tool {
        case .pencil: return Art.pencilCursor
        case .fill: return Art.fillCursor
        case .pick: return Art.pickCursor
        case .magnifier: return Art.zoomCursor
        case .text: return .iBeam
        case .eraser:
            let s = max(4, CGFloat(ToolOptions.eraserPixels[options.eraserSize]) * zoom)
            let img = NSImage(size: NSSize(width: s, height: s), flipped: true) { r in
                W95.black.setFill(); r.frame(withWidth: 1)
                W95.white.setFill(); r.insetBy(dx: 1, dy: 1).frame(withWidth: 1)
                return true
            }
            return NSCursor(image: img, hotSpot: NSPoint(x: s / 2, y: s / 2))
        default: return .crosshair
        }
    }

    private func pushUndo() {
        undoStack.append(Snapshot(width: bitmap.width, height: bitmap.height, data: bitmap.data))
        let limit = max(3, min(50, 400_000_000 / max(1, bitmap.width * bitmap.height * 4)))
        if undoStack.count > limit { undoStack.removeFirst(undoStack.count - limit) }
        redoStack.removeAll()
        dirty = true
    }

    private func apply(_ s: Snapshot) {
        if s.width != bitmap.width || s.height != bitmap.height {
            bitmap = Bitmap(width: s.width, height: s.height, data: s.data)
            canvas.updateSize()
        } else {
            bitmap.restore(s.data)
        }
        canvas.needsDisplay = true
    }

    func undo() {
        cancelPending()
        guard let s = undoStack.popLast() else { return }
        redoStack.append(Snapshot(width: bitmap.width, height: bitmap.height, data: bitmap.data))
        apply(s)
        dirty = true
    }

    func redo() {
        cancelPending()
        guard let s = redoStack.popLast() else { return }
        undoStack.append(Snapshot(width: bitmap.width, height: bitmap.height, data: bitmap.data))
        apply(s)
        dirty = true
    }

    /// Drops a floating selection, finishes text, polygons and curves
    func finishPending() {
        commitText()
        if polygon.count > 1 { drawPolygon(closed: true) }
        polygon = []
        curve = nil
        strokeBase = nil
        dropFloating()
        dragBox = nil
        freeTrail = []
        canvas.needsDisplay = true
    }

    /// Throws away what's in progress (Esc)
    func cancelPending() {
        if let tv = textView { tv.removeFromSuperview(); textView = nil }
        if let base = strokeBase, polygon.count > 0 || curve != nil { bitmap.restore(base) }
        polygon = []
        curve = nil
        strokeBase = nil
        if let f = floating, f.lifted { dropFloating() }   // a moved selection stays where it is
        floating = nil
        dragBox = nil
        freeTrail = []
        status.size = ""
        canvas.needsDisplay = true
    }

    // MARK: Floating selection

    func floatingImage(_ f: Floating) -> CGImage {
        guard options.transparent else { return f.bitmap.image() }
        let key = "\(ObjectIdentifier(f.bitmap).hashValue)-\(bg.u32)"
        if let c = floatCache, c.key == key { return c.image }
        let img = f.bitmap.clearing(bg.u32).image()
        floatCache = (key, img)
        return img
    }

    /// Clears the area under a selection to the background color the first time it moves
    private func lift(_ f: Floating) {
        guard !f.lifted else { return }
        pushUndo()
        if let p = f.path {
            var t = CGAffineTransform(translationX: CGFloat(f.x), y: CGFloat(f.y))
            if let moved = p.copy(using: &t) {
                bitmap.ctx.setFillColor(bg.cg)
                bitmap.ctx.addPath(moved)
                bitmap.ctx.fillPath()
            }
        } else {
            bitmap.fill(f.rect, bg)
        }
        f.lifted = true
    }

    private func stamp(_ f: Floating) {
        bitmap.ctx.setBlendMode(.normal)
        bitmap.draw(floatingImage(f), in: f.rect)
    }

    private func dropFloating() {
        guard let f = floating else { return }
        if f.lifted { stamp(f) }
        floating = nil
        status.size = ""
        canvas.needsDisplay = true
    }

    private func makeFloating(_ r: CGRect, path: CGPath?) {
        let x = max(0, Int(floor(r.minX))), y = max(0, Int(floor(r.minY)))
        let x2 = min(bitmap.width, Int(ceil(r.maxX))), y2 = min(bitmap.height, Int(ceil(r.maxY)))
        guard x2 - x >= 1, y2 - y >= 1 else { return }
        let piece = bitmap.crop(x: x, y: y, width: x2 - x, height: y2 - y)
        var local: CGPath?
        if let path {
            var t = CGAffineTransform(translationX: -CGFloat(x), y: -CGFloat(y))
            local = path.copy(using: &t)
            if let local { piece.mask(local) }
        }
        floating = Floating(bitmap: piece, x: x, y: y, path: local, lifted: false)
        status.size = "\(x2 - x)x\(y2 - y)"
    }

    func nudge(_ dx: Int, _ dy: Int) {
        guard let f = floating else { return }
        lift(f)
        f.x += dx; f.y += dy
        canvas.needsDisplay = true
    }

    // MARK: Drawing

    private var drawColor: RGBA { right ? bg : fg }
    private var otherColor: RGBA { right ? fg : bg }

    private func pixel(_ p: CGPoint) -> (Int, Int) { (Int(floor(p.x)), Int(floor(p.y))) }

    private func stampBrush(_ x: Int, _ y: Int, _ c: RGBA) {
        let i = options.brush, s = ToolOptions.brushSizes[i]
        switch i / 3 {
        case 0:
            if s == 1 { bitmap.plot(x, y, c.u32) } else {
                bitmap.ctx.setFillColor(c.cg)
                bitmap.ctx.fillEllipse(in: CGRect(x: x - s / 2, y: y - s / 2, width: s, height: s))
            }
        case 1: bitmap.fill(CGRect(x: x - s / 2, y: y - s / 2, width: s, height: s), c)
        default:
            for k in 0..<s {
                let d = k - s / 2
                bitmap.plot(x + d, i / 3 == 2 ? y - d : y + d, c.u32)
            }
        }
    }

    private func spray() {
        let r = ToolOptions.airRadius[options.airSize]
        let (cx, cy) = pixel(last)
        for _ in 0..<(r * 2) {
            let a = Double.random(in: 0..<(2 * .pi)), d = Double(r) * sqrt(Double.random(in: 0...1))
            bitmap.plot(cx + Int((cos(a) * d).rounded()), cy + Int((sin(a) * d).rounded()), drawColor.u32)
        }
        canvas.needsDisplay = true
    }

    /// Thick or thin straight line in the selected width
    private func strokeLine(_ a: CGPoint, _ b: CGPoint, width w: Int, _ c: RGBA) {
        let (x0, y0) = pixel(a), (x1, y1) = pixel(b)
        if w <= 1 { bitmap.line(x0, y0, x1, y1, c.u32); return }
        Bitmap.walk(x0, y0, x1, y1) { x, y in
            bitmap.fill(CGRect(x: x - w / 2, y: y - w / 2, width: w, height: w), c)
        }
    }

    private func shapeRect(_ a: CGPoint, _ b: CGPoint, square: Bool) -> CGRect {
        var (x0, y0) = pixel(a), (x1, y1) = pixel(b)
        if square {
            let s = max(abs(x1 - x0), abs(y1 - y0))
            x1 = x0 + (x1 >= x0 ? s : -s); y1 = y0 + (y1 >= y0 ? s : -s)
        }
        if x1 < x0 { swap(&x0, &x1) }
        if y1 < y0 { swap(&y0, &y1) }
        return CGRect(x: x0, y: y0, width: x1 - x0 + 1, height: y1 - y0 + 1)
    }

    /// Rectangle, ellipse or rounded rectangle with the fill style: outline, outline + fill, or fill
    private func drawShape(_ r: CGRect) {
        let w = CGFloat(options.lineWidth), style = options.fillStyle
        let outline = drawColor, inside = style == 2 ? drawColor : otherColor
        let ctx = bitmap.ctx
        func path(_ rect: CGRect) -> CGPath {
            switch tool {
            case .ellipse: return CGPath(ellipseIn: rect, transform: nil)
            case .roundRect:
                let rad = max(0, min(8 - (rect == r ? 0 : w), rect.width / 2, rect.height / 2))
                return CGPath(roundedRect: rect, cornerWidth: rad, cornerHeight: rad, transform: nil)
            default: return CGPath(rect: rect, transform: nil)
            }
        }
        let inner = r.insetBy(dx: w, dy: w)
        if style == 2 {
            ctx.setFillColor(inside.cg); ctx.addPath(path(r)); ctx.fillPath()
            return
        }
        if style == 1, inner.width > 0, inner.height > 0 {
            ctx.setFillColor(inside.cg); ctx.addPath(path(inner)); ctx.fillPath()
        }
        ctx.setFillColor(outline.cg)
        ctx.addPath(path(r))
        if inner.width > 0 && inner.height > 0 { ctx.addPath(path(inner)); ctx.fillPath(using: .evenOdd) } else { ctx.fillPath() }
    }

    private func drawPolygon(closed: Bool) {
        guard let base = strokeBase, polygon.count > 1 else { return }
        bitmap.restore(base)
        let w = options.lineWidth
        if closed && options.fillStyle > 0 && polygon.count > 2 {
            let p = CGMutablePath()
            p.addLines(between: polygon.map { CGPoint(x: floor($0.x) + 0.5, y: floor($0.y) + 0.5) })
            p.closeSubpath()
            bitmap.ctx.setFillColor((options.fillStyle == 2 ? drawColor : otherColor).cg)
            bitmap.ctx.addPath(p)
            bitmap.ctx.fillPath(using: .evenOdd)
            if options.fillStyle == 2 { canvas.needsDisplay = true; return }
        }
        for i in 1..<polygon.count { strokeLine(polygon[i - 1], polygon[i], width: w, drawColor) }
        if closed { strokeLine(polygon[polygon.count - 1], polygon[0], width: w, drawColor) }
        canvas.needsDisplay = true
    }

    private func drawCurve() {
        guard let base = strokeBase, let c = curve else { return }
        bitmap.restore(base)
        let w = options.lineWidth
        // Sample the Bézier and join the points with the same lines as the Line tool
        let n = max(16, Int((hypot(c.b.x - c.a.x, c.b.y - c.a.y) + hypot(c.c1.x - c.a.x, c.c1.y - c.a.y) + hypot(c.c2.x - c.b.x, c.c2.y - c.b.y)) / 2))
        var prev = c.a
        for i in 1...n {
            let t = CGFloat(i) / CGFloat(n), u = 1 - t
            let p = CGPoint(x: u * u * u * c.a.x + 3 * u * u * t * c.c1.x + 3 * u * t * t * c.c2.x + t * t * t * c.b.x,
                            y: u * u * u * c.a.y + 3 * u * u * t * c.c1.y + 3 * u * t * t * c.c2.y + t * t * t * c.b.y)
            strokeLine(prev, p, width: w, drawColor)
            prev = p
        }
        canvas.needsDisplay = true
    }

    private func constrained(_ p: CGPoint, from a: CGPoint, _ e: NSEvent) -> CGPoint {
        guard e.modifierFlags.contains(.shift) else { return p }
        let dx = p.x - a.x, dy = p.y - a.y
        if abs(dx) > 2 * abs(dy) { return CGPoint(x: p.x, y: a.y) }
        if abs(dy) > 2 * abs(dx) { return CGPoint(x: a.x, y: p.y) }
        let s = max(abs(dx), abs(dy))
        return CGPoint(x: a.x + (dx < 0 ? -s : s), y: a.y + (dy < 0 ? -s : s))
    }

    // MARK: Mouse

    func mouseDown(_ p: CGPoint, right r: Bool, event e: NSEvent) {
        right = r
        start = p; last = p
        if tool != .text { commitText() }
        switch tool {
        case .select, .freeSelect:
            if let f = floating, f.rect.contains(p) {
                if e.modifierFlags.contains(.option) {
                    if f.lifted { stamp(f) } else { pushUndo(); f.lifted = true }   // Option-drag leaves a copy behind
                } else {
                    lift(f)
                }
                moving = (p.x - CGFloat(f.x), p.y - CGFloat(f.y))
                return
            }
            dropFloating()
            if tool == .freeSelect { freeTrail = [p] } else { dragBox = CGRect(origin: p, size: .zero) }
        case .pick, .magnifier:
            break
        case .fill:
            pushUndo()
            let (x, y) = pixel(p)
            bitmap.floodFill(x, y, drawColor.u32)
            canvas.needsDisplay = true
        case .text:
            if let tv = textView, canvas.fromImage(textRect).contains(canvas.convert(e.locationInWindow, from: nil)) {
                canvas.window?.makeFirstResponder(tv)
                return
            }
            commitText()
            dropFloating()
            dragBox = CGRect(origin: p, size: .zero)
        case .polygon where !polygon.isEmpty:
            let q = constrained(p, from: polygon.last!, e)
            let near = polygon.count > 2 && hypot(q.x - polygon[0].x, q.y - polygon[0].y) * zoom < 6
            if e.clickCount >= 2 || near {
                drawPolygon(closed: true)
                polygon = []; strokeBase = nil
                return
            }
            polygon.append(q)
            drawPolygon(closed: false)
        case .curve where curve != nil:
            curve!.stage += 1
            if curve!.stage == 1 { curve!.c1 = p; curve!.c2 = p } else { curve!.c2 = p }
            drawCurve()
        default:
            dropFloating()
            pushUndo()
            strokeBase = bitmap.data
            switch tool {
            case .pencil: let (x, y) = pixel(p); bitmap.plot(x, y, drawColor.u32)
            case .brush: let (x, y) = pixel(p); stampBrush(x, y, drawColor)
            case .eraser: erase(from: p, to: p)
            case .airbrush:
                spray()
                airTimer = Timer.scheduledTimer(withTimeInterval: 0.03, repeats: true) { [weak self] _ in self?.spray() }
                RunLoop.main.add(airTimer!, forMode: .eventTracking)
            case .polygon: polygon = [p, p]
            case .curve: curve = (p, p, p, p, 0)
            default: break
            }
            canvas.needsDisplay = true
        }
    }

    private func erase(from a: CGPoint, to b: CGPoint) {
        let s = ToolOptions.eraserPixels[options.eraserSize]
        let (x0, y0) = pixel(a), (x1, y1) = pixel(b)
        Bitmap.walk(x0, y0, x1, y1) { x, y in
            if right {
                // Color eraser: only replaces the drawing color with the background color
                for yy in (y - s / 2)..<(y - s / 2 + s) {
                    for xx in (x - s / 2)..<(x - s / 2 + s) where bitmap.contains(xx, yy) && bitmap.get(xx, yy) == fg.u32 {
                        bitmap.plot(xx, yy, bg.u32)
                    }
                }
            } else {
                bitmap.fill(CGRect(x: x - s / 2, y: y - s / 2, width: s, height: s), bg)
            }
        }
    }

    func mouseDragged(_ p: CGPoint, event e: NSEvent) {
        defer { last = p }
        switch tool {
        case .select, .freeSelect:
            if let f = floating, let m = moving {
                f.x = Int((p.x - m.dx).rounded()); f.y = Int((p.y - m.dy).rounded())
            } else if tool == .freeSelect {
                freeTrail.append(p)
            } else {
                dragBox = shapeRect(start, p, square: false)
                status.size = "\(Int(dragBox!.width))x\(Int(dragBox!.height))"
            }
            canvas.needsDisplay = true
        case .text:
            dragBox = shapeRect(start, p, square: false)
            canvas.needsDisplay = true
        case .pencil:
            let (x0, y0) = pixel(last), (x1, y1) = pixel(p)
            bitmap.line(x0, y0, x1, y1, drawColor.u32)
            canvas.needsDisplay = true
        case .brush:
            let (x0, y0) = pixel(last), (x1, y1) = pixel(p)
            Bitmap.walk(x0, y0, x1, y1) { stampBrush($0, $1, drawColor) }
            canvas.needsDisplay = true
        case .eraser:
            erase(from: last, to: p)
            canvas.needsDisplay = true
        case .airbrush:
            break   // the timer sprays at `last`
        case .line:
            guard let base = strokeBase else { return }
            bitmap.restore(base)
            strokeLine(start, constrained(p, from: start, e), width: options.lineWidth, drawColor)
            canvas.needsDisplay = true
        case .rect, .ellipse, .roundRect:
            guard let base = strokeBase else { return }
            bitmap.restore(base)
            let r = shapeRect(start, p, square: e.modifierFlags.contains(.shift))
            drawShape(r)
            status.size = "\(Int(r.width))x\(Int(r.height))"
            canvas.needsDisplay = true
        case .polygon:
            guard polygon.count > 1 else { return }
            polygon[polygon.count - 1] = constrained(p, from: polygon[polygon.count - 2], e)
            drawPolygon(closed: false)
        case .curve:
            guard var c = curve else { return }
            switch c.stage {
            case 0: c.b = constrained(p, from: c.a, e); c.c1 = c.a; c.c2 = c.b
            case 1: c.c1 = p; c.c2 = p
            default: c.c2 = p
            }
            curve = c
            drawCurve()
        default: break
        }
    }

    func mouseUp(_ p: CGPoint, event e: NSEvent) {
        airTimer?.invalidate(); airTimer = nil
        // Lines and shapes end exactly where the button comes up
        if [.line, .rect, .ellipse, .roundRect, .pencil, .brush, .eraser].contains(tool) || (tool.isSelect && moving == nil && floating == nil) {
            mouseDragged(p, event: e)
        }
        switch tool {
        case .select, .freeSelect:
            if moving != nil { moving = nil; return }
            if tool == .freeSelect, freeTrail.count > 2 {
                let path = CGMutablePath()
                path.addLines(between: freeTrail.map { CGPoint(x: floor($0.x) + 0.5, y: floor($0.y) + 0.5) })
                path.closeSubpath()
                makeFloating(path.boundingBoxOfPath.insetBy(dx: -0.5, dy: -0.5), path: path)
            } else if let box = dragBox, box.width > 1 || box.height > 1 {
                makeFloating(box, path: nil)
            }
            dragBox = nil
            freeTrail = []
            canvas.needsDisplay = true
        case .pick:
            let (x, y) = pixel(p)
            if bitmap.contains(x, y) {
                let c = RGBA(u32: bitmap.get(x, y))
                if right { bg = c } else { fg = c }
            }
            setTool(previousTool)
        case .magnifier:
            setZoom(zoom == 1 ? 4 : 1, around: p)
        case .text:
            if let box = dragBox {
                dragBox = nil
                let r = box.width < 8 || box.height < 8
                    ? CGRect(x: box.minX, y: box.minY, width: 160, height: ceil(PaintDoc.textFont.boundingRectForFont.height) * 2)
                    : box
                startText(in: r)
            }
        case .polygon:
            if polygon.count == 2 && hypot(polygon[1].x - polygon[0].x, polygon[1].y - polygon[0].y) < 1 { polygon = [] }
        case .curve:
            if let c = curve, c.stage >= 2 {
                curve = nil; strokeBase = nil   // done: the curve stays drawn
            } else if let c = curve, c.stage == 0, hypot(c.b.x - c.a.x, c.b.y - c.a.y) < 1 {
                curve = nil; strokeBase = nil
            }
        default:
            if !tool.isShape { strokeBase = nil }
            status.size = ""
            if tool != .polygon && tool != .curve { strokeBase = nil }
        }
    }

    func mouseMoved(_ p: CGPoint) {
        // Polygons follow the pointer between clicks
        if tool == .polygon, polygon.count > 1 {
            polygon[polygon.count - 1] = p
            drawPolygon(closed: false)
        }
    }

    // MARK: Text

    private func startText(in r: CGRect) {
        let tv = PaintTextView(frame: canvas.fromImage(r))
        tv.doc = self
        tv.isRichText = false
        tv.allowsUndo = true
        tv.textContainerInset = .zero
        tv.textContainer?.lineFragmentPadding = 0
        tv.isAutomaticQuoteSubstitutionEnabled = false
        tv.isAutomaticSpellingCorrectionEnabled = false
        tv.isContinuousSpellCheckingEnabled = false
        tv.textColor = fg.ns
        tv.insertionPointColor = W95.black
        textView = tv
        textRect = r
        applyTextFont()
        updateTextBackground()
        canvas.addSubview(tv)
        window.makeFirstResponder(tv)
    }

    private func applyTextFont() {
        guard let tv = textView else { return }
        let f = NSFont(descriptor: PaintDoc.textFont.fontDescriptor, size: PaintDoc.textFont.pointSize * canvas.unit) ?? PaintDoc.textFont
        tv.font = f
        tv.typingAttributes = [.font: f, .foregroundColor: fg.ns]
    }

    private func updateTextBackground() {
        guard let tv = textView else { return }
        tv.drawsBackground = !options.transparent
        tv.backgroundColor = bg.ns
    }

    private func commitText() {
        guard let tv = textView else { return }
        let s = tv.string
        tv.removeFromSuperview()
        textView = nil
        guard !s.isEmpty else { return }
        pushUndo()
        NSGraphicsContext.saveGraphicsState()
        NSGraphicsContext.current = NSGraphicsContext(cgContext: bitmap.ctx, flipped: true)
        if !options.transparent { bitmap.fill(textRect, bg) }
        (s as NSString).draw(with: textRect, options: [.usesLineFragmentOrigin],
                             attributes: [.font: PaintDoc.textFont, .foregroundColor: fg.ns])
        NSGraphicsContext.restoreGraphicsState()
        canvas.needsDisplay = true
    }

    func showFonts() {
        NSFontManager.shared.setSelectedFont(PaintDoc.textFont, isMultiple: false)
        NSFontManager.shared.orderFrontFontPanel(nil)
    }

    func changeTextFont(_ sender: Any?) {
        guard let fm = sender as? NSFontManager else { return }
        PaintDoc.textFont = fm.convert(PaintDoc.textFont)
        applyTextFont()
    }

    // MARK: Edit

    private func pasteboardImage() -> Bitmap? {
        guard let img = NSImage(pasteboard: .general) else { return nil }
        var rect = NSRect(origin: .zero, size: img.size)
        if let rep = img.representations.first, rep.pixelsWide > 0 { rect.size = NSSize(width: rep.pixelsWide, height: rep.pixelsHigh) }
        guard let cg = img.cgImage(forProposedRect: &rect, context: nil, hints: nil) else { return nil }
        return Bitmap(image: cg, background: bg)
    }

    func copySelection() {
        guard let f = floating else { return }
        let rep = NSBitmapImageRep(cgImage: f.bitmap.image())
        let img = NSImage(size: NSSize(width: f.bitmap.width, height: f.bitmap.height))
        img.addRepresentation(rep)
        NSPasteboard.general.clearContents()
        NSPasteboard.general.writeObjects([img])
    }

    func cut() { copySelection(); clearSelection() }

    func clearSelection() {
        guard let f = floating else { return }
        lift(f)
        floating = nil
        status.size = ""
        canvas.needsDisplay = true
    }

    func paste() { if let b = pasteboardImage() { place(b) } }

    /// Puts a picture in as a floating selection at the top-left of the view, enlarging the bitmap if asked
    private func place(_ b: Bitmap) {
        finishPending()
        if b.width > bitmap.width || b.height > bitmap.height {
            let r = messageBox("The image in the clipboard is larger than the bitmap.\nWould you like the bitmap enlarged?",
                               icon: .warning, buttons: ["&Yes", "&No", "Cancel"], cancelIndex: 2, over: window)
            if r == 2 { return }
            if r == 0 { resizeImage(width: max(b.width, bitmap.width), height: max(b.height, bitmap.height)) }
        }
        pushUndo()
        let vis = canvas.toImage(scroll.contentView.bounds.origin)
        floating = Floating(bitmap: b, x: max(0, Int(vis.x)), y: max(0, Int(vis.y)), path: nil, lifted: true)
        if !tool.isSelect { setTool(.select) }
        status.size = "\(b.width)x\(b.height)"
        canvas.needsDisplay = true
    }

    func selectAll() {
        finishPending()
        setTool(.select)
        makeFloating(CGRect(x: 0, y: 0, width: bitmap.width, height: bitmap.height), path: nil)
        canvas.needsDisplay = true
    }

    func copyTo() {
        guard let f = floating else { return }
        let p = NSSavePanel()
        p.nameFieldStringValue = "selection.png"
        p.allowedContentTypes = [.png]
        guard p.runModal() == .OK, let u = p.url else { window.makeKeyAndOrderFront(nil); return }
        window.makeKeyAndOrderFront(nil)
        do { try f.bitmap.encoded(as: .png)?.write(to: u, options: .atomic) } catch {
            messageBox("Cannot save \(u.path).\n\n\(error.localizedDescription)", icon: .error, over: window)
        }
    }

    func pasteFrom() {
        let p = NSOpenPanel()
        p.allowedContentTypes = [.image]
        guard p.runModal() == .OK, let u = p.url else { window.makeKeyAndOrderFront(nil); return }
        window.makeKeyAndOrderFront(nil)
        do { place(try Bitmap.load(u)) } catch {
            messageBox("Cannot open \(u.path).\n\n\(error.localizedDescription)", icon: .error, over: window)
        }
    }

    // MARK: View

    func togglePanel(_ i: Int) {
        switch i {
        case 0: showTools.toggle()
        case 1: showColors.toggle()
        default: showStatus.toggle()
        }
        layoutPanels()
    }

    func setZoom(_ z: CGFloat, around p: CGPoint?) {
        commitText()
        let clip = scroll.contentView
        let center = p ?? canvas.toImage(NSPoint(x: clip.bounds.midX, y: clip.bounds.midY))
        zoom = z
        if zoom < 4 { showGrid = false }
        canvas.updateSize()
        let target = canvas.fromImage(CGRect(origin: center, size: .zero)).origin
        clip.scroll(to: NSPoint(x: max(0, target.x - clip.bounds.width / 2), y: max(0, target.y - clip.bounds.height / 2)))
        scroll.reflectScrolledClipView(clip)
        toolBox.needsDisplay = true
    }

    func zoomStep(_ d: Int) {
        let levels: [CGFloat] = [1, 2, 4, 6, 8]
        let i = levels.firstIndex(of: zoom) ?? 0
        setZoom(levels[max(0, min(levels.count - 1, i + d))], around: nil)
    }

    func toggleGrid() {
        guard zoom >= 4 else { return }
        showGrid.toggle()
        canvas.needsDisplay = true
    }

    /// The picture alone on a black screen; any click or key returns
    func viewBitmap() {
        finishPending()
        guard let screen = window.screen ?? NSScreen.main else { return }
        let w = NSWindow(contentRect: screen.frame, styleMask: [.borderless], backing: .buffered, defer: false)
        w.level = .screenSaver
        w.backgroundColor = .black
        w.isReleasedWhenClosed = false
        let iv = NSImageView(frame: NSRect(origin: .zero, size: screen.frame.size))
        iv.image = NSImage(cgImage: bitmap.image(), size: NSSize(width: bitmap.width, height: bitmap.height))
        iv.imageScaling = .scaleProportionallyDown
        w.contentView = iv
        w.makeKeyAndOrderFront(nil)
        NSCursor.hide()
        defer { NSCursor.unhide(); w.orderOut(nil); window.makeKeyAndOrderFront(nil) }
        while let e = NSApp.nextEvent(matching: [.keyDown, .leftMouseDown, .rightMouseDown], until: .distantFuture, inMode: .default, dequeue: true) {
            _ = e
            break
        }
    }

    // MARK: Image

    /// Runs an image change on the selection if there is one, otherwise on the whole picture
    private func transform(_ f: (Bitmap) -> Bitmap) {
        commitText()
        if let fl = floating {
            lift(fl)
            fl.bitmap = f(fl.bitmap)
            fl.path = nil
            floatCache = nil
            status.size = "\(fl.bitmap.width)x\(fl.bitmap.height)"
        } else {
            pushUndo()
            bitmap = f(bitmap)
            canvas.updateSize()
        }
        canvas.needsDisplay = true
    }

    func flipRotate() {
        var choice = 0, angle = 0
        var radios: [W95Radio] = [], angles: [W95Radio] = []
        let ok = runDialog(title: "Flip and Rotate", client: NSSize(width: 290, height: 120), over: window) { c, _ in
            c.addSubview(W95Group("Flip or rotate", frame: NSRect(x: 8, y: 6, width: 190, height: 106)))
            for (i, t) in ["Flip &horizontal", "Flip &vertical", "&Rotate by angle"].enumerated() {
                let r = W95Radio(t, frame: NSRect(x: 18, y: 24 + CGFloat(i) * 20, width: 150, height: 16), isOn: i == 0)
                r.onSelect = { choice = i; angles.forEach { $0.isEnabled = i == 2 } }
                radios.append(r)
                c.addSubview(r)
            }
            for (i, t) in ["&90°", "&180°", "&270°"].enumerated() {
                let r = W95Radio(t, frame: NSRect(x: 36 + CGFloat(i) * 54, y: 86, width: 52, height: 16), isOn: i == 0)
                r.isEnabled = false
                r.onSelect = { angle = i }
                angles.append(r)
                c.addSubview(r)
            }
            radios.forEach { $0.group = radios }
            angles.forEach { $0.group = angles }
            return { true }
        }
        guard ok else { return }
        transform { b in
            let w = CGFloat(b.width), h = CGFloat(b.height)
            switch (choice, angle) {
            case (0, _): return b.transformed(width: b.width, height: b.height, fill: nil, CGAffineTransform(a: -1, b: 0, c: 0, d: 1, tx: w, ty: 0))
            case (1, _): return b.transformed(width: b.width, height: b.height, fill: nil, CGAffineTransform(a: 1, b: 0, c: 0, d: -1, tx: 0, ty: h))
            case (_, 0): return b.transformed(width: b.height, height: b.width, fill: nil, CGAffineTransform(a: 0, b: 1, c: -1, d: 0, tx: h, ty: 0))
            case (_, 1): return b.transformed(width: b.width, height: b.height, fill: nil, CGAffineTransform(a: -1, b: 0, c: 0, d: -1, tx: w, ty: h))
            default: return b.transformed(width: b.height, height: b.width, fill: nil, CGAffineTransform(a: 0, b: -1, c: 1, d: 0, tx: 0, ty: w))
            }
        }
    }

    func stretchSkew() {
        var sx = 100, sy = 100, kx = 0, ky = 0
        var fields: [W95Field] = []
        let ok = runDialog(title: "Stretch and Skew", client: NSSize(width: 290, height: 170), over: window) { c, d in
            c.addSubview(W95Group("Stretch", frame: NSRect(x: 8, y: 6, width: 190, height: 74)))
            c.addSubview(W95Group("Skew", frame: NSRect(x: 8, y: 88, width: 190, height: 74)))
            let rows: [(String, CGFloat, Int, String)] = [("&Horizontal:", 26, 100, "%"), ("&Vertical:", 52, 100, "%"),
                                                          ("H&orizontal:", 108, 0, "Degrees"), ("V&ertical:", 134, 0, "Degrees")]
            for (label, y, v, unit) in rows {
                c.addSubview(W95Label(label, frame: NSRect(x: 18, y: y + 3, width: 70, height: 14)))
                let f = numberField(v, frame: NSRect(x: 92, y: y, width: 44, height: 21), d)
                c.addSubview(f)
                fields.append(f)
                c.addSubview(W95Label(unit, frame: NSRect(x: 142, y: y + 3, width: 52, height: 14)))
            }
            return {
                let v = fields.map { Int($0.field.stringValue.trimmingCharacters(in: .whitespaces)) }
                guard let a = v[0], let b = v[1], let e = v[2], let f = v[3], (1...500).contains(a), (1...500).contains(b),
                      (-89...89).contains(e), (-89...89).contains(f) else {
                    messageBox("Stretch must be between 1 and 500%, and skew between -89 and 89 degrees.", icon: .warning, over: nil)
                    return false
                }
                sx = a; sy = b; kx = e; ky = f
                return true
            }
        }
        guard ok, (sx, sy, kx, ky) != (100, 100, 0, 0) else { return }
        let fill = bg
        transform { b in
            let t = CGAffineTransform(a: CGFloat(sx) / 100, b: tan(CGFloat(ky) * .pi / 180) * CGFloat(sy) / 100,
                                      c: tan(CGFloat(kx) * .pi / 180) * CGFloat(sx) / 100, d: CGFloat(sy) / 100, tx: 0, ty: 0)
            let box = CGRect(x: 0, y: 0, width: b.width, height: b.height).applying(t)
            let w = min(8000, max(1, Int(ceil(box.width)))), h = min(8000, max(1, Int(ceil(box.height))))
            return b.transformed(width: w, height: h, fill: fill, t.concatenating(CGAffineTransform(translationX: -box.minX, y: -box.minY)))
        }
    }

    func invertColors() { transform { b in let c = b.copy(); c.invert(); return c } }

    func attributes() {
        finishPending()
        var w = bitmap.width, h = bitmap.height
        var fields: [W95Field] = []
        let ok = runDialog(title: "Attributes", client: NSSize(width: 260, height: 90), over: window) { c, d in
            c.addSubview(W95Label("&Width:", frame: NSRect(x: 10, y: 15, width: 50, height: 14)))
            c.addSubview(W95Label("&Height:", frame: NSRect(x: 10, y: 43, width: 50, height: 14)))
            fields = [numberField(w, frame: NSRect(x: 60, y: 12, width: 60, height: 21), d),
                      numberField(h, frame: NSRect(x: 60, y: 40, width: 60, height: 21), d)]
            fields.forEach { c.addSubview($0) }
            c.addSubview(W95Label("pixels", frame: NSRect(x: 126, y: 15, width: 40, height: 14)))
            c.addSubview(W95Label("pixels", frame: NSRect(x: 126, y: 43, width: 40, height: 14)))
            return {
                guard let a = Int(fields[0].field.stringValue), let b = Int(fields[1].field.stringValue),
                      (1...8000).contains(a), (1...8000).contains(b) else {
                    messageBox("Width and height must be whole numbers from 1 to 8000.", icon: .warning, over: nil)
                    return false
                }
                w = a; h = b
                return true
            }
        }
        if ok { resizeImage(width: w, height: h) }
    }

    /// Changes the picture size, cropping or adding background on the right and bottom
    func resizeImage(width w: Int, height h: Int) {
        guard w != bitmap.width || h != bitmap.height else { canvas.needsDisplay = true; return }
        finishPending()
        pushUndo()
        let b = Bitmap(width: w, height: h, fill: bg)
        b.draw(bitmap.image(), in: CGRect(x: 0, y: 0, width: bitmap.width, height: bitmap.height))
        bitmap = b
        canvas.updateSize()
    }

    func clearImage() {
        finishPending()
        pushUndo()
        bitmap.fill(CGRect(x: 0, y: 0, width: bitmap.width, height: bitmap.height), bg)
        canvas.needsDisplay = true
    }

    func editColor(_ i: Int) {
        editingColor = i
        let panel = NSColorPanel.shared
        panel.showsAlpha = false
        panel.setTarget(self)
        panel.setAction(#selector(colorPanelChanged(_:)))
        panel.color = palette[i].ns
        panel.orderFront(nil)
    }

    @objc func colorPanelChanged(_ sender: NSColorPanel) {
        guard let i = editingColor else { return }
        let c = RGBA(sender.color)
        palette[i] = c
        fg = c
    }

    // MARK: Files

    func confirmDiscard() -> Bool {
        commitText()
        guard dirty else { return true }
        tracker.close()
        window.makeKeyAndOrderFront(nil)
        let r = messageBox("Save changes to \(url?.path ?? "untitled")?", icon: .warning,
                           buttons: ["&Yes", "&No", "Cancel"], cancelIndex: 2, over: window)
        switch r {
        case 0: return save()
        case 1: return true
        default: return false
        }
    }

    func newImage() {
        guard confirmDiscard() else { return }
        cancelPending()
        bitmap = Bitmap(width: bitmap.width, height: bitmap.height, fill: .white)
        url = nil
        fileType = .png
        undoStack = []; redoStack = []
        dirty = false
        updateTitle()
        canvas.updateSize()
    }

    func openImage() {
        guard confirmDiscard() else { return }
        let p = NSOpenPanel()
        p.allowedContentTypes = [.image]
        if let u = url { p.directoryURL = u.deletingLastPathComponent() }
        if p.runModal() == .OK, let u = p.url { load(u) }
        window.makeKeyAndOrderFront(nil)
    }

    func load(_ u: URL) {
        do {
            let b = try Bitmap.load(u)
            cancelPending()
            bitmap = b
            url = u
            fileType = Self.type(for: u)
            undoStack = []; redoStack = []
            dirty = false
            updateTitle()
            canvas.updateSize()
            NSDocumentController.shared.noteNewRecentDocumentURL(u)
        } catch {
            messageBox("Cannot open \(u.path).\n\n\(error.localizedDescription)", icon: .error, over: window)
        }
    }

    static let saveTypes: [(String, UTType)] = [("PNG (*.png)", .png), ("24-bit Bitmap (*.bmp)", .bmp), ("JPEG (*.jpg)", .jpeg),
                                                ("GIF (*.gif)", .gif), ("TIFF (*.tif)", .tiff)]

    static func type(for u: URL) -> UTType {
        let t = UTType(filenameExtension: u.pathExtension.lowercased()) ?? .png
        return saveTypes.first { t.conforms(to: $0.1) }?.1 ?? .png
    }

    @discardableResult func save() -> Bool {
        commitText()
        guard let u = url else { return saveAs() }
        return write(to: u, as: fileType)
    }

    @discardableResult func saveAs() -> Bool {
        commitText()
        let p = NSSavePanel()
        let popup = NSPopUpButton(frame: NSRect(x: 90, y: 8, width: 220, height: 26), pullsDown: false)
        popup.addItems(withTitles: Self.saveTypes.map { $0.0 })
        popup.selectItem(at: Self.saveTypes.firstIndex { $0.1 == fileType } ?? 0)
        let label = NSTextField(labelWithString: "Save as type:")
        label.frame = NSRect(x: 0, y: 13, width: 88, height: 17)
        label.alignment = .right
        let accessory = NSView(frame: NSRect(x: 0, y: 0, width: 320, height: 42))
        accessory.addSubview(label)
        accessory.addSubview(popup)
        p.accessoryView = accessory
        let box = TypeChooser(panel: p, popup: popup)
        popup.target = box
        popup.action = #selector(TypeChooser.changed(_:))
        p.allowedContentTypes = [fileType]
        p.nameFieldStringValue = url?.deletingPathExtension().lastPathComponent ?? "untitled"
        if let u = url { p.directoryURL = u.deletingLastPathComponent() }
        p.canCreateDirectories = true
        let ok = withExtendedLifetime(box) { p.runModal() == .OK }
        window.makeKeyAndOrderFront(nil)
        guard ok, let u = p.url else { return false }
        return write(to: u, as: Self.saveTypes[popup.indexOfSelectedItem].1)
    }

    private func write(to u: URL, as type: UTType) -> Bool {
        dropFloating()
        guard let data = bitmap.encoded(as: type) else {
            messageBox("Cannot save \(u.path) in this format.", icon: .error, over: window)
            return false
        }
        do { try data.write(to: u, options: .atomic) } catch {
            messageBox("Cannot save \(u.path).\n\n\(error.localizedDescription)", icon: .error, over: window)
            return false
        }
        url = u
        fileType = type
        dirty = false
        updateTitle()
        NSDocumentController.shared.noteNewRecentDocumentURL(u)
        return true
    }

    func printImage() {
        finishPending()
        let iv = NSImageView(frame: NSRect(x: 0, y: 0, width: bitmap.width, height: bitmap.height))
        iv.image = NSImage(cgImage: bitmap.image(), size: NSSize(width: bitmap.width, height: bitmap.height))
        let info = NSPrintInfo.shared.copy() as! NSPrintInfo
        info.horizontalPagination = .fit
        info.verticalPagination = .fit
        let op = NSPrintOperation(view: iv, printInfo: info)
        op.jobTitle = displayName
        op.run()
    }

    func setAsDesktopPicture() {
        if url == nil || dirty {
            guard messageBox("The picture must be saved before it can be the desktop picture. Save it now?",
                             icon: .warning, buttons: ["OK", "Cancel"], cancelIndex: 1, over: window) == 0, save() else { return }
        }
        guard let u = url, let screen = window.screen ?? NSScreen.main else { return }
        do { try NSWorkspace.shared.setDesktopImageURL(u, for: screen, options: [:]) } catch {
            messageBox("Cannot set the desktop picture.\n\n\(error.localizedDescription)", icon: .error, over: window)
        }
    }

    // MARK: Window

    private var restoreFrame: NSRect?
    var isMaximized: Bool { restoreFrame != nil }

    func toggleMaximize() {
        if let r = restoreFrame {
            restoreFrame = nil
            window.setFrame(r, display: true)
        } else {
            let r = window.frame
            window.setFrame(window.screen?.visibleFrame ?? r, display: true)
            restoreFrame = r
        }
        chrome.titleBar.maximized = isMaximized
    }

    func close() {
        tracker.close()
        guard confirmDiscard() else { return }
        window.close()
    }

    func windowShouldClose(_ sender: NSWindow) -> Bool { confirmDiscard() }
    func windowWillClose(_ notification: Notification) { tracker.close(); AppDelegate.shared.docClosed(self) }
    func windowDidBecomeKey(_ notification: Notification) { AppDelegate.shared.activeDoc = self }
    func windowDidResignKey(_ notification: Notification) { tracker.close() }
    func windowDidMove(_ notification: Notification) { AppDelegate.shared.saveFrame(self) }
    func windowDidResize(_ notification: Notification) { AppDelegate.shared.saveFrame(self) }
}

extension Tool {
    var isSelect: Bool { self == .select || self == .freeSelect }
}

/// Keeps the save panel's extension in step with the "Save as type" menu
final class TypeChooser: NSObject {
    let panel: NSSavePanel, popup: NSPopUpButton
    init(panel: NSSavePanel, popup: NSPopUpButton) { self.panel = panel; self.popup = popup }
    @objc func changed(_ sender: Any?) { panel.allowedContentTypes = [PaintDoc.saveTypes[popup.indexOfSelectedItem].1] }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuItemValidation {
    static var shared: AppDelegate!
    var docs: [PaintDoc] = []
    weak var activeDoc: PaintDoc?

    func applicationWillFinishLaunching(_ note: Notification) {
        NSApp.appearance = NSAppearance(named: .aqua)
        buildMainMenu()
    }

    func applicationDidFinishLaunching(_ note: Notification) {
        if docs.isEmpty { newWindow() }
        NSApp.activate(ignoringOtherApps: true)
    }

    func application(_ application: NSApplication, open urls: [URL]) {
        for u in urls { open(u) }
    }

    func open(_ u: URL) {
        if let d = docs.first(where: { $0.url?.standardizedFileURL == u.standardizedFileURL }) {
            d.window.makeKeyAndOrderFront(nil)
            return
        }
        if docs.count == 1, let d = docs.first, d.url == nil, !d.dirty {
            d.load(u)
            d.window.makeKeyAndOrderFront(nil)
            return
        }
        newWindow().load(u)
    }

    @discardableResult func newWindow() -> PaintDoc {
        var frame = NSRect.zero
        if let a = activeDoc ?? docs.last {
            frame = a.window.frame.offsetBy(dx: 22, dy: -22)
        } else if let s = UserDefaults.standard.string(forKey: "windowFrame") {
            frame = NSRectFromString(s)
        }
        let titleBar = NSRect(x: frame.minX + 40, y: frame.maxY - 22, width: max(0, frame.width - 80), height: 18)
        if frame.width < 420 || frame.height < 380 || !NSScreen.screens.contains(where: { $0.visibleFrame.contains(titleBar) }) {
            let s = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1280, height: 800)
            let w = min(840, s.width - 40), h = min(720, s.height - 40)
            frame = NSRect(x: s.midX - w / 2, y: s.midY - h / 2, width: w, height: h)
        }
        let d = PaintDoc(frame: frame)
        docs.append(d)
        d.window.makeKeyAndOrderFront(nil)
        activeDoc = d
        return d
    }

    func docClosed(_ d: PaintDoc) {
        saveFrame(d)
        docs.removeAll { $0 === d }
        if activeDoc === d { activeDoc = docs.last }
    }

    func saveFrame(_ d: PaintDoc) {
        guard !d.isMaximized else { return }
        UserDefaults.standard.set(NSStringFromRect(d.window.frame), forKey: "windowFrame")
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        for d in docs {
            if !d.confirmDiscard() { return .terminateCancel }
        }
        return .terminateNow
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if !flag {
            if let d = docs.first(where: { $0.window.isMiniaturized }) { d.window.deminiaturize(nil) } else { newWindow() }
        }
        return true
    }

    func about() {
        let v = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        messageBox("Paint 95\nVersion \(v)\n\nA bitmap painter in the style of the Windows 95 Paint.\n\n"
                   + "© 2026 Mauricio Mendivelso. MIT License.\nProvided as is, without warranty.\n"
                   + "Not affiliated with or endorsed by Microsoft.",
                   title: "About Paint 95", icon: .app, over: activeDoc?.window)
    }

    // MARK: Mac menu bar (same commands as the window's own menus)

    private func doc(_ f: (PaintDoc) -> Void) { if let d = activeDoc { f(d) } }
    @objc func newImage(_ s: Any?) { doc { $0.newImage() } }
    @objc func newWindowAction(_ s: Any?) { newWindow() }
    @objc func openImage(_ s: Any?) { if let d = activeDoc { d.openImage() } else { newWindow().openImage() } }
    @objc func saveImage(_ s: Any?) { doc { $0.save() } }
    @objc func saveImageAs(_ s: Any?) { doc { $0.saveAs() } }
    @objc func printImage(_ s: Any?) { doc { $0.printImage() } }
    @objc func desktopPicture(_ s: Any?) { doc { $0.setAsDesktopPicture() } }
    @objc func closeWindow(_ s: Any?) { doc { $0.close() } }
    /// While text is being typed, the Edit commands work on the text box
    private var typing: PaintTextView? { NSApp.keyWindow?.firstResponder as? PaintTextView }
    @objc func undoAction(_ s: Any?) { if let t = typing { t.undoManager?.undo() } else { doc { $0.undo() } } }
    @objc func redoAction(_ s: Any?) { if let t = typing { t.undoManager?.redo() } else { doc { $0.redo() } } }
    @objc func cutAction(_ s: Any?) { if let t = typing { t.cut(nil) } else { doc { $0.cut() } } }
    @objc func copyAction(_ s: Any?) { if let t = typing { t.copy(nil) } else { doc { $0.copySelection() } } }
    @objc func pasteAction(_ s: Any?) { if let t = typing { t.paste(nil) } else { doc { $0.paste() } } }
    @objc func clearAction(_ s: Any?) { doc { $0.clearSelection() } }
    @objc func selectAllAction(_ s: Any?) { if let t = typing { t.selectAll(nil) } else { doc { $0.selectAll() } } }
    @objc func copyToAction(_ s: Any?) { doc { $0.copyTo() } }
    @objc func pasteFromAction(_ s: Any?) { doc { $0.pasteFrom() } }
    @objc func toolBoxAction(_ s: Any?) { doc { $0.togglePanel(0) } }
    @objc func colorBoxAction(_ s: Any?) { doc { $0.togglePanel(1) } }
    @objc func statusBarAction(_ s: Any?) { doc { $0.togglePanel(2) } }
    @objc func normalSize(_ s: Any?) { doc { $0.setZoom(1, around: nil) } }
    @objc func largeSize(_ s: Any?) { doc { $0.setZoom(4, around: nil) } }
    @objc func zoomIn(_ s: Any?) { doc { $0.zoomStep(1) } }
    @objc func zoomOut(_ s: Any?) { doc { $0.zoomStep(-1) } }
    @objc func gridAction(_ s: Any?) { doc { $0.toggleGrid() } }
    @objc func viewBitmapAction(_ s: Any?) { doc { $0.viewBitmap() } }
    @objc func textFontAction(_ s: Any?) { doc { $0.showFonts() } }
    @objc func flipRotateAction(_ s: Any?) { doc { $0.flipRotate() } }
    @objc func stretchSkewAction(_ s: Any?) { doc { $0.stretchSkew() } }
    @objc func invertAction(_ s: Any?) { doc { $0.invertColors() } }
    @objc func attributesAction(_ s: Any?) { doc { $0.attributes() } }
    @objc func clearImageAction(_ s: Any?) { doc { $0.clearImage() } }
    @objc func editColorsAction(_ s: Any?) { doc { d in d.editColor(d.palette.firstIndex(of: d.fg) ?? 0) } }
    @objc func opaqueAction(_ s: Any?) { doc { $0.options.transparent.toggle(); $0.toolBox.needsDisplay = true; $0.canvas.needsDisplay = true } }
    @objc func minimizeAction(_ s: Any?) { doc { $0.window.miniaturize(nil) } }
    @objc func zoomWindowAction(_ s: Any?) { doc { $0.toggleMaximize() } }
    @objc func aboutAction(_ s: Any?) { about() }

    func validateMenuItem(_ item: NSMenuItem) -> Bool {
        guard let d = activeDoc else {
            return [#selector(newWindowAction(_:)), #selector(openImage(_:)), #selector(aboutAction(_:))].contains(item.action)
        }
        if typing != nil, [#selector(cutAction(_:)), #selector(copyAction(_:)), #selector(pasteAction(_:)), #selector(selectAllAction(_:)),
                           #selector(undoAction(_:)), #selector(redoAction(_:))].contains(item.action) {
            return true
        }
        switch item.action {
        case #selector(toolBoxAction(_:)): item.state = d.showTools ? .on : .off
        case #selector(colorBoxAction(_:)): item.state = d.showColors ? .on : .off
        case #selector(statusBarAction(_:)): item.state = d.showStatus ? .on : .off
        case #selector(gridAction(_:)): item.state = d.showGrid ? .on : .off; return d.zoom >= 4
        case #selector(opaqueAction(_:)): item.state = d.options.transparent ? .off : .on
        case #selector(normalSize(_:)): item.state = d.zoom == 1 ? .on : .off
        case #selector(largeSize(_:)): item.state = d.zoom == 4 ? .on : .off
        case #selector(cutAction(_:)), #selector(copyAction(_:)), #selector(clearAction(_:)), #selector(copyToAction(_:)):
            return d.floating != nil
        case #selector(pasteAction(_:)): return NSImage(pasteboard: .general) != nil
        default: break
        }
        return true
    }

    private func buildMainMenu() {
        let main = NSMenu()
        func menu(_ title: String, _ items: [NSMenuItem]) -> NSMenu {
            let m = NSMenu(title: title)
            items.forEach { m.addItem($0) }
            let holder = NSMenuItem()
            holder.submenu = m
            main.addItem(holder)
            return m
        }
        func item(_ title: String, _ action: Selector, _ key: String = "", _ mods: NSEvent.ModifierFlags = [.command],
                  target: AnyObject? = nil) -> NSMenuItem {
            let i = NSMenuItem(title: title, action: action, keyEquivalent: key)
            i.keyEquivalentModifierMask = mods
            i.target = target ?? self
            return i
        }
        let sep = { NSMenuItem.separator() }

        _ = menu("Paint 95", [
            item("About Paint 95", #selector(aboutAction(_:))), sep(),
            item("Hide Paint 95", #selector(NSApplication.hide(_:)), "h", target: NSApp),
            item("Hide Others", #selector(NSApplication.hideOtherApplications(_:)), "h", [.command, .option], target: NSApp),
            item("Show All", #selector(NSApplication.unhideAllApplications(_:)), target: NSApp), sep(),
            item("Quit Paint 95", #selector(NSApplication.terminate(_:)), "q", target: NSApp),
        ])
        _ = menu("File", [
            item("New", #selector(newImage(_:)), "n"),
            item("New Window", #selector(newWindowAction(_:)), "n", [.command, .option]),
            item("Open…", #selector(openImage(_:)), "o"),
            item("Save", #selector(saveImage(_:)), "s"),
            item("Save As…", #selector(saveImageAs(_:)), "s", [.command, .shift]), sep(),
            item("Print…", #selector(printImage(_:)), "p"), sep(),
            item("Set As Desktop Picture", #selector(desktopPicture(_:))), sep(),
            item("Close", #selector(closeWindow(_:)), "w"),
        ])
        _ = menu("Edit", [
            item("Undo", #selector(undoAction(_:)), "z"),
            item("Repeat", #selector(redoAction(_:)), "z", [.command, .shift]), sep(),
            item("Cut", #selector(cutAction(_:)), "x"),
            item("Copy", #selector(copyAction(_:)), "c"),
            item("Paste", #selector(pasteAction(_:)), "v"),
            item("Clear Selection", #selector(clearAction(_:))),
            item("Select All", #selector(selectAllAction(_:)), "a"), sep(),
            item("Copy To…", #selector(copyToAction(_:))),
            item("Paste From…", #selector(pasteFromAction(_:))),
        ])
        _ = menu("View", [
            item("Tool Box", #selector(toolBoxAction(_:)), "t"),
            item("Color Box", #selector(colorBoxAction(_:)), "l"),
            item("Status Bar", #selector(statusBarAction(_:))), sep(),
            item("Normal Size", #selector(normalSize(_:)), "0"),
            item("Large Size", #selector(largeSize(_:)), "4"),
            item("Zoom In", #selector(zoomIn(_:)), "+"),
            item("Zoom Out", #selector(zoomOut(_:)), "-"),
            item("Show Grid", #selector(gridAction(_:)), "g"), sep(),
            item("View Bitmap", #selector(viewBitmapAction(_:)), "f"),
            item("Text Font…", #selector(textFontAction(_:))),
        ])
        _ = menu("Image", [
            item("Flip/Rotate…", #selector(flipRotateAction(_:)), "r"),
            item("Stretch/Skew…", #selector(stretchSkewAction(_:)), "w", [.command, .option]),
            item("Invert Colors", #selector(invertAction(_:)), "i"),
            item("Attributes…", #selector(attributesAction(_:)), "e"),
            item("Clear Image", #selector(clearImageAction(_:)), "n", [.command, .shift]),
        ])
        _ = menu("Options", [
            item("Edit Colors…", #selector(editColorsAction(_:))),
            item("Draw Opaque", #selector(opaqueAction(_:))),
        ])
        let win = menu("Window", [
            item("Minimize", #selector(minimizeAction(_:)), "m"),
            item("Zoom", #selector(zoomWindowAction(_:))), sep(),
            item("Bring All to Front", #selector(NSApplication.arrangeInFront(_:)), target: NSApp),
        ])
        NSApp.windowsMenu = win
        _ = menu("Help", [item("About Paint 95", #selector(aboutAction(_:)))])
        NSApp.mainMenu = main
    }
}

// MARK: - Self-test (used by build.sh: Paint95 --selftest)

func selfTest() -> Bool {
    var ok = true
    func check(_ c: Bool, _ what: String) { if !c { print("FAIL: \(what)"); ok = false } }
    let red = RGBA(255, 0, 0), navy = RGBA(0, 0, 128)
    let b = Bitmap(width: 40, height: 30, fill: .white)
    b.fill(CGRect(x: 5, y: 5, width: 10, height: 8), red)
    b.line(0, 29, 39, 29, navy.u32)
    check(b.get(5, 5) == red.u32 && b.get(14, 12) == red.u32 && b.get(15, 12) == RGBA.white.u32, "rectangle lands on the right pixels")
    check(b.get(0, 29) == navy.u32 && b.get(39, 29) == navy.u32, "line reaches both ends")
    b.floodFill(30, 2, RGBA(0, 255, 0).u32)
    check(b.get(30, 2) == RGBA(0, 255, 0).u32 && b.get(6, 6) == red.u32 && b.get(10, 29) == navy.u32, "flood fill stays in its area")
    let flipped = b.transformed(width: 40, height: 30, fill: nil, CGAffineTransform(a: -1, b: 0, c: 0, d: 1, tx: 40, ty: 0))
    check(flipped.get(34, 5) == red.u32 && flipped.get(25, 5) == red.u32, "flip horizontal")
    let turned = b.transformed(width: 30, height: 40, fill: nil, CGAffineTransform(a: 0, b: 1, c: -1, d: 0, tx: 30, ty: 0))
    check(turned.get(24, 5) == red.u32 && turned.get(0, 0) == navy.u32, "rotate 90°")
    let dir = FileManager.default.temporaryDirectory.appendingPathComponent("paint95-selftest-\(getpid())")
    try? FileManager.default.createDirectory(at: dir, withIntermediateDirectories: true)
    defer { try? FileManager.default.removeItem(at: dir) }
    for (name, type) in PaintDoc.saveTypes {
        let u = dir.appendingPathComponent("t").appendingPathExtension(type.preferredFilenameExtension ?? "img")
        guard let data = b.encoded(as: type), (try? data.write(to: u)) != nil, let back = try? Bitmap.load(u) else {
            check(false, "save and open \(name)"); continue
        }
        check(back.width == 40 && back.height == 30, "\(name) keeps the size")
        let c = RGBA(u32: back.get(8, 8)), n = RGBA(u32: back.get(20, 29))
        let near = { (a: RGBA, e: RGBA) in abs(Int(a.r) - Int(e.r)) < 40 && abs(Int(a.g) - Int(e.g)) < 40 && abs(Int(a.b) - Int(e.b)) < 40 }
        check(type == .jpeg ? near(c, red) : back.get(8, 8) == red.u32 && back.get(20, 29) == navy.u32, "\(name) keeps the colors")
        check(PaintDoc.type(for: u) == type, "\(name) is recognized by its extension")
    }
    print(ok ? "Self-test passed" : "Self-test FAILED")
    return ok
}

// MARK: - Main

let args = CommandLine.arguments
if args.count == 2 && args[1] == "--selftest" {
    exit(selfTest() ? 0 : 1)
}
if args.count == 3 && args[1] == "--write-iconset" {
    do { try W95Icon.writeIconset(Art.appIcon, to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}

W95.appIcon = Art.appIcon
W95.appName = "Paint"
let app = NSApplication.shared
let delegate = AppDelegate()
AppDelegate.shared = delegate
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
