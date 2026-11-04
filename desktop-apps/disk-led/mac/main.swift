// Disk LED for macOS: the hard-disk activity light of an old PC, in the menu bar.
// It blinks when the drives read or write, like the LED on the front of a computer with a
// mechanical disk; brighter with more data. A click opens a live graph.
// Reads the drives' own counters (IOKit block-storage statistics), so it works with SSDs.
// Single-file AppKit app; build.sh makes Disk LED.app. Twin of the Windows app in ../windows.
import AppKit
import IOKit
import ServiceManagement

// MARK: - Drive counters (since the Mac started, all drives together)

struct DiskStats {
    var readBytes: UInt64 = 0, writeBytes: UInt64 = 0
    var readOps: UInt64 = 0, writeOps: UInt64 = 0
    var busyNs: UInt64 = 0      // time spent reading + writing
    var drives = 0
}

enum Disk {
    static var entries: [io_registry_entry_t] = []
    static var listed = Date.distantPast
    // Changes whenever the drive list is re-read differently, so a jump in the totals isn't shown as activity
    static var generation = 0
    static var lastIDs: [UInt64] = []

    static func refreshList() {
        var it: io_iterator_t = 0
        guard IOServiceGetMatchingServices(mach_port_t(0), IOServiceMatching("IOBlockStorageDriver"), &it) == KERN_SUCCESS else { return }
        defer { IOObjectRelease(it) }
        var found: [io_registry_entry_t] = []
        var ids: [UInt64] = []
        while true {
            let e = IOIteratorNext(it)
            if e == 0 { break }
            var id: UInt64 = 0
            IORegistryEntryGetRegistryEntryID(e, &id)
            found.append(e)
            ids.append(id)
        }
        for e in entries { IOObjectRelease(e) }
        entries = found
        if ids != lastIDs { generation += 1; lastIDs = ids }
        listed = Date()
    }

    static func read() -> DiskStats {
        if Date().timeIntervalSince(listed) > 5 { refreshList() }
        var s = DiskStats()
        for e in entries {
            guard let p = IORegistryEntryCreateCFProperty(e, "Statistics" as CFString, kCFAllocatorDefault, 0)?
                    .takeRetainedValue() as? [String: Any] else { continue }
            func n(_ k: String) -> UInt64 { (p[k] as? NSNumber)?.uint64Value ?? 0 }
            s.readBytes &+= n("Bytes (Read)"); s.writeBytes &+= n("Bytes (Write)")
            s.readOps &+= n("Operations (Read)"); s.writeOps &+= n("Operations (Write)")
            s.busyNs &+= n("Total Time (Read)") &+ n("Total Time (Write)")
            s.drives += 1
        }
        return s
    }
}

func rateText(_ bytesPerSec: Double) -> String {
    let units = ["B/s", "KB/s", "MB/s", "GB/s"]
    var v = bytesPerSec, i = 0
    while v >= 1000 && i < units.count - 1 { v /= 1000; i += 1 }
    return i == 0 ? String(format: "%.0f %@", v, units[i]) : String(format: "%.1f %@", v, units[i])
}

// MARK: - Lamps

enum LampStyle: String, CaseIterable {
    case amber, red, green, blue, pair
    var title: String {
        switch self {
        case .amber: return "One Lamp: Amber"
        case .red: return "One Lamp: Red"
        case .green: return "One Lamp: Green"
        case .blue: return "One Lamp: Blue"
        case .pair: return "Two Lamps: Read (Green) and Write (Red)"
        }
    }
}

enum LED {
    static let amber = NSColor(srgbRed: 1.00, green: 0.66, blue: 0.10, alpha: 1)
    static let red = NSColor(srgbRed: 1.00, green: 0.22, blue: 0.18, alpha: 1)
    static let green = NSColor(srgbRed: 0.20, green: 1.00, blue: 0.30, alpha: 1)
    static let blue = NSColor(srgbRed: 0.25, green: 0.55, blue: 1.00, alpha: 1)

    static func color(_ s: LampStyle) -> NSColor {
        switch s { case .amber: return amber; case .red: return red; case .green: return green; case .blue: return blue; case .pair: return green }
    }

