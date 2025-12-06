#!/bin/bash
# OnlyT macOS Build and Notarization Script
# This script builds, optionally signs/notarizes, and creates a DMG for OnlyT

set -e

# Configuration
APP_NAME="OnlyT"
APP_VERSION="2.4.0.14"
BUNDLE_ID="com.onlyt.timer"

# Signing/Notarization Configuration (optional - leave empty to skip)
# To enable signing/notarization, set these environment variables or edit below:
DEVELOPER_ID_APP="${DEVELOPER_ID_APP:-}"  # "Developer ID Application: Your Name (TEAM_ID)"
APPLE_ID="${APPLE_ID:-}"                   # your-apple-id@example.com
TEAM_ID="${TEAM_ID:-}"                     # YOUR_TEAM_ID
APP_SPECIFIC_PASSWORD="${APP_SPECIFIC_PASSWORD:-}"  # xxxx-xxxx-xxxx-xxxx from appleid.apple.com

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/dist/macOS"
PUBLISH_DIR="$PROJECT_ROOT/publish/osx-x64"
APP_BUNDLE="$BUILD_DIR/$APP_NAME.app"
DMG_NAME="$APP_NAME-$APP_VERSION.dmg"

# Runtime identifiers for different architectures
# osx-x64 for Intel, osx-arm64 for Apple Silicon
RUNTIME_ID="${RUNTIME_ID:-osx-x64}"

# Determine if we can sign/notarize
CAN_SIGN=false
CAN_NOTARIZE=false

if [ -n "$DEVELOPER_ID_APP" ]; then
    CAN_SIGN=true
    if [ -n "$APPLE_ID" ] && [ -n "$TEAM_ID" ] && [ -n "$APP_SPECIFIC_PASSWORD" ]; then
        CAN_NOTARIZE=true
    fi
fi

echo "=== OnlyT macOS Build Script ==="
echo "Version: $APP_VERSION"
echo "Runtime: $RUNTIME_ID"
echo "Code Signing: $CAN_SIGN"
echo "Notarization: $CAN_NOTARIZE"
echo ""

if [ "$CAN_SIGN" = false ]; then
    echo "NOTE: Building WITHOUT code signing."
    echo "      Users will need to right-click > Open to bypass Gatekeeper."
    echo ""
    echo "To enable signing, set these environment variables:"
    echo "  export DEVELOPER_ID_APP=\"Developer ID Application: Your Name (TEAM_ID)\""
    echo ""
    echo "To enable notarization, also set:"
    echo "  export APPLE_ID=\"your-apple-id@example.com\""
    echo "  export TEAM_ID=\"YOUR_TEAM_ID\""
    echo "  export APP_SPECIFIC_PASSWORD=\"xxxx-xxxx-xxxx-xxxx\""
    echo ""
    echo "Requires a paid Apple Developer account ($99/year)"
    echo ""
fi

# Clean and create build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Step 1: Build the application
echo "Step 1: Building application..."
cd "$PROJECT_ROOT/OnlyT.Avalonia"
dotnet publish -c Release -r "$RUNTIME_ID" --self-contained true -p:PublishSingleFile=false -o "$PUBLISH_DIR"

# Step 2: Create app bundle structure
echo "Step 2: Creating app bundle..."
mkdir -p "$APP_BUNDLE/Contents/MacOS"
mkdir -p "$APP_BUNDLE/Contents/Resources"

# Copy binaries
cp -R "$PUBLISH_DIR/"* "$APP_BUNDLE/Contents/MacOS/"

# Create Info.plist
cat > "$APP_BUNDLE/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>OnlyT</string>
    <key>CFBundleIconFile</key>
    <string>onlyt.icns</string>
    <key>CFBundleIdentifier</key>
    <string>$BUNDLE_ID</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$APP_VERSION</string>
    <key>CFBundleVersion</key>
    <string>$APP_VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.14</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSHumanReadableCopyright</key>
    <string>Copyright 2024 OnlyT</string>
    <key>LSApplicationCategoryType</key>
    <string>public.app-category.utilities</string>
</dict>
</plist>
EOF

# Copy icon (convert from PNG if needed)
if [ -f "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.icns" ]; then
    cp "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.icns" "$APP_BUNDLE/Contents/Resources/"
elif [ -f "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" ]; then
    echo "Converting PNG to ICNS..."
    mkdir -p "$BUILD_DIR/icon.iconset"
    sips -z 16 16 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_16x16.png"
    sips -z 32 32 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_16x16@2x.png"
    sips -z 32 32 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_32x32.png"
    sips -z 64 64 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_32x32@2x.png"
    sips -z 128 128 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_128x128.png"
    sips -z 256 256 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_128x128@2x.png"
    sips -z 256 256 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_256x256.png"
    sips -z 512 512 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_256x256@2x.png"
    sips -z 512 512 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_512x512.png"
    sips -z 1024 1024 "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" --out "$BUILD_DIR/icon.iconset/icon_512x512@2x.png"
    iconutil -c icns "$BUILD_DIR/icon.iconset" -o "$APP_BUNDLE/Contents/Resources/onlyt.icns"
    rm -rf "$BUILD_DIR/icon.iconset"
