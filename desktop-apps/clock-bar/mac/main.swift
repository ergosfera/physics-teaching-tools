// Clock Bar for Mac: the date and time ("April 5  2:34") in bold, outlined letters on a see-through
// strip that stays on top of everything, readable over any background. Drag it into place once and it
// remembers; a click opens a month calendar. Twin of the Windows app in ../windows.
// Builds with swiftc (Xcode or the Command Line Tools); see build.sh.
import AppKit
import CoreText
import ServiceManagement

// MARK: - Drawing: sizes are in points at scale 1 (same numbers as the Windows app)

enum Art {
    static let dateColor = NSColor(srgbRed: 0xff / 255.0, green: 0xc4 / 255.0, blue: 0x2e / 255.0, alpha: 1)   // amber
    static let timeColor = NSColor.white
    static let dateEm: CGFloat = 17, timeEm: CGFloat = 21, gap: CGFloat = 10, pad: CGFloat = 4

    static let english: DateFormatter = {
        let f = DateFormatter()
        f.locale = Locale(identifier: "en_US_POSIX")
        return f
    }()

    static func dateText(_ t: Date) -> String { english.dateFormat = "MMMM d"; return english.string(from: t) }   // April 5
    static func timeText(_ t: Date) -> String { english.dateFormat = "h:mm"; return english.string(from: t) }     // 2:34, no AM/PM

    static func font(_ em: CGFloat) -> CTFont { NSFont.systemFont(ofSize: em, weight: .bold) as CTFont }

    // The letters as one outline, with the baseline at y = 0
    static func words(_ text: String, _ em: CGFloat) -> CGPath {
        let line = CTLineCreateWithAttributedString(NSAttributedString(string: text, attributes: [.font: font(em)]))
        let path = CGMutablePath()
        for run in CTLineGetGlyphRuns(line) as! [CTRun] {
            let runFont = (CTRunGetAttributes(run) as NSDictionary)[kCTFontAttributeName] as! CTFont
            let n = CTRunGetGlyphCount(run)
            var glyphs = [CGGlyph](repeating: 0, count: n)
            var points = [CGPoint](repeating: .zero, count: n)
            CTRunGetGlyphs(run, CFRange(location: 0, length: n), &glyphs)
            CTRunGetPositions(run, CFRange(location: 0, length: n), &points)
            for i in 0..<n {
                if let g = CTFontCreatePathForGlyph(runFont, glyphs[i], nil) {
                    path.addPath(g, transform: CGAffineTransform(translationX: points[i].x, y: points[i].y))
                }
            }
        }
        return path
    }

    // The bar keeps one size whatever the date and time, so it doesn't jump around: wide enough
    // for the longest month and time, and tall enough for capitals and descenders
    static func barSize(_ scale: CGFloat) -> NSSize {
        let w = words("September 30", dateEm).boundingBoxOfPath.width + gap
            + words("12:59", timeEm).boundingBoxOfPath.width + 2 * pad
        let h = timeEm * 1.25 + 2 * pad
        return NSSize(width: ceil(w * scale), height: ceil(h * scale))
    }

    // Dark outline under bright letters: readable on a white page and on a black window alike
    static func outlined(_ g: CGContext, _ p: CGPath, _ fill: NSColor, _ em: CGFloat) {
        g.setLineJoin(.round)
        g.saveGState()
        g.translateBy(x: 0.6, y: -0.9)
        g.addPath(p)
        g.setStrokeColor(NSColor(white: 0, alpha: 150 / 255.0).cgColor)
        g.setLineWidth(em * 0.30)
        g.strokePath()
        g.restoreGState()
        g.addPath(p)
        g.setStrokeColor(NSColor(white: 0, alpha: 235 / 255.0).cgColor)
        g.setLineWidth(em * 0.18)
        g.strokePath()
        g.addPath(p)
        g.setFillColor(fill.cgColor)
        g.fillPath()
    }

