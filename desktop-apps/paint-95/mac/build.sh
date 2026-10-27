#!/bin/bash
# Builds "Paint 95.app" and Paint95.dmg (universal: Apple Silicon + Intel), a bitmap painter
# that looks and works like the Windows 95 Paint.
# First checks drawing and saving in every file format with --selftest.
# Needs a Mac with Xcode or the Command Line Tools (xcode-select --install). Usage: ./build.sh
set -euo pipefail
cd "$(dirname "$0")"
APP="Paint 95"
OUT=build
rm -rf "$OUT"
mkdir -p "$OUT"

if ! swiftc -O -target arm64-apple-macos11 -o "$OUT/Paint95-arm64" main.swift ../../win95-kit/Win95Kit.swift 2> "$OUT/swiftc.log"; then
  cat "$OUT/swiftc.log"
  if [ -n "${GITHUB_ACTIONS:-}" ]; then
    grep -E "error:" "$OUT/swiftc.log" | head -20 | while IFS= read -r line; do echo "::error::$line"; done
  fi
  exit 1
fi
# Some Command Line Tools installs can't link Intel; then build for Apple Silicon only (CI always builds both)
if swiftc -O -target x86_64-apple-macos11 -o "$OUT/Paint95-x86_64" main.swift ../../win95-kit/Win95Kit.swift 2> "$OUT/x86.log"; then
  lipo -create -output "$OUT/Paint95" "$OUT/Paint95-arm64" "$OUT/Paint95-x86_64"
elif [ -n "${GITHUB_ACTIONS:-}" ]; then
  cat "$OUT/x86.log"
  exit 1
else
  echo "Intel build unavailable here; building for Apple Silicon only."
  cp "$OUT/Paint95-arm64" "$OUT/Paint95"
fi

# Drawing, flood fill, flips and every file format must work
"$OUT/Paint95" --selftest

C="$OUT/$APP.app/Contents"
mkdir -p "$C/MacOS" "$C/Resources"
cp "$OUT/Paint95" "$C/MacOS/Paint95"
cp ../../LICENSE "$C/Resources/LICENSE.txt"
"$OUT/Paint95" --write-iconset "$OUT/AppIcon.iconset"
iconutil -c icns "$OUT/AppIcon.iconset" -o "$C/Resources/AppIcon.icns"

cat > "$C/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key><string>$APP</string>
  <key>CFBundleDisplayName</key><string>$APP</string>
  <key>CFBundleExecutable</key><string>Paint95</string>
  <key>CFBundleIdentifier</key><string>net.ergosfera.paint95</string>
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
      <key>CFBundleTypeName</key><string>Picture</string>
      <key>CFBundleTypeRole</key><string>Editor</string>
      <key>LSHandlerRank</key><string>Alternate</string>
      <key>LSItemContentTypes</key>
      <array><string>public.png</string><string>com.microsoft.bmp</string><string>public.jpeg</string><string>com.compuserve.gif</string><string>public.tiff</string><string>public.image</string></array>
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
hdiutil create -volname "$APP" -srcfolder "$OUT/dmg" -ov -format UDZO "$OUT/Paint95.dmg"
echo "Built $OUT/Paint95.dmg"
