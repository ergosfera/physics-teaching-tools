// Notepad 95 for macOS: a plain-text editor that looks and works like the Windows 95 Notepad.
// AppKit app; build.sh makes "Notepad 95.app" and a .dmg.
import AppKit
// The Windows 95 look comes from ../../win95-kit/Win95Kit.swift.

// MARK: - Text files

enum TextCodec {
    struct Decoded { var text: String; var encoding: String.Encoding; var bom: Bool; var lineEnding: String }

    /// Reads UTF-8 (with or without BOM) and UTF-16 with BOM; anything else as Windows-1252, like Notepad.
    /// Line endings become "\n" while editing and are put back on save.
    static func decode(_ data: Data) -> Decoded {
        var enc: String.Encoding = .utf8, bom = false, body = data
        if data.starts(with: [0xEF, 0xBB, 0xBF]) { bom = true; body = data.dropFirst(3) }
        else if data.starts(with: [0xFF, 0xFE]) { enc = .utf16LittleEndian; bom = true; body = data.dropFirst(2) }
        else if data.starts(with: [0xFE, 0xFF]) { enc = .utf16BigEndian; bom = true; body = data.dropFirst(2) }
        body = Data(body)
        var s = String(data: body, encoding: enc)
        if s == nil { enc = .windowsCP1252; bom = false; body = data; s = String(data: body, encoding: enc) }
        if s == nil { enc = .isoLatin1; s = String(data: body, encoding: enc) }
        let raw = (s ?? "") as NSString
        let le = raw.range(of: "\r\n").location != NSNotFound ? "\r\n" : raw.range(of: "\r").location != NSNotFound ? "\r" : "\n"
        let text = raw.replacingOccurrences(of: "\r\n", with: "\n", options: .literal, range: NSRange(location: 0, length: raw.length))
        return Decoded(text: (text as NSString).replacingOccurrences(of: "\r", with: "\n"), encoding: enc, bom: bom, lineEnding: le)
    }

    static func withLineEndings(_ s: String, _ le: String) -> String {
        le == "\n" ? s : (s as NSString).replacingOccurrences(of: "\n", with: le)
    }

    /// nil when the text has characters the encoding can't hold
    static func encode(_ s: String, _ enc: String.Encoding, bom: Bool) -> Data? {
        guard let body = s.data(using: enc, allowLossyConversion: false) else { return nil }
        var out = Data()
        if bom {
            switch enc {
            case .utf8: out.append(contentsOf: [0xEF, 0xBB, 0xBF])
            case .utf16LittleEndian: out.append(contentsOf: [0xFF, 0xFE])
            case .utf16BigEndian: out.append(contentsOf: [0xFE, 0xFF])
            default: break
            }
        }
        out.append(body)
        return out
    }

    static func name(_ enc: String.Encoding) -> String {
        switch enc {
        case .utf8: return "UTF-8"
        case .utf16LittleEndian, .utf16BigEndian: return "Unicode"
        default: return "ANSI (Western)"
        }
    }

    static func selfTest() -> Bool {
        var ok = true
        func check(_ c: Bool, _ what: String) { if !c { print("FAIL: \(what)"); ok = false } }
        let crlf = decode(Data("a\r\nb\r\n".utf8))
        check(crlf.text == "a\nb\n" && crlf.lineEnding == "\r\n", "CRLF")
        let cr = decode(Data("a\rb".utf8))
        check(cr.text == "a\nb" && cr.lineEnding == "\r", "CR")
        let bom = decode(Data([0xEF, 0xBB, 0xBF] + Array("hé".utf8)))
        check(bom.text == "hé" && bom.bom && bom.encoding == .utf8, "UTF-8 BOM")
        let u16 = decode(Data([0xFF, 0xFE, 0x68, 0x00, 0x69, 0x00]))
        check(u16.text == "hi" && u16.encoding == .utf16LittleEndian, "UTF-16")
        let ansi = decode(Data([0x63, 0x61, 0x66, 0xE9]))
        check(ansi.text == "café" && ansi.encoding == .windowsCP1252, "ANSI")
        let samples = [Data("x\r\ny\r\n".utf8), Data([0xEF, 0xBB, 0xBF, 0x41]), Data([0xFF, 0xFE, 0x68, 0x00, 0x0A, 0x00]),
                       Data([0x63, 0x61, 0x66, 0xE9]), Data("plain\n✓ ünïcode\n".utf8), Data()]
        for sample in samples {
            let d = decode(sample)
            check(encode(withLineEndings(d.text, d.lineEnding), d.encoding, bom: d.bom) == sample, "round trip \(Array(sample))")
        }
        check(encode("emoji 🙂", .windowsCP1252, bom: false) == nil, "unencodable text is refused")
        print(ok ? "Self-test passed" : "Self-test FAILED")
        return ok
    }
}

// MARK: - Find and Replace

enum FindState {
    static let d = UserDefaults.standard
    static var text: String { get { d.string(forKey: "findText") ?? "" } set { d.set(newValue, forKey: "findText") } }
    static var replacement: String { get { d.string(forKey: "replaceText") ?? "" } set { d.set(newValue, forKey: "replaceText") } }
    static var matchCase: Bool { get { d.bool(forKey: "matchCase") } set { d.set(newValue, forKey: "matchCase") } }
    static var up: Bool { get { d.bool(forKey: "findUp") } set { d.set(newValue, forKey: "findUp") } }
}

