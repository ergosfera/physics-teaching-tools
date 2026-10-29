// Field for macOS: the Field dot timer (tools/teaching/field-timer.html) as a desktop app
// (twin of ../windows/Program.cs). Single-file AppKit app; build.sh makes Field.app and a .dmg.
//
// Field is a tribute to Untime, the iPhone and iPad timer that Perceptor, the Dutch design studio of
// Bastiaan Terhorst and Casper Schipper, made in 2012 (https://perceptor.nl/everything/untime.html).
// The idea of time as a field of dots, the dot-matrix numbers and the glowing end are theirs.
import AppKit

// MARK: - Look (same palette and glyphs as the web page)

enum Skin {
    static func rgb(_ r: Int, _ g: Int, _ b: Int, _ a: CGFloat = 1) -> NSColor {
        NSColor(srgbRed: CGFloat(r) / 255, green: CGFloat(g) / 255, blue: CGFloat(b) / 255, alpha: a)
    }
    typealias RGB = (Double, Double, Double)

    static let dark: RGB = (0x24, 0x24, 0x24), lit: RGB = (0x8c, 0x8c, 0x8c), litPaused: RGB = (0x4a, 0x4a, 0x4a)
    static let halo: RGB = (0x0e, 0x0e, 0x0e), glyph: RGB = (0xff, 0xff, 0xff), unit: RGB = (0xe6, 0xe6, 0xe6)
    // The glow at the end, from the centre (0) to the edge (1)
    static let warm: [(Double, RGB)] = [(0, (255, 255, 255)), (0.3, (255, 243, 176)), (0.5, (255, 210, 90)),
                                        (0.7, (255, 143, 42)), (0.86, (168, 64, 26)), (1, (42, 18, 8))]
    static let partialSteps = 16, warmSteps = 48

    static func mix(_ a: RGB, _ b: RGB, _ t: Double) -> RGB {
        (a.0 + (b.0 - a.0) * t, a.1 + (b.1 - a.1) * t, a.2 + (b.2 - a.2) * t)
    }
    static func warmAt(_ t: Double) -> RGB {
        let t = min(1, max(0, t))
        for i in 1..<warm.count where t <= warm[i].0 {
            let a = warm[i - 1], b = warm[i]
            return mix(a.1, b.1, (t - a.0) / (b.0 - a.0))
        }
        return warm[warm.count - 1].1
    }

    // Colour slots: 0 dark, 1 lit, 2 lit paused, 3 halo, 4 glyph, 5 unit, then partial and warm ramps
    static let pPartial = 6, pPartialPaused = pPartial + partialSteps, pWarm = pPartialPaused + partialSteps
    static let palette: [CGColor] = {
        var p: [RGB] = [dark, lit, litPaused, halo, glyph, unit]
        for i in 0..<partialSteps { p.append(mix(dark, lit, (Double(i) + 0.5) / Double(partialSteps))) }
        for i in 0..<partialSteps { p.append(mix(dark, litPaused, (Double(i) + 0.5) / Double(partialSteps))) }
        for i in 0..<warmSteps { p.append(warmAt(Double(i) / Double(warmSteps - 1))) }
        return p.map { rgb(Int($0.0.rounded()), Int($0.1.rounded()), Int($0.2.rounded())).cgColor }
    }()

