#!/bin/bash
set -e

APP_NAME="PixelThis"
BUNDLE="$APP_NAME.app"
PUBLISH_DIR="./publish"
DOTNET="/usr/local/share/dotnet/dotnet"

echo "Publishing..."
$DOTNET publish src/PixelThis/PixelThis.csproj \
  -r osx-arm64 \
  --self-contained true \
  -c Release \
  -o "$PUBLISH_DIR"

echo "Building .app bundle..."
rm -rf "$BUNDLE"
mkdir -p "$BUNDLE/Contents/MacOS"
mkdir -p "$BUNDLE/Contents/Resources"

cp -r "$PUBLISH_DIR"/* "$BUNDLE/Contents/MacOS/"

cat > "$BUNDLE/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>com.pixelthis.app</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleVersion</key>
    <string>1.0</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
EOF

echo "Installing to /Applications..."
rm -rf "/Applications/$BUNDLE"
cp -r "$BUNDLE" "/Applications/$BUNDLE"
rm -rf "$BUNDLE"

echo "Done! $APP_NAME is now in Launchpad."
