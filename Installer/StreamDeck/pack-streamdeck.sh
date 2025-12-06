#!/bin/bash
# OnlyT StreamDeck Plugin Packaging Script
# This script packages the StreamDeck plugin for distribution

set -e

# Configuration
PLUGIN_ID="com.onlyt.timer"
PLUGIN_VERSION="2.4.0.14"

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PLUGIN_SRC="$PROJECT_ROOT/StreamDeck/$PLUGIN_ID.sdPlugin"
BUILD_DIR="$PROJECT_ROOT/dist/StreamDeck"

echo "=== OnlyT StreamDeck Plugin Packaging ==="
echo "Plugin: $PLUGIN_ID"
echo "Version: $PLUGIN_VERSION"
echo ""

# Check if plugin source exists
if [ ! -d "$PLUGIN_SRC" ]; then
    echo "Error: Plugin source not found at $PLUGIN_SRC"
    exit 1
fi

# Clean and create build directory
rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

# Method 1: Using Stream Deck CLI (if available)
if command -v streamdeck &> /dev/null; then
    echo "Using Stream Deck CLI to package plugin..."
    cd "$PROJECT_ROOT/StreamDeck"
    streamdeck pack "$PLUGIN_ID.sdPlugin" -o "$BUILD_DIR"

    # Rename to include version
    if [ -f "$BUILD_DIR/$PLUGIN_ID.streamDeckPlugin" ]; then
        mv "$BUILD_DIR/$PLUGIN_ID.streamDeckPlugin" "$BUILD_DIR/$PLUGIN_ID-$PLUGIN_VERSION.streamDeckPlugin"
    fi
else
    echo "Stream Deck CLI not found, using manual packaging..."

    # Method 2: Manual packaging (zip-based)
    # The .streamDeckPlugin format is essentially a renamed ZIP file

    cd "$PROJECT_ROOT/StreamDeck"

    # Create the plugin package
    zip -r "$BUILD_DIR/$PLUGIN_ID-$PLUGIN_VERSION.streamDeckPlugin" "$PLUGIN_ID.sdPlugin" \
        -x "*.DS_Store" \
        -x "*__MACOSX*" \
        -x "*.git*"

    echo ""
    echo "Note: Plugin was packaged manually."
    echo "For official distribution, install Stream Deck CLI:"
    echo "  npm install -g @elgato/cli"
    echo "  streamdeck pack $PLUGIN_ID.sdPlugin"
fi

echo ""
echo "=== Packaging Complete ==="
echo "Output: $BUILD_DIR/$PLUGIN_ID-$PLUGIN_VERSION.streamDeckPlugin"
echo ""
echo "Installation:"
echo "  Double-click the .streamDeckPlugin file to install"
echo "  Or copy $PLUGIN_ID.sdPlugin to your Stream Deck plugins folder:"
echo "    Windows: %APPDATA%\\Elgato\\StreamDeck\\Plugins\\"
echo "    macOS:   ~/Library/Application Support/com.elgato.StreamDeck/Plugins/"
echo "    Linux:   ~/.local/share/StreamDeck/Plugins/"