    // Dot-matrix glyphs: digits 5×7, letters 3×5 (small caps)
    static let big: [Character: [String]] = [
        "0": ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        "1": ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        "2": ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        "3": ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        "4": ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
        "5": ["11111", "10000", "11110", "00001", "00001", "10001", "01110"],
        "6": ["00110", "01000", "10000", "11110", "10001", "10001", "01110"],
        "7": ["11111", "00001", "00010", "00100", "01000", "01000", "01000"],
        "8": ["01110", "10001", "10001", "01110", "10001", "10001", "01110"],
        "9": ["01110", "10001", "10001", "01111", "00001", "00010", "01100"],
        ":": ["0", "0", "1", "0", "1", "0", "0"],
    ]
    static let small: [Character: [String]] = [
        "A": ["010", "101", "111", "101", "101"], "C": ["011", "100", "100", "100", "011"],
        "D": ["110", "101", "101", "101", "110"], "E": ["111", "100", "110", "100", "111"],
        "H": ["101", "101", "111", "101", "101"], "I": ["111", "010", "010", "010", "111"],
        "M": ["10001", "11011", "10101", "10001", "10001"], "N": ["1001", "1101", "1011", "1001", "1001"],
        "P": ["110", "101", "110", "100", "100"], "R": ["110", "101", "110", "101", "101"],
        "S": ["011", "100", "010", "001", "110"], "T": ["111", "010", "010", "010", "010"],
        "U": ["101", "101", "101", "101", "111"],
    ]

    static func textWidth(_ s: String, _ font: [Character: [String]]) -> Int {
        var w = 0
        for (i, ch) in s.enumerated() { w += (font[ch]?[0].count ?? 0) + (i > 0 ? 1 : 0) }
        return w
    }
}

// MARK: - Settings

final class Settings {
    static let minSeconds = 1, maxSeconds = 6 * 60 * 60
    private let d = UserDefaults.standard

    init() { d.register(defaults: ["seconds": 600, "topMost": false, "sound": true]) }

    var seconds: Int {
        get { Settings.snap(d.integer(forKey: "seconds")) }
        set { d.set(Settings.snap(newValue), forKey: "seconds") }
    }
    var topMost: Bool { get { d.bool(forKey: "topMost") } set { d.set(newValue, forKey: "topMost") } }
    var sound: Bool { get { d.bool(forKey: "sound") } set { d.set(newValue, forKey: "sound") } }

    static func clampSeconds(_ v: Int) -> Int { min(maxSeconds, max(minSeconds, v)) }

    // Seconds below a minute, whole minutes below an hour, then 5-minute steps
    static func snap(_ v: Int) -> Int {
        let s = clampSeconds(v)
        if s < 60 { return s }
        if s < 3600 { return clampSeconds(Int((Double(s) / 60).rounded()) * 60) }
        return clampSeconds(Int((Double(s) / 300).rounded()) * 300)
    }
}

// MARK: - The field

final class FieldView: NSView, NSViewToolTipOwner {
    enum State { case setup, running, paused, done }

    struct Button { let id: String; let label: String; let rect: NSRect }

    // Same geometry as the web page
    static let dot: CGFloat = 8, cell: CGFloat = 14, pad: CGFloat = 14
    static let logMin = log(Double(Settings.minSeconds)), logMax = log(Double(Settings.maxSeconds))
    static let glowSeconds: TimeInterval = 60   // how long the glow at the end keeps moving

    let cfg: Settings
    var state = State.setup
    var duration: TimeInterval = 0
    var banked: TimeInterval = 0       // time already counted before the current run
    var runStart: TimeInterval = 0     // uptime when the current run began
    var doneAt: TimeInterval = 0
    var startLit: Double = 0           // lit dots when the countdown began
    var dragging = false
    var pressed: String?
    var hover: String?
    var beepsLeft = 0
    var nextBeep: TimeInterval = 0
    var scrollAccum: CGFloat = 0
    var ticker: Timer?