fi

# Step 3: Code signing (optional)
if [ "$CAN_SIGN" = true ]; then
    echo "Step 3: Code signing..."

    # Create entitlements file for hardened runtime
    cat > "$BUILD_DIR/entitlements.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <true/>
    <key>com.apple.security.cs.allow-unsigned-executable-memory</key>
    <true/>
    <key>com.apple.security.cs.disable-library-validation</key>
    <true/>
    <key>com.apple.security.automation.apple-events</key>
    <true/>
    <key>com.apple.security.network.client</key>
    <true/>
    <key>com.apple.security.network.server</key>
    <true/>
</dict>
</plist>
EOF

    # Sign all executables and dylibs first
    find "$APP_BUNDLE" -type f \( -name "*.dylib" -o -perm +111 \) -exec \
        codesign --force --options runtime --timestamp \
        --entitlements "$BUILD_DIR/entitlements.plist" \
        --sign "$DEVELOPER_ID_APP" {} \;

    # Sign the app bundle
    codesign --force --deep --options runtime --timestamp \
        --entitlements "$BUILD_DIR/entitlements.plist" \
        --sign "$DEVELOPER_ID_APP" "$APP_BUNDLE"

    # Verify signature
    echo "Verifying signature..."
    codesign --verify --verbose "$APP_BUNDLE"
    spctl --assess --type execute --verbose "$APP_BUNDLE" || echo "Note: spctl assessment may fail before notarization"
else
    echo "Step 3: Skipping code signing (no Developer ID configured)"
fi

# Step 4: Create DMG
echo "Step 4: Creating DMG..."
# Create temporary DMG folder
DMG_TEMP="$BUILD_DIR/dmg_temp"
mkdir -p "$DMG_TEMP"
cp -R "$APP_BUNDLE" "$DMG_TEMP/"

# Copy StreamDeck plugin to DMG
if [ -d "$PROJECT_ROOT/StreamDeck/com.onlyt.timer.sdPlugin" ]; then
    # Package the StreamDeck plugin
    cd "$PROJECT_ROOT/StreamDeck"
    zip -r "$DMG_TEMP/OnlyT-StreamDeck-Plugin.streamDeckPlugin" "com.onlyt.timer.sdPlugin"
fi

# Create Applications symlink
ln -s /Applications "$DMG_TEMP/Applications"

# Create README with appropriate instructions
if [ "$CAN_SIGN" = true ]; then
    INSTALL_NOTE=""
else
    INSTALL_NOTE="
NOTE: This app is not signed with an Apple Developer certificate.
To open it for the first time:
1. Right-click (or Control-click) on OnlyT.app
2. Select 'Open' from the context menu
3. Click 'Open' in the dialog that appears
"
fi

cat > "$DMG_TEMP/README.txt" << EOF
OnlyT - Meeting Timer Application
Version $APP_VERSION

Installation:
1. Drag OnlyT.app to the Applications folder
2. Double-click OnlyT-StreamDeck-Plugin.streamDeckPlugin to install the Stream Deck plugin (optional)
$INSTALL_NOTE
For more information, visit: https://github.com/AntonyCorbett/OnlyT
EOF

# Create DMG
hdiutil create -volname "$APP_NAME $APP_VERSION" \
    -srcfolder "$DMG_TEMP" \
    -ov -format UDZO \
    "$BUILD_DIR/$DMG_NAME"

rm -rf "$DMG_TEMP"

# Step 5: Sign DMG (optional)
if [ "$CAN_SIGN" = true ]; then
    echo "Step 5: Signing DMG..."
    codesign --force --sign "$DEVELOPER_ID_APP" "$BUILD_DIR/$DMG_NAME"
else
    echo "Step 5: Skipping DMG signing"
fi

# Step 6: Notarize (optional)
if [ "$CAN_NOTARIZE" = true ]; then
    echo "Step 6: Submitting for notarization..."
    echo "This may take several minutes..."

    xcrun notarytool submit "$BUILD_DIR/$DMG_NAME" \
        --apple-id "$APPLE_ID" \
        --team-id "$TEAM_ID" \
        --password "$APP_SPECIFIC_PASSWORD" \
        --wait

    # Step 7: Staple the ticket
    echo "Step 7: Stapling notarization ticket..."
    xcrun stapler staple "$BUILD_DIR/$DMG_NAME"

    # Verify final result
    echo ""
    echo "Verifying notarization..."
    spctl --assess --type open --context context:primary-signature --verbose "$BUILD_DIR/$DMG_NAME"
else
    echo "Step 6: Skipping notarization"
    echo "Step 7: Skipping stapling"
fi

echo ""
echo "=== Build Complete ==="
echo "DMG: $BUILD_DIR/$DMG_NAME"
echo ""

if [ "$CAN_SIGN" = false ]; then
    echo "IMPORTANT: This build is NOT signed or notarized."
    echo "Users will see a warning from macOS Gatekeeper."
    echo "They can bypass this by right-clicking the app and selecting 'Open'."
    echo ""
fi

echo "Done!"
