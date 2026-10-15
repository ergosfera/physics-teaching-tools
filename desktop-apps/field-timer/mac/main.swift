// Field for macOS: the Field dot timer (tools/teaching/field-timer.html) as a desktop app
// (twin of ../windows/Program.cs). Single-file AppKit app; build.sh makes Field.app and a .dmg.
import AppKit

// MARK: - Dark Windows 95 skin (same tokens as the web page)

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
    static let titleFrom = rgb(0x0a, 0x0a, 0x5c)
    static let titleTo = rgb(0x16, 0x3a, 0x70)
    static let closeHover = rgb(0xc0, 0x00, 0x00)

    static let bg = rgb(0x06, 0x06, 0x07)
    static let dotIdle = rgb(255, 255, 255, 0.10)
    static let setupFill = rgb(0xcf, 0xcd, 0xc6)
    static let active = rgb(0xf4, 0xf1, 0xea)
    static let elapsed = rgb(0x33, 0x33, 0x38)

    static let lcd = rgb(0x7f, 0xae, 0x86)
    static let lcdDigit = rgb(0x16, 0x32, 0x1c)

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

    static func draw(_ s: String, _ font: NSFont, _ color: NSColor, in r: NSRect, left: Bool = false) {
        let attrs: [NSAttributedString.Key: Any] = [.font: font, .foregroundColor: color]
        let sz = (s as NSString).size(withAttributes: attrs)
        let x = left ? r.minX : r.midX - sz.width / 2
        (s as NSString).draw(at: NSPoint(x: x, y: r.midY - sz.height / 2), withAttributes: attrs)
    }

    static func symbol(_ name: String, in r: NSRect, color: NSColor) {
        guard let base = NSImage(systemSymbolName: name, accessibilityDescription: nil)?
                .withSymbolConfiguration(.init(pointSize: 11, weight: .semibold)) else { return }
        let tinted = NSImage(size: base.size, flipped: false) { rect in
            base.draw(in: rect)
            color.set()
            rect.fill(using: .sourceAtop)
            return true
        }
        let s = base.size
        tinted.draw(in: NSRect(x: r.midX - s.width / 2, y: r.midY - s.height / 2, width: s.width, height: s.height),
                    from: .zero, operation: .sourceOver, fraction: 1, respectFlipped: true, hints: nil)
    }
}

// MARK: - Settings

final class Settings {
    static let minSeconds = 1, maxSeconds = 6 * 60 * 60
    private let d = UserDefaults.standard

    init() { d.register(defaults: ["seconds": 600, "topMost": false, "sound": true]) }

    var seconds: Int {
        get { Settings.clampSeconds(d.integer(forKey: "seconds")) }
        set { d.set(Settings.clampSeconds(newValue), forKey: "seconds") }
    }
    var topMost: Bool { get { d.bool(forKey: "topMost") } set { d.set(newValue, forKey: "topMost") } }
    var sound: Bool { get { d.bool(forKey: "sound") } set { d.set(newValue, forKey: "sound") } }

    static func clampSeconds(_ v: Int) -> Int { min(maxSeconds, max(minSeconds, v)) }
}

// MARK: - The field

final class FieldView: NSView, NSViewToolTipOwner {
    enum State { case setup, running, paused, done }
    enum Hit: Equatable { case none, full, pin, about, start, cancel, ok, field }

    // Same geometry as the web page
    static let dot: CGFloat = 11, gap: CGFloat = 11, pad: CGFloat = 18
    static let logMin = log(Double(Settings.minSeconds)), logMax = log(Double(Settings.maxSeconds))
    static let hintText = "Drag the field to set time"

    let cfg: Settings
    var state = State.setup
    var duration: TimeInterval = 0
    var banked: TimeInterval = 0       // time already counted before the current run
    var runStart: TimeInterval = 0     // uptime when the current run began
    var doneAt: TimeInterval = 0
    var lastGray = -1
    var dragging = false
    var pressed = Hit.none
    var hover = Hit.none
    var beepsLeft = 0
    var nextBeep: TimeInterval = 0
    var scrollAccum: CGFloat = 0
    var ticker: Timer?

