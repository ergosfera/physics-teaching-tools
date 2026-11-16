# Signal Studio desktop

Author: **Mauricio Mendivelso-Villaquirán**  
Implementation assistance: OpenAI Codex  
Inspiration: **Valentyn Vieshchunov’s Casse-o-player**; additional player ideas credited to **Fab2** (https://fab2m.fr/).

A desktop edition of the existing Signal Studio visualization, with cassette playback, playlists, stereo VU and peak meters, channel waveforms, spectrum inspection, ten-band EQ, master volume, and educational guides.

## Install

- **Apple Silicon Mac:** open `SignalStudio-mac-arm64.dmg` and copy Signal Studio to Applications.
- **Windows x64:** run `SignalStudio-win-x64.exe`, the installer.

These builds are unsigned and the Mac builds are not notarized. The operating system may display an unknown-publisher or security prompt. For public distribution, configure signing and Apple notarization with the author’s own credentials; none are bundled here.

## Use

Choose **+ ADD TRACKS** to select local audio or video files. Play, pause, seek, adjust volume, or change the EQ. The information circles open a separate, movable reading window for each module. The same reading window switches topics, stays alongside the player where screen space permits, and scrolls independently. About opens there too. Playback and controls remain available while you read.

The player uses three columns on wide windows, two on medium windows, and a vertical stack on narrow windows. Wide layouts adapt to available height. Maximized windows fit the complete rack to the content area, including high display scaling, without a page scrollbar. Small, non-maximized windows may scroll vertically to keep controls readable. A custom dark title bar provides dragging, minimize, maximize/restore, and close controls on both platforms.

Fonts, icons, image assets, guides, and playback code are bundled locally. Playback does not require an internet connection. Reference links open in your default browser and require internet access.

EQ settings, bypass state, and master volume persist locally between launches. Use SAVE PLAYLIST to write a `.signalplaylist` file, and OPEN PLAYLIST to restore its ordered tracks. The playlist stores file references, not copies of your music. Keep the media files available; relative paths allow moving a playlist together with its media folder. If some files are missing, available tracks are loaded and the missing count is reported. If none are available or the file is invalid, the current playlist stays unchanged. The application does not upload media. Codec availability still depends on the packaged Chromium media decoder: a file extension alone does not guarantee playback. WAV playback was exercised in the desktop smoke test; all audio/video codecs have not been tested.

## Develop and rebuild

Install Node.js 26 LTS, then run from this directory:

```sh
npm ci
npm test
npm start
```

Build installers:

```sh
npm run build:mac
npm run build:win
```

Outputs go to `dist/`. `npm run build:mac` also makes an Intel package, but the repository's CI publishes only the Apple Silicon one. Windows is x64. Build configuration is in `package.json`, dependencies are pinned in `package-lock.json`, and the repository's `.github/workflows/desktop-apps.yml` builds the Mac and Windows installers on every push to `main` and publishes them on the `signal-studio` GitHub release.

The Windows configuration skips executable resource editing to allow building on this Mac without Wine. The executable retains Electron’s default icon/embedded vendor metadata, though its filename, app title, and installer identify Signal Studio. A future signed release should configure a dedicated icon and native resource editing/signing.

## Validation

- Automated settings tests cover persistence, corrupt storage, and numeric bounds.
- Renderer scripts parse and have no remote script/font dependencies.
- `scripts/smoke.cjs` runs a hidden, muted Electron window, generates an in-memory WAV, verifies playback, opens all five guides and About in a native child window, confirms player height stays unchanged, closes/reopens the reading window, checks local font/icons and volume persistence, and checks horizontal overflow at 360, 600, 900, 1280, and 1920 pixels.
- The Apple Silicon Electron smoke test passed locally. Intel Mac and Windows binaries were cross-built; their native execution has not been tested here.

Run the smoke test on a desktop session with:

```sh
npx electron scripts/smoke.cjs
```

## Structure and security

`main.cjs` and `preload.cjs` create the desktop window with renderer sandboxing, context isolation, and Node integration disabled. Navigation is blocked; only the project’s known HTTPS reference hosts can open in the external browser. The renderer uses a restrictive CSP. A narrow context-isolated preload bridge supports known guide topics, fixed window actions, file-path extraction for selected media, and native playlist dialogs. The main process validates sender frames, topics, actions, and playlist structure; writes happen only to a path selected in the Save dialog. `app/state.js` stores bounded settings using local storage. `app/player.js`, `app/player.css`, and `app/index.html` contain the desktop player.

Third-party font/icon licenses are included in `app/assets/`. Electron and Chromium licenses are retained in packaged builds. Source graphics retain the original cassette references and their displayed marks; no new third-party rights are asserted by this packaging work.

Casse-o-player developer: https://play.google.com/store/apps/dev?id=4726554229261549763  
Casse-o-player project: https://play.google.com/store/apps/details?id=com.hornwerk.compactcassetteplayer&hl=en

## Version 1.2 validation

Playlist tests cover ordered relative-path round trips, Unicode names, missing files, and malformed input. Electron integration tests exercise native-dialog save/open (dialogs stubbed to test paths), actual file-backed playback, cancel behavior, window controls, guide windows, and maximized geometry at 100%, 125%, 150%, and 200% renderer scale. These simulate scaled content sizes on the Mac test host; native Windows execution remains untested.