    var cols = 1, rows = 1, total = 1
    var gx0: CGFloat = 0, gy0: CGFloat = 0   // centre of the first dot
    var px: CGFloat = 3                      // size of one pixel of the button letters
    var buttons: [Button] = []

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
        cols = max(1, Int(((W - 2 * Self.pad - Self.dot) / Self.cell).rounded(.down)) + 1)
        rows = max(1, Int(((H - 2 * Self.pad - Self.dot) / Self.cell).rounded(.down)) + 1)
        total = cols * rows
        gx0 = (W - CGFloat(cols - 1) * Self.cell) / 2
        gy0 = (H - CGFloat(rows - 1) * Self.cell) / 2
        px = max(2, min(6, (min(W, H) / 150).rounded()))
        if state == .running || state == .paused { startLit = litFor(duration) }
        layoutButtons()
        updateToolTips()
        needsDisplay = true
    }

    func card(_ id: String, _ label: String, _ cx: CGFloat, _ cy: CGFloat) -> Button {
        let w = CGFloat(Skin.textWidth(label, Skin.small) + 8) * px, h = 11 * px
        return Button(id: id, label: label, rect: NSRect(x: (cx - w / 2).rounded(), y: (cy - h / 2).rounded(), width: w, height: h))
    }

    func layoutButtons() {
        let W = bounds.width, cy = (bounds.height * 0.74).rounded()
        var b: [Button] = []
        switch state {
        case .setup: b.append(card("start", "START", W / 2, cy))
        case .running: b.append(card("pause", "PAUSE", W / 2, cy))
        case .paused:
            let a = card("resume", "RESUME", 0, cy), r = card("reset", "RESET", 0, cy), gap = 4 * px
            let x = (W / 2 - (a.rect.width + gap + r.rect.width) / 2).rounded()
            b.append(Button(id: a.id, label: a.label, rect: NSRect(x: x, y: a.rect.minY, width: a.rect.width, height: a.rect.height)))
            b.append(Button(id: r.id, label: r.label, rect: NSRect(x: x + a.rect.width + gap, y: r.rect.minY, width: r.rect.width, height: r.rect.height)))
        case .done: b.append(card("dismiss", "DISMISS", W / 2, cy))
        }
        let mw = 11 * px, mh = 7 * px
        b.append(Button(id: "menu", label: "", rect: NSRect(x: (W / 2 - mw / 2).rounded(), y: cy + 11 * px, width: mw, height: mh)))
        buttons = b
    }

    func button(at p: NSPoint) -> Button? { buttons.first { $0.rect.contains(p) } }

    // MARK: Time

    static func secondsToFrac(_ seconds: Double) -> Double {
        (log(min(Double(Settings.maxSeconds), max(Double(Settings.minSeconds), seconds))) - logMin) / (logMax - logMin)
    }

    static func fracToSeconds(_ frac: Double) -> Int {
        Settings.snap(Int(exp(logMin + min(1, max(0, frac)) * (logMax - logMin)).rounded()))
    }

    // What the big numbers say: seconds, whole minutes, or h:mm
    static func display(_ seconds: Double) -> (text: String, unit: String) {
        let s = max(0, Int(seconds.rounded(.up)))
        if s < 60 { return (String(s), "SEC") }
        let m = (s + 59) / 60
        if m < 60 { return (String(m), "MIN") }
        return (String(format: "%d:%02d", m / 60, m % 60), "H")
    }

    static func spoken(_ seconds: Int) -> String {
        let d = display(Double(seconds))
        return d.text + (d.unit == "SEC" ? " seconds" : d.unit == "MIN" ? " minutes" : " hours")
    }

    var now: TimeInterval { ProcessInfo.processInfo.systemUptime }

    var elapsed: TimeInterval { state == .running ? banked + (now - runStart) : banked }

    func litFor(_ seconds: Double) -> Double { (Self.secondsToFrac(seconds) * Double(total)).rounded() }

    func setSeconds(_ s: Int) {
        let v = Settings.snap(s)
        guard v != cfg.seconds else { return }
        cfg.seconds = v
        needsDisplay = true
    }

    // Drag: the higher the pointer, the less time; the lit dots end where you point
    func setFromY(_ y: CGFloat) {
        setSeconds(Self.fracToSeconds(Double((y - (gy0 - Self.cell / 2)) / (CGFloat(rows) * Self.cell))))
    }

    func nudge(_ dir: Int) {
        var s = Self.fracToSeconds(Self.secondsToFrac(Double(cfg.seconds)) + Double(dir) * 0.012)
        if s == cfg.seconds {
            let step = cfg.seconds < 60 ? 1 : cfg.seconds < 3600 ? 60 : 300
            s = cfg.seconds + dir * step
        }
        setSeconds(s)
    }

    func start() {
        duration = TimeInterval(cfg.seconds)
        startLit = litFor(duration)
        banked = 0
        runStart = now
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
        pressed = nil
        layoutButtons()
        needsDisplay = true
    }

    @objc func tick() {
        switch state {
        case .running:
            if elapsed >= duration { finish(); return }
            needsDisplay = true
        case .done:
            let t = now - doneAt
            if beepsLeft > 0 && t >= nextBeep {
                NSSound(named: "Glass")?.play()
                beepsLeft -= 1
                nextBeep += 1.3
            }
            if t < Self.glowSeconds && !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion { needsDisplay = true }
        default:
            break
        }
    }

    // MARK: Commands

    func setTopMost(_ on: Bool) {
        cfg.topMost = on
        window?.level = on ? .floating : .normal
    }

    @objc func toggleTopMost() { setTopMost(!cfg.topMost) }
    @objc func toggleSound() { cfg.sound = !cfg.sound }
    @objc func toggleFullScreen() { window?.toggleFullScreen(nil) }
    @objc func quit() { NSApp.terminate(nil) }

    @objc func showAbout() {
        let body: [NSAttributedString.Key: Any] = [.font: NSFont.systemFont(ofSize: 11), .foregroundColor: NSColor.labelColor]
        let bold: [NSAttributedString.Key: Any] = [.font: NSFont.boldSystemFont(ofSize: 11), .foregroundColor: NSColor.labelColor]
        let credits = NSMutableAttributedString()
        func add(_ s: String, _ a: [NSAttributedString.Key: Any]) { credits.append(NSAttributedString(string: s, attributes: a)) }
        add("Field is a tribute to ", body); add("Untime", bold)
        add(", the timer app made in 2012 by ", body); add("Perceptor", bold)
        add(", the Dutch design studio of ", body); add("Bastiaan Terhorst", bold)
        add(" and ", body); add("Casper Schipper", bold)
        add(". Showing time as a quiet field of dots was their idea, and the numbers drawn with dots and the glow at the end " +
            "come from their app. Thank you for a wonderful idea.\n", body)
        var link = body
        link[.link] = URL(string: "https://perceptor.nl/everything/untime.html")!
        add("Untime on perceptor.nl", link)
        add("\n\nFree software, provided \"as is\", without warranty of any kind. " +
            "Don't rely on it where an interrupted countdown could cause harm or loss.\n" +
            "This app collects no personal data and never connects to the internet.\n\n", body)
        credits.append(NSAttributedString(string: License.text,
            attributes: [.font: NSFont.systemFont(ofSize: 10), .foregroundColor: NSColor.secondaryLabelColor]))
        NSApp.activate(ignoringOtherApps: true)
        NSApp.orderFrontStandardAboutPanel(options: [
            .applicationName: "Field",
            .applicationVersion: "2.0",
            .version: "",
            .credits: credits,
            NSApplication.AboutPanelOptionKey(rawValue: "Copyright"): "© 2026 Mauricio Mendivelso · MIT License",
        ])
    }

    func activate(_ id: String) {
        switch id {
        case "start": start()
        case "pause", "resume": togglePause()
        case "reset", "dismiss": reset()
        case "menu":
            if let b = buttons.last {
                buildMenu().popUp(positioning: nil, at: NSPoint(x: b.rect.midX - 60, y: b.rect.maxY + 4), in: self)
            }
        default: break
        }
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

    func point(_ e: NSEvent) -> NSPoint { convert(e.locationInWindow, from: nil) }

    override func updateTrackingAreas() {
        super.updateTrackingAreas()
        for t in trackingAreas { removeTrackingArea(t) }
        addTrackingArea(NSTrackingArea(rect: bounds,
                                       options: [.mouseMoved, .mouseEnteredAndExited, .activeAlways, .inVisibleRect, .cursorUpdate],
                                       owner: self, userInfo: nil))
        updateToolTips()
    }

    func updateToolTips() {
        removeAllToolTips()
        addToolTip(bounds, owner: self, userData: nil)
    }

    func view(_ view: NSView, stringForToolTip tag: NSView.ToolTipTag, point: NSPoint, userData data: UnsafeMutableRawPointer?) -> String {
        if let b = button(at: point) {
            switch b.id {
            case "start": return "Start (Space)"
            case "pause": return "Pause (Space)"
            case "resume": return "Resume (Space)"
            case "reset", "dismiss": return "Reset (Esc)"
            default: return "Options and About"
            }
        }
        switch state {
        case .setup: return "Drag down for more time, or scroll (now \(Self.spoken(cfg.seconds)))"
        case .paused: return "Click to resume (Space)"
        case .running: return "Click to pause (Space)"
        case .done: return "Time's up"
        }
    }

    func updateCursor(_ p: NSPoint) {
        if button(at: p) != nil { NSCursor.pointingHand.set() }
        else if state == .setup { NSCursor.resizeUpDown.set() }
        else { NSCursor.arrow.set() }
    }

    override func cursorUpdate(with e: NSEvent) { updateCursor(point(e)) }

    override func mouseMoved(with e: NSEvent) {
        let p = point(e)
        updateCursor(p)
        let h = button(at: p)?.id
        if h != hover { hover = h; needsDisplay = true }
    }

    override func mouseExited(with e: NSEvent) {
        if hover != nil { hover = nil; needsDisplay = true }
    }

    override func mouseDown(with e: NSEvent) {
        window?.makeFirstResponder(self)
        let p = point(e)
        if let b = button(at: p) {
            pressed = b.id
            needsDisplay = true
            return
        }
        switch state {
        case .setup: dragging = true; setFromY(p.y)
        case .running, .paused: pressed = "field"
        case .done: break
        }
    }

    override func mouseDragged(with e: NSEvent) {
        if dragging { setFromY(point(e).y) }
    }

    override func mouseUp(with e: NSEvent) {
        if dragging { dragging = false; return }
        guard let p = pressed else { return }
        pressed = nil
        needsDisplay = true
        let b = button(at: point(e))
        if p == "field" { if b == nil { togglePause() }; return }
        if b?.id == p { activate(p) }
    }

    override func scrollWheel(with e: NSEvent) {
        guard state == .setup else { return }
        if e.hasPreciseScrollingDeltas {
            // Trackpad: one step per ~14 points of scrolling
            scrollAccum += e.scrollingDeltaY
            while scrollAccum >= 14 { nudge(1); scrollAccum -= 14 }
            while scrollAccum <= -14 { nudge(-1); scrollAccum += 14 }
        } else if e.scrollingDeltaY != 0 {
            nudge(e.scrollingDeltaY > 0 ? 1 : -1)
        }
    }

    func buildMenu() -> NSMenu {
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

    override func menu(for event: NSEvent) -> NSMenu? { buildMenu() }

    // MARK: Drawing

    override func draw(_ dirtyRect: NSRect) {
        guard let ctx = NSGraphicsContext.current?.cgContext else { return }
        NSColor.black.setFill()
        bounds.fill()

        var slot = [UInt8](repeating: 0, count: total)
        var over = [UInt8](repeating: 0, count: total)
        let paused = state == .paused

        switch state {
        case .setup:
            let n = min(total, Int(litFor(Double(cfg.seconds))))
            for i in 0..<n { slot[i] = 1 }
            stamp(&over, Self.display(Double(cfg.seconds)))
        case .running, .paused:
            let remaining = max(0, duration - elapsed)
            let lit = startLit * remaining / max(0.001, duration)
            let full = min(total, Int(lit.rounded(.down)))
            for i in 0..<full { slot[i] = paused ? 2 : 1 }
            if full < total && lit > Double(full) {
                let step = min(Skin.partialSteps - 1, Int((lit - Double(full)) * Double(Skin.partialSteps)))
                slot[full] = UInt8((paused ? Skin.pPartialPaused : Skin.pPartial) + step)
            }
            stamp(&over, Self.display(remaining))
        case .done:
            // The glow: warm colours spreading from the centre, breathing, with a little sparkle
            let still = NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
            let t = still ? 3 : min(now - doneAt, Self.glowSeconds)
            let grow = min(1, 0.2 + t / 1.4)
            let breathe = 1 + 0.08 * sin(t * 2 * .pi / 2.4)
            let R = 0.62 * Double(max(bounds.width, bounds.height)) * grow * breathe
            let cx = Double(bounds.width) / 2, cy = Double(bounds.height) * 0.42
            for r in 0..<rows {
                for c in 0..<cols {
                    let k = r * cols + c
                    let dx = Double(gx0) + Double(c) * Double(Self.cell) - cx
                    let dy = Double(gy0) + Double(r) * Double(Self.cell) - cy
                    let tw = still ? 0 : 0.07 * sin(Self.hash(k) * 6.283 + t * (1.5 + 2 * Self.hash(k + 7)))
                    let v = (dx * dx + dy * dy).squareRoot() / R + tw
                    slot[k] = UInt8(Skin.pWarm + min(Skin.warmSteps - 1, max(0, Int((v * Double(Skin.warmSteps - 1)).rounded()))))
                }
            }
            stamp(&over, ("0", ""))
        }

        // One path per colour
        var paths = [CGMutablePath?](repeating: nil, count: Skin.palette.count)
        let rad = Self.dot / 2
        for i in 0..<total {
            let o = over[i]
            let p = o == 2 ? 4 : o == 3 ? 5 : o == 1 ? 3 : Int(slot[i])
            let x = gx0 + CGFloat(i % cols) * Self.cell, y = gy0 + CGFloat(i / cols) * Self.cell
            if paths[p] == nil { paths[p] = CGMutablePath() }
            paths[p]!.addEllipse(in: CGRect(x: x - rad, y: y - rad, width: Self.dot, height: Self.dot))
        }
        for (i, path) in paths.enumerated() {
            guard let path = path else { continue }
            ctx.setFillColor(Skin.palette[i])
            ctx.addPath(path)
            ctx.fillPath()
        }

        for b in buttons { drawButton(b) }
    }

    static func hash(_ i: Int) -> Double {
        let x = sin(Double(i) * 12.9898) * 43758.5453
        return x - x.rounded(.down)
    }

    // Marks which dots carry the numbers (2), the unit (3) or the dark ring around the numbers (1)
    func stamp(_ over: inout [UInt8], _ d: (text: String, unit: String)) {
        let digits = Skin.textWidth(d.text, Skin.big)
        var s = max(1, min((cols - 4) / max(1, digits), Int(Double(rows) * 0.5) / 7,
                           Int(min(Double(cols) / 34, Double(rows) / 20).rounded())))
        if digits * s > cols - 2 { return }
        // Keep clear of the buttons: drop the unit first, then make the numbers smaller
        let buttonTop = bounds.height * 0.74 - 5.5 * px
        let limit = min(rows - 1, Int(((buttonTop - gy0 - Self.cell) / Self.cell).rounded(.down)) + 1)
        var unit = d.unit, u = 1, blockH = 0, top = 1
        while true {
            u = max(1, Int((Double(s) * 0.6).rounded()))
            blockH = 7 * s + (unit.isEmpty ? 0 : 2 * s + 5 * u)
            top = max(1, min(Int((Double(rows) * 0.40 - Double(blockH) / 2).rounded()), limit - blockH))
            if top + blockH <= limit { break }
            if !unit.isEmpty { unit = "" }
            else if s > 1 { s -= 1; unit = d.unit }
            else { return }
        }
        var x = (cols - digits * s) / 2
        for ch in d.text {
            guard let g = Skin.big[ch] else { continue }
            paint(&over, g, x, top, s, 2)
            x += (g[0].count + 1) * s
        }
        if unit.isEmpty { return }
        x = (cols - Skin.textWidth(unit, Skin.small) * u) / 2
        let uy = top + 7 * s + 2 * s
        for ch in unit {
            guard let g = Skin.small[ch] else { continue }
            paint(&over, g, x, uy, u, 3)
            x += (g[0].count + 1) * u
        }
    }

    func paint(_ over: inout [UInt8], _ glyph: [String], _ col: Int, _ row: Int, _ s: Int, _ value: UInt8) {
        for (r, line) in glyph.enumerated() {
            for (c, ch) in line.enumerated() where ch == "1" {
                for dy in -1...s {
                    for dx in -1...s {
                        let gr = row + r * s + dy, gc = col + c * s + dx
                        if gr < 0 || gc < 0 || gr >= rows || gc >= cols { continue }
                        let k = gr * cols + gc
                        if dy >= 0 && dx >= 0 && dy < s && dx < s { over[k] = value }
                        else if over[k] == 0 && value == 2 { over[k] = 1 }
                    }
                }
            }
        }
    }

    // White cards with dot letters, and the ··· menu button
    func drawButton(_ b: Button) {
        let hot = hover == b.id, down = hot && pressed == b.id
        if b.id == "menu" {
            NSColor.black.setFill()
            NSBezierPath(roundedRect: b.rect, xRadius: px * 1.5, yRadius: px * 1.5).fill()
            (hot ? Skin.rgb(0xe0, 0xe0, 0xe0) : Skin.rgb(0x80, 0x80, 0x80)).setFill()
            for i in -1...1 {
                let cx = b.rect.midX + CGFloat(i) * 2.6 * px, cy = b.rect.midY, r = px * 0.65
                NSBezierPath(ovalIn: NSRect(x: cx - r, y: cy - r, width: 2 * r, height: 2 * r)).fill()
            }
            return
        }
        let r = down ? b.rect.offsetBy(dx: 0, dy: 1) : b.rect
        (down ? Skin.rgb(0xc8, 0xc8, 0xc8) : hot ? NSColor.white : Skin.rgb(0xec, 0xec, 0xec)).setFill()
        NSBezierPath(roundedRect: r, xRadius: px, yRadius: px).fill()
        Skin.rgb(0x11, 0x11, 0x11).setFill()
        let path = NSBezierPath()
        var x = r.minX + 4 * px
        let y = r.minY + 3 * px, dr = px * 0.42
        for ch in b.label {
            guard let g = Skin.small[ch] else { continue }
            for (row, line) in g.enumerated() {
                for (col, bit) in line.enumerated() where bit == "1" {
                    let cx = x + CGFloat(col) * px + px / 2, cy = y + CGFloat(row) * px + px / 2
                    path.appendOval(in: NSRect(x: cx - dr, y: cy - dr, width: 2 * dr, height: 2 * dr))
                }
            }
            x += CGFloat(g[0].count + 1) * px
        }
        path.fill()
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
    // A small field of dots on black, the first ones lit
    static func draw(_ s: CGFloat) {
        let inset = s * 0.098
        let body = NSRect(x: inset, y: inset, width: s - 2 * inset, height: s - 2 * inset)
        Skin.rgb(0x0a, 0x0a, 0x0a).setFill()
        NSBezierPath(roundedRect: body, xRadius: body.width * 0.225, yRadius: body.width * 0.225).fill()
        let inner = body.insetBy(dx: body.width * 0.12, dy: body.width * 0.12)

        let n = 4
        let cell = inner.width / CGFloat(n), d = cell * 0.56
        let dim = Skin.rgb(0x3a, 0x3a, 0x3a), lit = Skin.rgb(0xf4, 0xf4, 0xf4)
        for r in 0..<n {
            for c in 0..<n {
                // Not flipped here: row 0 is the top row, drawn at the highest y
                let cx = inner.minX + (CGFloat(c) + 0.5) * cell
                let cy = inner.maxY - (CGFloat(r) + 0.5) * cell
                (r * n + c < 6 ? lit : dim).setFill()
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
        // No chrome to speak of: a black title bar that melts into the field
        window.titlebarAppearsTransparent = true
        window.titleVisibility = .hidden
        window.backgroundColor = .black
        window.appearance = NSAppearance(named: .darkAqua)
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