    // Draws in a y-up context of the given size (already scaled)
    static func drawBar(_ g: CGContext, _ size: NSSize, _ t: Date) {
        let d = words(dateText(t), dateEm), tm = words(timeText(t), timeEm)
        // Both sit on one baseline, the pair centered in the bar, capitals centered top to bottom
        let db = d.boundingBoxOfPath, tb = tm.boundingBoxOfPath
        let x = (size.width - (db.width + gap + tb.width)) / 2
        let baseline = (size.height - CTFontGetCapHeight(font(timeEm))) / 2
        var md = CGAffineTransform(translationX: x - db.minX, y: baseline)
        var mt = CGAffineTransform(translationX: x + db.width + gap - tb.minX, y: baseline)
        outlined(g, d.copy(using: &md)!, dateColor, dateEm)
        outlined(g, tm.copy(using: &mt)!, timeColor, timeEm)
    }

    // --render out.png [scale]: draws the bar at 2:34 on 5 April, to check how it looks
    static func renderPNG(_ path: String, scale: CGFloat) -> Bool {
        let size = barSize(scale)
        guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(size.width), pixelsHigh: Int(size.height),
                                         bitsPerSample: 8, samplesPerPixel: 4, hasAlpha: true, isPlanar: false,
                                         colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0),
              let ctx = NSGraphicsContext(bitmapImageRep: rep) else { return false }
        var c = DateComponents()
        (c.year, c.month, c.day, c.hour, c.minute) = (2026, 4, 5, 14, 34)
        let g = ctx.cgContext
        g.scaleBy(x: scale, y: scale)
        drawBar(g, NSSize(width: size.width / scale, height: size.height / scale), Calendar.current.date(from: c)!)
        guard let data = rep.representation(using: .png, properties: [:]) else { return false }
        return (try? data.write(to: URL(fileURLWithPath: path))) != nil
    }
}

// MARK: - App icon (used by build.sh: ClockBar --write-iconset <dir>)