    // level 0 = dark, 1 = full brightness
    static func draw(_ color: NSColor, level: CGFloat, center: CGPoint, r: CGFloat) {
        let dim = color.blended(withFraction: 0.78, of: .black) ?? .black
        let lit = dim.blended(withFraction: level, of: color) ?? color
        if level > 0.05 {
            // Glow around the lit lamp
            let g = NSGradient(colors: [color.withAlphaComponent(0.55 * level), color.withAlphaComponent(0)])!
            g.draw(in: NSBezierPath(ovalIn: NSRect(x: center.x - r * 1.9, y: center.y - r * 1.9, width: r * 3.8, height: r * 3.8)),
                   relativeCenterPosition: .zero)
        }
        let body = NSBezierPath(ovalIn: NSRect(x: center.x - r, y: center.y - r, width: 2 * r, height: 2 * r))
        lit.setFill(); body.fill()
        NSColor.black.withAlphaComponent(0.55).setStroke(); body.lineWidth = 0.8; body.stroke()
        // Little highlight, like a plastic dome
        NSColor.white.withAlphaComponent(0.25 + 0.35 * level).setFill()
        NSBezierPath(ovalIn: NSRect(x: center.x - r * 0.55, y: center.y + r * 0.05, width: r * 0.7, height: r * 0.55)).fill()
    }

    static func image(_ style: LampStyle, read: CGFloat, write: CGFloat) -> NSImage {
        if style == .pair {
            return NSImage(size: NSSize(width: 30, height: 18), flipped: false) { _ in
                draw(green, level: read, center: CGPoint(x: 8, y: 9), r: 4.5)
                draw(red, level: write, center: CGPoint(x: 22, y: 9), r: 4.5)
                return true
            }
        }
        return NSImage(size: NSSize(width: 18, height: 18), flipped: false) { _ in
            draw(color(style), level: max(read, write), center: CGPoint(x: 9, y: 9), r: 4.8)
            return true
        }
    }
}

// MARK: - Drive sounds: short synthesized clicks, like the heads of a hard disk seeking

enum Clicks {
    static let rate = 22050

    // A few milliseconds of decaying noise over a low thump; each variant a little different
    static func wav(seed: UInt32, length: Double, thump: Double) -> Data {
        let n = Int(Double(rate) * length)
        var x = seed
        var samples = [Int16](repeating: 0, count: n)
        var lp = 0.0
        for i in 0..<n {
            x = x &* 1664525 &+ 1013904223
            let noise = Double(Int32(bitPattern: x)) / Double(Int32.max)
            lp += (noise - lp) * 0.45                         // soften the hiss
            let t = Double(i) / Double(rate)
            let env = exp(-t / (length * 0.22))
            let low = sin(2 * .pi * thump * t) * exp(-t / (length * 0.35))
            samples[i] = Int16(max(-1, min(1, (lp * 0.55 + low * 0.45) * env)) * 9000)
        }
        var d = Data()
        func u32(_ v: UInt32) { var le = v.littleEndian; d.append(Data(bytes: &le, count: 4)) }
        func u16(_ v: UInt16) { var le = v.littleEndian; d.append(Data(bytes: &le, count: 2)) }
        d.append("RIFF".data(using: .ascii)!); u32(UInt32(36 + n * 2)); d.append("WAVE".data(using: .ascii)!)
        d.append("fmt ".data(using: .ascii)!); u32(16); u16(1); u16(1); u32(UInt32(rate)); u32(UInt32(rate * 2)); u16(2); u16(16)
        d.append("data".data(using: .ascii)!); u32(UInt32(n * 2))
        samples.withUnsafeBufferPointer { d.append(Data(buffer: $0)) }
        return d
    }

    static let sounds: [NSSound] = [
        (11, 0.010, 180.0), (23, 0.007, 240.0), (37, 0.013, 150.0), (41, 0.006, 300.0),
    ].compactMap { NSSound(data: wav(seed: $0.0, length: $0.1, thump: $0.2)) }
}

// MARK: - Live graph: reading grows up, writing grows down

final class GraphView: NSView {
    static let samples = 120          // 60 s at two samples a second
    var rd: [Double] = []
    var wr: [Double] = []
    var busy = 0.0
    var readColor = LED.green, writeColor = LED.red
    let rdLabel = NSTextField(labelWithString: "")
    let wrLabel = NSTextField(labelWithString: "")
    let busyLabel = NSTextField(labelWithString: "")