    let fTitle = Skin.font("Tahoma", 12, bold: true)
    let fAbout = Skin.font("Tahoma", 12.5, bold: true)
    let fHint = Skin.font("Tahoma", 12)
    let fStart = Skin.font("Tahoma", 13.6, bold: true)
    let fCancel = Skin.font("Tahoma", 15)
    let fPaused = Skin.font("Tahoma", 11.5, bold: true)
    let fAlertTitle = Skin.font("Tahoma", 11.5, bold: true)
    let fDone = Skin.font("Tahoma", 13.6)
    let fOk = Skin.font("Tahoma", 12.5, bold: true)
    var fReadout = NSFont.monospacedSystemFont(ofSize: 38, weight: .bold)

    var rcHeader = NSRect.zero, rcFull = NSRect.zero, rcPin = NSRect.zero, rcAbout = NSRect.zero
    var rcScreen = NSRect.zero, rcApp = NSRect.zero
    var cols = 1, rows = 1, total = 1
    var gx0: CGFloat = 0, gy0: CGFloat = 0

    init(settings: Settings) {
        cfg = settings
        super.init(frame: NSRect(x: 0, y: 0, width: 520, height: 420))
        ticker = Timer(timeInterval: 0.03, target: self, selector: #selector(tick), userInfo: nil, repeats: true)
        RunLoop.main.add(ticker!, forMode: .common)
    }

    required init?(coder: NSCoder) { fatalError("not used") }

    override var isFlipped: Bool { true }
    override var acceptsFirstResponder: Bool { true }
    override func acceptsFirstMouse(for event: NSEvent?) -> Bool { true }

    var isFullScreen: Bool { window?.styleMask.contains(.fullScreen) ?? false }

    // MARK: Layout

    override func setFrameSize(_ newSize: NSSize) {
        super.setFrameSize(newSize)
        doLayout()
    }

    func doLayout() {
        let W = bounds.width, H = bounds.height
        let hh: CGFloat = 34
        rcHeader = NSRect(x: 0, y: 0, width: W, height: hh)
        let bw: CGFloat = 26, bh: CGFloat = 24, by = (hh - bh) / 2
        var x = W - 8
        rcAbout = NSRect(x: x - bw, y: by, width: bw, height: bh); x -= bw + 3
        rcPin = NSRect(x: x - bw, y: by, width: bw, height: bh); x -= bw + 3
        rcFull = NSRect(x: x - bw, y: by, width: bw, height: bh)

        let m: CGFloat = 6
        rcScreen = NSRect(x: m, y: hh + m, width: max(5, W - 2 * m), height: max(5, H - hh - 2 * m))
        rcApp = rcScreen.insetBy(dx: 2, dy: 2)

        let cell = Self.dot + Self.gap
        cols = max(1, Int(((rcApp.width - 2 * Self.pad) / cell).rounded(.down)))
        rows = max(1, Int(((rcApp.height - 2 * Self.pad) / cell).rounded(.down)))
        total = cols * rows
        let gw = CGFloat(cols) * Self.dot + CGFloat(cols - 1) * Self.gap
        let gh = CGFloat(rows) * Self.dot + CGFloat(rows - 1) * Self.gap
        gx0 = rcApp.minX + (rcApp.width - gw) / 2
        gy0 = rcApp.minY + (rcApp.height - gh) / 2
        lastGray = -1

        // The readout grows with the window, like clamp(2.4rem, 10vw, 4.4rem) on the web page
        let px = max(38, min(70, (0.10 * rcApp.width).rounded()))
        if abs(px - fReadout.pointSize) > 0.5 { fReadout = NSFont.monospacedSystemFont(ofSize: px, weight: .bold) }

        window?.invalidateCursorRects(for: self)
        updateToolTips()
        needsDisplay = true
    }

    func setupRects() -> (lcd: NSRect, hint: NSRect, start: NSRect) {
        let ts = Skin.size(Self.format(cfg.seconds), fReadout)
        let hs = Skin.size(Self.hintText, fHint)
        let ss = Skin.size("Start", fStart)
        let lw = ts.width + 48 + 4, lh = ts.height + 24 + 4
        let bw = ss.width + 16 + 48, bh = ss.height + 18
        let gap: CGFloat = 18
        let totalH = lh + gap + hs.height + gap + 2 + bh
        let cx = rcApp.midX
        var y = (rcApp.minY + (rcApp.height - totalH) / 2).rounded()
        let lcd = NSRect(x: (cx - lw / 2).rounded(), y: y, width: lw.rounded(), height: lh.rounded()); y += lh + gap
        let hint = NSRect(x: cx - hs.width / 2 - 4, y: y, width: hs.width + 8, height: hs.height); y += hs.height + gap + 2
        let start = NSRect(x: (cx - bw / 2).rounded(), y: y.rounded(), width: bw.rounded(), height: bh.rounded())
        return (lcd, hint, start)
    }

    func doneRects() -> (box: NSRect, title: NSRect, label: NSRect, ok: NSRect) {
        let bw: CGFloat = 230
        let th = Skin.size("Field Timer", fAlertTitle).height + 10
        let lh = Skin.size("Time’s up!", fDone).height + 32
        let os = Skin.size("OK", fOk)
        let okw = (os.width + 48).rounded(), okh = (os.height + 12).rounded()
        let bh = 2 + th + lh + okh + 16 + 2
        let box = NSRect(x: (rcApp.midX - bw / 2).rounded(), y: (rcApp.midY - bh / 2).rounded(), width: bw, height: bh.rounded())
        let title = NSRect(x: box.minX + 2, y: box.minY + 2, width: bw - 4, height: th.rounded())
        let label = NSRect(x: box.minX + 2, y: title.maxY, width: bw - 4, height: lh.rounded())
        let ok = NSRect(x: (box.midX - okw / 2).rounded(), y: label.maxY, width: okw, height: okh)
        return (box, title, label, ok)
    }

    var rcCancel: NSRect { NSRect(x: rcApp.maxX - 6 - 22, y: rcApp.minY + 6, width: 22, height: 20) }

    // MARK: Time

    static func secondsToFrac(_ seconds: Int) -> Double {
        (log(Double(Settings.clampSeconds(seconds))) - logMin) / (logMax - logMin)
    }

    static func fracToSeconds(_ frac: Double) -> Int {
        Int(exp(logMin + min(1, max(0, frac)) * (logMax - logMin)).rounded())
    }

    static func format(_ totalSeconds: Int) -> String {
        let h = totalSeconds / 3600, m = (totalSeconds % 3600) / 60, s = totalSeconds % 60
        return h > 0 ? String(format: "%d:%02d:%02d", h, m, s) : String(format: "%d:%02d", m, s)
    }

    var now: TimeInterval { ProcessInfo.processInfo.systemUptime }

    var elapsed: TimeInterval { state == .running ? banked + (now - runStart) : banked }

    func grayCount() -> Int {
        Int((min(1, elapsed / duration) * Double(total)).rounded(.down))
    }

    func setSeconds(_ s: Int) {
        let v = Settings.clampSeconds(s)
        guard v != cfg.seconds else { return }
        cfg.seconds = v
        needsDisplay = true
    }

    func setFromY(_ y: CGFloat) {
        setSeconds(Self.fracToSeconds(Double(1 - (y - rcApp.minY) / max(1, rcApp.height))))
    }

    // Wheel and arrow keys: about 10% more or less time per step
    func nudge(_ dir: Int) {
        var s = Self.fracToSeconds(Self.secondsToFrac(cfg.seconds) + Double(dir) * 0.01)
        if s == cfg.seconds { s += dir }
        setSeconds(s)
    }

    func start() {
        duration = TimeInterval(cfg.seconds)
        banked = 0
        runStart = now
        lastGray = -1
        beepsLeft = 0
        state = .running
        stateChanged()
    }

    func togglePause() {
        switch state {
        case .running: banked += now - runStart; state = .paused
        case .paused: runStart = now; state = .running
        default: return
        }
        stateChanged()
    }

    func reset() {
        banked = 0
        beepsLeft = 0
        state = .setup
        stateChanged()
    }

    func finish() {
        banked = duration
        state = .done
        doneAt = now
        beepsLeft = cfg.sound ? 3 : 0
        nextBeep = 0
        if !NSApp.isActive { NSApp.requestUserAttention(.informationalRequest) }
        stateChanged()
    }

    func stateChanged() {
        window?.invalidateCursorRects(for: self)
        needsDisplay = true
    }

    @objc func tick() {
        switch state {
        case .running:
            if elapsed >= duration { finish(); return }
            let gray = grayCount()
            if gray != lastGray { lastGray = gray; setNeedsDisplay(rcApp) }
        case .done:
            let t = now - doneAt
            if beepsLeft > 0 && t >= nextBeep {
                NSSound(named: "Glass")?.play()
                beepsLeft -= 1
                nextBeep += 1.3
            }
            if t < 2.4 { setNeedsDisplay(rcApp) }
        default:
            break
        }
    }

    // MARK: Commands

    func setTopMost(_ on: Bool) {
        cfg.topMost = on
        window?.level = on ? .floating : .normal
        needsDisplay = true
    }

    @objc func toggleTopMost() { setTopMost(!cfg.topMost) }
    @objc func toggleSound() { cfg.sound = !cfg.sound }
    @objc func toggleFullScreen() { window?.toggleFullScreen(nil) }
    @objc func quit() { NSApp.terminate(nil) }

    @objc func showAbout() {
        let credits = NSMutableAttributedString(
            string: "Free software, provided \"as is\", without warranty of any kind. " +
                    "Don't rely on it where an interrupted countdown could cause harm or loss.\n" +
                    "This app collects no personal data and never connects to the internet.\n\n",
            attributes: [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.labelColor])
        credits.append(NSAttributedString(string: License.text,
            attributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]))
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Field",
            .applicationVersion: "1.0",
            .version: "",
            .credits: credits,
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): "© 2026 Mauricio Mendivelso · MIT License",
        ])
    }

    // MARK: Keyboard

    override func keyDown(with e: NSEvent) {
        if e.modifierFlags.contains(.command) { super.keyDown(with: e); return }
        switch e.keyCode {
        case 49, 36, 76:                                        // space, return, enter
            switch state {
            case .setup: start()
            case .done: reset()
            default: togglePause()
            }
        case 53:                                                // escape
            if isFullScreen { toggleFullScreen() } else if state != .setup { reset() }
        case 126: if state == .setup { nudge(1) }               // up
        case 125: if state == .setup { nudge(-1) }              // down
        default: super.keyDown(with: e)
        }
    }

    // MARK: Mouse

    func region(at p: NSPoint) -> Hit {
        if rcFull.contains(p) { return .full }
        if rcPin.contains(p) { return .pin }
        if rcAbout.contains(p) { return .about }
        guard rcApp.contains(p) else { return .none }
        switch state {
        case .setup: return setupRects().start.contains(p) ? .start : .field
        case .running, .paused: return rcCancel.contains(p) ? .cancel : .field
        case .done: return doneRects().ok.contains(p) ? .ok : .none
        }
    }

    func point(_ e: NSEvent) -> NSPoint { convert(e.locationInWindow, from: nil) }

    override func resetCursorRects() {
        guard state == .setup else { return }
        addCursorRect(rcApp, cursor: .resizeUpDown)
    }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        for t in trackingAreas { removeTrackingArea(t) }
        addTrackingArea(NSTrackingArea(rect: bounds,
                                       options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect],
                                       owner: self, userInfo: nil))
        updateToolTips()
    }

    func updateToolTips() {
        removeAllToolTips()
        for r in [rcFull, rcPin, rcAbout, rcApp] { addToolTip(r, owner: self, userData: nil) }
    }

    func view(_ view: NSView, stringForToolTip tag: NSView.ToolTipTag, point: NSPoint, userData data: UnsafeMutableRawPointer?) -> String {
        switch region(at: point) {
        case .full: return isFullScreen ? "Exit full screen (⌃⌘F)" : "Full screen (⌃⌘F)"
        case .pin: return cfg.topMost ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"
        case .about: return "About Field"
        case .start: return "Start (Space)"
        case .cancel: return "Cancel (Esc)"
        case .field:
            switch state {
            case .setup: return "Drag up or down, or scroll, to set the time"
            case .paused: return "Click to resume (Space)"
            default: return "Click to pause (Space)"
            }
        default: return ""
        }
    }

    override func mouseMoved(with e: NSEvent) {
        let h = region(at: point(e))
        if h != hover {
            if h == .cancel || hover == .cancel { setNeedsDisplay(rcCancel) }
            hover = h
        }
    }

    override func mouseExited(with e: NSEvent) {
        if hover == .cancel { setNeedsDisplay(rcCancel) }
        hover = .none
    }

    override func mouseDown(with e: NSEvent) {
        window?.makeFirstResponder(self)
        let p = point(e)
        let h = region(at: p)
        if h == .field && state == .setup {
            dragging = true
            setFromY(p.y)
            return
        }
        if h != .none {
            pressed = h
            needsDisplay = true
        }
    }

    override func mouseDragged(with e: NSEvent) {
        if dragging { setFromY(point(e).y) }
    }

    override func mouseUp(with e: NSEvent) {
        if dragging { dragging = false; return }
        guard pressed != .none else { return }
        let p = pressed
        pressed = .none
        needsDisplay = true
        guard region(at: point(e)) == p else { return }
        switch p {
        case .full: toggleFullScreen()
        case .pin: toggleTopMost()
        case .about: showAbout()
        case .start: start()
        case .cancel, .ok: reset()
        case .field: togglePause()
        case .none: break
        }
    }

    override func scrollWheel(with e: NSEvent) {
        guard state == .setup, rcApp.contains(point(e)) else { return }
        if e.hasPreciseScrollingDeltas {
            // Trackpad: one step per ~14 points of scrolling
            scrollAccum += e.scrollingDeltaY
            while scrollAccum >= 14 { nudge(1); scrollAccum -= 14 }
            while scrollAccum <= -14 { nudge(-1); scrollAccum += 14 }
        } else if e.scrollingDeltaY != 0 {
            nudge(e.scrollingDeltaY > 0 ? 1 : -1)
        }
    }

    override func menu(for event: NSEvent) -> NSMenu? {
        let m = NSMenu()
        m.addItem(withTitle: "Always on top", action: #selector(toggleTopMost), keyEquivalent: "").state = cfg.topMost ? .on : .off
        m.addItem(withTitle: "Sound at the end", action: #selector(toggleSound), keyEquivalent: "").state = cfg.sound ? .on : .off
        m.addItem(withTitle: "Full screen", action: #selector(toggleFullScreen), keyEquivalent: "").state = isFullScreen ? .on : .off
        m.addItem(withTitle: "About Field", action: #selector(showAbout), keyEquivalent: "")
        m.addItem(.separator())
        m.addItem(withTitle: "Quit", action: #selector(quit), keyEquivalent: "")
        for item in m.items { item.target = self }
        return m
    }

    // MARK: Drawing

    override func draw(_ dirtyRect: NSRect) {
        Skin.face.setFill()
        bounds.fill()

        // Header: title, then full screen, pin and about buttons
        Skin.sh.setFill()
        NSRect(x: 0, y: rcHeader.maxY - 2, width: rcHeader.width, height: 1).fill()
        Skin.lt.setFill()
        NSRect(x: 0, y: rcHeader.maxY - 1, width: rcHeader.width, height: 1).fill()
        Skin.draw("FIELD", fTitle, Skin.text, in: NSRect(x: 10, y: 0, width: max(0, rcFull.minX - 14), height: rcHeader.height - 2), left: true)
        button(rcFull, .full, active: isFullScreen) { r in
            Skin.symbol(self.isFullScreen ? "arrow.down.right.and.arrow.up.left" : "arrow.up.left.and.arrow.down.right", in: r, color: Skin.text)
        }
        button(rcPin, .pin, active: cfg.topMost) { r in
            Skin.symbol(self.cfg.topMost ? "pin.fill" : "pin", in: r, color: Skin.text)
        }
        button(rcAbout, .about, active: false) { r in Skin.draw("?", self.fAbout, Skin.text, in: r) }

        // Screen
        Skin.sunken(rcScreen)
        Skin.bg.setFill()
        rcApp.fill()
        NSGraphicsContext.saveGraphicsState()
        NSBezierPath(rect: rcApp).addClip()
        drawDots()
        if state == .paused { veil(0.55) }
        if state == .done {
            // Two slow "breaths" of the gray field, as on the web page
            let t = now - doneAt
            if t < 2.2 { veil(0.25 * (1 - cos(2 * .pi * t / 1.1))) }
        }
        switch state {
        case .setup: drawSetup()
        case .running, .paused:
            drawCancel()
            if state == .paused { drawPausedLabel() }
        case .done: drawDone()
        }
        NSGraphicsContext.restoreGraphicsState()
    }

    func veil(_ alpha: Double) {
        Skin.bg.withAlphaComponent(CGFloat(max(0, min(1, alpha)))).setFill()
        rcApp.fill(using: .sourceOver)
    }

    // Dots fill and empty from the bottom-right corner, row by row
    func drawDots() {
        guard let ctx = NSGraphicsContext.current?.cgContext else { return }
        let n: Int
        switch state {
        case .setup: n = Int((Self.secondsToFrac(cfg.seconds) * Double(total)).rounded())
        case .done: n = total
        default: n = grayCount()
        }
        let first = CGMutablePath(), rest = CGMutablePath()
        let glow = [CGMutablePath(), CGMutablePath(), CGMutablePath()]
        let cell = Self.dot + Self.gap, d = Self.dot
        for r in 0..<rows {
            for c in 0..<cols {
                let order = (rows - 1 - r) * cols + (cols - 1 - c)
                let x = gx0 + CGFloat(c) * cell, y = gy0 + CGFloat(r) * cell
                let rect = CGRect(x: x, y: y, width: d, height: d)
                if order < n {
                    first.addEllipse(in: rect)
                } else if state == .setup {
                    rest.addEllipse(in: rect.insetBy(dx: 0.5, dy: 0.5))
                } else {
                    rest.addEllipse(in: rect)
                    for (i, g) in glow.enumerated() {
                        let grow = CGFloat(3 - i) * 1.2
                        g.addEllipse(in: rect.insetBy(dx: -grow, dy: -grow))
                    }
                }
            }
        }
        switch state {
        case .setup:
            ctx.setFillColor(Skin.setupFill.cgColor)
            ctx.addPath(first); ctx.fillPath()
            ctx.setStrokeColor(Skin.dotIdle.cgColor)
            ctx.setLineWidth(1)
            ctx.addPath(rest); ctx.strokePath()
        default:
            ctx.setFillColor(Skin.elapsed.cgColor)
            ctx.addPath(first); ctx.fillPath()
            // Soft glow, like box-shadow: 0 0 5px rgba(244,241,234,0.30)
            for (i, g) in glow.enumerated() {
                ctx.setFillColor(Skin.active.withAlphaComponent([0.055, 0.11, 0.18][i]).cgColor)
                ctx.addPath(g); ctx.fillPath()
            }
            ctx.setFillColor(Skin.active.cgColor)
            ctx.addPath(rest); ctx.fillPath()
        }
    }

    func drawSetup() {
        let (lcd, hint, start) = setupRects()
        Skin.lcd.setFill()
        lcd.fill()
        Skin.sunken(lcd)
        Skin.draw(Self.format(cfg.seconds), fReadout, Skin.lcdDigit, in: lcd)

        Skin.bg.setFill()
        hint.insetBy(dx: -4, dy: -3).fill()   // keeps the hint readable over lit dots
        Skin.draw(Self.hintText, fHint, Skin.text, in: hint)

        button(start, .start, active: false) { r in
            let ts = Skin.size("Start", self.fStart)
            let tri: CGFloat = 9, gap: CGFloat = 7
            let x = r.midX - (tri + gap + ts.width) / 2, cy = r.midY
            let p = NSBezierPath()
            p.move(to: NSPoint(x: x, y: cy - tri * 0.55))
            p.line(to: NSPoint(x: x + tri, y: cy))
            p.line(to: NSPoint(x: x, y: cy + tri * 0.55))
            p.close()
            Skin.text.setFill()
            p.fill()
            Skin.draw("Start", self.fStart, Skin.text, in: NSRect(x: x + tri + gap, y: r.minY, width: ts.width + 2, height: r.height), left: true)
        }
    }

    func drawCancel() {
        let r = rcCancel
        let hot = hover == .cancel
        (hot ? Skin.closeHover : Skin.face).setFill()
        r.fill()
        if pressed == .cancel { Skin.sunken(r) } else { Skin.raised(r) }
        Skin.draw("×", fCancel, hot ? .white : Skin.text, in: r)
    }

    func drawPausedLabel() {
        let text = "Paused — click to resume"
        let s = Skin.size(text, fPaused)
        let w = (s.width + 32).rounded(), h = (s.height + 14).rounded()
        let r = NSRect(x: (rcApp.midX - w / 2).rounded(), y: (rcApp.midY - h / 2).rounded(), width: w, height: h)
        Skin.face.setFill()
        r.fill()
        Skin.raised(r)
        Skin.draw(text, fPaused, Skin.text, in: r)
    }

    func drawDone() {
        let (box, title, label, ok) = doneRects()
        Skin.face.setFill()
        box.fill()
        Skin.raised(box)
        NSGradient(starting: Skin.titleFrom, ending: Skin.titleTo)?.draw(in: title, angle: 0)
        Skin.draw("Field Timer", fAlertTitle, .white, in: NSRect(x: title.minX + 8, y: title.minY, width: title.width - 16, height: title.height), left: true)
        Skin.draw("Time’s up!", fDone, Skin.text, in: NSRect(x: label.minX, y: label.minY + 8, width: label.width, height: label.height - 8))
        button(ok, .ok, active: false) { r in Skin.draw("OK", self.fOk, Skin.text, in: r) }
    }

    // A Windows 95 push button; `content` draws the label, shifted while pressed
    func button(_ r: NSRect, _ h: Hit, active: Bool, _ content: (NSRect) -> Void) {
        let down = active || pressed == h
        Skin.face.setFill()
        r.fill()
        if down { Skin.sunken(r) } else { Skin.raised(r) }
        content(down ? r.offsetBy(dx: 1, dy: 1) : r)
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

// MARK: - App icon (used by build.sh: Field --write-iconset <dir>)

enum IconArt {
    // A small field of dots, the bottom ones already gray
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        Skin.face.setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()
        let inner = body.insetBy(dx: body.width * 0.09, dy: body.width * 0.09)
        Skin.bg.setFill()
        NSBezierPath(roundedRect: inner, xRadius: inner.width * 0.13, yRadius: inner.width * 0.13).fill()

        let n = 4
        let cell = inner.width / CGFloat(n), d = cell * 0.56
        let gray = Skin.rgb(0x4a, 0x4a, 0x50)
        for r in 0..<n {
            for c in 0..<n {
                // Not flipped here: row 0 is the top row, drawn at the highest y
                let order = (n - 1 - r) * n + (n - 1 - c)
                let cx = inner.minX + (CGFloat(c) + 0.5) * cell
                let cy = inner.maxY - (CGFloat(r) + 0.5) * cell
                (order < 6 ? gray : Skin.active).setFill()
                NSBezierPath(ovalIn: NSRect(x: cx - d / 2, y: cy - d / 2, width: d, height: d)).fill()
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
    let cfg = Settings()
    var window: NSWindow!
    var field: FieldView!

    func applicationDidFinishLaunching(_ note: Notification) {
        field = FieldView(settings: cfg)
        window = NSWindow(contentRect: field.frame, styleMask: [.titled, .closable, .miniaturizable, .resizable],
                          backing: .buffered, defer: false)
        window.title = "Field"
        window.contentView = field
        window.contentMinSize = NSSize(width: 260, height: 240)
        window.collectionBehavior = [.fullScreenPrimary]
        window.delegate = self
        window.isReleasedWhenClosed = false
        if !window.setFrameUsingName("FieldTimer") { window.center() }
        window.setFrameAutosaveName("FieldTimer")
        buildMenu()
        field.doLayout()
        field.setTopMost(cfg.topMost)
        window.makeKeyAndOrderFront(nil)
        window.makeFirstResponder(field)
        NSApp.activate(ignoringOtherApps: true)
    }

    func buildMenu() {
        let main = NSMenu()

        let appItem = NSMenuItem()
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About Field", action: #selector(FieldView.showAbout), keyEquivalent: "").target = field
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Hide Field", action: #selector(NSApplication.hide(_:)), keyEquivalent: "h")
        appMenu.addItem(withTitle: "Quit Field", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu
        main.addItem(appItem)

        let viewItem = NSMenuItem()
        let view = NSMenu(title: "View")
        let full = view.addItem(withTitle: "Enter Full Screen", action: #selector(NSWindow.toggleFullScreen(_:)), keyEquivalent: "f")
        full.keyEquivalentModifierMask = [.control, .command]
        viewItem.submenu = view
        main.addItem(viewItem)

        let winItem = NSMenuItem()
        let win = NSMenu(title: "Window")
        win.addItem(withTitle: "Minimize", action: #selector(NSWindow.performMiniaturize(_:)), keyEquivalent: "m")
        win.addItem(withTitle: "Always on Top", action: #selector(FieldView.toggleTopMost), keyEquivalent: "").target = field
        winItem.submenu = win
        main.addItem(winItem)

        NSApp.mainMenu = main
    }

    func windowDidEnterFullScreen(_ notification: Notification) { field.doLayout() }
    func windowDidExitFullScreen(_ notification: Notification) { field.doLayout() }

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
