// Countdown Timer for macOS: the Mac twin of desktop-apps/countdown-timer (Windows).
// Single-file AppKit app; build.sh compiles it into Countdown Timer.app and a .dmg.
import AppKit

// MARK: - Look

enum Palette {
    static func rgb(_ r: Int, _ g: Int, _ b: Int) -> NSColor {
        NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: 1)
    }
    static let bg = rgb(30, 31, 36)
    static let border = rgb(62, 64, 72)
    static let dim = rgb(150, 154, 162)
    static let track = rgb(52, 54, 62)
    static let btn = rgb(44, 46, 53)
    static let btnHover = rgb(62, 65, 75)
    static let segHover = rgb(40, 42, 49)
    static let segSelected = rgb(52, 55, 64)
    static let closeHover = rgb(196, 43, 28)
    static let text = rgb(235, 236, 240)
    // Pure, saturated signal colors (no amber/sepia).
    static let green = rgb(34, 197, 94)
    static let yellow = rgb(255, 230, 0)
    static let red = rgb(255, 45, 45)

    static func blend(_ a: NSColor, _ b: NSColor, _ t: CGFloat) -> NSColor {
        a.blended(withFraction: t, of: b) ?? a
    }
}

final class FlippedView: NSView {
    override var isFlipped: Bool { true }
}

// MARK: - Settings

final class Settings {
    private let d = UserDefaults.standard

    init() {
        d.register(defaults: ["hours": 0, "minutes": 5, "seconds": 0,
                              "yellowPct": 30, "redPct": 10,
                              "topMost": true, "sound": true])
    }

    var hours: Int { get { d.integer(forKey: "hours") } set { d.set(clamp(newValue, 0, 99), forKey: "hours") } }
    var minutes: Int { get { d.integer(forKey: "minutes") } set { d.set(clamp(newValue, 0, 59), forKey: "minutes") } }
    var seconds: Int { get { d.integer(forKey: "seconds") } set { d.set(clamp(newValue, 0, 59), forKey: "seconds") } }
    var yellowPct: Int { get { d.integer(forKey: "yellowPct") } set { d.set(clamp(newValue, 1, 100), forKey: "yellowPct") } }
    var redPct: Int { get { d.integer(forKey: "redPct") } set { d.set(clamp(newValue, 0, 99), forKey: "redPct") } }
    var topMost: Bool { get { d.bool(forKey: "topMost") } set { d.set(newValue, forKey: "topMost") } }
    var sound: Bool { get { d.bool(forKey: "sound") } set { d.set(newValue, forKey: "sound") } }

    subscript(seg: Int) -> Int {
        get { seg == 0 ? hours : seg == 1 ? minutes : seconds }
        set { if seg == 0 { hours = newValue } else if seg == 1 { minutes = newValue } else { seconds = newValue } }
    }

    var duration: TimeInterval { TimeInterval(hours * 3600 + minutes * 60 + seconds) }
}

func clamp(_ v: Int, _ lo: Int, _ hi: Int) -> Int { min(hi, max(lo, v)) }

// MARK: - Window

final class TimerWindow: NSWindow {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { true }
}

// MARK: - Timer view

final class TimerView: NSView, NSViewToolTipOwner {
    enum Hit: Equatable { case none, close, min, pin, gear, seg(Int), main, reset }
    enum State { case idle, running, paused, finished }

    let cfg: Settings
    var state = State.idle
    var total: TimeInterval = 0
    var banked: TimeInterval = 0       // time already counted before the current run
    var runStart: TimeInterval = 0     // uptime when the current run began
    var finishAt: TimeInterval = 0
    var beepsLeft = 0
    var nextBeep: TimeInterval = 0
    var sel = -1
    var typed = 0
    var hover = Hit.none
    var pressed = Hit.none
    var scrollAccum: CGFloat = 0
    var ticker: Timer?

    let digitFont = NSFont.monospacedDigitSystemFont(ofSize: 44, weight: .semibold)
    let uiFont = NSFont.systemFont(ofSize: 13)
    let titleFont = NSFont.systemFont(ofSize: 12)