final class FindDialog: NSObject, NSTextFieldDelegate {
    static let shared = FindDialog()
    private(set) var window: Win95Window?
    private var replaceMode = false
    private var findField: W95Field?, replaceField: W95Field?
    private var needsText: [W95Button] = []
    var visibleWindow: NSWindow? { window?.isVisible == true ? window : nil }

    func show(replace: Bool, seed: String?, near doc: NSWindow?) {
        if window != nil && replaceMode != replace { window?.orderOut(nil); window = nil }
        replaceMode = replace
        if let s = seed, !s.isEmpty, !s.contains("\n") { FindState.text = s }
        if window == nil { build(near: doc) }
        findField?.field.stringValue = FindState.text
        updateButtons()
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(findField?.field)
        findField?.field.selectText(nil)
    }

    func close() {
        window?.orderOut(nil)
        window = nil
        AppDelegate.shared.activeDoc?.window.makeKeyAndOrderFront(nil)
    }

    private func build(near doc: NSWindow?) {
        let client = replaceMode ? NSSize(width: 350, height: 124) : NSSize(width: 350, height: 88)
        let size = ChromeView.windowSize(forClient: client)
        let win = Win95Window(contentRect: NSRect(origin: .zero, size: size), minimizable: false)
        win.acceptsMain = false
        win.level = .floating
        win.hidesOnDeactivate = true
        win.isExcludedFromWindowsMenu = true
        let chrome = ChromeView(frame: NSRect(origin: .zero, size: size), title: replaceMode ? "Replace" : "Find",
                                kinds: [.close], resizable: false, showIcon: false)
        win.contentView = chrome
        chrome.titleBar.onButton = { [weak self] _ in self?.close() }
        let c = chrome.client
        let fx: CGFloat = replaceMode ? 88 : 72

        c.addSubview(W95Label("Fi&nd what:", frame: NSRect(x: 8, y: 13, width: fx - 10, height: 14)))
        let ff = W95Field(frame: NSRect(x: fx, y: 9, width: 252 - fx, height: 21))
        ff.field.delegate = self
        c.addSubview(ff)
        findField = ff

        let findNext = W95Button("&Find Next", frame: NSRect(x: 262, y: 8, width: 80, height: 23)) { [weak self] in self?.run(.next) }
        findNext.isDefault = true
        c.addSubview(findNext)
        needsText = [findNext]
        var cancelY: CGFloat = 36
        if replaceMode {
            c.addSubview(W95Label("Re&place with:", frame: NSRect(x: 8, y: 41, width: fx - 10, height: 14)))
            let rf = W95Field(frame: NSRect(x: fx, y: 37, width: 252 - fx, height: 21))
            rf.field.stringValue = FindState.replacement
            rf.field.delegate = self
            c.addSubview(rf)
            replaceField = rf
            let rep = W95Button("&Replace", frame: NSRect(x: 262, y: 36, width: 80, height: 23)) { [weak self] in self?.run(.replace) }
            let all = W95Button("Replace &All", frame: NSRect(x: 262, y: 64, width: 80, height: 23)) { [weak self] in self?.run(.all) }
            c.addSubview(rep); c.addSubview(all)
            needsText += [rep, all]
            cancelY = 92
        } else {
            replaceField = nil
        }
        c.addSubview(W95Button("Cancel", frame: NSRect(x: 262, y: cancelY, width: 80, height: 23)) { [weak self] in self?.close() })

        let mc = W95Check("Match &case", frame: NSRect(x: 8, y: replaceMode ? 100 : 62, width: 100, height: 16), isOn: FindState.matchCase)
        mc.onChange = { FindState.matchCase = $0 }
        c.addSubview(mc)

        if !replaceMode {
            c.addSubview(W95Group("Direction", frame: NSRect(x: 150, y: 40, width: 102, height: 40)))
            let up = W95Radio("&Up", frame: NSRect(x: 160, y: 57, width: 40, height: 16), isOn: FindState.up)
            let down = W95Radio("&Down", frame: NSRect(x: 202, y: 57, width: 46, height: 16), isOn: !FindState.up)
            up.group = [up, down]; down.group = [up, down]
            up.onSelect = { FindState.up = true }
            down.onSelect = { FindState.up = false }
            c.addSubview(up); c.addSubview(down)
        }

        win.onEnter = { [weak self] in
            if let b = win.firstResponder as? W95Button { b.action() } else { self?.run(.next) }
        }
        win.onEscape = { [weak self] in self?.close() }
        if let d = doc {
            win.setFrameOrigin(NSPoint(x: min(d.frame.maxX - size.width - 30, (d.screen?.visibleFrame.maxX ?? .greatestFiniteMagnitude) - size.width),
                                       y: d.frame.maxY - size.height - 70))
        } else {
            win.center()
        }
        window = win
    }

    private enum Op { case next, replace, all }

