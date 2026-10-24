// Net LEDs for macOS: two little lights in the menu bar, like the activity LEDs on an old modem.
// Green blinks when data comes in (download), red blinks when data goes out (upload).
// Single-file AppKit app; build.sh makes Net LEDs.app.
import AppKit
import ServiceManagement

// MARK: - Byte counters (64-bit, from the routing table's interface list)

struct Counters { var rx: UInt64 = 0; var tx: UInt64 = 0 }

func readCounters() -> Counters {
    var mib: [Int32] = [CTL_NET, PF_ROUTE, 0, 0, NET_RT_IFLIST2, 0]
    var len = 0
    guard sysctl(&mib, 6, nil, &len, nil, 0) == 0, len > 0 else { return Counters() }
    var buf = [UInt8](repeating: 0, count: len)
    guard sysctl(&mib, 6, &buf, &len, nil, 0) == 0 else { return Counters() }
    var c = Counters()
    buf.withUnsafeBytes { raw in
        var off = 0
        while off + MemoryLayout<if_msghdr>.size <= len {
            let p = raw.baseAddress!.advanced(by: off)
            let hdr = p.loadUnaligned(as: if_msghdr.self)
            if hdr.ifm_msglen == 0 { break }
            if Int32(hdr.ifm_type) == RTM_IFINFO2 {
                let m = p.loadUnaligned(as: if_msghdr2.self)
                // Skip loopback: it's the Mac talking to itself, not the network
                if m.ifm_flags & IFF_LOOPBACK == 0 {
                    c.rx &+= m.ifm_data.ifi_ibytes
                    c.tx &+= m.ifm_data.ifi_obytes
                }
            }
            off += Int(hdr.ifm_msglen)
        }
    }
    return c
}

func rateText(_ bytesPerSec: Double) -> String {
    let units = ["B/s", "KB/s", "MB/s", "GB/s"]
    var v = bytesPerSec, i = 0
    while v >= 1000 && i < units.count - 1 { v /= 1000; i += 1 }
    return i == 0 ? String(format: "%.0f %@", v, units[i]) : String(format: "%.1f %@", v, units[i])
}

// MARK: - LED drawing

enum LED {
    static let green = NSColor(srgbRed: 0.20, green: 1.00, blue: 0.30, alpha: 1)
    static let red = NSColor(srgbRed: 1.00, green: 0.22, blue: 0.18, alpha: 1)

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

