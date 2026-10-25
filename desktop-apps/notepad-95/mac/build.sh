#!/bin/bash
# Builds "Notepad 95.app" and Notepad95.dmg (universal: Apple Silicon + Intel), a plain-text editor
# that looks and works like the Windows 95 Notepad.
# First checks reading and saving text files (encodings, line endings) with --selftest.
# Needs a Mac with Xcode or the Command Line Tools (xcode-select --install). Usage: ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
APP="Notepad 95"
OUT=build
rm -rf "$OUT"
mkdir -p "$OUT"

if ! swiftc -O -target arm64-apple-macos11 -o "$OUT/Notepad95-arm64" main.swift 2> "$OUT/swiftc.log"; then
  cat "$OUT/swiftc.log"
  if [ -n "${GITHUB_ACTIONS:-}" ]; then
    grep -E "error:" "$OUT/swiftc.log" | head -20 | while IFS= read -r line; do echo "::error::$line"; done
  fi
  exit 1
fi
# Some Command Line Tools installs can't link Intel; then build for Apple Silicon only (CI always builds both)
if swiftc -O -target x86_64-apple-macos11 -o "$OUT/Notepad95-x86_64" main.swift 2> "$OUT/x86.log"; then
  lipo -create -output "$OUT/Notepad95" "$OUT/Notepad95-arm64" "$OUT/Notepad95-x86_64"
elif [ -n "${GITHUB_ACTIONS:-}" ]; then
  cat "$OUT/x86.log"
  exit 1
else
  echo "Intel build unavailable here; building for Apple Silicon only."
  cp "$OUT/Notepad95-arm64" "$OUT/Notepad95"
fi

# Files must come back byte for byte as they were read
"$OUT/Notepad95" --selftest

C="$OUT/$APP.app/Contents"
mkdir -p "$C/MacOS" "$C/Resources"
cp "$OUT/Notepad95" "$C/MacOS/Notepad95"
cp ../../LICENSE "$C/Resources/LICENSE.txt"
"$OUT/Notepad95" --write-iconset "$OUT/AppIcon.iconset"
iconutil -c icns "$OUT/AppIcon.iconset" -o "$C/Resources/AppIcon.icns"

cat > "$C/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP</string>
  <key>CFBundleDisplayName</key><string>$APP</string>
  <key>CFBundleExecutable</key><string>Notepad95</string>
  <key>CFBundleIdentifier</key><string>net.ergosfera.notepad95</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>NSHumanReadableCopyright</key><string>© 2026 Mauricio Mendivelso. MIT License.</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>Text Document</string>
      <key>CFBundleTypeRole</key><string>Editor</string>
      <key>LSHandlerRank</key><string>Alternate</string>
      <key>LSItemContentTypes</key>
      <array><string>public.plain-text</string><string>public.text</string><string>public.data</string></array>
    </dict>
  </array>
</dict>
</plist>
PLIST

# Ad-hoc signature (required for Apple Silicon; not a paid Developer ID).
codesign --force --deep --sign - "$OUT/$APP.app"

mkdir -p "$OUT/dmg"
cp -R "$OUT/$APP.app" "$OUT/dmg/"
cp ../../LICENSE "$OUT/dmg/LICENSE.txt"
cp ../../README.md "$OUT/dmg/README.md"
ln -s /Applications "$OUT/dmg/Applications"
hdiutil create -volname "$APP" -srcfolder "$OUT/dmg" -ov -format UDZO "$OUT/Notepad95.dmg"
echo "Built $OUT/Notepad95.dmg"
