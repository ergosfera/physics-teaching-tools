# Ergosfera Desktop Apps

Small, free desktop apps for Windows and macOS, made for teaching:

- **Countdown Timer**: a compact countdown whose bar and numbers turn from green to yellow to red.
- **Physics Calculator**: the calculator from the Ergosfera *Notebook & Physics Calculator* web tool, where numbers carry units.
- **Field**: the Ergosfera *Field* dot timer, a field of dots that go dark as time runs out, with no numbers.
- **Sticky Notes**: sticky notes on your desktop that sync between your Mac and Windows computers through Google Drive.
- **Screen Ruler**: up to three rulers and a protractor that float over anything on the screen.
- **Screen Shapes**: lines, rectangles, circles and guide lines that float over the screen and measure what's under them.
- **Reading Window**: shades the screen except a clear window that follows the pointer, to help focus while reading.

## Download and install

Download the file for your system from the app's release page:
[Countdown Timer](https://github.com/ergosfera/physics-teaching-tools/releases/tag/countdown-timer) ·
[Physics Calculator](https://github.com/ergosfera/physics-teaching-tools/releases/tag/physics-calculator) ·
[Field](https://github.com/ergosfera/physics-teaching-tools/releases/tag/field-timer) ·
[Sticky Notes](https://github.com/ergosfera/physics-teaching-tools/releases/tag/sticky-notes) ·
[Screen Ruler](https://github.com/ergosfera/physics-teaching-tools/releases/tag/screen-ruler) ·
[Screen Shapes](https://github.com/ergosfera/physics-teaching-tools/releases/tag/screen-shapes) ·
[Reading Window](https://github.com/ergosfera/physics-teaching-tools/releases/tag/reading-window).

**Windows (10 or 11):** double-click the `.exe`. No installation is needed.
The apps aren't signed with a paid certificate, so the first time Windows may show
"Windows protected your PC". Click **More info**, then **Run anyway**.

**macOS (11 Big Sur or later; Apple Silicon and Intel):** open the `.dmg` and drag the app
into **Applications**. The apps aren't signed with a paid Apple Developer ID, so the first time
macOS may block them. Open **System Settings → Privacy & Security** and click **Open Anyway**.

## Countdown Timer

- Set hours, minutes and seconds, then start.
- A bar shrinks as time runs out and turns from green to yellow to red, at percentages you choose.
- Optional "always on top" keeps the timer visible over other windows.
- Optional sound when the time is up.

| Action | Mouse | Keyboard |
|---|---|---|
| Set the time | Click hours, minutes or seconds, then type or scroll | Type digits; arrows move and change the fields |
| Start / pause / resume | **Start** button | Space or Enter |
| Reset | **Reset** button | R |
| Always on top | Pin button in the top bar | — |
| Settings (time, color levels, sound) | Gear button or right-click → Settings | ⌘, on Mac |
| About and license | Right-click → About | — |

Move the window by dragging any empty area.

## Physics Calculator

- Numbers carry units: `3 m/s × 2 s` gives `6 m`, and `10 N / 0.2 m` gives `50 N/m`.
- Convert with "to": `72 km/h to m/s`, `25 °C to °F`.
- Built-in constants (`G`, `c`, `hP`, `kB`, `NA`, `Mearth`…) on the **Constants** pad, and common units on the **Units** pad.
- DEG/RAD mode, decimal places or significant figures, and ×10^ notation.
- A history tape: click a line to reuse its result; ↑/↓ recall earlier expressions.
- Optional "always on top" (pin button) and an **About** button (?).

It gives the same results as the web version: both desktop versions are checked against
[`physics-calculator/tests.tsv`](physics-calculator/tests.tsv), 230 results recorded from the web calculator.

## Field

- Drag up or down on the field to set the time, from 1 second to 6 hours. The lit dots show how much.
- Press **Start**. The dots go dark one by one from the bottom, and a box says when the time is up.
- Click the field to pause or resume.
- Full screen for a projector, optional "always on top" (pin button) and optional sound at the end.

| Action | Mouse | Keyboard |
|---|---|---|
| Set the time | Drag up or down on the field, or scroll over it | ↑ / ↓ |
| Start / pause / resume | **Start** button, then click the field | Space or Enter |
| Cancel | × button (top right of the field) | Esc |
| Full screen | Full-screen button in the top bar | F11 on Windows, ⌃⌘F on Mac (Esc leaves) |
| Always on top | Pin button in the top bar | — |
| Sound, About | Right-click the window | — |

## Sticky Notes

Notes you write on one computer appear on the other: write at work on the Mac, and they're
there when you open the Windows PC at home.

**Setup (once on each computer):**

1. Install [Google Drive for desktop](https://www.google.com/drive/download/) and sign in
   with the **same Google account** on both computers.
2. Open Sticky Notes. It finds your Google Drive and keeps the notes in **My Drive/StickyNotes**.
   If you have more than one Google account, it asks which one to use: pick the same one everywhere.
3. On Windows the app lives in the notification area (the ^ next to the clock); on the Mac it's in the Dock.
   Turn on **Start with Windows** (tray menu) or **Open at Login** (Sticky Notes menu, macOS 13+)
   so your notes are there when you start the computer.

| Action | How |
|---|---|
| New note | **+** on a note, Ctrl+N / ⌘N, or click the tray icon (Windows) / Dock icon (Mac) |
| Color, keep on top, delete | **⋯** on the note's top bar (or right-click the bar) |
| Move / resize | Drag the top bar / drag an edge or the bottom-right corner |
| Bring all notes forward | Tray icon → Show all notes (Windows) · click the Dock icon (Mac) |
| Change where notes sync | Tray menu → Change sync folder (Windows) · Sticky Notes menu → Change Sync Folder (Mac) |

How it works:

- Each note is a small text file in My Drive/StickyNotes. You can open it in any text editor,
  or in Google Drive on your phone. A plain `.txt` file you drop into that folder becomes a note.
- Notes save as you type. Each app checks the folder every few seconds, so a change made on the
  other computer shows up as soon as Google Drive has copied it (usually within a minute).
- Where each note sits on screen is remembered separately on each computer.
- If the same note is edited on both computers before Drive syncs, you get both versions as
  separate notes, so nothing is lost. Delete the one you don't need.
- Deleted notes move to **My Drive/StickyNotes/Trash**. To get one back, move its file back into
  StickyNotes; to free space, empty that folder yourself.
- Notes are plain text (no bold, pictures or checkboxes), so they look the same on both systems.
- The first time, macOS may ask to let Sticky Notes access files in Google Drive. Click **Allow**.

## Screen Ruler

- Up to three see-through rulers and a protractor, always on top of other windows. Clicks on empty space around them go to the window underneath.
- Each ruler measures from its 0 to its red marker, and shows its own angle.
- Units (right-click a ruler): cm, in and px on the screen, or m, km and cm to a scale you set. Each ruler keeps its own scale.
- **Set Scale…**: lay the ruler along a length you know (a map's scale bar, an object in a photo), put the marker at its end and type its real length.
- **Correct Screen Size…**: when centimeters on screen don't match a real ruler (projectors often report no size), hold a real ruler against the screen, put the marker on a length you can read and type it.
- The protractor's arm reads the angle from its baseline. Where two rulers cross, the angle between them is shown, and the panel lists every reading.
- Closing the panel quits; the tools come back where they were.

| Action | Mouse | Keyboard (on a tool you clicked) |
|---|---|---|
| Add a ruler / the protractor | **+ Ruler** / **+ Protractor** in the panel, or right-click a tool | ⌘N / ⇧⌘N on Mac |
| Move a tool | Drag it | Arrows: 1 pixel (Shift: 10) |
| Turn a tool | Drag its round knob (Shift: 15° steps) | Alt/⌥ + ← / →: 0.1° (Shift: 1°) |
| Make a ruler longer or shorter | Drag its far end (the three lines) | — |
| Read a length | Drag the red marker (Shift: snap to ticks) | — |
| Read an angle | Drag the protractor's red arm (Shift: 5° steps); drag its rim to resize | — |
| Units, scale, screen size, remove | Right-click a tool | Delete removes the tool |

## Screen Shapes

The lines and shapes of the *Image Measurement Tool* web page, floating over the whole screen.

- Lines, rectangles, circles, and horizontal and vertical guide lines (up to 20), see-through and always on top. Clicks on empty space around them go to the window underneath.
- A line shows its length and angle; a rectangle its width × height and area; a circle its radius and area;
  a guide line its position from the screen's top or left edge and the gap to the next guide line of the same kind.
- One set of units for every shape: cm, in and px on the screen, or m, km and cm to a scale you set
  (**Set Scale from This Line…**: lay a line along a length you know and type its real length).
- Each shape's color can be changed (a palette or any color), and rectangles and circles can be filled or not;
  with no fill, clicks inside them reach the window below. Where a line crosses a filled shape, the line still gets the click.
- **Copy** in the panel puts every measurement on the clipboard as a tab-separated table, ready for a spreadsheet.
- Closing the panel quits; the shapes come back where they were.

| Action | Mouse | Keyboard |
|---|---|---|
| Add a shape | **+ Line**, **+ Rect**, **+ Circle**, **+ H-line**, **+ V-line** in the panel | ⌘1–⌘5 on Mac, Ctrl+1–5 on Windows (panel) |
| Move a shape | Drag its outline, label or (when filled) inside | Arrows on a shape you clicked: 1 pixel (Shift: 10) |
| Change a shape | Drag its round handles (Shift: 15° line steps, square rectangle) | — |
| Color, fill, units, scale, duplicate, remove | Right-click a shape | Delete removes the shape |
| Copy the table | **Copy** in the panel | ⇧⌘C on Mac, Ctrl+Shift+C on Windows (panel) |

## Reading Window

A focus aid for reading, for example for people with ADHD: the whole screen is darkened except a clear window.

- Shapes: rectangle, rounded rectangle, ellipse, or a full-width reading band. Width and height are set in the panel.
- Darkness from 10% to 95%, and any shade color (a palette of dark, calm colors, or Other color…).
- The clear window follows the pointer, and the page under it can still be scrolled. A click holds it in place
  (the click also reaches the page). While held, the clear window works like the screen under it; click the dark area
  to grab it again, or drag just outside its edge to resize it.
- The panel stays above the shade, and the clear window doesn't follow the pointer onto it. Closing the panel quits.

| Action | Mouse | Keyboard (panel) |
|---|---|---|
| Hold the clear window | Click (anywhere but the panel) | — |
| Grab it again | Click the dark area | — |
| Resize | Drag just outside its edge while held, or the Width/Height sliders | Scroll over a slider to fine-tune |
| Shape | Rect, Round, Ellipse, Band buttons | ⌘1–⌘4 on Mac, Ctrl+1–4 on Windows |
| Shade on/off | **Shade: On/Off** | ⌘S on Mac, Ctrl+S on Windows |

## Privacy

These apps **collect no personal data and never connect to the internet**.
They only save their own settings on your computer (for example the last time set, display
options, always-on-top and the window position).

Screen Ruler and Screen Shapes don't capture or record the screen; they only remember where their tools and shapes are, their units, scales and colors, and any screen-size correction.
Reading Window doesn't capture or record the screen either; it only follows the pointer and clicks to move and hold its clear window, and remembers its shape, size, darkness, color and panel position.

Sticky Notes saves your notes as files in the folder you choose (normally your Google Drive folder).
The app itself doesn't connect to Google; Google Drive for desktop syncs that folder, under your
Google account's terms.

- Windows: `%APPDATA%\CountdownTimer\`, `%APPDATA%\PhysicsCalculator\`, `%APPDATA%\FieldTimer\`, `%APPDATA%\StickyNotes\`, `%APPDATA%\ScreenRuler\`, `%APPDATA%\ScreenShapes\` and `%APPDATA%\ReadingWindow\`
- macOS: each app's standard preferences (`net.ergosfera.countdowntimer`, `net.ergosfera.physicscalculator`, `net.ergosfera.fieldtimer`, `net.ergosfera.stickynotes`, `net.ergosfera.screenruler`, `net.ergosfera.screenshapes`, `net.ergosfera.readingwindow`)

## Disclaimer

This software is provided **"as is", without warranty of any kind**. Don't rely on it
where an inaccurate result, measurement or an interrupted countdown could cause harm or loss, and check
important results independently. The author is not liable for any claim, damages or other
liability arising from its use. See [LICENSE](LICENSE) for the full terms.

## Building from source

- **Windows:** run `powershell -ExecutionPolicy Bypass -File <app>\build.ps1`
  (`countdown-timer\build.ps1`, `physics-calculator\windows\build.ps1`, `field-timer\windows\build.ps1`, `sticky-notes\build.ps1`, `screen-ruler\windows\build.ps1`, `screen-shapes\windows\build.ps1` or `reading-window\windows\build.ps1`).
  It uses the C# compiler included with Windows, so nothing needs to be installed.
- **macOS:** install the Xcode Command Line Tools (`xcode-select --install`), then run
  `countdown-timer-mac/build.sh`, `physics-calculator/mac/build.sh`, `field-timer/mac/build.sh`, `sticky-notes-mac/build.sh`, `screen-ruler/mac/build.sh`, `screen-shapes/mac/build.sh` or `reading-window/mac/build.sh`. Each creates the app and a `.dmg` in its `build/` folder.

## License

[MIT](LICENSE) © 2026 Mauricio Mendivelso