    override init(frame: NSRect) {
        super.init(frame: frame)
        for l in [rdLabel, wrLabel, busyLabel] {
            l.font = NSFont.monospacedDigitSystemFont(ofSize: 13, weight: .semibold)
            addSubview(l)
        }
        busyLabel.textColor = .white
        busyLabel.alignment = .center
        let hint = NSTextField(labelWithString: "Last 60 s · drag to keep it open · right-click the lamp for options")
        hint.font = NSFont.systemFont(ofSize: 10)
        hint.textColor = .secondaryLabelColor
        hint.frame = NSRect(x: 12, y: 6, width: frame.width - 24, height: 14)
        addSubview(hint)
        rdLabel.frame = NSRect(x: 12, y: frame.height - 26, width: 150, height: 18)
        busyLabel.frame = NSRect(x: frame.width / 2 - 60, y: frame.height - 26, width: 120, height: 18)
        wrLabel.frame = NSRect(x: frame.width - 162, y: frame.height - 26, width: 150, height: 18)
        wrLabel.alignment = .right
    }
    required init?(coder: NSCoder) { fatalError() }

    func push(read r: Double, write w: Double, busy b: Double) {
        rd.append(r); wr.append(w)
        if rd.count > Self.samples { rd.removeFirst(); wr.removeFirst() }
        busy = b
        rdLabel.stringValue = "R " + rateText(r)
        wrLabel.stringValue = "W " + rateText(w)
        busyLabel.stringValue = String(format: "Busy %.0f %%", b * 100)
        rdLabel.textColor = readColor
        wrLabel.textColor = writeColor
        needsDisplay = true
    }

    // Round the scale up to 1, 2 or 5 × a power of ten, so the grid labels are friendly
    static func niceMax(_ v: Double) -> Double {
        let v = max(v, 100_000)
        let p = pow(10, floor(log10(v)))
        for m in [1.0, 2.0, 5.0, 10.0] where m * p >= v { return m * p }
        return 10 * p
    }