    static func image(rx: CGFloat, tx: CGFloat) -> NSImage {
        let size = NSSize(width: 30, height: 18)
        return NSImage(size: size, flipped: false) { _ in
            draw(green, level: rx, center: CGPoint(x: 8, y: 9), r: 4.5)
            draw(red, level: tx, center: CGPoint(x: 22, y: 9), r: 4.5)
            return true
        }
    }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
    let downItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    let upItem = NSMenuItem(title: "", action: nil, keyEquivalent: "")
    let speedsItem = NSMenuItem(title: "Show Speeds in Menu Bar", action: #selector(toggleSpeeds), keyEquivalent: "")
    let loginItem = NSMenuItem(title: "Open at Login", action: #selector(toggleLogin), keyEquivalent: "")

    var last = readCounters()
    var lastSecond = readCounters()
    var lastSecondTime = Date()
    var rxRate = 0.0, txRate = 0.0
    var rxLevel: CGFloat = 0, txLevel: CGFloat = 0
    var shownRx: CGFloat = -1, shownTx: CGFloat = -1
    var showSpeeds = UserDefaults.standard.bool(forKey: "showSpeeds")

    func applicationDidFinishLaunching(_ n: Notification) {
        let menu = NSMenu()
        menu.delegate = self
        downItem.isEnabled = false; upItem.isEnabled = false
        menu.addItem(downItem); menu.addItem(upItem)
        menu.addItem(.separator())
        speedsItem.target = self; loginItem.target = self
        menu.addItem(speedsItem)
        if #available(macOS 13, *) { menu.addItem(loginItem) }
        menu.addItem(.separator())
        menu.addItem(NSMenuItem(title: "Quit Net LEDs", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q"))
        item.menu = menu
        item.button?.imagePosition = .imageLeft
        item.button?.font = NSFont.monospacedDigitSystemFont(ofSize: 10, weight: .regular)
        refreshMenu()
        render()

        // 30 ticks a second: a burst of packets makes the lamp flicker, a steady stream keeps it lit
        let t = Timer(timeInterval: 1.0 / 30, repeats: true) { [weak self] _ in self?.tick() }
        RunLoop.main.add(t, forMode: .common)
    }

    func tick() {
        let now = readCounters()
        let drx = now.rx &- last.rx, dtx = now.tx &- last.tx
        last = now
        // Any traffic in this tick lights the lamp; with nothing it fades out quickly
        rxLevel = drx > 0 ? min(1, 0.55 + 0.09 * CGFloat(log10(Double(drx)))) : rxLevel * 0.35
        txLevel = dtx > 0 ? min(1, 0.55 + 0.09 * CGFloat(log10(Double(dtx)))) : txLevel * 0.35
        if rxLevel < 0.03 { rxLevel = 0 }
        if txLevel < 0.03 { txLevel = 0 }

        let dt = Date().timeIntervalSince(lastSecondTime)
        if dt >= 1 {
            rxRate = Double(now.rx &- lastSecond.rx) / dt
            txRate = Double(now.tx &- lastSecond.tx) / dt
            lastSecond = now; lastSecondTime = Date()
            refreshMenu()
            if showSpeeds { renderTitle() }
        }
        render()
    }

    func render() {
        // Redraw only when a lamp visibly changes, to keep CPU near zero
        if abs(rxLevel - shownRx) < 0.02 && abs(txLevel - shownTx) < 0.02 { return }
        shownRx = rxLevel; shownTx = txLevel
        item.button?.image = LED.image(rx: rxLevel, tx: txLevel)
    }

    func renderTitle() {
        item.button?.title = showSpeeds ? " ↓\(rateText(rxRate)) ↑\(rateText(txRate))" : ""
    }

    func refreshMenu() {
        downItem.title = "● Download  \(rateText(rxRate))"
        upItem.title = "● Upload      \(rateText(txRate))"
        downItem.attributedTitle = colored(downItem.title, LED.green)
        upItem.attributedTitle = colored(upItem.title, LED.red)
        item.button?.toolTip = "Download \(rateText(rxRate)) · Upload \(rateText(txRate))"
        speedsItem.state = showSpeeds ? .on : .off
        if #available(macOS 13, *) { loginItem.state = SMAppService.mainApp.status == .enabled ? .on : .off }
    }

    func colored(_ s: String, _ c: NSColor) -> NSAttributedString {
        let a = NSMutableAttributedString(string: s, attributes: [.font: NSFont.monospacedDigitSystemFont(ofSize: 13, weight: .regular),
                                                                  .foregroundColor: NSColor.labelColor])
        a.addAttribute(.foregroundColor, value: c, range: NSRange(location: 0, length: 1))
        return a
    }

    func menuWillOpen(_ menu: NSMenu) { refreshMenu() }

    @objc func toggleSpeeds() {
        showSpeeds.toggle()
        UserDefaults.standard.set(showSpeeds, forKey: "showSpeeds")
        renderTitle(); refreshMenu()
    }

    @objc func toggleLogin() {
        guard #available(macOS 13, *) else { return }
        do {
            if SMAppService.mainApp.status == .enabled { try SMAppService.mainApp.unregister() }
            else { try SMAppService.mainApp.register() }
        } catch {
            let a = NSAlert()
            a.messageText = "Couldn't change Open at Login"
            a.informativeText = "Move Net LEDs into Applications and try again.\n\n\(error.localizedDescription)"
            a.runModal()
        }
        refreshMenu()
    }
}

if CommandLine.arguments.contains("--selftest") {
    let a = readCounters()
    print("rx \(a.rx) tx \(a.tx)")
    exit(a.rx > 0 || a.tx > 0 ? 0 : 1)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.accessory)
app.run()
