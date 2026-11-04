# Ergosfera Desktop Apps

Small, free desktop apps for Windows and macOS, made for teaching:

- **Countdown Timer**: a compact countdown whose bar and numbers turn from green to yellow to red.
- **Physics Calculator**: the calculator from the Ergosfera *Notebook & Physics Calculator* web tool, where numbers carry units.
- **Field**: the Ergosfera *Field* dot timer, a field of dots that go dark as time runs out, with big numbers drawn in dots. A tribute to Untime by Perceptor.
- **Sticky Notes**: sticky notes on your desktop that sync between your Mac and Windows computers through Google Drive.
- **Screen Ruler**: up to three rulers and a protractor that float over anything on the screen.
- **Screen Shapes**: lines, rectangles, circles and guide lines that float over the screen and measure what's under them.
- **Reading Window**: shades the screen except a clear window that follows the pointer, to help focus while reading.
- **Net LEDs**: green and red lights next to the clock that blink with download and upload traffic, with a live graph.
- **Disk LED**: the hard-disk activity light of an old PC, next to the clock: it blinks when the drives read or write, SSDs included.
- **Notepad 95** (macOS): a plain-text editor that looks and works like the Windows 95 Notepad.
- **Paint 95** (macOS): a bitmap painter that looks and works like the Windows 95 Paint.
- **Clock Bar**: the date and time in bold, outlined letters on a see-through bar that stays on top.
- **Citorio**: every so often a small window pops up with a quote, then hides again. Comes with 1,315 quotes in Spanish; add your own categories as text files.

## Download and install

