#!/bin/bash
# Builds Citorio.app and Citorio.dmg (universal: Apple Silicon + Intel).
# First checks that the quote lists in ../quotes read correctly and that a quote draws,
# with Citorio --render, and leaves the picture in build/quote-preview.png.
# Needs a Mac with Xcode or the Command Line Tools (xcode-select --install). Usage: ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
APP="Citorio"
OUT=build
rm -rf "$OUT"
mkdir -p "$OUT"

for arch in arm64 x86_64; do
  if ! swiftc -O -target "$arch-apple-macos11" -o "$OUT/Citorio-$arch" main.swift 2> "$OUT/swiftc.log"; then
    # Some Command Line Tools installs can't link Intel; then build for Apple Silicon only (GitHub always builds both)
    if [ "$arch" = x86_64 ] && [ -z "${GITHUB_ACTIONS:-}" ]; then
      echo "Intel build unavailable here; building for Apple Silicon only."
      continue
    fi
    cat "$OUT/swiftc.log"
    if [ -n "${GITHUB_ACTIONS:-}" ]; then
      grep -E "error:" "$OUT/swiftc.log" | head -20 | while IFS= read -r line; do echo "::error::$line"; done
    fi
    exit 1
  fi
  cat "$OUT/swiftc.log"
done
lipo -create -output "$OUT/Citorio" "$OUT"/Citorio-arm64 $([ -f "$OUT/Citorio-x86_64" ] && echo "$OUT/Citorio-x86_64")

# The lists must read and a quote must draw
"$OUT/Citorio" --render "$OUT/quote-preview.png" ../quotes
echo "Quote drawn: $OUT/quote-preview.png"

C="$OUT/$APP.app/Contents"
mkdir -p "$C/MacOS" "$C/Resources/Quotes"
cp "$OUT/Citorio" "$C/MacOS/Citorio"
cp ../quotes/*.txt "$C/Resources/Quotes/"
cp ../../LICENSE "$C/Resources/LICENSE.txt"
"$OUT/Citorio" --write-iconset "$OUT/AppIcon.iconset"
iconutil -c icns "$OUT/AppIcon.iconset" -o "$C/Resources/AppIcon.icns"

cat > "$C/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP</string>
  <key>CFBundleDisplayName</key><string>$APP</string>
  <key>CFBundleExecutable</key><string>Citorio</string>
  <key>CFBundleIdentifier</key><string>net.ergosfera.citorio</string>
  <key>CFBundleIconFile</key><string>AppIcon</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>1.0</string>
  <key>CFBundleVersion</key><string>1</string>
  <key>NSHumanReadableCopyright</key><string>© 2026 Mauricio Mendivelso. MIT License.</string>
  <key>NSDocumentsFolderUsageDescription</key><string>Citorio keeps your quote lists in Documents/Citorio.</string>
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
hdiutil create -volname "$APP" -srcfolder "$OUT/dmg" -ov -format UDZO "$OUT/Citorio.dmg"
echo "Built $OUT/Citorio.dmg"