    let rcClose = NSRect(x: 252, y: 0, width: 28, height: 26)
    let rcMin = NSRect(x: 224, y: 0, width: 28, height: 26)
    let rcPin = NSRect(x: 196, y: 0, width: 28, height: 26)
    let rcGear = NSRect(x: 168, y: 0, width: 28, height: 26)
    let rcBar = NSRect(x: 14, y: 86, width: 252, height: 8)
    let rcMain = NSRect(x: 46, y: 100, width: 90, height: 24)
    let rcReset = NSRect(x: 144, y: 100, width: 90, height: 24)
    let digitsTop: CGFloat = 26
    let digitsHeight: CGFloat = 56
    var textX: CGFloat = 0
    var segRects = [NSRect](repeating: .zero, count: 3)

    init(settings: Settings) {
        cfg = settings
        super.init(frame: NSRect(x: 0, y: 0, width: 280, height: 130))
        computeLayout()
        ticker = Timer(timeInterval: 0.04, target: self, selector: #selector(tick), userInfo: nil, repeats: true)
        RunLoop.main.add(ticker!, forMode: .common)
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    var digitAttrs: [NSAttributedString.Key: Any] { [.font: digitFont] }

    func width(_ s: String) -> CGFloat { (s as NSString).size(withAttributes: digitAttrs).width }

    // The whole "00:00:00" is drawn as one string (so the colons sit at digit height);
    // the three editable fields are located from the measured prefix widths.
    func computeLayout() {
        let full = width("00:00:00")
        textX = (bounds.width - full) / 2
        let edges: [(String, String)] = [("", "00"), ("00:", "00:00"), ("00:00:", "00:00:00")]
        for (i, e) in edges.enumerated() {
            let a = textX + width(e.0), b = textX + width(e.1)
            segRects[i] = NSRect(x: a - 5, y: digitsTop, width: b - a + 10, height: digitsHeight)
        }
    }

    // MARK: Timer logic

    var now: TimeInterval { ProcessInfo.processInfo.systemUptime }

    func remaining() -> TimeInterval {
        switch state {
        case .idle: return cfg.duration
        case .finished: return 0
        case .paused: return max(0, total - banked)
        case .running: return max(0, total - banked - (now - runStart))
        }
    }

    func fraction() -> CGFloat {
        if state == .idle { return 1 }
        guard total > 0 else { return 0 }
        return CGFloat(min(1, max(0, remaining() / total)))
    }

    func level(_ frac: CGFloat) -> NSColor {
        let pct = frac * 100
        if pct <= CGFloat(cfg.redPct) { return Palette.red }
        if pct <= CGFloat(cfg.yellowPct) { return Palette.yellow }
        return Palette.green
    }

    static func fmt(_ t: TimeInterval) -> String {
        let s = max(0, Int((t - 1e-9).rounded(.up)))
        return String(format: "%02d:%02d:%02d", s / 3600, (s / 60) % 60, s % 60)
    }

    func start() {
        total = cfg.duration
        guard total > 0 else { NSSound.beep(); return }
        banked = 0
        runStart = now
        state = .running
        sel = -1
        beepsLeft = 0
    }

    func mainAction() {
        switch state {
        case .idle, .finished: start()
        case .running: banked += now - runStart; state = .paused
        case .paused: runStart = now; state = .running
        }
        needsDisplay = true
    }

    func reset() {
        banked = 0
        beepsLeft = 0
        state = .idle
        needsDisplay = true
    }

    func finish() {
        state = .finished
        finishAt = now
        beepsLeft = cfg.sound ? 3 : 0
        nextBeep = 0
        if !NSApp.isActive { NSApp.requestUserAttention(.informationalRequest) }
    }

    @objc func tick() {
        switch state {
        case .running:
            if total - banked - (now - runStart) <= 0 { finish() }
            needsDisplay = true
        case .finished:
            let t = now - finishAt
            if beepsLeft > 0 && t >= nextBeep {
                NSSound(named: "Glass")?.play()
                beepsLeft -= 1
                nextBeep += 1.3
            }
            if t < 11.2 { needsDisplay = true }
        default:
            break
        }
    }

    func setTopMost(_ on: Bool) {
        cfg.topMost = on
        window?.level = on ? .floating : .normal
        window?.collectionBehavior = on ? [.canJoinAllSpaces, .fullScreenAuxiliary] : []
        needsDisplay = true
    }

    @objc func toggleTopMost() { setTopMost(!cfg.topMost) }
    @objc func toggleSound() { cfg.sound = !cfg.sound }
    @objc func minimizeWindow() { window?.miniaturize(nil) }
    @objc func quit() { NSApp.terminate(nil) }

    @objc func showAbout() {
        let credits = NSMutableAttributedString(
            string: "Free software, provided \"as is\", without warranty of any kind.\n" +
                    "This app collects no personal data and never connects to the internet.\n\n",
            attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.labelColor])
        credits.append(NSAttributedString(
            string: License.text,
            attributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]))
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Countdown Timer",
            .applicationVersion: "1.0",
            .version: "",
            .credits: credits,
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): "© 2026 Mauricio Mendivelso · MIT License",
        ])
    }

    @objc func openSettings() {
        let view = FlippedView(frame: NSRect(x: 0, y: 0, width: 300, height: 196))
        func label(_ s: String, _ x: CGFloat, _ y: CGFloat, bold: Bool = false) {
            let l = NSTextField(labelWithString: s)
            if bold { l.font = NSFont.boldSystemFont(ofSize: 13) }
            l.frame.origin = NSPoint(x: x, y: y)
            l.sizeToFit()
            view.addSubview(l)
        }
        func field(_ v: Int, _ x: CGFloat, _ y: CGFloat) -> NSTextField {
            let f = NSTextField(string: "\(v)")
            f.alignment = .right
            f.frame = NSRect(x: x, y: y, width: 48, height: 22)
            view.addSubview(f)
            return f
        }
        label("Countdown time", 0, 0, bold: true)
        let fH = field(cfg.hours, 0, 24); label("h", 52, 26)
        let fM = field(cfg.minutes, 80, 24); label("min", 132, 26)
        let fS = field(cfg.seconds, 176, 24); label("s", 228, 26)
        label("Bar colors", 0, 64, bold: true)
        label("Turn yellow at", 0, 92); let fY = field(cfg.yellowPct, 110, 89); label("% of time left", 164, 92)
        label("Turn red at", 0, 122); let fR = field(cfg.redPct, 110, 119); label("% of time left", 164, 122)
        let cTop = NSButton(checkboxWithTitle: "Always on top", target: nil, action: nil)
        cTop.state = cfg.topMost ? .on : .off
        cTop.frame.origin = NSPoint(x: 0, y: 160); cTop.sizeToFit(); view.addSubview(cTop)
        let cSound = NSButton(checkboxWithTitle: "Sound at the end", target: nil, action: nil)
        cSound.state = cfg.sound ? .on : .off
        cSound.frame.origin = NSPoint(x: 140, y: 160); cSound.sizeToFit(); view.addSubview(cSound)

        func num(_ f: NSTextField, _ old: Int, _ lo: Int, _ hi: Int) -> Int {
            clamp(Int(f.stringValue.trimmingCharacters(in: .whitespaces)) ?? old, lo, hi)
        }

        while true {
            let alert = NSAlert()
            alert.messageText = "Timer settings"
            alert.addButton(withTitle: "OK")
            alert.addButton(withTitle: "Cancel")
            alert.accessoryView = view
            alert.window.initialFirstResponder = fH
            guard alert.runModal() == .alertFirstButtonReturn else { return }
            let y = num(fY, cfg.yellowPct, 1, 100), r = num(fR, cfg.redPct, 0, 99)
            if r >= y {
                let warn = NSAlert()
                warn.messageText = "The red level must be lower than the yellow level."
                warn.runModal()
                continue
            }
            cfg.hours = num(fH, cfg.hours, 0, 99)
            cfg.minutes = num(fM, cfg.minutes, 0, 59)
            cfg.seconds = num(fS, cfg.seconds, 0, 59)
            cfg.yellowPct = y
            cfg.redPct = r
            cfg.sound = cSound.state == .on
            setTopMost(cTop.state == .on)
            if state == .finished { reset() }
            needsDisplay = true
            return
        }
    }

    // MARK: Editing the time (only before starting)

    func step(_ s: Int, _ delta: Int) {
        let maxV = s == 0 ? 99 : 59
        var v = cfg[s] + delta
        if v > maxV { v = 0 }
        if v < 0 { v = maxV }
        cfg[s] = v
        needsDisplay = true
    }

    func typeDigit(_ d: Int) {
        if sel < 0 { sel = 0; typed = 0 }
        cfg[sel] = typed == 0 ? d : cfg[sel] * 10 + d
        typed += 1
        if typed >= 2 {
            typed = 0
            if sel < 2 { sel += 1 }
        }
        needsDisplay = true
    }

    override func keyDown(with e: NSEvent) {
        if e.modifierFlags.contains(.command) { super.keyDown(with: e); return }
        let ch = e.charactersIgnoringModifiers ?? ""
        switch e.keyCode {
        case 49, 36, 76: sel = -1; mainAction(); return          // space, return, enter
        case 53: sel = -1; needsDisplay = true; return            // escape
        default: break
        }
        if ch.lowercased() == "r" { reset(); return }
        guard state == .idle else { super.keyDown(with: e); return }
        if ch.count == 1, let d = Int(ch) { typeDigit(d); return }
        let shift = e.modifierFlags.contains(.shift)
        switch e.keyCode {
        case 126: if sel < 0 { sel = 1 }; typed = 0; step(sel, 1)                     // up
        case 125: if sel < 0 { sel = 1 }; typed = 0; step(sel, -1)                    // down
        case 123: sel = max(0, sel - 1); typed = 0                                     // left
        case 124: sel = sel < 0 ? 0 : min(2, sel + 1); typed = 0                       // right
        case 48: sel = shift ? max(0, sel - 1) : (sel < 0 ? 0 : min(2, sel + 1)); typed = 0  // tab
        case 51, 117: if sel >= 0 { cfg[sel] = 0; typed = 0 }                          // delete
        default: super.keyDown(with: e); return
        }
        needsDisplay = true
    }

    // MARK: Mouse

    func region(at p: NSPoint) -> Hit {
        if rcClose.contains(p) { return .close }
        if rcMin.contains(p) { return .min }
        if rcPin.contains(p) { return .pin }
        if rcGear.contains(p) { return .gear }
        if rcMain.contains(p) { return .main }
        if rcReset.contains(p) { return .reset }
        if state == .idle {
            for i in 0..<3 where segRects[i].contains(p) { return .seg(i) }
        }
        return .none
    }

    func point(_ e: NSEvent) -> NSPoint { convert(e.locationInWindow, from: nil) }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        for t in trackingAreas { removeTrackingArea(t) }
        addTrackingArea(NSTrackingArea(rect: bounds,
                                       options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                                       owner: self, userInfo: nil))
        removeAllToolTips()
        for r in [rcClose, rcMin, rcPin, rcGear, rcMain, rcReset] + segRects {
            addToolTip(r, owner: self, userData: nil)
        }
    }

    func view(_ view: NSView, stringForToolTip tag: NSView.ToolTipTag, point: NSPoint, userData data: UnsafeMutableRawPointer?) -> String {
        switch region(at: point) {
        case .close: return "Quit"
        case .min: return "Minimize"
        case .pin: return cfg.topMost ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"
        case .gear: return "Settings"
        case .seg: return "Scroll, or click and type, to set the time"
        case .main: return mainLabel + " (Space)"
        case .reset: return "Reset (R)"
        case .none: return ""
        }
    }

    override func mouseMoved(with e: NSEvent) {
        let h = region(at: point(e))
        if h != hover { hover = h; needsDisplay = true }
    }

    override func mouseExited(with e: NSEvent) {
        hover = .none
        needsDisplay = true
    }

    override func mouseDown(with e: NSEvent) {
        window?.makeFirstResponder(self)
        let h = region(at: point(e))
        switch h {
        case .seg(let i):
            sel = i; typed = 0; needsDisplay = true
        case .none:
            sel = -1; needsDisplay = true
            window?.performDrag(with: e)
        default:
            pressed = h; needsDisplay = true
        }
    }

    override func mouseUp(with e: NSEvent) {
        guard pressed != .none else { return }
        let p = pressed
        pressed = .none
        needsDisplay = true
        guard region(at: point(e)) == p else { return }
        switch p {
        case .close: quit()
        case .min: minimizeWindow()
        case .pin: toggleTopMost()
        case .gear: openSettings()
        case .main: sel = -1; mainAction()
        case .reset: reset()
        default: break
        }
    }

    override func scrollWheel(with e: NSEvent) {
        guard state == .idle, case .seg(let i) = region(at: point(e)) else { return }
        sel = i
        typed = 0
        if e.hasPreciseScrollingDeltas {
            // Trackpad: one step per ~14 points of scrolling.
            scrollAccum += e.scrollingDeltaY
            while scrollAccum >= 14 { step(i, 1); scrollAccum -= 14 }
            while scrollAccum <= -14 { step(i, -1); scrollAccum += 14 }
        } else if e.scrollingDeltaY != 0 {
            step(i, e.scrollingDeltaY > 0 ? 1 : -1)
        }
    }

    override func menu(for event: NSEvent) -> NSMenu? {
        let m = NSMenu()
        let top = m.addItem(withTitle: "Always on top", action: #selector(toggleTopMost), keyEquivalent: "")
        top.state = cfg.topMost ? .on : .off
        let snd = m.addItem(withTitle: "Sound at the end", action: #selector(toggleSound), keyEquivalent: "")
        snd.state = cfg.sound ? .on : .off
        m.addItem(withTitle: "Settings…", action: #selector(openSettings), keyEquivalent: "")
        m.addItem(withTitle: "About Countdown Timer", action: #selector(showAbout), keyEquivalent: "")
        m.addItem(.separator())
        m.addItem(withTitle: "Quit", action: #selector(quit), keyEquivalent: "")
        for item in m.items { item.target = self }
        return m
    }

    var mainLabel: String {
        switch state {
        case .running: return "Pause"
        case .paused: return "Resume"
        case .finished: return "Restart"
        case .idle: return "Start"
        }
    }

    // MARK: Drawing

    static let cornerRadius: CGFloat = 10

    func drawText(_ s: String, font: NSFont, color: NSColor, in r: NSRect, alignLeft: Bool = false) {
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color]
        let size = (s as NSString).size(withAttributes: attrs)
        let x = alignLeft ? r.minX : r.midX - size.width / 2
        (s as NSString).draw(at: NSPoint(x: x, y: r.midY - size.height / 2), withAttributes: attrs)
    }

    func symbol(_ name: String, in r: NSRect, color: NSColor) {
        guard let base = NSImage(systemSymbolName: name, accessibilityDescription: nil)?
                .withSymbolConfiguration(.init(pointSize: 11, weight: .medium)) else { return }
        let tinted = NSImage(size: base.size, flipped: false) { rect in
            base.draw(in: rect)
            color.set()
            rect.fill(using: .sourceAtop)
            return true
        }
        let s = base.size
        let dst = NSRect(x: r.midX - s.width / 2, y: r.midY - s.height / 2, width: s.width, height: s.height)
        tinted.draw(in: dst, from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true, hints: nil)
    }

    func fillRound(_ r: NSRect, _ radius: CGFloat, _ c: NSColor) {
        c.setFill()
        NSBezierPath(roundedRect: r, xRadius: radius, yRadius: radius).fill()
    }

    override func draw(_ dirtyRect: NSRect) {
        let shape = NSBezierPath(roundedRect: bounds, xRadius: Self.cornerRadius, yRadius: Self.cornerRadius)
        Palette.bg.setFill()
        shape.fill()
        NSGraphicsContext.saveGraphicsState()
        shape.addClip()

        // Title bar
        let title = state == .idle ? "Timer" : "Timer  ·  " + Self.fmt(total)
        drawText(title, font: titleFont, color: Palette.dim, in: NSRect(x: 12, y: 0, width: 150, height: 26), alignLeft: true)
        iconButton(rcGear, "gearshape", .gear, Palette.dim)
        iconButton(rcPin, cfg.topMost ? "pin.fill" : "pin", .pin, cfg.topMost ? Palette.text : Palette.dim)
        iconButton(rcMin, "minus", .min, Palette.dim)
        iconButton(rcClose, "xmark", .close, Palette.dim)

        // Digits
        let frac = fraction()
        let lvl = state == .finished ? Palette.red : level(frac)
        let sinceFinish = now - finishAt
        let blinkOff = state == .finished && sinceFinish < 11 && Int(sinceFinish / 0.5) % 2 == 1
        var digitColor = blinkOff ? Palette.rgb(110, 30, 30) : lvl
        if state == .paused { digitColor = Palette.blend(lvl, Palette.bg, 0.35) }

        if state == .idle {
            for i in 0..<3 where sel == i || hover == .seg(i) {
                fillRound(segRects[i], 6, sel == i ? Palette.segSelected : Palette.segHover)
                if sel == i {
                    let r = segRects[i]
                    fillRound(NSRect(x: r.minX + 8, y: r.maxY - 5, width: r.width - 16, height: 2.5), 1, lvl)
                }
            }
        }
        let attrs: [NSAttributedString.Key: Any] = [.font: digitFont, .foregroundColor: digitColor]
        let str = Self.fmt(remaining()) as NSString
        let h = str.size(withAttributes: attrs).height
        str.draw(at: NSPoint(x: textX, y: digitsTop + (digitsHeight - h) / 2), withAttributes: attrs)

        // Bar: shrinks from right to left
        let radius = rcBar.height / 2
        fillRound(rcBar, radius, state == .finished && !blinkOff ? Palette.blend(Palette.red, Palette.bg, 0.45) : Palette.track)
        let fw = (rcBar.width * frac).rounded()
        if fw > 0 {
            NSGraphicsContext.saveGraphicsState()
            NSBezierPath(rect: NSRect(x: rcBar.minX, y: rcBar.minY - 1, width: fw, height: rcBar.height + 2)).addClip()
            fillRound(rcBar, radius, lvl)
            NSGraphicsContext.restoreGraphicsState()
        }

        // Buttons
        let canStart = state != .idle || cfg.duration > 0
        button(rcMain, mainLabel, .main, canStart ? Palette.text : Palette.dim)
        button(rcReset, "Reset", .reset, state == .idle ? Palette.dim : Palette.text)

        NSGraphicsContext.restoreGraphicsState()
        Palette.border.setStroke()
        let edge = NSBezierPath(roundedRect: bounds.insetBy(dx: 0.5, dy: 0.5), xRadius: Self.cornerRadius - 0.5, yRadius: Self.cornerRadius - 0.5)
        edge.lineWidth = 1
        edge.stroke()
    }

    func iconButton(_ r: NSRect, _ name: String, _ h: Hit, _ fg: NSColor) {
        var color = fg
        if hover == h || pressed == h {
            let close = h == .close
            (close ? Palette.closeHover : Palette.btnHover).setFill()
            r.fill()
            color = close ? .white : Palette.text
        }
        symbol(name, in: r, color: color)
    }

    func button(_ r: NSRect, _ text: String, _ h: Hit, _ fg: NSColor) {
        let bg = pressed == h ? Palette.border : hover == h ? Palette.btnHover : Palette.btn
        fillRound(r, 5, bg)
        drawText(text, font: uiFont, color: fg, in: r)
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

// MARK: - App icon (used by build.sh: CountdownTimer --write-iconset <dir>)

enum IconArt {
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        Palette.bg.setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()

        let u = body.width
        func y(_ fromTop: CGFloat) -> CGFloat { body.maxY - fromTop * u }
        let cx = body.minX + 0.5 * u, cy = y(0.42), r = 0.25 * u, w = max(1.4, 0.075 * u)

        let ring = NSBezierPath(ovalIn: NSRect(x: cx - r, y: cy - r, width: 2 * r, height: 2 * r))
        ring.lineWidth = w
        Palette.text.setStroke()
        ring.stroke()

        let hands = NSBezierPath()
        hands.move(to: NSPoint(x: cx, y: cy + r * 0.62))
        hands.line(to: NSPoint(x: cx, y: cy))
        hands.line(to: NSPoint(x: cx + r * 0.45, y: cy))
        hands.lineWidth = w
        hands.lineCapStyle = .round
        hands.lineJoinStyle = .round
        Palette.yellow.setStroke()
        hands.stroke()

        let bh = max(2, 0.085 * u)
        let bar = NSRect(x: body.minX + 0.17 * u, y: y(0.77) - bh, width: 0.66 * u, height: bh)
        for (part, color) in [(1.0, Palette.red), (0.8, Palette.yellow), (0.55, Palette.green)] {
            NSGraphicsContext.saveGraphicsState()
            NSBezierPath(rect: NSRect(x: bar.minX - 1, y: bar.minY - 1, width: bar.width * CGFloat(part) + 1, height: bh + 2)).addClip()
            color.setFill()
            NSBezierPath(roundedRect: bar, xRadius: bh / 2, yRadius: bh / 2).fill()
            NSGraphicsContext.restoreGraphicsState()
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

final class AppDelegate: NSObject, NSApplicationDelegate {
    let cfg = Settings()
    var window: TimerWindow!
    var view: TimerView!

    func applicationDidFinishLaunching(_ note: Notification) {
        view = TimerView(settings: cfg)
        window = TimerWindow(contentRect: view.frame, styleMask: [.borderless, .miniaturizable],
                             backing: .buffered, defer: false)
        window.title = "Countdown Timer"
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = true
        window.contentView = view
        window.isReleasedWhenClosed = false

        if !window.setFrameUsingName("CountdownTimer"), let screen = NSScreen.main {
            let vf = screen.visibleFrame
            window.setFrameOrigin(NSPoint(x: vf.maxX - 280 - 24, y: vf.minY + 24))
        }
        window.setFrameAutosaveName("CountdownTimer")
        view.setTopMost(cfg.topMost)

        buildMainMenu()
        window.makeKeyAndOrderFront(nil)
        window.makeFirstResponder(view)
        window.invalidateShadow()
        NSApp.activate(ignoringOtherApps: true)
    }

    func buildMainMenu() {
        let main = NSMenu()
        let appItem = NSMenuItem()
        main.addItem(appItem)
        let appMenu = NSMenu()
        let about = appMenu.addItem(withTitle: "About Countdown Timer", action: #selector(TimerView.showAbout), keyEquivalent: "")
        about.target = view
        appMenu.addItem(.separator())
        let settings =appMenu.addItem(withTitle: "Settings…", action: #selector(TimerView.openSettings), keyEquivalent: ",")
        settings.target = view
        appMenu.addItem(.separator())
        let minimize = appMenu.addItem(withTitle: "Minimize", action: #selector(TimerView.minimizeWindow), keyEquivalent: "m")
        minimize.target = view
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Quit Countdown Timer", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu
        NSApp.mainMenu = main
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        if window.isMiniaturized { window.deminiaturize(nil) }
        window.makeKeyAndOrderFront(nil)
        return true
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
