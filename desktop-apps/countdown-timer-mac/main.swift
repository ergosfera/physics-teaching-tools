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
    static let note = rgb(205, 208, 216)
    static let link = rgb(110, 175, 255)
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

// MARK: - Timer data (every timer window has its own)

final class TimerModel {
    let id: String
    var hours = 0, minutes = 5, seconds = 0
    var yellowPct = 30, redPct = 10
    var topMost = true, sound = true
    var note = ""

    init(id: String = UUID().uuidString) { self.id = id }

    subscript(seg: Int) -> Int {
        get { seg == 0 ? hours : seg == 1 ? minutes : seconds }
        set {
            let v = clamp(newValue, 0, seg == 0 ? 99 : 59)
            if seg == 0 { hours = v } else if seg == 1 { minutes = v } else { seconds = v }
        }
    }

    var duration: TimeInterval { TimeInterval(hours * 3600 + minutes * 60 + seconds) }

    /// A new timer starts with the same time and settings as the one it came from, but no note.
    func copy() -> TimerModel {
        let m = TimerModel()
        m.hours = hours; m.minutes = minutes; m.seconds = seconds
        m.yellowPct = yellowPct; m.redPct = redPct
        m.topMost = topMost; m.sound = sound
        return m
    }

    var dict: [String: Any] {
        ["id": id, "hours": hours, "minutes": minutes, "seconds": seconds,
         "yellowPct": yellowPct, "redPct": redPct, "topMost": topMost, "sound": sound, "note": note]
    }

    convenience init(dict d: [String: Any]) {
        self.init(id: d["id"] as? String ?? UUID().uuidString)
        func int(_ k: String, _ def: Int, _ lo: Int, _ hi: Int) -> Int { clamp(d[k] as? Int ?? def, lo, hi) }
        hours = int("hours", 0, 0, 99)
        minutes = int("minutes", 5, 0, 59)
        seconds = int("seconds", 0, 0, 59)
        yellowPct = int("yellowPct", 30, 1, 100)
        redPct = int("redPct", 10, 0, 99)
        if redPct >= yellowPct { redPct = max(0, yellowPct - 1) }
        topMost = d["topMost"] as? Bool ?? true
        sound = d["sound"] as? Bool ?? true
        note = d["note"] as? String ?? ""
    }
}

func clamp(_ v: Int, _ lo: Int, _ hi: Int) -> Int { min(hi, max(lo, v)) }

// MARK: - Note text: plain text with links

