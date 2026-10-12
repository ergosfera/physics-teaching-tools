# Countdown Timer

A small, free countdown timer for Windows and macOS.

- Set hours, minutes and seconds, then start.
- A bar shrinks as time runs out and turns from green to yellow to red, at percentages you choose.
- Optional "always on top" so the timer stays visible over other windows.
- Optional sound when the time is up.

## Download and install

**Windows (10 or 11):** download `CountdownTimer.exe` and double-click it. No installation is needed.
The app isn't signed with a paid certificate, so the first time Windows may show
"Windows protected your PC". Click **More info**, then **Run anyway**.

**macOS (11 Big Sur or later; Apple Silicon and Intel):** open `CountdownTimer.dmg` and drag
**Countdown Timer** into **Applications**. The app isn't signed with a paid Apple Developer ID,
so the first time macOS may block it. Open **System Settings → Privacy & Security** and
click **Open Anyway**.

## How to use

| Action | Mouse | Keyboard |
|---|---|---|
| Set the time | Click hours, minutes or seconds, then type or scroll | Type digits; arrows move and change the fields |
| Start / pause / resume | **Start** button | Space or Enter |
| Reset | **Reset** button | R |
| Always on top | Pin button in the top bar | — |
| Settings (time, color levels, sound) | Gear button or right-click → Settings | ⌘, on Mac |
| About and license | Right-click → About | — |

Move the window by dragging any empty area.

## Privacy

Countdown Timer **collects no personal data and never connects to the internet**.
It only saves its own settings on your computer: the last time you set, the color levels,
the sound and always-on-top options, and the window position.

- Windows: `%APPDATA%\CountdownTimer\settings.ini`
- macOS: the app's standard preferences (`net.ergosfera.countdowntimer`)

## Disclaimer

This software is provided **"as is", without warranty of any kind**. Don't rely on it
where an inaccurate or interrupted countdown could cause harm or loss. The author is not liable
for any claim, damages or other liability arising from its use. See [LICENSE](LICENSE)
for the full terms.

## Building from source

- **Windows:** run `powershell -ExecutionPolicy Bypass -File countdown-timer/build.ps1`.
  It uses the C# compiler included with Windows, so nothing needs to be installed.
- **macOS:** install the Xcode Command Line Tools (`xcode-select --install`), then run
  `countdown-timer-mac/build.sh`. It creates `build/Countdown Timer.app` and `build/CountdownTimer.dmg`.

## License

[MIT](LICENSE) © 2026 Mauricio Mendivelso
