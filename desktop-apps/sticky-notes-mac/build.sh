#!/bin/bash
# Builds "Sticky Notes.app" and StickyNotes.dmg (universal: Apple Silicon + Intel).
# First checks the note file format (shared with the Windows app) with --selftest.
# Needs a Mac with Xcode or the Command Line Tools (xcode-select --install). Usage: ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
APP="Sticky Notes"
OUT=build
rm -rf "$OUT"
mkdir -p "$OUT"

for arch in arm64 x86_64; do
  if ! swiftc -O -target "$arch-apple-macos11" -o "$OUT/StickyNotes-$arch" main.swift 2> "$OUT/swiftc.log"; then
    cat "$OUT/swiftc.log"
    if [ -n "${GITHUB_ACTIONS:-}" ]; then
      grep -E "error:" "$OUT/swiftc.log" | head -20 | while IFS= read -r line; do echo "::error::$line"; done
    fi
    exit 1
  fi
  cat "$OUT/swiftc.log"
done
lipo -create -output "$OUT/StickyNotes" "$OUT/StickyNotes-arm64" "$OUT/StickyNotes-x86_64"

# Both apps must read and write the same note files
"$OUT/StickyNotes" --selftest

C="$OUT/$APP.app/Contents"
mkdir -p "$C/MacOS" "$C/Resources"
cp "$OUT/StickyNotes" "$C/MacOS/StickyNotes"
cp ../LICENSE "$C/Resources/LICENSE.txt"
"$OUT/StickyNotes" --write-iconset "$OUT/AppIcon.iconset"
iconutil -c icns "$OUT/AppIcon.iconset" -o "$C/Resources/AppIcon.icns"

cat > "$C/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP</string>
  <key>CFBundleDisplayName</key><string>$APP</string>
  <key>CFBundleExecutable</key><string>StickyNotes</string>
  <key>CFBundleIdentifier</key><string>net.ergosfera.stickynotes</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.2.1</string>
  <key>CFBundleVersion</key><string>5</string>
  <key>NSHumanReadableCopyright</key><string>© 2026 Mauricio Mendivelso. MIT License.</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

# Ad-hoc signature (required for Apple Silicon; not a paid Developer ID).
codesign --force --deep --sign - "$OUT/$APP.app"

mkdir -p "$OUT/dmg"
cp -R "$OUT/$APP.app" "$OUT/dmg/"
cp ../LICENSE "$OUT/dmg/LICENSE.txt"
cp ../README.md "$OUT/dmg/README.md"
ln -s /Applications "$OUT/dmg/Applications"
hdiutil create -volname "$APP" -srcfolder "$OUT/dmg" -ov -format UDZO "$OUT/StickyNotes.dmg"
echo "Built $OUT/StickyNotes.dmg"