Download the file for your system from the app's release page:
[Countdown Timer](https://github.com/ergosfera/physics-teaching-tools/releases/tag/countdown-timer) ·
[Physics Calculator](https://github.com/ergosfera/physics-teaching-tools/releases/tag/physics-calculator) ·
[Field](https://github.com/ergosfera/physics-teaching-tools/releases/tag/field-timer) ·
[Sticky Notes](https://github.com/ergosfera/physics-teaching-tools/releases/tag/sticky-notes) ·
[Screen Ruler](https://github.com/ergosfera/physics-teaching-tools/releases/tag/screen-ruler) ·
[Screen Shapes](https://github.com/ergosfera/physics-teaching-tools/releases/tag/screen-shapes) ·
[Reading Window](https://github.com/ergosfera/physics-teaching-tools/releases/tag/reading-window) ·
[Net LEDs](https://github.com/ergosfera/physics-teaching-tools/releases/tag/net-leds) ·
[Disk LED](https://github.com/ergosfera/physics-teaching-tools/releases/tag/disk-led) ·
[Notepad 95](https://github.com/ergosfera/physics-teaching-tools/releases/tag/notepad-95) ·
[Paint 95](https://github.com/ergosfera/physics-teaching-tools/releases/tag/paint-95) ·
[Clock Bar](https://github.com/ergosfera/physics-teaching-tools/releases/tag/clock-bar) ·
[Citorio](https://github.com/ergosfera/physics-teaching-tools/releases/tag/citorio) (Notepad 95 and Paint 95: Mac only).

**Windows (10 or 11):** double-click the `.exe`. No installation is needed.
The apps aren't signed with a paid certificate, so the first time Windows may show
"Windows protected your PC". Click **More info**, then **Run anyway**.

**macOS (11 Big Sur or later; Apple Silicon and Intel):** open the `.dmg` and drag the app
into **Applications**. The apps aren't signed with a paid Apple Developer ID, so the first time
macOS may block them. Open **System Settings → Privacy & Security** and click **Open Anyway**.

## Countdown Timer

- Set hours, minutes and seconds, then start. Or give the time it should end, like 14:30, and it works out how long that is.
- A bar shrinks as time runs out and turns from green to yellow to red, at percentages you choose.
- Optional "always on top" keeps the timer visible over other windows.
- Optional sound when the time is up.

| Action | Mouse | Keyboard |
|---|---|---|
| Set the time | Click hours, minutes or seconds, then type or scroll | Type digits; arrows move and change the fields |
| Set the end time | Clock button in the top bar or right-click → Set end time | E |
| Start / pause / resume | **Start** button | Space or Enter |
| Reset | **Reset** button | R |
| Always on top | Pin button in the top bar | — |
| Settings (time, color levels, sound) | Gear button or right-click → Settings | ⌘, on Mac |
| About and license | Right-click → About | — |

A countdown to an end time ends at that time whenever you press **Start**. A time that has already passed today means tomorrow. Pausing lets go of the end time: resuming continues from what was left.

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

Field is a tribute to **Untime**, the iPhone and iPad timer made in 2012 by **Perceptor**, the Dutch design
studio of **Bastiaan Terhorst** and **Casper Schipper**. Showing time as a field of dots was their idea,
and the numbers drawn with dots and the glow at the end come from their app. Thank you for a wonderful idea.
([Untime on perceptor.nl](https://perceptor.nl/everything/untime.html))

- Drag down on the field to set the time, from 1 second to 6 hours. Gray dots fill the field from the top and big dot numbers show the time.
- Press **start**. The whole field lights up, whatever the time, and the dots go dark one by one from the end. Only the dots show while it counts down, and the field glows when the time is up.
- Click anywhere to pause: the numbers and the **resume** and **reset** buttons come back.
- Full screen for a projector, optional "always on top" and optional sound at the end, all in the **···** menu.

| Action | Mouse | Keyboard |
|---|---|---|
| Set the time | Drag down on the field for more time, or scroll over it | ↑ / ↓ |
| Start / pause / resume | **start**, then click anywhere to pause, then **resume** | Space or Enter |
| Reset | **reset** (while paused) or **dismiss** (at the end) | Esc |
| Full screen | **···** → Full screen | F11 on Windows, ⌃⌘F on Mac (Esc leaves) |
| Always on top, sound, About | **···** button, or right-click the window | — |

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
   so your notes are there when you start the computer. On Windows this keeps a copy of the app in
   `%LOCALAPPDATA%\Programs\StickyNotes`, because Google Drive isn't ready yet when Windows starts its
   startup apps; opening a newer StickyNotes.exe updates that copy.

| Action | How |
|---|---|
| New note | **+** on a note, Ctrl+N / ⌘N, or click the tray icon (Windows) / Dock icon (Mac) |
| Checklist | **☐** on the top bar, Ctrl+Shift+L / ⇧⌘L, or type `[ ]` and a space at the start of a line. Click a box to check it; Enter adds the next one, Enter on an empty one ends the list |
| Bold | Select text, then **B** on the top bar or Ctrl+B / ⌘B |
| Text color | Select text, then **A** on the top bar and pick Black, Red, Orange, Green, Blue or Purple (also in the ⋯ menu, and Format → Text Color on the Mac) |
| Tags | The tag button on the top bar → pick a tag or **Date…**, under **At the start of the line** or **At the end of the line**. Each tag is a small colored label; Backspace removes it whole |
| Your tag list | Tag button → **Edit tags…** (also in the tray menu / Sticky Notes menu): name each tag and pick one of nine colors. Renaming a tag renames it in your notes. A date looks like a little calendar page: the month in white on red, the day in red on white (OCT \| 30), with the year when it isn't this one |
| Links | Web addresses (`https://…`, `www.…`) and email addresses turn into links; click one to open it |
| Title | Double-click the top bar, type the title and press Enter (Esc cancels). Also ⋯ → Add title, or Ctrl+Shift+T / ⇧⌘T. A note without a title shows an empty bar |
| Color, keep on top, delete | **⋯** on the note's top bar (or right-click the bar) |
| Move / resize | Drag the top bar / drag any edge or the bottom-right corner |
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
- Notes can have checklists, bold text, six text colors, tags and a title, and nothing else (no pictures or fonts), so they look the same on both systems.
  In the file they're plain text markers: `[ ] milk` and `[x] milk` for checkboxes, `**word**` for bold, `{red}word{/red}` for color
  (red, orange, green, blue, purple), `[#Delayed]` for a tag, `[@2026-10-30]` for a date, and a `title:` line above the `---`.
- The tag list is `tags.conf` in the notes folder, one `name: color` a line, so it syncs like the notes.
- Tags need version 1.3 on every computer (an older one shows `[#Delayed]` as typed). Titles and colors need version 1.2 or newer: an older version shows the color markers as typed and drops the title when it saves the note.
  A checked item shows crossed out.
- The first time, macOS may ask to let Sticky Notes access files in Google Drive. Click **Allow**.

## Screen Ruler

- Up to three see-through rulers and a protractor, always on top of other windows. Clicks on empty space around them go to the window underneath.
- Each ruler measures from its 0 to its red marker, and shows its own angle.
- Units (right-click a ruler): cm, in and px on the screen, or m, km and cm to a scale you set. Each ruler keeps its own scale.
- **Set Scale…**: lay the ruler along a length you know (a map's scale bar, an object in a photo), put the marker at its end and type its real length.
- **Correct Screen Size…**: when centimeters on screen don't match a real ruler (projectors often report no size), hold a real ruler against the screen, put the marker on a length you can read and type it.
- The protractor's arm reads the angle from its baseline. Where two rulers cross, the angle between them is shown, and the panel lists every reading.
- **Colored guides and marks**: right-click a ruler for **Add Guide Here**, or the protractor for **Add Mark Here** (up to 8 each). Each one shows its own reading, here and in the panel. Right-click one to change its color or remove it.
- Closing the panel quits; the tools come back where they were.

| Action | Mouse | Keyboard (on a tool you clicked) |
|---|---|---|
| Add a ruler / the protractor | **+ Ruler** / **+ Protractor** in the panel, or right-click a tool | ⌘N / ⇧⌘N on Mac |
| Move a tool | Drag it | Arrows: 1 pixel (Shift: 10) |
| Turn a tool | Drag its round knob (Shift: 15° steps) | Alt/⌥ + ← / →: 0.1° (Shift: 1°) |
| Make a ruler longer or shorter | Drag its far end (the three lines) | — |
| Read a length | Drag the red marker (Shift: snap to ticks) | — |
| Read an angle | Drag the protractor's red arm (Shift: 5° steps); drag its rim to resize | — |
| Move a guide or mark | Drag it (Shift: ticks on a ruler, 5° steps on the protractor) | — |
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
- The clear window follows the pointer, and the page under it can still be scrolled. A click holds it in place,
  and another click makes it follow again (the clicks also reach the page). While held, drag just outside its edge to resize it.
- The panel stays above the shade, and the clear window doesn't follow the pointer onto it. Closing the panel quits.

| Action | Mouse | Keyboard (panel) |
|---|---|---|
| Hold the clear window / make it follow again | Click (anywhere but the panel) | — |
| Resize | Drag just outside its edge while held, or the Width/Height sliders | Scroll over a slider to fine-tune |
| Shape | Rect, Round, Ellipse, Band buttons | ⌘1–⌘4 on Mac, Ctrl+1–4 on Windows |
| Shade on/off | **Shade: On/Off** | ⌘S on Mac, Ctrl+S on Windows |

## Net LEDs

Two lamps like the activity LEDs on an old modem, in the Mac menu bar or the Windows system tray.

- Green blinks when data comes in (download), red when data goes out (upload); brighter with more traffic.
- A live graph of the last 60 seconds: download above the line, upload below, each side auto-scaled.
- Counts all network adapters together, leaving out loopback. It reads the system's byte counters only.

| Action | Mouse | Keyboard (graph) |
|---|---|---|
| Show / hide the graph | Click the lights | Esc hides it |
| Keep the graph open | Drag it away from the menu bar or tray | — |
| Speeds, start at login, quit | Right-click (or Control-click on Mac) the lights | — |
| Speeds next to the lights (Mac) | Right-click → Show Speeds in Menu Bar | — |

## Disk LED

The hard-disk activity light from the front of an old PC, in the Mac menu bar or the Windows system tray.
It blinks with every burst of reading or writing on the drives, SSDs included, and glows brighter with more data.

- It reads the drives' own activity counters 30 times a second: IOKit's block-storage statistics on a Mac, and each physical drive's
  performance counters on Windows (the ones Task Manager uses; no administrator rights needed). All drives are counted together.
- SSD operations take microseconds, so each burst keeps the lamp lit for a moment, which brings back the flicker of the old LEDs.
  Like them, it doesn't blink for files the system already has in memory.
- One lamp (amber, red, green or blue) or two: green for reading, red for writing.
- **Busy** is the share of time the drives spent working; near 100 % for a while means the drives are the bottleneck.
  Windows also shows the **queue**, the operations waiting their turn.
- A live graph of the last 60 seconds: reading above the line, writing below.
- **Drive sounds** (off at first): soft synthesized clicks with the activity, like a hard disk seeking.

| Action | Mouse | Keyboard (graph) |
|---|---|---|
| Show / hide the graph | Click the lamp | Esc hides it |
| Keep the graph open | Drag it away from the menu bar or tray | — |
| Speeds, busy, lamp style, drive sounds, start at login, quit | Right-click (or Control-click on Mac) the lamp | — |

`DiskLED --watch 10` (Mac) prints the read and write speeds and the busy share twice a second, to check the counters.

## Clock Bar

The date and time, like **April 5  2:34**, on a see-through bar that stays on top of everything:
the date in amber and the time in white, both bold with a dark outline, so they read over light and dark windows alike.
The time is on a 12-hour clock without AM/PM.

- Drag it into place once (for example at the top of the screen); it remembers where it goes. **Lock position** stops it moving by accident.
- It stays in front of other windows, the taskbar and the Mac menu bar. Hover over it for the full date.
- On a Mac it has no Dock icon; right-click it to quit.

| Action | Mouse |
|---|---|
| Move it | Drag it |
| Month calendar | Click it (Esc or a click elsewhere closes it) |
| Size, lock position, start at login, quit | Right-click (or Control-click on Mac) it |

## Citorio

Every so often a small window pops up over whatever you are doing with a quote, then hides again.
It lives in the notification area by the clock (Windows) or in the menu bar (Mac), and the window stays on top without taking the keyboard.
A tribute to **Citorio 1.1** (1995–96) for Windows 3.1 by **Javier Cámara Melgosa**, itself based on *Pundit* (1990) by Jeff Bienstadt;
the 1,315 quotes in Spanish that it comes with are the ones he collected and translated. The program itself is new.

- Longer quotes stay up longer (10 to 40 seconds), then it hides for a minute; all three times are adjustable. Moving the mouse over a quote holds it.
- Quotes are set in a serif font (Georgia unless you pick another), with an icon for each category.
- A quote doesn't come back until 60 % of the others have been shown, like the original.
- **Never hide** goes straight from one quote to the next, for a projector.
- Made for school: the original's category of sex jokes is left out, along with other quotes with sexual content, vulgar language, or that make fun of a religion, a people or women.

**Your own quotes.** The quotes are plain-text files in `Documents/Citorio`, one per category; Citorio puts its nine built-in lists there
on first launch and reads the folder again before each quote. Add a `.txt` file to add a category, edit one to add quotes. In a file:

```
# name: Física
# icon: 🔭
Si he visto más lejos es porque estoy sentado sobre los hombros de gigantes.
— Isaac Newton
%
La imaginación es más importante que el conocimiento.
— Albert Einstein
```

Quotes are separated by a line with only `%`; a last line starting with `—` or `--` is the author. Lines starting with `#` at the top are notes,
and `# name:` / `# icon:` set the category's name (otherwise the file name) and icon (several icons separated by spaces are used in turn).
A file without any `%` line has one quote per line, with the author after ` — ` or ` -- `. Files are read as UTF-8, UTF-16 or Windows-1252, and
files whose name starts with `_` are ignored. The built-in lists are in [`citorio/quotes`](citorio/quotes).

| Action | Mouse | Keyboard |
|---|---|---|
| Another quote / hide now and show this one next time / pause / copy | **Next**, **Later**, **Pause**, **Copy** buttons (double-click the quote for the next one) | — |
| Hide until the next quote | Close button of the window | — |
| Show a quote now | Click the icon by the clock (Windows), or open Citorio again | — |
| Times, font, icons, sound, categories | **Settings…** button or the icon's menu | ⌘, on Mac (from the menu) |
| New category, import a list, edit, open the folder, restore the built-in lists | Buttons in Settings | — |
| Start with Windows / Open at Login, About, Quit | The icon's menu (right-click on Windows, click on Mac) | — |

## Notepad 95

The Windows 95 Notepad for the Mac: its window, menus, scroll bars and dialogs, drawn the Windows 95 way. Plain text only.

- Files are saved back the way they were read: UTF-8 (with or without BOM), UTF-16, or Windows-1252 (ANSI) text, and CRLF, LF or CR line endings. New files are UTF-8 with LF.
  If the text gains characters the file's encoding can't hold, it asks before saving as UTF-8 instead.
- No rich text, smart quotes, autocorrect or spell-check underlines.
- **Find** (up/down, match case), **Find Next**, **Replace**, **Replace All**, **Word Wrap**, **Set Font**, **Time/Date**, Page Setup and Print.
- A file whose first line is `.LOG` gets the time and date added at the end each time it's opened.
- Like the original it doesn't save by itself; it asks before closing or quitting with unsaved changes. Closing the last window quits.

| Action | Mouse | Keyboard |
|---|---|---|
| Menus | **File**, **Edit**, **Search**, **Help** in the window (or the Mac menu bar) | F10, then arrows and Enter; the underlined letter picks an item |
| New / New window / Open / Save / Save As | File menu | ⌘N / ⇧⌘N / ⌘O / ⌘S / ⇧⌘S |
| Find / Find next / Find previous / Replace | Search menu | ⌘F / F3 or ⌘G / ⇧F3 or ⇧⌘G / ⌥⌘F |
| Time/Date | Edit menu | F5 |
| Word wrap, font | Edit menu | ⌘T for the font |
| Cut, copy, paste, delete, select all | Edit menu or right-click | ⌘X, ⌘C, ⌘V, Delete, ⌘A |
| Open a file | Drop it on the window, or Finder → Open With → Notepad 95 | — |
| Move / resize / maximize | Drag the title bar / drag an edge or corner / double-click the title bar | ⌘M minimizes |

## Paint 95

The Windows 95 Paint for the Mac, with its tool box, options box, color box and status bar.

- All 16 tools: free-form and rectangular select, eraser (right-drag: color eraser), fill, pick color, magnifier, pencil, brush (12 shapes),
  airbrush (3 sizes), text, line and curve (5 widths), rectangle, polygon, ellipse and rounded rectangle (outline, outline + fill, fill).
- Left button draws with the drawing color, right button (or Control-click) with the background color. Shift: 45° lines, squares, circles.
- Selections: move, Option-drag to copy, opaque or transparent (the background color shows through), cut/copy/paste with other apps, Copy To / Paste From files.
- Image: Flip/Rotate, Stretch/Skew, Invert Colors, Attributes (size in pixels), Clear Image; or drag the handles at the picture's edges. These work on the selection if there is one.
- Opens any picture macOS can read; saves PNG, 24-bit BMP, JPEG, GIF or TIFF. Undo up to 50 steps (fewer for very large pictures).

| Action | Mouse | Keyboard |
|---|---|---|
| Menus | **File**, **Edit**, **View**, **Image**, **Options**, **Help** in the window (or the Mac menu bar) | F10, then arrows and Enter |
| Drawing / background color | Click / right-click a color in the color box; double-click to edit it | — |
| Undo / Repeat | Edit menu | ⌘Z / ⇧⌘Z |
| Cut, copy, paste, clear selection, select all | Edit menu | ⌘X, ⌘C, ⌘V, Delete, ⌘A |
| Move a selection by one pixel | — | Arrow keys |
| Finish a polygon | Double-click, or click its first point | Return |
| Cancel what's in progress | — | Esc |
| Zoom | Magnifier tool, View menu | ⌘0 normal, ⌘4 large, ⌘+ / ⌘−, ⌘G grid |
| Tool box, color box | View menu | ⌘T, ⌘L |
| Flip/Rotate, Stretch/Skew, Invert, Attributes, Clear | Image menu | ⌘R, ⌥⌘W, ⌘I, ⌘E, ⇧⌘N |

## Privacy

These apps **collect no personal data and never connect to the internet**.
They only save their own settings on your computer (for example the last time set, display
options, always-on-top and the window position).

Screen Ruler and Screen Shapes don't capture or record the screen; they only remember where their tools and shapes are, their units, scales and colors, and any screen-size correction.
Reading Window doesn't capture or record the screen either; it only follows the pointer and clicks to move and hold its clear window, and remembers its shape, size, darkness, color and panel position.
Notepad 95 only opens and saves the files you choose, and remembers its font, word wrap, window position and last search.
Paint 95 only opens and saves the pictures you choose, and remembers its window position.

Sticky Notes saves your notes as files in the folder you choose (normally your Google Drive folder).
The app itself doesn't connect to Google; Google Drive for desktop syncs that folder, under your
Google account's terms.

- Windows: `%APPDATA%\CountdownTimer\`, `%APPDATA%\PhysicsCalculator\`, `%APPDATA%\FieldTimer\`, `%APPDATA%\StickyNotes\`, `%APPDATA%\ScreenRuler\`, `%APPDATA%\ScreenShapes\` and `%APPDATA%\ReadingWindow\`
- macOS: each app's standard preferences (`net.ergosfera.countdowntimer`, `net.ergosfera.physicscalculator`, `net.ergosfera.fieldtimer`, `net.ergosfera.stickynotes`, `net.ergosfera.screenruler`, `net.ergosfera.screenshapes`, `net.ergosfera.readingwindow`, `net.ergosfera.notepad95`, `net.ergosfera.paint95`)

## Disclaimer

This software is provided **"as is", without warranty of any kind**. Don't rely on it
where an inaccurate result, measurement or an interrupted countdown could cause harm or loss, and check
important results independently. The author is not liable for any claim, damages or other
liability arising from its use. See [LICENSE](LICENSE) for the full terms.

## Building from source

- **Windows:** run `powershell -ExecutionPolicy Bypass -File <app>\build.ps1`
  (`countdown-timer\build.ps1`, `physics-calculator\windows\build.ps1`, `field-timer\windows\build.ps1`, `sticky-notes\build.ps1`, `screen-ruler\windows\build.ps1`, `screen-shapes\windows\build.ps1`, `reading-window\windows\build.ps1`, `citorio\windows\build.ps1` or `disk-led\windows\build.ps1`).
  It uses the C# compiler included with Windows, so nothing needs to be installed.
- **macOS:** install the Xcode Command Line Tools (`xcode-select --install`), then run
  `countdown-timer-mac/build.sh`, `physics-calculator/mac/build.sh`, `field-timer/mac/build.sh`, `sticky-notes-mac/build.sh`, `screen-ruler/mac/build.sh`, `screen-shapes/mac/build.sh`, `reading-window/mac/build.sh`, `notepad-95/mac/build.sh`, `paint-95/mac/build.sh`, `citorio/mac/build.sh` or `disk-led/mac/build.sh`.
  Notepad 95 and Paint 95 share their Windows 95 look from `win95-kit/Win95Kit.swift`. Each creates the app and a `.dmg` in its `build/` folder.

## License

[MIT](LICENSE) © 2026 Mauricio Mendivelso