    override func draw(_ dirty: NSRect) {
        let plot = NSRect(x: 12, y: 26, width: bounds.width - 24, height: bounds.height - 58)
        NSColor.black.withAlphaComponent(0.85).setFill()
        NSBezierPath(roundedRect: plot, xRadius: 6, yRadius: 6).fill()

        let mid = plot.midY, half = plot.height / 2 - 4
        let topR = Self.niceMax(rd.max() ?? 0), topW = Self.niceMax(wr.max() ?? 0)

        NSColor.white.withAlphaComponent(0.10).setStroke()
        for f in [-1.0, -0.5, 0.5, 1.0] {
            let y = mid + CGFloat(f) * half
            let l = NSBezierPath(); l.move(to: NSPoint(x: plot.minX, y: y)); l.line(to: NSPoint(x: plot.maxX, y: y))
            l.lineWidth = 0.5; l.stroke()
        }
        NSColor.white.withAlphaComponent(0.30).setStroke()
        let axis = NSBezierPath(); axis.move(to: NSPoint(x: plot.minX, y: mid)); axis.line(to: NSPoint(x: plot.maxX, y: mid))
        axis.lineWidth = 0.8; axis.stroke()

        let step = plot.width / CGFloat(Self.samples - 1)
        func area(_ data: [Double], top: Double, sign: CGFloat, color: NSColor) {
            guard data.count > 1 else { return }
            let x0 = plot.maxX - CGFloat(data.count - 1) * step
            let line = NSBezierPath()
            for (i, v) in data.enumerated() {
                let p = NSPoint(x: x0 + CGFloat(i) * step, y: mid + sign * CGFloat(min(v / top, 1)) * half)
                i == 0 ? line.move(to: p) : line.line(to: p)
            }
            let fill = line.copy() as! NSBezierPath
            fill.line(to: NSPoint(x: plot.maxX, y: mid)); fill.line(to: NSPoint(x: x0, y: mid)); fill.close()
            NSGradient(colors: [color.withAlphaComponent(0.55), color.withAlphaComponent(0.08)])!
                .draw(in: fill, angle: sign > 0 ? -90 : 90)
            color.setStroke(); line.lineWidth = 1.5; line.lineJoinStyle = .round; line.stroke()
        }
        NSGraphicsContext.current?.saveGraphicsState()
        NSBezierPath(roundedRect: plot, xRadius: 6, yRadius: 6).addClip()
        area(rd, top: topR, sign: 1, color: readColor)
        area(wr, top: topW, sign: -1, color: writeColor)
        NSGraphicsContext.current?.restoreGraphicsState()

        let attrs: [NSAttributedString.Key: Any] = [.font: NSFont.monospacedDigitSystemFont(ofSize: 9, weight: .regular),
                                                    .foregroundColor: NSColor.white.withAlphaComponent(0.55)]
        ("read " + rateText(topR) as NSString).draw(at: NSPoint(x: plot.minX + 4, y: mid + half - 11), withAttributes: attrs)
        ("write " + rateText(topW) as NSString).draw(at: NSPoint(x: plot.minX + 4, y: mid - half + 1), withAttributes: attrs)
    }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate, NSPopoverDelegate {
    let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    let menu = NSMenu()
    let popover = NSPopover()
    let graph = GraphView(frame: NSRect(x: 0, y: 0, width: 420, height: 220))
    let readItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    let writeItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    let busyItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    let soundItem = NSMenuItem(title: "Drive Sounds", action: #selector(toggleSound), keyEquivalent: "")
    let loginItem = NSMenuItem(title: "Open at Login", action: #selector(toggleLogin), keyEquivalent: "")
    var styleItems: [NSMenuItem] = []

    var last = Disk.read()
    var lastHalf = Disk.read()
    var lastHalfTime = Date()
    var gen = Disk.generation
    var readRate = 0.0, writeRate = 0.0, busy = 0.0
    var readLevel: CGFloat = 0, writeLevel: CGFloat = 0
    var shownRead: CGFloat = -1, shownWrite: CGFloat = -1
    var style = LampStyle(rawValue: UserDefaults.standard.string(forKey: "style") ?? "") ?? .amber
    var sound = UserDefaults.standard.bool(forKey: "sound")
    var lastClick = Date.distantPast

    func applicationDidFinishLaunching(_ n: Notification) {
        menu.delegate = self
        for i in [readItem, writeItem, busyItem] { i.isEnabled = false; menu.addItem(i) }
        menu.addItem(.separator())
        for s in LampStyle.allCases {
            let i = NSMenuItem(title: s.title, action: #selector(pickStyle(_:)), keyEquivalent: "")
            i.target = self
            i.representedObject = s.rawValue
            styleItems.append(i)
            menu.addItem(i)
        }
        menu.addItem(.separator())
        soundItem.target = self; loginItem.target = self
        soundItem.toolTip = "Soft clicks with each burst of activity, like the heads of a hard disk seeking"
        menu.addItem(soundItem)
        if #available(macOS 13, *) { menu.addItem(loginItem) }
        menu.addItem(.separator())
        menu.addItem(withTitle: "About Disk LED", action: #selector(about), keyEquivalent: "").target = self
        menu.addItem(NSMenuItem(title: "Quit Disk LED", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))
        // Left click: live graph. Right click (or Control-click): the menu
        item.button?.target = self
        item.button?.action = #selector(clicked)
        item.button?.sendAction(on: [.leftMouseUp, .rightMouseUp])
        let vc = NSViewController()
        vc.view = graph
        popover.contentViewController = vc
        popover.behavior = .transient
        popover.appearance = NSAppearance(named: .darkAqua)
        popover.delegate = self
        applyStyleColors()
        refreshMenu()
        render(force: true)

        // 30 ticks a second: each burst of disk activity lights the lamp for a moment, steady work keeps it lit
        let t = Timer(timeInterval: 1.0 / 30, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(t, forMode: .common)
    }

    // Brighter with more data: a few kilobytes give a dim blink, megabytes a full one
    static func level(bytes: UInt64, ops: UInt64, before: CGFloat) -> CGFloat {
        if ops > 0 || bytes > 0 { return min(1, 0.5 + 0.08 * CGFloat(log10(Double(max(bytes, 512))))) }
        let l = before * 0.35
        return l < 0.03 ? 0 : l
    }

    func tick() {
        let now = Disk.read()
        if Disk.generation != gen {
            // A drive came or went: start counting again from here
            gen = Disk.generation
            last = now; lastHalf = now; lastHalfTime = Date()
            return
        }
        let dro = now.readOps &- last.readOps, dwo = now.writeOps &- last.writeOps
        let drb = now.readBytes &- last.readBytes, dwb = now.writeBytes &- last.writeBytes
        readLevel = Self.level(bytes: drb, ops: dro, before: readLevel)
        writeLevel = Self.level(bytes: dwb, ops: dwo, before: writeLevel)
        last = now

        // Drive sounds: a click for a burst, not more than about 20 a second, with some randomness
        if sound && (dro > 0 || dwo > 0) && Date().timeIntervalSince(lastClick) > 0.045 && Double.random(in: 0..<1) < 0.75 {
            if let s = Clicks.sounds.randomElement() {
                s.stop()
                s.volume = Float.random(in: 0.25...0.6)
                s.play()
            }
            lastClick = Date()
        }

        // Two samples a second feed the speeds, the busy share and the graph
        let dt = Date().timeIntervalSince(lastHalfTime)
        if dt >= 0.5 {
            readRate = Double(now.readBytes &- lastHalf.readBytes) / dt
            writeRate = Double(now.writeBytes &- lastHalf.writeBytes) / dt
            busy = min(1, Double(now.busyNs &- lastHalf.busyNs) / 1e9 / dt)
            lastHalf = now; lastHalfTime = Date()
            graph.push(read: readRate, write: writeRate, busy: busy)
            refreshMenu()
        }
        render(force: false)
    }

    @objc func clicked() {
        let e = NSApp.currentEvent
        if e?.type == .rightMouseUp || e?.modifierFlags.contains(.control) == true {
            popover.performClose(nil)
            item.menu = menu
            item.button?.performClick(nil)
            item.menu = nil       // so the next left click opens the graph again
        } else if popover.isShown {
            popover.performClose(nil)
        } else if let b = item.button {
            popover.show(relativeTo: b.bounds, of: b, preferredEdge: .minY)
            NSApp.activate(ignoringOtherApps: true)
        }
    }

    // Dragging the graph away from the menu bar turns it into a small window that stays open
    func popoverShouldDetach(_ popover: NSPopover) -> Bool { true }

    func render(force: Bool) {
        // Redraw only when the lamp visibly changes, to keep CPU near zero
        if !force && abs(readLevel - shownRead) < 0.02 && abs(writeLevel - shownWrite) < 0.02 { return }
        shownRead = readLevel; shownWrite = writeLevel
        item.button?.image = LED.image(style, read: readLevel, write: writeLevel)
    }

    func refreshMenu() {
        readItem.attributedTitle = colored("● Read     \(rateText(readRate))", graph.readColor)
        writeItem.attributedTitle = colored("● Write    \(rateText(writeRate))", graph.writeColor)
        busyItem.attributedTitle = colored(String(format: "● Busy     %.0f %%", busy * 100), busy > 0.9 ? LED.amber : NSColor.secondaryLabelColor)
        item.button?.toolTip = "Read \(rateText(readRate)) · Write \(rateText(writeRate)) · Busy \(Int((busy * 100).rounded())) %"
        for i in styleItems { i.state = (i.representedObject as? String) == style.rawValue ? .on : .off }
        soundItem.state = sound ? .on : .off
        if #available(macOS 13, *) { loginItem.state = SMAppService.mainApp.status == .enabled ? .on : .off }
    }

    func colored(_ s: String, _ c: NSColor) -> NSAttributedString {
        let a = NSMutableAttributedString(string: s, attributes: [.font: NSFont.monospacedDigitSystemFont(ofSize: 13, weight: .regular),
                                                                  .foregroundColor: NSColor.labelColor])
        a.addAttribute(.foregroundColor, value: c, range: NSRange(location: 0, length: 1))
        return a
    }

    func applyStyleColors() {
        graph.readColor = style == .pair ? LED.green : LED.color(style)
        graph.writeColor = style == .pair ? LED.red : (LED.color(style).blended(withFraction: 0.35, of: .white) ?? LED.color(style))
    }

    func menuWillOpen(_ menu: NSMenu) { refreshMenu() }

    @objc func pickStyle(_ sender: NSMenuItem) {
        guard let raw = sender.representedObject as? String, let s = LampStyle(rawValue: raw) else { return }
        style = s
        UserDefaults.standard.set(raw, forKey: "style")
        applyStyleColors()
        refreshMenu()
        render(force: true)
    }

    @objc func toggleSound() {
        sound.toggle()
        UserDefaults.standard.set(sound, forKey: "sound")
        refreshMenu()
    }

    @objc func about() {
        let a = NSAlert()
        a.messageText = "Disk LED 1.0"
        a.informativeText = """
        The hard-disk activity light of an old PC, for any drive, SSDs included. It blinks when \
        the drives read or write, brighter with more data. "Busy" is the share of time the drives \
        spent working: near 100 % for a while means they are the bottleneck.

        It reads the drives' own activity counters, the same ones Activity Monitor uses. \
        It collects no data and never connects to the internet.

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
            a.informativeText = "Move Disk LED into Applications and try again.\n\n\(error.localizedDescription)"
            a.runModal()
        }
        refreshMenu()
    }
}

// MARK: - App icon (used by build.sh: DiskLED --write-iconset <dir>)

enum IconArt {
    // The front of an old PC case: a dark bezel with a drive slot and a lit amber lamp
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        NSColor(srgbRed: 0.83, green: 0.82, blue: 0.78, alpha: 1).setFill()             // beige case
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.2, yRadius: body.width * 0.2).fill()
        NSColor(white: 0.25, alpha: 1).setFill()
        NSBezierPath(roundedRect: NSRect(x: s * 0.2, y: s * 0.56, width: s * 0.6, height: s * 0.1), xRadius: s * 0.02, yRadius: s * 0.02).fill()
        NSColor(white: 0.45, alpha: 1).setFill()
        NSBezierPath(rect: NSRect(x: s * 0.24, y: s * 0.6, width: s * 0.52, height: s * 0.02)).fill()
        LED.draw(LED.amber, level: 1, center: CGPoint(x: s * 0.66, y: s * 0.33), r: s * 0.075)
        LED.draw(LED.green, level: 0.9, center: CGPoint(x: s * 0.34, y: s * 0.33), r: s * 0.075)
    }

    static func writeIconset(to dir: String) throws {
        try FileManager.default.createDirectory(atPath: dir, withIntermediateDirectories: true)
        for base in [16, 32, 128, 256, 512] {
            for scale in [1, 2] {
                let px = base * scale
                guard let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: px, pixelsHigh: px, bitsPerSample: 8,
                                                 samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB,
                                                 bytesPerRow: 0, bitsPerPixel: 0),
                      let ctx = NSGraphicsContext(bitmapImageRep: rep) else { continue }
                NSGraphicsContext.saveGraphicsState()
                NSGraphicsContext.current = ctx
                draw(CGFloat(px))
                NSGraphicsContext.restoreGraphicsState()
                let name = scale == 1 ? "icon_\(base)x\(base).png" : "icon_\(base)x\(base)@2x.png"
                try rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: dir).appendingPathComponent(name))
            }
        }
    }
}

let args = CommandLine.arguments
if args.contains("--selftest") {
    let a = Disk.read()
    print("drives \(a.drives) read \(a.readBytes) B / \(a.readOps) ops, write \(a.writeBytes) B / \(a.writeOps) ops, busy \(a.busyNs) ns")
    print("click sounds \(Clicks.sounds.count) of 4")
    exit(a.drives > 0 && Clicks.sounds.count == 4 ? 0 : 1)
}
// --watch [seconds]: prints the activity twice a second, to check the counters move
if let i = args.firstIndex(of: "--watch") {
    let secs = i + 1 < args.count ? Double(args[i + 1]) ?? 5 : 5
    var prev = Disk.read()
    let start = Date()
    while Date().timeIntervalSince(start) < secs {
        Thread.sleep(forTimeInterval: 0.5)
        let now = Disk.read()
        print(String(format: "read %@  write %@  ops %llu  busy %.0f %%",
                     rateText(Double(now.readBytes &- prev.readBytes) * 2), rateText(Double(now.writeBytes &- prev.writeBytes) * 2),
                     (now.readOps &- prev.readOps) &+ (now.writeOps &- prev.writeOps),
                     min(1, Double(now.busyNs &- prev.busyNs) / 0.5e9) * 100))
        prev = now
    }
    exit(0)
}
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}
// --render-graph out.png: draws the graph with made-up activity, to check how it looks
if let i = args.firstIndex(of: "--render-graph"), i + 1 < args.count {
    let g = GraphView(frame: NSRect(x: 0, y: 0, width: 420, height: 220))
    g.appearance = NSAppearance(named: .darkAqua)
    g.readColor = LED.amber
    g.writeColor = LED.amber.blended(withFraction: 0.35, of: .white)!
    for k in 0..<GraphView.samples {
        let x = Double(k)
        g.push(read: max(0, 40e6 * (sin(x / 9) + 1) + (k % 23 == 0 ? 300e6 : 0)), write: max(0, 8e6 * (cos(x / 5) + 1.1)), busy: 0.37)
    }
    let rep = g.bitmapImageRepForCachingDisplay(in: g.bounds)!
    g.cacheDisplay(in: g.bounds, to: rep)
    try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: args[i + 1]))
    exit(0)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
