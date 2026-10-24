#!/bin/bash
# Builds "Net LEDs.app" and NetLEDs.dmg (universal: Apple Silicon + Intel), a menu bar app with
# download (green) and upload (red) activity lights.
# Needs a Mac with Xcode or the Command Line Tools (xcode-select --install). Usage: ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
APP="Net LEDs"
OUT=build
rm -rf "$OUT"
mkdir -p "$OUT"

swiftc -O -target arm64-apple-macos11 -o "$OUT/NetLEDs-arm64" main.swift
# Some Command Line Tools installs can't link Intel; then build for Apple Silicon only
if swiftc -O -target x86_64-apple-macos11 -o "$OUT/NetLEDs-x86_64" main.swift 2> "$OUT/x86.log"; then
  lipo -create -output "$OUT/NetLEDs" "$OUT/NetLEDs-arm64" "$OUT/NetLEDs-x86_64"
else
  echo "Intel build unavailable here; building for Apple Silicon only."
  cp "$OUT/NetLEDs-arm64" "$OUT/NetLEDs"
fi

# The byte counters must be readable
"$OUT/NetLEDs" --selftest

C="$OUT/$APP.app/Contents"
mkdir -p "$C/MacOS" "$C/Resources"
cp "$OUT/NetLEDs" "$C/MacOS/NetLEDs"
cp ../../LICENSE "$C/Resources/LICENSE.txt"

cat > "$C/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP</string>
  <key>CFBundleDisplayName</key><string>$APP</string>
  <key>CFBundleExecutable</key><string>NetLEDs</string>
  <key>CFBundleIdentifier</key><string>net.ergosfera.netleds</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>NSHumanReadableCopyright</key><string>© 2026 Mauricio Mendivelso. MIT License.</string>
  <key>LSMinimumSystemVersion</key><string>11.0</string>
  <key>LSUIElement</key><true/>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSPrincipalClass</key><string>NSApplication</string>
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
hdiutil create -volname "$APP" -srcfolder "$OUT/dmg" -ov -format UDZO "$OUT/NetLEDs.dmg"
echo "Built $OUT/NetLEDs.dmg"