    private func run(_ op: Op) {
        syncFields()
        guard !FindState.text.isEmpty, let doc = AppDelegate.shared.activeDoc else { return }
        switch op {
        case .next: doc.find(up: replaceMode ? false : FindState.up)
        case .replace: doc.replaceOnce()
        case .all: doc.replaceAll()
        }
    }

    private func syncFields() {
        if let f = findField { FindState.text = f.field.stringValue }
        if let r = replaceField { FindState.replacement = r.field.stringValue }
    }

    private func updateButtons() {
        let has = !(findField?.field.stringValue.isEmpty ?? true)
        needsText.forEach { $0.isEnabled = has }
    }

    func controlTextDidChange(_ obj: Notification) { syncFields(); updateButtons() }

    func control(_ control: NSControl, textView: NSTextView, doCommandBy sel: Selector) -> Bool {
        if sel == #selector(NSResponder.insertNewline(_:)) { window?.onEnter?(); return true }
        if sel == #selector(NSResponder.cancelOperation(_:)) { close(); return true }
        return false
    }
}

// MARK: - Notepad window

final class PadTextView: NSTextView {
    weak var doc: Doc?

    override func keyDown(with e: NSEvent) {
        switch e.keyCode {
        case 99: doc?.find(up: e.modifierFlags.contains(.shift))      // F3
        case 96: doc?.insertTimeDate()                                // F5
        case 109: doc?.tracker.openBar(0, keyboard: true)            // F10 opens the menus, like Windows
        default: super.keyDown(with: e)
        }
    }

    override func menu(for event: NSEvent) -> NSMenu? { nil }
    override func rightMouseDown(with event: NSEvent) { doc?.showContextMenu() }
    override func mouseDown(with event: NSEvent) {
        if event.modifierFlags.contains(.control) { doc?.showContextMenu() } else { super.mouseDown(with: event) }
    }

    override func changeFont(_ sender: Any?) {
        guard let fm = sender as? NSFontManager else { return }
        AppDelegate.shared.setFont(fm.convert(Doc.font))
    }

    // Dropping a file opens it, as in Notepad
    override func performDragOperation(_ info: NSDraggingInfo) -> Bool {
        if let urls = info.draggingPasteboard.readObjects(forClasses: [NSURL.self], options: [.urlReadingFileURLsOnly: true]) as? [URL],
           let u = urls.first {
            DispatchQueue.main.async { [weak self] in
                guard let doc = self?.doc, doc.confirmDiscard() else { return }
                doc.load(u)
            }
            return true
        }
        return super.performDragOperation(info)
    }
}

/// Keeps the selection navy while Find has the focus (macOS would draw it pale gray)
final class PadLayoutManager: NSLayoutManager {
    override func fillBackgroundRectArray(_ rects: UnsafePointer<NSRect>, count: Int, forCharacterRange range: NSRange, color: NSColor) {
        if let tv = firstTextView, color != tv.backgroundColor,
           tv.selectedRanges.contains(where: { NSIntersectionRange($0.rangeValue, range).length > 0 }) {
            W95.navy.setFill()
        }
        super.fillBackgroundRectArray(rects, count: count, forCharacterRange: range, color: color)
    }
}

final class EditFrameView: FlippedView {
    override func draw(_ dirtyRect: NSRect) {
        W95.crisp()
        W95.fill(bounds, W95.face)
        W95.bevel(bounds, .sunken)
    }
}

final class Doc: NSObject, NSWindowDelegate, NSTextViewDelegate {
    static var font: NSFont = {
        let d = UserDefaults.standard
        if let name = d.string(forKey: "fontName"), d.double(forKey: "fontSize") > 0,
           let f = NSFont(name: name, size: CGFloat(d.double(forKey: "fontSize"))) { return f }
        return NSFont(name: "Monaco", size: 12) ?? .userFixedPitchFont(ofSize: 12) ?? .systemFont(ofSize: 12)
    }()
    static var wordWrap = UserDefaults.standard.bool(forKey: "wordWrap")

    let window: Win95Window
    let chrome: ChromeView
    let menuBar = MenuBarView()
    let tracker = MenuTracker()
    let scroll = NSScrollView()
    let storage = NSTextStorage()
    let text: PadTextView
    var url: URL?
    var encoding: String.Encoding = .utf8
    var bom = false
    var lineEnding = "\n"
    var dirty = false { didSet { if dirty != oldValue { updateTitle() } } }
    private var restoreFrame: NSRect?
    private lazy var printInfo: NSPrintInfo = {
        let p = NSPrintInfo.shared.copy() as! NSPrintInfo
        p.horizontalPagination = .fit
        p.isHorizontallyCentered = false
        p.isVerticallyCentered = false
        p.dictionary()[NSPrintInfo.AttributeKey.headerAndFooter] = true
        return p
    }()

    var displayName: String { url?.lastPathComponent ?? "Untitled" }