enum IconArt {
    // "2:34" in the bar's colors on a dark rounded square
    static func draw(_ g: CGContext, _ s: CGFloat) {
        let inset = s * 0.098
        let body = CGRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        g.addPath(CGPath(roundedRect: body, cornerWidth: body.width * 0.225, cornerHeight: body.width * 0.225, transform: nil))
        g.setFillColor(CGColor(srgbRed: 0x26 / 255.0, green: 0x26 / 255.0, blue: 0x24 / 255.0, alpha: 1))
        g.fillPath()
        for (text, em, y, color) in [("APR 5", 0.20, 0.66, Art.dateColor), ("2:34", 0.34, 0.38, Art.timeColor)] {
            let p = Art.words(text, CGFloat(em) * s)
            let b = p.boundingBoxOfPath
            let k = min(1, body.width * 0.84 / b.width)
            var m = CGAffineTransform(translationX: s / 2, y: CGFloat(y) * s).scaledBy(x: k, y: k)
                .translatedBy(x: -b.midX, y: -b.midY)
            g.addPath(p.copy(using: &m)!)
            g.setFillColor(color.cgColor)
            g.fillPath()
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

// MARK: - The bar

final class BarView: NSView {
    weak var app: AppDelegate?

    override var isFlipped: Bool { false }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    override func draw(_ dirtyRect: NSRect) {
        guard let g = NSGraphicsContext.current?.cgContext, let app = app else { return }
        // Almost invisible fill, so the spaces between letters still catch the mouse
        g.setFillColor(NSColor(white: 0, alpha: 0.01).cgColor)
        g.fill(bounds)
        g.scaleBy(x: app.scale, y: app.scale)
        Art.drawBar(g, NSSize(width: bounds.width / app.scale, height: bounds.height / app.scale), Date())
    }

    // Drag anywhere to move it (unless locked); a click without moving opens the calendar
    override func mouseDown(with event: NSEvent) {
        guard let win = window, let app = app else { return }
        let start = NSEvent.mouseLocation, origin = win.frame.origin
        var moved = false
        while let e = win.nextEvent(matching: [.leftMouseDragged, .leftMouseUp]), e.type == .leftMouseDragged {
            let p = NSEvent.mouseLocation
            if abs(p.x - start.x) + abs(p.y - start.y) > 3 { moved = true }
            if moved && !app.locked { win.setFrameOrigin(NSPoint(x: origin.x + p.x - start.x, y: origin.y + p.y - start.y)) }
        }
        if moved { if !app.locked { app.savePosition() } }
        else { app.toggleCalendar() }
    }

    override func menu(for event: NSEvent) -> NSMenu? { app?.menu }
}

final class BarPanel: NSPanel {
    override var canBecomeKey: Bool { false }
    override var canBecomeMain: Bool { false }
    // Allowed over the menu bar, so it can sit right at the top of the screen
    override func constrainFrameRect(_ frameRect: NSRect, to screen: NSScreen?) -> NSRect { frameRect }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate, NSPopoverDelegate {
    static let sizes: [CGFloat] = [0.75, 1, 1.25, 1.5, 2, 2.5]
    let defaults = UserDefaults.standard
    var scale: CGFloat = 1
    var locked = false
    var panel: BarPanel!
    var view: BarView!
    var timer: Timer?
    var shown = ""
    let menu = NSMenu()
    var lockItem: NSMenuItem!, loginItem: NSMenuItem!, sizeItem: NSMenuItem!
    let popover = NSPopover()
    var hiddenAt = Date.distantPast

    func applicationDidFinishLaunching(_ notification: Notification) {
        let s = defaults.double(forKey: "scale")
        scale = s > 0 ? CGFloat(s) : 1
        locked = defaults.bool(forKey: "locked")

        let size = Art.barSize(scale)
        panel = BarPanel(contentRect: NSRect(origin: startOrigin(size), size: size),
                         styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.level = .statusBar
        panel.hidesOnDeactivate = false
        panel.isMovable = false
        panel.collectionBehavior = [.canJoinAllSpaces, .stationary, .fullScreenAuxiliary, .ignoresCycle]
        view = BarView(frame: NSRect(origin: .zero, size: size))
        view.app = self
        panel.contentView = view
        panel.orderFrontRegardless()

        buildMenu()
        let picker = NSDatePicker()
        picker.datePickerStyle = .clockAndCalendar
        picker.datePickerElements = .yearMonthDay
        picker.isBezeled = false
        picker.drawsBackground = false
        picker.sizeToFit()
        let holder = NSViewController()
        holder.view = NSView(frame: NSRect(x: 0, y: 0, width: picker.frame.width + 16, height: picker.frame.height + 16))
        picker.setFrameOrigin(NSPoint(x: 8, y: 8))
        holder.view.addSubview(picker)
        popover.contentViewController = holder
        popover.behavior = .transient
        popover.delegate = self

        redraw(force: true)
        // Twice a second, so the minute turns over on time
        timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in self?.redraw(force: false) }
        NotificationCenter.default.addObserver(forName: NSApplication.didChangeScreenParametersNotification, object: nil, queue: .main) { [weak self] _ in
            self?.keepOnScreen()
        }
    }

    // Where it was left; the first time, at the top center of the main screen, under the menu bar
    func startOrigin(_ size: NSSize) -> NSPoint {
        if defaults.object(forKey: "x") != nil {
            let p = NSPoint(x: defaults.double(forKey: "x"), y: defaults.double(forKey: "y"))
            if NSScreen.screens.contains(where: { $0.frame.intersects(NSRect(origin: p, size: size)) }) { return p }
        }
        let vf = (NSScreen.main ?? NSScreen.screens[0]).visibleFrame
        return NSPoint(x: vf.midX - size.width / 2, y: vf.maxY - size.height - 6 * scale)
    }

    func keepOnScreen() {
        panel.setFrameOrigin(startOrigin(panel.frame.size))
    }

    func savePosition() {
        defaults.set(Double(panel.frame.origin.x), forKey: "x")
        defaults.set(Double(panel.frame.origin.y), forKey: "y")
    }

    func redraw(force: Bool) {
        let now = Date()
        let key = Art.english.string(from: now, format: "yyyyMMddHHmm") + "\(scale)"
        if !force && key == shown { return }
        shown = key
        let f = DateFormatter()
        f.dateStyle = .full
        f.timeStyle = .short
        view.toolTip = f.string(from: now)
        view.needsDisplay = true
    }

    func setScale(_ s: CGFloat) {
        // Grow or shrink around the center, so it stays where it was put
        let c = NSPoint(x: panel.frame.midX, y: panel.frame.midY)
        scale = s
        defaults.set(Double(s), forKey: "scale")
        let size = Art.barSize(s)
        panel.setFrame(NSRect(x: c.x - size.width / 2, y: c.y - size.height / 2, width: size.width, height: size.height), display: true)
        savePosition()
        redraw(force: true)
    }

    // ── The month calendar that opens on a click ──

    func toggleCalendar() {
        // Clicking the bar first closes the open calendar; don't reopen it right away
        if popover.isShown { popover.performClose(nil); return }
        if Date().timeIntervalSince(hiddenAt) < 0.3 { return }
        if let picker = popover.contentViewController?.view.subviews.first as? NSDatePicker { picker.dateValue = Date() }
        NSApp.activate(ignoringOtherApps: true)
        // Below the bar, or above it in the lower half of the screen
        let screen = panel.screen ?? NSScreen.main!
        let edge: NSRectEdge = panel.frame.midY > screen.frame.midY ? .minY : .maxY
        popover.show(relativeTo: view.bounds, of: view, preferredEdge: edge)
    }

    func popoverDidClose(_ notification: Notification) { hiddenAt = Date() }

    // ── Right-click menu ──

    func buildMenu() {
        menu.delegate = self
        menu.addItem(withTitle: "Show calendar", action: #selector(showCalendar), keyEquivalent: "").target = self
        menu.addItem(.separator())
        sizeItem = NSMenuItem(title: "Size", action: nil, keyEquivalent: "")
        let sub = NSMenu()
        for s in Self.sizes {
            let item = NSMenuItem(title: "\(Int((s * 100).rounded())) %", action: #selector(pickSize(_:)), keyEquivalent: "")
            item.target = self
            item.representedObject = s
            sub.addItem(item)
        }
        sizeItem.submenu = sub
        menu.addItem(sizeItem)
        lockItem = menu.addItem(withTitle: "Lock position", action: #selector(toggleLock), keyEquivalent: "")
        lockItem.target = self
        loginItem = menu.addItem(withTitle: "Open at Login", action: #selector(toggleLogin), keyEquivalent: "")
        loginItem.target = self
        if #unavailable(macOS 13) { loginItem.isHidden = true }
        menu.addItem(.separator())
        menu.addItem(withTitle: "Quit Clock Bar", action: #selector(quit), keyEquivalent: "").target = self
    }

    func menuWillOpen(_ menu: NSMenu) {
        lockItem.state = locked ? .on : .off
        if #available(macOS 13, *) { loginItem.state = SMAppService.mainApp.status == .enabled ? .on : .off }
        for item in sizeItem.submenu!.items {
            item.state = abs((item.representedObject as! CGFloat) - scale) < 0.01 ? .on : .off
        }
    }

    @objc func showCalendar() { if !popover.isShown { toggleCalendar() } }
    @objc func pickSize(_ sender: NSMenuItem) { setScale(sender.representedObject as! CGFloat) }

    @objc func toggleLock() {
        locked.toggle()
        defaults.set(locked, forKey: "locked")
    }

    @objc func toggleLogin() {
        guard #available(macOS 13, *) else { return }
        do {
            if SMAppService.mainApp.status == .enabled { try SMAppService.mainApp.unregister() }
            else { try SMAppService.mainApp.register() }
        } catch {
            let a = NSAlert()
            a.messageText = "Couldn't change Open at Login"
            a.informativeText = "Move Clock Bar into Applications and try again.\n\n\(error.localizedDescription)"
            NSApp.activate(ignoringOtherApps: true)
            a.runModal()
        }
    }

    @objc func quit() { NSApp.terminate(nil) }
}

extension DateFormatter {
    func string(from date: Date, format: String) -> String {
        let old = dateFormat
        dateFormat = format
        defer { dateFormat = old }
        return string(from: date)
    }
}

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}
if (args.count == 3 || args.count == 4) && args[1] == "--render" {
    let s = args.count == 4 ? CGFloat(Double(args[3]) ?? 3) : 3
    exit(Art.renderPNG(args[2], scale: s) ? 0 : 1)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
