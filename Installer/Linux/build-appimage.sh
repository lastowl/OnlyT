#!/bin/bash
# OnlyT Linux AppImage Build Script
# This script builds an AppImage for Linux distribution

set -e

# Configuration
APP_NAME="OnlyT"
APP_VERSION="2.4.0.14"
APP_ID="com.onlyt.timer"

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
BUILD_DIR="$PROJECT_ROOT/dist/Linux"
PUBLISH_DIR="$PROJECT_ROOT/publish/linux-x64"
APPDIR="$BUILD_DIR/$APP_NAME.AppDir"

echo "=== OnlyT Linux AppImage Build Script ==="
echo "Version: $APP_VERSION"
echo ""

# Check for required tools
if ! command -v dotnet &> /dev/null; then
    echo "Error: dotnet SDK not found. Please install .NET SDK."
    exit 1
fi

# Clean and create build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Step 1: Build the application
echo "Step 1: Building application..."
cd "$PROJECT_ROOT/OnlyT.Avalonia"
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -o "$PUBLISH_DIR"

# Step 2: Create AppDir structure
echo "Step 2: Creating AppDir structure..."
mkdir -p "$APPDIR/usr/bin"
mkdir -p "$APPDIR/usr/lib"
mkdir -p "$APPDIR/usr/share/applications"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$APPDIR/usr/share/metainfo"

# Copy application files
cp -R "$PUBLISH_DIR/"* "$APPDIR/usr/bin/"

# Step 3: Create desktop entry
echo "Step 3: Creating desktop entry..."
cat > "$APPDIR/usr/share/applications/$APP_ID.desktop" << EOF
[Desktop Entry]
Type=Application
Name=$APP_NAME
Comment=Meeting Timer Application
Exec=OnlyT
Icon=$APP_ID
Categories=Utility;Office;
Terminal=false
StartupWMClass=OnlyT
EOF

# Copy desktop file to AppDir root (required by AppImage)
cp "$APPDIR/usr/share/applications/$APP_ID.desktop" "$APPDIR/"

# Step 4: Create/copy icon
echo "Step 4: Setting up icon..."
if [ -f "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" ]; then
    cp "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/$APP_ID.png"
    cp "$PROJECT_ROOT/OnlyT.Avalonia/Assets/onlyt.png" "$APPDIR/$APP_ID.png"
else
    # Create a placeholder icon if none exists
    echo "Warning: No icon found, creating placeholder..."
    # Create a simple 256x256 PNG (requires ImageMagick)
    if command -v convert &> /dev/null; then
        convert -size 256x256 xc:navy -fill white -gravity center -pointsize 72 -annotate 0 "OT" "$APPDIR/usr/share/icons/hicolor/256x256/apps/$APP_ID.png"
        cp "$APPDIR/usr/share/icons/hicolor/256x256/apps/$APP_ID.png" "$APPDIR/$APP_ID.png"
    fi
fi

# Step 5: Create AppStream metadata
echo "Step 5: Creating AppStream metadata..."
cat > "$APPDIR/usr/share/metainfo/$APP_ID.appdata.xml" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<component type="desktop-application">
  <id>$APP_ID</id>
  <name>$APP_NAME</name>
  <summary>Meeting Timer Application</summary>
  <metadata_license>MIT</metadata_license>
  <project_license>MIT</project_license>
  <description>
    <p>OnlyT is a meeting timer application designed for timing talks and presentations.</p>
    <p>Features include:</p>
    <ul>
      <li>Multiple timer presets</li>
      <li>Countdown and stopwatch modes</li>
      <li>Visual and audio alerts</li>
      <li>Stream Deck integration</li>
      <li>Web API for remote control</li>
    </ul>
  </description>
  <url type="homepage">https://github.com/AntonyCorbett/OnlyT</url>
  <launchable type="desktop-id">$APP_ID.desktop</launchable>
  <provides>
    <binary>OnlyT</binary>
  </provides>
  <releases>
    <release version="$APP_VERSION" date="$(date +%Y-%m-%d)"/>
  </releases>
</component>
EOF

# Step 6: Create AppRun script
echo "Step 6: Creating AppRun script..."
cat > "$APPDIR/AppRun" << 'EOF'
#!/bin/bash
SELF=$(readlink -f "$0")
HERE=${SELF%/*}
export PATH="${HERE}/usr/bin/:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/lib/:${LD_LIBRARY_PATH}"

# Set XDG directories if not set
export XDG_DATA_HOME="${XDG_DATA_HOME:-$HOME/.local/share}"
export XDG_CONFIG_HOME="${XDG_CONFIG_HOME:-$HOME/.config}"

# Run the application
exec "${HERE}/usr/bin/OnlyT" "$@"
EOF
chmod +x "$APPDIR/AppRun"

# Make the main executable... executable
chmod +x "$APPDIR/usr/bin/OnlyT"

# Step 7: Download and run appimagetool
echo "Step 7: Creating AppImage..."

ARCH=$(uname -m)
APPIMAGETOOL="$BUILD_DIR/appimagetool-$ARCH.AppImage"

if [ ! -f "$APPIMAGETOOL" ]; then
    echo "Downloading appimagetool..."
    wget -q "https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-$ARCH.AppImage" -O "$APPIMAGETOOL"
    chmod +x "$APPIMAGETOOL"
fi

# Create the AppImage
cd "$BUILD_DIR"
ARCH=$ARCH "$APPIMAGETOOL" "$APPDIR" "$APP_NAME-$APP_VERSION-$ARCH.AppImage"

# Step 8: Also create a tar.gz for users who prefer that
echo "Step 8: Creating portable archive..."
cd "$BUILD_DIR"
mkdir -p "$APP_NAME-$APP_VERSION"
cp -R "$PUBLISH_DIR/"* "$APP_NAME-$APP_VERSION/"

# Add a simple run script
cat > "$APP_NAME-$APP_VERSION/run.sh" << 'EOF'
#!/bin/bash
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$SCRIPT_DIR/OnlyT" "$@"
EOF
chmod +x "$APP_NAME-$APP_VERSION/run.sh"

# Copy StreamDeck plugin
if [ -d "$PROJECT_ROOT/StreamDeck/com.onlyt.timer.sdPlugin" ]; then
    mkdir -p "$APP_NAME-$APP_VERSION/StreamDeck"
    cp -R "$PROJECT_ROOT/StreamDeck/com.onlyt.timer.sdPlugin" "$APP_NAME-$APP_VERSION/StreamDeck/"
fi

tar -czvf "$APP_NAME-$APP_VERSION-linux-x64.tar.gz" "$APP_NAME-$APP_VERSION"
rm -rf "$APP_NAME-$APP_VERSION"

echo ""
echo "=== Build Complete ==="
echo "AppImage: $BUILD_DIR/$APP_NAME-$APP_VERSION-$ARCH.AppImage"
echo "Portable: $BUILD_DIR/$APP_NAME-$APP_VERSION-linux-x64.tar.gz"
echo ""
echo "To run the AppImage:"
echo "  chmod +x $APP_NAME-$APP_VERSION-$ARCH.AppImage"
echo "  ./$APP_NAME-$APP_VERSION-$ARCH.AppImage"