    init(frame: NSRect) {
        window = Win95Window(contentRect: frame, minimizable: true)
        chrome = ChromeView(frame: NSRect(origin: .zero, size: frame.size), title: "Untitled - Notepad",
                            kinds: [.minimize, .maximize, .close], resizable: true, showIcon: true)
        let lm = PadLayoutManager()
        storage.addLayoutManager(lm)
        let tc = NSTextContainer(containerSize: NSSize(width: 100, height: CGFloat.greatestFiniteMagnitude))
        lm.addTextContainer(tc)
        text = PadTextView(frame: NSRect(x: 0, y: 0, width: 100, height: 100), textContainer: tc)
        super.init()

        window.contentView = chrome
        window.delegate = self
        let c = chrome.client
        menuBar.frame = NSRect(x: 0, y: 0, width: c.bounds.width, height: 19)
        menuBar.autoresizingMask = [.width]
        let edit = EditFrameView(frame: NSRect(x: 0, y: 19, width: c.bounds.width, height: c.bounds.height - 19))
        edit.autoresizingMask = [.width, .height]
        c.addSubview(menuBar)
        c.addSubview(edit)

        scroll.frame = edit.bounds.insetBy(dx: 2, dy: 2)
        scroll.autoresizingMask = [.width, .height]
        scroll.verticalScroller = Win95Scroller()
        scroll.horizontalScroller = Win95Scroller()
        scroll.scrollerStyle = .legacy
        scroll.hasVerticalScroller = true
        scroll.autohidesScrollers = false
        scroll.borderType = .noBorder
        scroll.drawsBackground = false
        scroll.contentView.drawsBackground = true
        scroll.contentView.backgroundColor = W95.white
        edit.addSubview(scroll)

        text.doc = self
        text.delegate = self
        text.minSize = NSSize(width: 0, height: 0)
        text.maxSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
        text.isVerticallyResizable = true
        text.autoresizingMask = [.width]
        text.isRichText = false
        text.importsGraphics = false
        text.allowsUndo = true
        text.usesFindBar = false
        text.usesFontPanel = true
        text.isAutomaticQuoteSubstitutionEnabled = false
        text.isAutomaticDashSubstitutionEnabled = false
        text.isAutomaticTextReplacementEnabled = false
        text.isAutomaticSpellingCorrectionEnabled = false
        text.isAutomaticLinkDetectionEnabled = false
        text.isAutomaticDataDetectionEnabled = false
        text.isContinuousSpellCheckingEnabled = false
        text.isGrammarCheckingEnabled = false
        text.smartInsertDeleteEnabled = false
        text.drawsBackground = true
        text.backgroundColor = W95.white
        text.textColor = W95.black
        text.insertionPointColor = W95.black
        text.selectedTextAttributes = [.backgroundColor: W95.navy, .foregroundColor: W95.white]
        text.textContainerInset = NSSize(width: 0, height: 1)
        tc.lineFragmentPadding = 2
        lm.allowsNonContiguousLayout = true
        scroll.documentView = text
        applyFont()
        applyWrap()

        tracker.window = window
        tracker.bar = menuBar
        menuBar.tracker = tracker
        menuBar.menus = [
            .init(title: "&File") { [weak self] in self?.fileItems() ?? [] },
            .init(title: "&Edit") { [weak self] in self?.editItems(full: true) ?? [] },
            .init(title: "&Search") { [weak self] in self?.searchItems() ?? [] },
            .init(title: "&Help") { [MenuItem95("&About Notepad 95") { AppDelegate.shared.about() }] },
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
        updateTitle()
        window.makeFirstResponder(text)
    }

    // MARK: Menus

    private func fileItems() -> [MenuItem95] {
        [MenuItem95("&New", "Cmd+N") { [weak self] in self?.newDocument() },
         MenuItem95("New &Window", "Shift+Cmd+N") { AppDelegate.shared.newWindow() },
         MenuItem95("&Open...", "Cmd+O") { [weak self] in self?.openDocument() },
         MenuItem95("&Save", "Cmd+S") { [weak self] in self?.save() },
         MenuItem95("Save &As...", "Shift+Cmd+S") { [weak self] in self?.saveAs() },
         .separator,
         MenuItem95("Page Se&tup...") { [weak self] in self?.pageSetup() },
         MenuItem95("&Print...", "Cmd+P") { [weak self] in self?.printText() },
         .separator,
         MenuItem95("E&xit", "Cmd+W") { [weak self] in self?.close() }]
    }

    private func editItems(full: Bool) -> [MenuItem95] {
        let t = text
        let hasSel = { t.selectedRange().length > 0 }
        var items = [
            MenuItem95("&Undo", "Cmd+Z", enabled: { t.undoManager?.canUndo == true }) { [weak self] in
                self?.window.makeFirstResponder(t); t.undoManager?.undo() },
            .separator,
            MenuItem95("Cu&t", "Cmd+X", enabled: hasSel) { t.cut(nil) },
            MenuItem95("&Copy", "Cmd+C", enabled: hasSel) { t.copy(nil) },
            MenuItem95("&Paste", "Cmd+V", enabled: { NSPasteboard.general.string(forType: .string) != nil }) { t.paste(nil) },
            MenuItem95("De&lete", "Del", enabled: hasSel) { t.delete(nil) },
            .separator,
            MenuItem95("Select &All", "Cmd+A") { t.selectAll(nil) },
        ]
        if full {
            items += [
                MenuItem95("Time/&Date", "F5") { [weak self] in self?.insertTimeDate() },
                .separator,
                MenuItem95("&Word Wrap", checked: { Doc.wordWrap }) { AppDelegate.shared.setWordWrap(!Doc.wordWrap) },
                MenuItem95("Set &Font...", "Cmd+T") { [weak self] in self?.showFonts() },
            ]
        }
        return items
    }

    private func searchItems() -> [MenuItem95] {
        [MenuItem95("&Find...", "Cmd+F") { AppDelegate.shared.showFind(replace: false) },
         MenuItem95("Find &Next", "F3", enabled: { !FindState.text.isEmpty }) { [weak self] in self?.find(up: FindState.up) },
         MenuItem95("&Replace...", "Opt+Cmd+F") { AppDelegate.shared.showFind(replace: true) }]
    }

    func showContextMenu() {
        window.makeFirstResponder(text)
        tracker.openContext(editItems(full: false), at: NSEvent.mouseLocation)
    }

    // MARK: Text settings

    func applyFont() {
        // The text sits inside the magnified window, so it's drawn smaller to show at the chosen size
        let f = NSFont(descriptor: Doc.font.fontDescriptor, size: Doc.font.pointSize / W95.scale) ?? Doc.font
        text.font = f
        text.typingAttributes = [.font: f, .foregroundColor: W95.black]
        let line = ceil(text.layoutManager?.defaultLineHeight(for: f) ?? 16)
        scroll.verticalLineScroll = line
        scroll.horizontalLineScroll = line
    }

    func applyWrap() {
        guard let tc = text.textContainer else { return }
        scroll.hasHorizontalScroller = !Doc.wordWrap
        scroll.tile()
        let w = scroll.contentSize.width
        if Doc.wordWrap {
            text.isHorizontallyResizable = false
            text.autoresizingMask = [.width]
            text.setFrameSize(NSSize(width: w, height: text.frame.height))
            tc.containerSize = NSSize(width: w, height: CGFloat.greatestFiniteMagnitude)
            tc.widthTracksTextView = true
        } else {
            tc.widthTracksTextView = false
            tc.containerSize = NSSize(width: CGFloat.greatestFiniteMagnitude, height: CGFloat.greatestFiniteMagnitude)
            text.isHorizontallyResizable = true
            text.autoresizingMask = []
            text.setFrameSize(NSSize(width: max(w, text.frame.width), height: text.frame.height))
        }
        text.minSize = NSSize(width: w, height: scroll.contentSize.height)
        text.sizeToFit()
        text.scrollRangeToVisible(text.selectedRange())
    }

    func updateTitle() {
        let t = "\(displayName) - Notepad"
        chrome.titleBar.title = t
        window.title = t
        window.isDocumentEdited = dirty
    }

    func textDidChange(_ notification: Notification) { dirty = true }

    // MARK: Files

    /// Asks to save unsaved changes. false means the user cancelled.
    func confirmDiscard() -> Bool {
        guard dirty else { return true }
        tracker.close()
        window.makeKeyAndOrderFront(nil)
        let r = messageBox("The text in the \(url?.path ?? "Untitled") file has changed.\n\nDo you want to save the changes?",
                           icon: .warning, buttons: ["&Yes", "&No", "Cancel"], defaultIndex: 0, cancelIndex: 2, over: window)
        switch r {
        case 0: return save()
        case 1: return true
        default: return false
        }
    }

    func newDocument() {
        guard confirmDiscard() else { return }
        text.string = ""
        url = nil; encoding = .utf8; bom = false; lineEnding = "\n"
        text.undoManager?.removeAllActions()
        dirty = false
        updateTitle()
    }

    func openDocument() {
        guard confirmDiscard() else { return }
        let p = NSOpenPanel()
        p.canChooseFiles = true
        p.canChooseDirectories = false
        p.allowsMultipleSelection = false
        if let u = url { p.directoryURL = u.deletingLastPathComponent() }
        if p.runModal() == .OK, let u = p.url { load(u) }
        window.makeKeyAndOrderFront(nil)
    }

    func load(_ u: URL) {
        do {
            let d = TextCodec.decode(try Data(contentsOf: u))
            text.string = d.text
            url = u; encoding = d.encoding; bom = d.bom; lineEnding = d.lineEnding
            text.undoManager?.removeAllActions()
            dirty = false
            updateTitle()
            text.setSelectedRange(NSRange(location: 0, length: 0))
            text.scrollRangeToVisible(NSRange(location: 0, length: 0))
            NSDocumentController.shared.noteNewRecentDocumentURL(u)
            // Windows 95 trick: a file that starts with .LOG gets the time and date added each time it's opened
            if d.text.hasPrefix(".LOG") {
                let end = (text.string as NSString).length
                text.setSelectedRange(NSRange(location: end, length: 0))
                text.insertText("\n" + Doc.timeDate() + "\n", replacementRange: NSRange(location: end, length: 0))
                text.scrollRangeToVisible(text.selectedRange())
            }
        } catch {
            messageBox("Cannot open the \(u.path) file.\n\n\(error.localizedDescription)", icon: .error, over: window)
        }
    }

    @discardableResult func save() -> Bool {
        guard let u = url else { return saveAs() }
        return write(to: u)
    }

    @discardableResult func saveAs() -> Bool {
        let p = NSSavePanel()
        p.nameFieldStringValue = url?.lastPathComponent ?? "Untitled.txt"
        if let u = url { p.directoryURL = u.deletingLastPathComponent() }
        p.canCreateDirectories = true
        p.isExtensionHidden = false
        let ok = p.runModal() == .OK
        window.makeKeyAndOrderFront(nil)
        guard ok, let u = p.url else { return false }
        return write(to: u)
    }

    private func write(to u: URL) -> Bool {
        let s = TextCodec.withLineEndings(text.string, lineEnding)
        var data = TextCodec.encode(s, encoding, bom: bom)
        if data == nil {
            let r = messageBox("This file contains characters that can't be saved as \(TextCodec.name(encoding)) text. "
                               + "They would be lost.\n\nSave it as UTF-8 (Unicode) instead?",
                               icon: .warning, buttons: ["OK", "Cancel"], cancelIndex: 1, over: window)
            guard r == 0 else { return false }
            encoding = .utf8; bom = false
            data = TextCodec.encode(s, .utf8, bom: false)
        }
        guard let data else { return false }
        do {
            // Keep the file's permissions; the atomic write replaces the file
            let perms = (try? FileManager.default.attributesOfItem(atPath: u.path))?[.posixPermissions]
            try data.write(to: u, options: .atomic)
            if let perms { try? FileManager.default.setAttributes([.posixPermissions: perms], ofItemAtPath: u.path) }
        } catch {
            messageBox("Cannot save the \(u.path) file.\n\n\(error.localizedDescription)", icon: .error, over: window)
            return false
        }
        url = u
        dirty = false
        updateTitle()
        NSDocumentController.shared.noteNewRecentDocumentURL(u)
        return true
    }

    func pageSetup() { NSPageLayout().runModal(with: printInfo) }

    func printText() {
        let w = printInfo.paperSize.width - printInfo.leftMargin - printInfo.rightMargin
        let st = NSTextStorage(string: text.string, attributes: [.font: Doc.font, .foregroundColor: W95.black])
        let lm = NSLayoutManager()
        st.addLayoutManager(lm)
        let tc = NSTextContainer(containerSize: NSSize(width: w, height: CGFloat.greatestFiniteMagnitude))
        lm.addTextContainer(tc)
        let tv = NSTextView(frame: NSRect(x: 0, y: 0, width: w, height: 100), textContainer: tc)
        tv.isVerticallyResizable = true
        tv.textContainerInset = .zero
        lm.ensureLayout(for: tc)
        tv.setFrameSize(NSSize(width: w, height: ceil(lm.usedRect(for: tc).height) + 2))
        let op = NSPrintOperation(view: tv, printInfo: printInfo)
        op.jobTitle = displayName
        withExtendedLifetime(st) { _ = op.run() }
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

    // MARK: Edit and Search

    static func timeDate() -> String {
        let t = DateFormatter(), d = DateFormatter()
        t.setLocalizedDateFormatFromTemplate("jmm")
        d.setLocalizedDateFormatFromTemplate("yMd")
        let now = Date()
        return "\(t.string(from: now)) \(d.string(from: now))"
    }

    func insertTimeDate() {
        window.makeFirstResponder(text)
        text.insertText(Doc.timeDate(), replacementRange: text.selectedRange())
    }

    func showFonts() {
        window.makeFirstResponder(text)
        NSFontManager.shared.setSelectedFont(Doc.font, isMultiple: false)
        NSFontManager.shared.orderFrontFontPanel(nil)
    }

    private var findOptions: NSString.CompareOptions { FindState.matchCase ? [] : [.caseInsensitive] }

    @discardableResult func find(up: Bool) -> Bool {
        let needle = FindState.text
        guard !needle.isEmpty else { AppDelegate.shared.showFind(replace: false); return false }
        let ns = text.string as NSString, sel = text.selectedRange()
        var opts = findOptions
        let range: NSRange
        if up {
            opts.insert(.backwards)
            range = NSRange(location: 0, length: sel.location)
        } else {
            let s = sel.location + sel.length
            range = NSRange(location: s, length: ns.length - s)
        }
        let r = ns.range(of: needle, options: opts, range: range)
        guard r.location != NSNotFound else {
            messageBox("Cannot find \"\(needle)\"", icon: .info, over: FindDialog.shared.visibleWindow ?? window)
            return false
        }
        text.setSelectedRange(r)
        text.scrollRangeToVisible(r)
        return true
    }

    func replaceOnce() {
        let sel = text.selectedRange(), ns = text.string as NSString
        if sel.length > 0, ns.substring(with: sel).compare(FindState.text, options: findOptions) == .orderedSame {
            text.insertText(FindState.replacement, replacementRange: sel)
        }
        find(up: false)
    }

    func replaceAll() {
        let ns = text.string as NSString, all = NSRange(location: 0, length: ns.length)
        let new = ns.replacingOccurrences(of: FindState.text, with: FindState.replacement, options: findOptions, range: all)
        guard new != text.string else {
            messageBox("Cannot find \"\(FindState.text)\"", icon: .info, over: FindDialog.shared.visibleWindow ?? window)
            return
        }
        if text.shouldChangeText(in: all, replacementString: new) {
            text.replaceCharacters(in: all, with: new)
            text.didChangeText()
            text.undoManager?.setActionName("Replace All")
        }
    }
}

// MARK: - App

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuItemValidation {
    static var shared: AppDelegate!
    var docs: [Doc] = []
    weak var activeDoc: Doc?

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
        // Reuse the empty window that opens at launch
        if docs.count == 1, let d = docs.first, d.url == nil, !d.dirty, d.text.string.isEmpty {
            d.load(u)
            d.window.makeKeyAndOrderFront(nil)
            return
        }
        newWindow().load(u)
    }

    @discardableResult func newWindow() -> Doc {
        var frame = NSRect.zero
        if let a = activeDoc ?? docs.last {
            frame = a.window.frame.offsetBy(dx: 22, dy: -22)
        } else if let s = UserDefaults.standard.string(forKey: "windowFrame") {
            frame = NSRectFromString(s)
        }
        // A new window must have its title bar on a screen; otherwise it's centered
        let titleBar = NSRect(x: frame.minX + 40, y: frame.maxY - 22, width: max(0, frame.width - 80), height: 18)
        if frame.width < 360 || frame.height < 210 || !NSScreen.screens.contains(where: { $0.visibleFrame.contains(titleBar) }) {
            let s = NSScreen.main?.visibleFrame ?? NSRect(x: 0, y: 0, width: 1280, height: 800)
            frame = NSRect(x: s.midX - 380, y: s.midY - 260, width: 760, height: 520)
        }
        let d = Doc(frame: frame)
        docs.append(d)
        d.window.makeKeyAndOrderFront(nil)
        activeDoc = d
        return d
    }

    func docClosed(_ d: Doc) {
        saveFrame(d)
        docs.removeAll { $0 === d }
        if activeDoc === d { activeDoc = docs.last }
        if docs.isEmpty, let f = FindDialog.shared.window { f.close() }
    }

    func saveFrame(_ d: Doc) {
        guard !d.isMaximized else { return }
        UserDefaults.standard.set(NSStringFromRect(d.window.frame), forKey: "windowFrame")
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }

    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        for d in docs where d.dirty {
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

    func setFont(_ f: NSFont) {
        Doc.font = f
        UserDefaults.standard.set(f.fontName, forKey: "fontName")
        UserDefaults.standard.set(Double(f.pointSize), forKey: "fontSize")
        docs.forEach { $0.applyFont() }
    }

    func setWordWrap(_ on: Bool) {
        Doc.wordWrap = on
        UserDefaults.standard.set(on, forKey: "wordWrap")
        docs.forEach { $0.applyWrap() }
    }

    func showFind(replace: Bool) {
        let doc = activeDoc
        let seed = doc.map { ($0.text.string as NSString).substring(with: $0.text.selectedRange()) }
        FindDialog.shared.show(replace: replace, seed: seed, near: doc?.window)
    }

    func about() {
        let v = Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        messageBox("Notepad 95\nVersion \(v)\n\nA plain-text editor in the style of the Windows 95 Notepad.\n\n"
                   + "© 2026 Mauricio Mendivelso. MIT License.\nProvided as is, without warranty.\n"
                   + "Not affiliated with or endorsed by Microsoft.",
                   title: "About Notepad 95", icon: .app, over: activeDoc?.window)
    }

    // MARK: Mac menu bar (same commands as the window's own menus)

    @objc func newDocument(_ sender: Any?) { activeDoc?.newDocument() }
    @objc func newWindowAction(_ sender: Any?) { newWindow() }
    @objc func openDocument(_ sender: Any?) { if let d = activeDoc { d.openDocument() } else { newWindow().openDocument() } }
    @objc func saveDocument(_ sender: Any?) { activeDoc?.save() }
    @objc func saveDocumentAs(_ sender: Any?) { activeDoc?.saveAs() }
    @objc func pageSetup(_ sender: Any?) { activeDoc?.pageSetup() }
    @objc func printText(_ sender: Any?) { activeDoc?.printText() }
    @objc func closeWindow(_ sender: Any?) {
        if let f = FindDialog.shared.window, NSApp.keyWindow === f { FindDialog.shared.close() } else { activeDoc?.close() }
    }
    @objc func insertTimeDate(_ sender: Any?) { activeDoc?.insertTimeDate() }
    @objc func toggleWordWrap(_ sender: Any?) { setWordWrap(!Doc.wordWrap) }
    @objc func setFontAction(_ sender: Any?) { activeDoc?.showFonts() }
    @objc func findAction(_ sender: Any?) { showFind(replace: false) }
    @objc func replaceAction(_ sender: Any?) { showFind(replace: true) }
    @objc func findNextAction(_ sender: Any?) { activeDoc?.find(up: false) }
    @objc func findPreviousAction(_ sender: Any?) { activeDoc?.find(up: true) }
    @objc func minimizeAction(_ sender: Any?) { activeDoc?.window.miniaturize(nil) }
    @objc func zoomAction(_ sender: Any?) { activeDoc?.toggleMaximize() }
    @objc func aboutAction(_ sender: Any?) { about() }

    func validateMenuItem(_ item: NSMenuItem) -> Bool {
        switch item.action {
        case #selector(toggleWordWrap(_:)):
            item.state = Doc.wordWrap ? .on : .off
            return activeDoc != nil
        case #selector(findNextAction(_:)), #selector(findPreviousAction(_:)):
            return activeDoc != nil && !FindState.text.isEmpty
        case #selector(newWindowAction(_:)), #selector(openDocument(_:)), #selector(aboutAction(_:)):
            return true
        default:
            return activeDoc != nil
        }
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
        func std(_ title: String, _ action: String, _ key: String, _ mods: NSEvent.ModifierFlags = [.command]) -> NSMenuItem {
            let i = NSMenuItem(title: title, action: Selector(action), keyEquivalent: key)
            i.keyEquivalentModifierMask = mods
            return i   // no target: goes to the focused text view
        }
        let sep = { NSMenuItem.separator() }
        let f5 = String(Character(UnicodeScalar(NSF5FunctionKey)!))

        _ = menu("Notepad 95", [
            item("About Notepad 95", #selector(aboutAction(_:))), sep(),
            item("Hide Notepad 95", #selector(NSApplication.hide(_:)), "h", target: NSApp),
            item("Hide Others", #selector(NSApplication.hideOtherApplications(_:)), "h", [.command, .option], target: NSApp),
            item("Show All", #selector(NSApplication.unhideAllApplications(_:)), target: NSApp), sep(),
            item("Quit Notepad 95", #selector(NSApplication.terminate(_:)), "q", target: NSApp),
        ])
        _ = menu("File", [
            item("New", #selector(newDocument(_:)), "n"),
            item("New Window", #selector(newWindowAction(_:)), "n", [.command, .shift]),
            item("Open…", #selector(openDocument(_:)), "o"),
            item("Save", #selector(saveDocument(_:)), "s"),
            item("Save As…", #selector(saveDocumentAs(_:)), "s", [.command, .shift]), sep(),
            item("Page Setup…", #selector(pageSetup(_:)), "p", [.command, .shift]),
            item("Print…", #selector(printText(_:)), "p"), sep(),
            item("Close", #selector(closeWindow(_:)), "w"),
        ])
        _ = menu("Edit", [
            std("Undo", "undo:", "z"), std("Redo", "redo:", "z", [.command, .shift]), sep(),
            std("Cut", "cut:", "x"), std("Copy", "copy:", "c"), std("Paste", "paste:", "v"), std("Delete", "delete:", ""), sep(),
            std("Select All", "selectAll:", "a"),
            item("Time/Date", #selector(insertTimeDate(_:)), f5, []), sep(),
            item("Word Wrap", #selector(toggleWordWrap(_:))),
            item("Set Font…", #selector(setFontAction(_:)), "t"),
        ])
        _ = menu("Search", [
            item("Find…", #selector(findAction(_:)), "f"),
            item("Find Next", #selector(findNextAction(_:)), "g"),
            item("Find Previous", #selector(findPreviousAction(_:)), "g", [.command, .shift]),
            item("Replace…", #selector(replaceAction(_:)), "f", [.command, .option]),
        ])
        let win = menu("Window", [
            item("Minimize", #selector(minimizeAction(_:)), "m"),
            item("Zoom", #selector(zoomAction(_:))), sep(),
            item("Bring All to Front", #selector(NSApplication.arrangeInFront(_:)), target: NSApp),
        ])
        NSApp.windowsMenu = win
        _ = menu("Help", [item("About Notepad 95", #selector(aboutAction(_:)))])
        NSApp.mainMenu = main
    }
}

// MARK: - App icon (used by build.sh: Notepad95 --write-iconset <dir>)

enum IconArt {
    // 16 × 16 notepad with a spiral binding, in the Windows 95 palette
    static let pad = [
        "................",
        "...k.k.k.k.k....",
        "..kdkdkdkdkdk...",
        "..kbbbbbbbbbbk..",
        "..kbbbbbbbbbbk..",
        "..kwwwwwwwwwwkd.",
        "..kwkkkkkwwwwkd.",
        "..kwwwwwwwwwwkd.",
        "..kwkkkkkkkwwkd.",
        "..kwwwwwwwwwwkd.",
        "..kwkkkkwwwwwkd.",
        "..kwwwwwwwwwwkd.",
        "..kwkkkkkkwwwkd.",
        "..kwwwwwwwwwwkd.",
        "..kkkkkkkkkkkkd.",
        "...dddddddddddd.",
    ]
}

// MARK: - Main

let args = CommandLine.arguments
if args.count == 2 && args[1] == "--selftest" {
    exit(TextCodec.selfTest() ? 0 : 1)
}
if args.count == 3 && args[1] == "--write-iconset" {
    do { try W95Icon.writeIconset(IconArt.pad, to: args[2]) } catch { print("Icon error: \(error)"); exit(1) }
    exit(0)
}

W95.appIcon = IconArt.pad
W95.appName = "Notepad"
let app = NSApplication.shared
let delegate = AppDelegate()
AppDelegate.shared = delegate
app.delegate = delegate
app.setActivationPolicy(.regular)
app.run()
