#!/bin/bash
# OnlyT StreamDeck Plugin Packaging Script
# Packages StreamDeck/com.onlyt.timer.sdPlugin as dist/StreamDeck/com.onlyt.timer-<version>.streamDeckPlugin,
# with the plugin version set from SolutionInfo.cs. Prints the package path on its last line.

set -euo pipefail

# Configuration
PLUGIN_ID="com.onlyt.timer"
# Single source of truth: derive the version from SolutionInfo.cs (avoids per-platform drift).
PLUGIN_VERSION="$(sed -n 's/.*AssemblyVersion("\([0-9.]*\)").*/\1/p' "$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)/SolutionInfo.cs" | head -1)"
if [ -z "$PLUGIN_VERSION" ]; then
    echo "Error: could not read AssemblyVersion from SolutionInfo.cs"
    exit 1
fi

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PLUGIN_SRC="$PROJECT_ROOT/StreamDeck/$PLUGIN_ID.sdPlugin"
BUILD_DIR="$PROJECT_ROOT/dist/StreamDeck"
PACKAGE="$BUILD_DIR/$PLUGIN_ID-$PLUGIN_VERSION.streamDeckPlugin"

echo "=== OnlyT StreamDeck Plugin Packaging ==="
echo "Plugin: $PLUGIN_ID"
echo "Version: $PLUGIN_VERSION"
echo ""

if [ ! -d "$PLUGIN_SRC" ]; then
    echo "Error: Plugin source not found at $PLUGIN_SRC"
    exit 1
fi

# Stage a copy so the version can be stamped into the manifest without touching the source
STAGE_DIR="$(mktemp -d)"
trap 'rm -rf "$STAGE_DIR"' EXIT
COPYFILE_DISABLE=1 cp -R "$PLUGIN_SRC" "$STAGE_DIR/"
find "$STAGE_DIR" -name '.DS_Store' -delete
# Only the top-level "Version" (4-space indent), not Nodejs.Version
sed -i.bak -E "s/^    \"Version\": *\"[^\"]*\"/    \"Version\": \"$PLUGIN_VERSION\"/" "$STAGE_DIR/$PLUGIN_ID.sdPlugin/manifest.json"
rm "$STAGE_DIR/$PLUGIN_ID.sdPlugin/manifest.json.bak"
grep -q "^    \"Version\": \"$PLUGIN_VERSION\"" "$STAGE_DIR/$PLUGIN_ID.sdPlugin/manifest.json" || {
    echo "Error: could not set the version in manifest.json"
    exit 1
}

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR"

if command -v streamdeck &> /dev/null; then
    # The official CLI validates the plugin as it packs it
    echo "Using Stream Deck CLI to package plugin..."
    streamdeck pack "$STAGE_DIR/$PLUGIN_ID.sdPlugin" --output "$BUILD_DIR" --force
    mv "$BUILD_DIR/$PLUGIN_ID.streamDeckPlugin" "$PACKAGE"
else
    # A .streamDeckPlugin is a zip with the plugin folder at its root
    echo "Stream Deck CLI not found, packaging with zip (install it with: npm install -g @elgato/cli)"
    (cd "$STAGE_DIR" && zip -qr "$PACKAGE" "$PLUGIN_ID.sdPlugin")
fi

echo ""
echo "=== Packaging Complete ==="
echo "Double-click the package to install it in Stream Deck (7.1 or later)."
echo "$PACKAGE"