enum NoteText {
    // [name](address), or a bare web address.
    static let pattern = try! NSRegularExpression(
        pattern: #"\[([^\]\n]+)\]\(([^()\s]+)\)|(?:https?://|www\.)[^\s<>"]+"#, options: [.caseInsensitive])

    static func url(_ s: String) -> URL? {
        let hasScheme = s.range(of: #"^[a-z][a-z0-9+.-]*:"#, options: [.regularExpression, .caseInsensitive]) != nil
        return URL(string: hasScheme ? s : "https://" + s)
    }

    /// Long bare addresses are shown short; the link still opens the full address.
    static func shortLabel(_ raw: String) -> String {
        var s = raw
        for p in ["https://", "http://"] where s.lowercased().hasPrefix(p) { s.removeFirst(p.count) }
        if s.hasSuffix("/") { s.removeLast() }
        return s.count > 36 ? String(s.prefix(35)) + "…" : s
    }

    /// The text to show, and where the links are in it.
    static func parse(_ source: String) -> (String, [(NSRange, URL)]) {
        let ns = source as NSString
        var out = ""
        var links: [(NSRange, URL)] = []
        var last = 0
        for m in pattern.matches(in: source, range: NSRange(location: 0, length: ns.length)) {
            out += ns.substring(with: NSRange(location: last, length: m.range.location - last))
            last = m.range.location + m.range.length
            var label: String, target: String, trailing = ""
            if m.range(at: 1).location != NSNotFound {
                label = ns.substring(with: m.range(at: 1))
                target = ns.substring(with: m.range(at: 2))
            } else {
                // Punctuation right after an address belongs to the sentence, not the address.
                var raw = ns.substring(with: m.range)
                while let c = raw.last, ".,;:!?'\"".contains(c) ||
                        (c == ")" && raw.filter({ $0 == "(" }).count < raw.filter({ $0 == ")" }).count) {
                    trailing = String(c) + trailing
                    raw.removeLast()
                }
                label = shortLabel(raw)
                target = raw
            }
            guard let u = url(target) else { out += ns.substring(with: m.range); continue }
            let start = (out as NSString).length
            out += label
            links.append((NSRange(location: start, length: (label as NSString).length), u))
            out += trailing
        }
        out += ns.substring(from: last)
        return (out, links)
    }
}

// MARK: - Window

final class TimerWindow: NSWindow {
    override var canBecomeKey: Bool { true }
    override var canBecomeMain: Bool { true }
}

// MARK: - Timer view

final class TimerView: NSView, NSViewToolTipOwner {
    enum Hit: Equatable { case none, close, min, pin, gear, end, noteBtn, seg(Int), main, reset, note, link(Int) }
    enum State { case idle, running, paused, finished }

    let cfg: TimerModel
    var onChange: (() -> Void)?            // something worth saving changed
    var onClose: ((TimerView) -> Void)?
    var onNew: ((TimerView) -> Void)?
    var state = State.idle
    var total: TimeInterval = 0
    var banked: TimeInterval = 0       // time already counted before the current run
    var runStart: TimeInterval = 0     // uptime when the current run began
    var finishAt: TimeInterval = 0
    var endDate: Date?                 // set while counting to a clock time instead of a duration
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
    let noteFont = NSFont.systemFont(ofSize: 13)

    // The note sits under the buttons; the window grows to fit it.
    static let baseHeight: CGFloat = 130
    let noteTop: CGFloat = 136
    let noteStorage = NSTextStorage()
    let noteLayout = NSLayoutManager()
    let noteContainer = NSTextContainer(size: NSSize(width: 252, height: 220))
    var noteLinks: [(NSRange, URL)] = []
    var noteHeight: CGFloat = 0
    var noteRect: NSRect { NSRect(x: 14, y: noteTop, width: 252, height: noteHeight) }

    let rcNoteBtn = NSRect(x: 112, y: 0, width: 28, height: 26)
    let rcClose = NSRect(x: 252, y: 0, width: 28, height: 26)
    let rcMin = NSRect(x: 224, y: 0, width: 28, height: 26)
    let rcPin = NSRect(x: 196, y: 0, width: 28, height: 26)
    let rcGear = NSRect(x: 168, y: 0, width: 28, height: 26)
    let rcEnd = NSRect(x: 140, y: 0, width: 28, height: 26)
    let rcBar = NSRect(x: 14, y: 86, width: 252, height: 8)
    let rcMain = NSRect(x: 46, y: 100, width: 90, height: 24)
    let rcReset = NSRect(x: 144, y: 100, width: 90, height: 24)
    let digitsTop: CGFloat = 26
    let digitsHeight: CGFloat = 56
    var textX: CGFloat = 0
    var segRects = [NSRect](repeating: .zero, count: 3)

    init(model: TimerModel) {
        cfg = model
        super.init(frame: NSRect(x: 0, y: 0, width: 280, height: Self.baseHeight))
        noteContainer.lineFragmentPadding = 0
        noteLayout.addTextContainer(noteContainer)
        noteStorage.addLayoutManager(noteLayout)
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

    // MARK: Note

    func changed() { onChange?() }

    /// Lays out the note, resizes the window to fit it and names the window after it.
    func layoutNote() {
        let (text, links) = NoteText.parse(cfg.note.trimmingCharacters(in: .whitespacesAndNewlines))
        noteLinks = links
        let para = NSMutableParagraphStyle()
        para.lineBreakMode = .byWordWrapping
        para.paragraphSpacing = 2
        let a = NSMutableAttributedString(string: text, attributes: [.font: noteFont, .foregroundColor: Palette.note,
                                                                     .paragraphStyle: para])
        for (r, _) in links {
            a.addAttributes([.foregroundColor: Palette.link, .underlineStyle: NSUnderlineStyle.single.rawValue], range: r)
        }
        noteStorage.setAttributedString(a)
        noteLayout.ensureLayout(for: noteContainer)
        noteHeight = text.isEmpty ? 0 : ceil(noteLayout.usedRect(for: noteContainer).height)

        let h = noteHeight > 0 ? noteTop + noteHeight + 12 : Self.baseHeight
        if let w = window, w.frame.height != h {
            var f = w.frame
            f.origin.y += f.height - h      // keep the top edge where it is
            f.size.height = h
            w.setFrame(f, display: true)
            w.invalidateShadow()
        } else if window == nil {
            setFrameSize(NSSize(width: 280, height: h))
        }
        let first = text.split(separator: "\n").first.map(String.init) ?? ""
        window?.title = first.isEmpty ? "Countdown Timer" : first
        updateTrackingAreas()
        needsDisplay = true
    }

    func linkIndex(at p: NSPoint) -> Int? {
        guard noteHeight > 0, noteRect.contains(p) else { return nil }
        let local = NSPoint(x: p.x - noteRect.minX, y: p.y - noteRect.minY)
        var frac: CGFloat = 0
        let gi = noteLayout.glyphIndex(for: local, in: noteContainer, fractionOfDistanceThroughGlyph: &frac)
        guard noteLayout.boundingRect(forGlyphRange: NSRange(location: gi, length: 1), in: noteContainer)
                .insetBy(dx: -1, dy: -1).contains(local) else { return nil }
        let ci = noteLayout.characterIndexForGlyph(at: gi)
        return noteLinks.firstIndex { NSLocationInRange(ci, $0.0) }
    }

    @objc func openNote() {
        let box = FlippedView(frame: NSRect(x: 0, y: 0, width: 340, height: 168))
        let scroll = NSScrollView(frame: NSRect(x: 0, y: 0, width: 340, height: 130))
        scroll.hasVerticalScroller = true
        scroll.borderType = .bezelBorder
        let size = scroll.contentSize
        let tv = NSTextView(frame: NSRect(origin: .zero, size: size))
        tv.isRichText = false
        tv.allowsUndo = true
        tv.font = NSFont.systemFont(ofSize: 13)
        tv.isAutomaticQuoteSubstitutionEnabled = false
        tv.isAutomaticDashSubstitutionEnabled = false
        tv.isAutomaticLinkDetectionEnabled = false
        tv.minSize = NSSize(width: 0, height: size.height)
        tv.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        tv.isVerticallyResizable = true
        tv.autoresizingMask = [.width]
        tv.textContainer?.widthTracksTextView = true
        tv.string = cfg.note
        scroll.documentView = tv
        box.addSubview(scroll)
        let hint = NSTextField(wrappingLabelWithString:
            "Paste a web address to make a link, or write [name](address).\n⌘Return saves.")
        hint.font = NSFont.systemFont(ofSize: 11)
        hint.textColor = .secondaryLabelColor
        hint.frame = NSRect(x: 0, y: 136, width: 340, height: 32)
        box.addSubview(hint)

        let alert = NSAlert()
        alert.messageText = "Note"
        alert.informativeText = "What this timer is counting down to. It shows under the timer."
        let ok = alert.addButton(withTitle: "OK")
        alert.addButton(withTitle: "Cancel")
        // Return makes a new line in the note; ⌘Return saves.
        ok.keyEquivalent = "\r"
        ok.keyEquivalentModifierMask = [.command]
        alert.accessoryView = box
        alert.window.initialFirstResponder = tv
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        cfg.note = tv.string.trimmingCharacters(in: .whitespacesAndNewlines)
        layoutNote()
        changed()
    }

    // MARK: Saving and restoring

    func snapshot() -> [String: Any] {
        var d = cfg.dict
        if let f = window?.frame { d["x"] = Double(f.minX); d["top"] = Double(f.maxY) }
        switch state {
        case .running:
            // Saved as the moment it ends, so it keeps counting while the app is closed.
            d["run"] = "running"
            d["endsAt"] = Date().addingTimeInterval(remaining()).timeIntervalSince1970
            d["total"] = total
        case .paused:
            d["run"] = "paused"
            d["total"] = total
            d["banked"] = banked
        case .idle:
            if let e = endDate { d["run"] = "until"; d["endsAt"] = e.timeIntervalSince1970 }
        case .finished:
            break
        }
        return d
    }

    func restoreRun(_ d: [String: Any]) {
        let t = d["total"] as? Double ?? 0
        let ends = (d["endsAt"] as? Double).map { Date(timeIntervalSince1970: $0) }
        switch d["run"] as? String {
        case "running":
            guard let e = ends, t > 0 else { break }
            total = t
            banked = 0
            runStart = now
            if e.timeIntervalSinceNow > 0 { endDate = e; state = .running } else { finish() }
        case "paused":
            guard t > 0 else { break }
            total = t
            banked = min(t, max(0, d["banked"] as? Double ?? 0))
            state = .paused
        case "until":
            if let e = ends, e.timeIntervalSinceNow > 0 { endDate = e }
        default:
            break
        }
        needsDisplay = true
    }

    /// A timer with a note or a countdown going is worth a question before closing it.
    var worthKeeping: Bool { !cfg.note.isEmpty || state == .running || state == .paused || endDate != nil }

    // MARK: Timer logic

    var now: TimeInterval { ProcessInfo.processInfo.systemUptime }

    func remaining() -> TimeInterval {
        // Counting to a clock time follows the wall clock, so it stays right after sleep.
        if let e = endDate { return max(0, e.timeIntervalSinceNow) }
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
        total = endDate?.timeIntervalSinceNow ?? cfg.duration
        guard total > 0 else { NSSound.beep(); return }
        banked = 0
        runStart = now
        state = .running
        sel = -1
        beepsLeft = 0
        changed()
    }

    func mainAction() {
        switch state {
        case .idle, .finished: start()
        case .running:
            // Pausing lets go of the clock time: resuming continues from what was left.
            if endDate != nil { banked = total - remaining(); endDate = nil } else { banked += now - runStart }
            state = .paused
        case .paused: runStart = now; state = .running
        }
        needsDisplay = true
        changed()
    }

    func reset() {
        banked = 0
        beepsLeft = 0
        endDate = nil
        state = .idle
        needsDisplay = true
        changed()
    }

    func finish() {
        endDate = nil
        state = .finished
        finishAt = now
        beepsLeft = cfg.sound ? 3 : 0
        nextBeep = 0
        if !NSApp.isActive { NSApp.requestUserAttention(.informationalRequest) }
        changed()
    }

    @objc func tick() {
        switch state {
        case .running:
            if remaining() <= 0 { finish() }
            needsDisplay = true
        case .idle where endDate != nil:
            // Nobody pressed Start before the clock time passed: go back to the set duration.
            if remaining() <= 0 { endDate = nil }
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
        changed()
    }

    @objc func toggleTopMost() { setTopMost(!cfg.topMost) }
    @objc func toggleSound() { cfg.sound = !cfg.sound; changed() }
    @objc func minimizeWindow() { window?.miniaturize(nil) }
    @objc func quit() { NSApp.terminate(nil) }
    @objc func closeTimer() { onClose?(self) }
    @objc func newTimer() { onNew?(self) }

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
            .applicationVersion: "1.2",
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
            let before = cfg.duration
            cfg.hours = num(fH, cfg.hours, 0, 99)
            cfg.minutes = num(fM, cfg.minutes, 0, 59)
            cfg.seconds = num(fS, cfg.seconds, 0, 59)
            if state == .idle && cfg.duration != before { endDate = nil }
            cfg.yellowPct = y
            cfg.redPct = r
            cfg.sound = cSound.state == .on
            setTopMost(cTop.state == .on)
            if state == .finished { reset() }
            needsDisplay = true
            changed()
            return
        }
    }

    @objc func openEndTime() {
        guard state == .idle || state == .finished else { return }
        let view = FlippedView(frame: NSRect(x: 0, y: 0, width: 320, height: 60))
        let title = NSTextField(labelWithString: "Count down until")
        title.font = NSFont.boldSystemFont(ofSize: 13)
        title.frame.origin = NSPoint(x: 0, y: 3)
        title.sizeToFit()
        view.addSubview(title)
        let picker = NSDatePicker()
        picker.datePickerStyle = .textFieldAndStepper
        picker.datePickerElements = .hourMinute
        picker.dateValue = endDate ?? Date().addingTimeInterval(cfg.duration)
        picker.sizeToFit()
        picker.frame.origin = NSPoint(x: 130, y: 0)
        view.addSubview(picker)
        let note = NSTextField(labelWithString: "A time that has already passed today means tomorrow.")
        note.frame.origin = NSPoint(x: 0, y: 38)
        note.sizeToFit()
        view.addSubview(note)

        let alert = NSAlert()
        alert.messageText = "End time"
        alert.addButton(withTitle: "OK")
        alert.addButton(withTitle: "Cancel")
        alert.accessoryView = view
        alert.window.initialFirstResponder = picker
        guard alert.runModal() == .alertFirstButtonReturn else { return }
        if state == .finished { reset() }
        // A time that has already passed today means tomorrow.
        let cal = Calendar.current
        let c = cal.dateComponents([.hour, .minute], from: picker.dateValue)
        endDate = cal.nextDate(after: Date(), matching: DateComponents(hour: c.hour, minute: c.minute, second: 0),
                               matchingPolicy: .nextTime)
        sel = -1
        needsDisplay = true
        changed()
    }

    // MARK: Editing the time (only before starting)

    // Editing the digits while an end time is set: carry on from what they show, as a plain duration.
    func releaseEndTime() {
        guard endDate != nil else { return }
        let s = max(0, Int((remaining() - 1e-9).rounded(.up)))
        endDate = nil
        cfg.hours = s / 3600
        cfg.minutes = (s / 60) % 60
        cfg.seconds = s % 60
    }

    func step(_ s: Int, _ delta: Int) {
        releaseEndTime()
        let maxV = s == 0 ? 99 : 59
        var v = cfg[s] + delta
        if v > maxV { v = 0 }
        if v < 0 { v = maxV }
        cfg[s] = v
        needsDisplay = true
        changed()
    }

    func typeDigit(_ d: Int) {
        releaseEndTime()
        if sel < 0 { sel = 0; typed = 0 }
        cfg[sel] = typed == 0 ? d : cfg[sel] * 10 + d
        typed += 1
        if typed >= 2 {
            typed = 0
            if sel < 2 { sel += 1 }
        }
        needsDisplay = true
        changed()
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
        if ch.lowercased() == "e" { openEndTime(); return }
        if ch.lowercased() == "n" { openNote(); return }
        guard state == .idle else { super.keyDown(with: e); return }
        if ch.count == 1, let d = Int(ch) { typeDigit(d); return }
        let shift = e.modifierFlags.contains(.shift)
        switch e.keyCode {
        case 126: if sel < 0 { sel = 1 }; typed = 0; step(sel, 1)                     // up
        case 125: if sel < 0 { sel = 1 }; typed = 0; step(sel, -1)                    // down
        case 123: sel = max(0, sel - 1); typed = 0                                     // left
        case 124: sel = sel < 0 ? 0 : min(2, sel + 1); typed = 0                       // right
        case 48: sel = shift ? max(0, sel - 1) : (sel < 0 ? 0 : min(2, sel + 1)); typed = 0  // tab
        case 51, 117: if sel >= 0 { releaseEndTime(); cfg[sel] = 0; typed = 0; changed() }  // delete
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
        if rcEnd.contains(p) { return .end }
        if rcNoteBtn.contains(p) { return .noteBtn }
        if rcMain.contains(p) { return .main }
        if rcReset.contains(p) { return .reset }
        if state == .idle {
            for i in 0..<3 where segRects[i].contains(p) { return .seg(i) }
        }
        if let i = linkIndex(at: p) { return .link(i) }
        if noteHeight > 0 && noteRect.contains(p) { return .note }
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
        for r in [rcClose, rcMin, rcPin, rcGear, rcEnd, rcNoteBtn, rcMain, rcReset, noteRect] + segRects where !r.isEmpty {
            addToolTip(r, owner: self, userData: nil)
        }
    }

    func view(_ view: NSView, stringForToolTip tag: NSView.ToolTipTag, point: NSPoint, userData data: UnsafeMutableRawPointer?) -> String {
        switch region(at: point) {
        case .close: return "Close this timer"
        case .min: return "Minimize"
        case .pin: return cfg.topMost ? "Always on top: ON (click to turn off)" : "Always on top: OFF (click to turn on)"
        case .gear: return "Settings"
        case .end: return state == .running || state == .paused ? "Reset first to set an end time" : "Set an end time (E)"
        case .seg: return "Scroll, or click and type, to set the time"
        case .main: return mainLabel + " (Space)"
        case .reset: return "Reset (R)"
        case .noteBtn: return cfg.note.isEmpty ? "Add a note (N)" : "Edit the note (N)"
        case .link(let i): return noteLinks[i].1.absoluteString
        case .note: return "Double-click to edit the note"
        case .none: return ""
        }
    }

    override func mouseMoved(with e: NSEvent) {
        let h = region(at: point(e))
        if h != hover {
            if case .link = h { NSCursor.pointingHand.set() } else if case .link = hover { NSCursor.arrow.set() }
            hover = h
            needsDisplay = true
        }
    }

    override func mouseExited(with e: NSEvent) {
        if case .link = hover { NSCursor.arrow.set() }
        hover = .none
        needsDisplay = true
    }

    override func mouseDown(with e: NSEvent) {
        window?.makeFirstResponder(self)
        let h = region(at: point(e))
        switch h {
        case .seg(let i):
            sel = i; typed = 0; needsDisplay = true
        case .note where e.clickCount == 2:
            openNote()
        case .none, .note:
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
        case .close: closeTimer()
        case .min: minimizeWindow()
        case .pin: toggleTopMost()
        case .gear: openSettings()
        case .end: openEndTime()
        case .noteBtn: openNote()
        case .link(let i): if i < noteLinks.count { NSWorkspace.shared.open(noteLinks[i].1) }
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
        if case .link(let i) = region(at: point(event)), i < noteLinks.count {
            let url = noteLinks[i].1
            let open = m.addItem(withTitle: "Open Link", action: #selector(openLinkItem(_:)), keyEquivalent: "")
            open.representedObject = url
            let copy = m.addItem(withTitle: "Copy Link", action: #selector(copyLinkItem(_:)), keyEquivalent: "")
            copy.representedObject = url
            m.addItem(.separator())
        }
        m.addItem(withTitle: "New Timer", action: #selector(newTimer), keyEquivalent: "")
        m.addItem(withTitle: cfg.note.isEmpty ? "Add Note…" : "Edit Note…", action: #selector(openNote), keyEquivalent: "")
        m.addItem(.separator())
        if state == .idle || state == .finished {
            m.addItem(withTitle: "Set End Time…", action: #selector(openEndTime), keyEquivalent: "")
        }
        let top = m.addItem(withTitle: "Always on top", action: #selector(toggleTopMost), keyEquivalent: "")
        top.state = cfg.topMost ? .on : .off
        let snd = m.addItem(withTitle: "Sound at the end", action: #selector(toggleSound), keyEquivalent: "")
        snd.state = cfg.sound ? .on : .off
        m.addItem(withTitle: "Settings…", action: #selector(openSettings), keyEquivalent: "")
        m.addItem(withTitle: "About Countdown Timer", action: #selector(showAbout), keyEquivalent: "")
        m.addItem(.separator())
        m.addItem(withTitle: "Close This Timer", action: #selector(closeTimer), keyEquivalent: "")
        m.addItem(withTitle: "Quit", action: #selector(quit), keyEquivalent: "")
        for item in m.items { item.target = self }
        return m
    }

    @objc func openLinkItem(_ item: NSMenuItem) {
        if let u = item.representedObject as? URL { NSWorkspace.shared.open(u) }
    }

    @objc func copyLinkItem(_ item: NSMenuItem) {
        guard let u = item.representedObject as? URL else { return }
        NSPasteboard.general.clearContents()
        NSPasteboard.general.setString(u.absoluteString, forType: .string)
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
    static let clockFormat: DateFormatter = {
        let f = DateFormatter()
        f.dateStyle = .none
        f.timeStyle = .short
        return f
    }()

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
        let title = endDate.map { "Until " + Self.clockFormat.string(from: $0) }
            ?? (state == .idle ? "Timer" : "Timer · " + Self.fmt(total))
        drawText(title, font: titleFont, color: Palette.dim, in: NSRect(x: 12, y: 0, width: 100, height: 26), alignLeft: true)
        iconButton(rcNoteBtn, "square.and.pencil", .noteBtn, cfg.note.isEmpty ? Palette.dim : Palette.text)
        iconButton(rcEnd, "clock", .end, endDate != nil ? Palette.text : Palette.dim)
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
        let canStart = state != .idle || endDate != nil || cfg.duration > 0
        button(rcMain, mainLabel, .main, canStart ? Palette.text : Palette.dim)
        button(rcReset, "Reset", .reset, state == .idle && endDate == nil ? Palette.dim : Palette.text)

        // Note
        if noteHeight > 0 {
            Palette.track.setFill()
            NSRect(x: 14, y: noteTop - 6, width: 252, height: 1).fill()
            let glyphs = noteLayout.glyphRange(for: noteContainer)
            noteLayout.drawGlyphs(forGlyphRange: glyphs, at: noteRect.origin)
        }

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

let newTimerNotice = Notification.Name("net.ergosfera.countdowntimer.newTimer")

final class AppDelegate: NSObject, NSApplicationDelegate, NSWindowDelegate {
    let defaults = UserDefaults.standard
    var views: [TimerView] = []
    var restoring = false

    /// The timer the user is working with: the one in front, else the newest.
    var currentView: TimerView? { NSApp.keyWindow?.contentView as? TimerView ?? views.last }

    func applicationDidFinishLaunching(_ note: Notification) {
        buildMainMenu()
        // Opening the app again (for example with "open -n") asks this copy for a new timer.
        DistributedNotificationCenter.default().addObserver(forName: newTimerNotice, object: nil, queue: .main) { [weak self] _ in
            self?.newTimer(nil)
        }

        restoring = true
        if let saved = defaults.array(forKey: "timers") as? [[String: Any]], !saved.isEmpty {
            for t in saved {
                var top: NSPoint?
                if let x = t["x"] as? Double, let y = t["top"] as? Double { top = NSPoint(x: x, y: y) }
                addTimer(TimerModel(dict: t), topLeft: top).restoreRun(t)
            }
        } else {
            // First start of 1.2: carry over the single timer of earlier versions.
            addTimer(TimerModel(dict: defaults.dictionaryRepresentation()), topLeft: nil, oldFrameName: "CountdownTimer")
        }
        restoring = false
        save()
        NSApp.activate(ignoringOtherApps: true)
    }

    @discardableResult
    func addTimer(_ model: TimerModel, topLeft: NSPoint?, oldFrameName: String? = nil) -> TimerView {
        let view = TimerView(model: model)
        let window = TimerWindow(contentRect: view.frame, styleMask: [.borderless, .miniaturizable],
                                 backing: .buffered, defer: false)
        window.title = "Countdown Timer"
        window.isOpaque = false
        window.backgroundColor = .clear
        window.hasShadow = true
        window.contentView = view
        window.isReleasedWhenClosed = false
        window.delegate = self

        var placed = false
        if let name = oldFrameName { placed = window.setFrameUsingName(name) }
        if let p = topLeft, onScreen(NSRect(x: p.x, y: p.y - 26, width: 280, height: 26)) {
            window.setFrameTopLeftPoint(p)
            placed = true
        }
        if !placed, let screen = NSScreen.main {
            let vf = screen.visibleFrame
            window.setFrameOrigin(NSPoint(x: vf.maxX - 280 - 24, y: vf.minY + 24))
        }

        view.onChange = { [weak self] in self?.save() }
        view.onClose = { [weak self] v in self?.closeTimer(v) }
        view.onNew = { [weak self] v in self?.newTimer(from: v) }
        view.layoutNote()
        view.setTopMost(model.topMost)
        views.append(view)

        window.makeKeyAndOrderFront(nil)
        window.makeFirstResponder(view)
        window.invalidateShadow()
        return view
    }

    /// True when the title strip of a window there would be on some screen, enough to grab it.
    func onScreen(_ strip: NSRect) -> Bool {
        NSScreen.screens.contains { $0.visibleFrame.intersection(strip.insetBy(dx: 20, dy: 0)).height >= 20 }
    }

    @objc func newTimer(_ sender: Any?) { newTimer(from: currentView) }

    func newTimer(from source: TimerView?) {
        var topLeft: NSPoint?
        if let f = source?.window?.frame {
            // Next to the timer it came from: up and to the left, or down if there is no room above.
            let up = NSPoint(x: f.minX - 30, y: f.maxY + f.height + 10)
            let down = NSPoint(x: f.minX - 30, y: f.minY - 10)
            topLeft = onScreen(NSRect(x: up.x, y: up.y - 26, width: 280, height: 26)) &&
                      NSScreen.screens.contains(where: { $0.visibleFrame.contains(NSPoint(x: up.x, y: up.y)) }) ? up : down
        }
        addTimer(source?.cfg.copy() ?? TimerModel(), topLeft: topLeft)
        save()
        NSApp.activate(ignoringOtherApps: true)
    }

    func closeTimer(_ view: TimerView) {
        // The last timer stays for next time, as before: closing it quits the app.
        guard views.count > 1 else { NSApp.terminate(nil); return }
        if view.worthKeeping {
            let alert = NSAlert()
            alert.messageText = "Close this timer?"
            let first = view.cfg.note.split(separator: "\n").first.map(String.init) ?? ""
            alert.informativeText = first.isEmpty ? "Its countdown will be gone." : "“\(first)” will be gone."
            alert.addButton(withTitle: "Close Timer")
            alert.addButton(withTitle: "Cancel")
            guard alert.runModal() == .alertFirstButtonReturn else { return }
        }
        views.removeAll { $0 === view }
        view.ticker?.invalidate()
        view.window?.orderOut(nil)
        view.window?.close()
        save()
    }

    func save() {
        guard !restoring else { return }
        defaults.set(views.map { $0.snapshot() }, forKey: "timers")
    }

    func windowDidMove(_ notification: Notification) { save() }

    func applicationWillTerminate(_ notification: Notification) { save() }

    func buildMainMenu() {
        let main = NSMenu()

        let appItem = NSMenuItem()
        main.addItem(appItem)
        let appMenu = NSMenu()
        appMenu.addItem(withTitle: "About Countdown Timer", action: #selector(TimerView.showAbout), keyEquivalent: "")
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Settings…", action: #selector(TimerView.openSettings), keyEquivalent: ",")
        appMenu.addItem(.separator())
        appMenu.addItem(withTitle: "Quit Countdown Timer", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        appItem.submenu = appMenu

        let fileItem = NSMenuItem()
        main.addItem(fileItem)
        let fileMenu = NSMenu(title: "Timer")
        fileMenu.addItem(withTitle: "New Timer", action: #selector(newTimer(_:)), keyEquivalent: "n").target = self
        fileMenu.addItem(withTitle: "Edit Note…", action: #selector(TimerView.openNote), keyEquivalent: "")
        fileMenu.addItem(withTitle: "Set End Time…", action: #selector(TimerView.openEndTime), keyEquivalent: "")
        fileMenu.addItem(.separator())
        fileMenu.addItem(withTitle: "Minimize", action: #selector(TimerView.minimizeWindow), keyEquivalent: "m")
        fileMenu.addItem(withTitle: "Close Timer", action: #selector(TimerView.closeTimer), keyEquivalent: "w")
        fileItem.submenu = fileMenu

        // Needed so copy and paste work in the note and settings boxes.
        let editItem = NSMenuItem()
        main.addItem(editItem)
        let editMenu = NSMenu(title: "Edit")
        editMenu.addItem(withTitle: "Undo", action: Selector(("undo:")), keyEquivalent: "z")
        let redo = editMenu.addItem(withTitle: "Redo", action: Selector(("redo:")), keyEquivalent: "z")
        redo.keyEquivalentModifierMask = [.command, .shift]
        editMenu.addItem(.separator())
        editMenu.addItem(withTitle: "Cut", action: #selector(NSText.cut(_:)), keyEquivalent: "x")
        editMenu.addItem(withTitle: "Copy", action: #selector(NSText.copy(_:)), keyEquivalent: "c")
        editMenu.addItem(withTitle: "Paste", action: #selector(NSText.paste(_:)), keyEquivalent: "v")
        editMenu.addItem(withTitle: "Select All", action: #selector(NSText.selectAll(_:)), keyEquivalent: "a")
        editItem.submenu = editMenu

        NSApp.mainMenu = main
    }

    func applicationDockMenu(_ sender: NSApplication) -> NSMenu? {
        let m = NSMenu()
        m.addItem(withTitle: "New Timer", action: #selector(newTimer(_:)), keyEquivalent: "").target = self
        return m
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        for v in views {
            if v.window?.isMiniaturized == true { v.window?.deminiaturize(nil) }
            v.window?.orderFront(nil)
        }
        currentView?.window?.makeKey()
        return true
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
}

let args = CommandLine.arguments
if args.count == 3 && args[1] == "--write-iconset" {
    do { try IconArt.writeIconset(to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}

// One copy of the app holds all the timers. A second copy asks the first for a new timer and leaves.
if let bid = Bundle.main.bundleIdentifier,
   let first = NSRunningApplication.runningApplications(withBundleIdentifier: bid)
       .first(where: { $0.processIdentifier != ProcessInfo.processInfo.processIdentifier }) {
    DistributedNotificationCenter.default().postNotificationName(newTimerNotice, object: nil, userInfo: nil,
                                                                 deliverImmediately: true)
    first.activate(options: [])
    exit(0)
}

let app = NSApplication.shared
let delegate = AppDelegate()
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
