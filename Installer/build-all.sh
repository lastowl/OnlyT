#!/bin/bash
# OnlyT Unified Build Script
# This script orchestrates building installers for all platforms

set -e

# Configuration
APP_NAME="OnlyT"
APP_VERSION="2.4.0.16"

# Paths
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
DIST_DIR="$PROJECT_ROOT/dist"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Functions
print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Detect current platform
detect_platform() {
    case "$(uname -s)" in
        Darwin*)    echo "macos";;
        Linux*)     echo "linux";;
        MINGW*|MSYS*|CYGWIN*) echo "windows";;
        *)          echo "unknown";;
    esac
}

# Check prerequisites
check_prerequisites() {
    print_header "Checking Prerequisites"

    local missing=0

    # Check for .NET SDK
    if command -v dotnet &> /dev/null; then
        local dotnet_version=$(dotnet --version)
        print_success ".NET SDK found: $dotnet_version"
    else
        print_error ".NET SDK not found"
        missing=1
    fi

    # Platform-specific checks
    local platform=$(detect_platform)

    case "$platform" in
        macos)
            if command -v codesign &> /dev/null; then
                print_success "codesign available"
            else
                print_warning "codesign not found (required for signing)"
            fi
            if command -v hdiutil &> /dev/null; then
                print_success "hdiutil available"
            else
                print_error "hdiutil not found"
                missing=1
            fi
            ;;
        linux)
            if command -v wget &> /dev/null; then
                print_success "wget available"
            else
                print_warning "wget not found (needed for appimagetool download)"
            fi
            ;;
        windows)
            if command -v iscc &> /dev/null; then
                print_success "Inno Setup Compiler available"
            else
                print_warning "Inno Setup Compiler (iscc) not found"
                echo "  Install from: https://jrsoftware.org/isdl.php"
            fi
            ;;
    esac

    # Check for StreamDeck CLI (optional)
    if command -v streamdeck &> /dev/null; then
        print_success "Stream Deck CLI available"
    else
        print_warning "Stream Deck CLI not found (will use manual packaging)"
        echo "  Install with: npm install -g @elgato/cli"
    fi

    if [ $missing -eq 1 ]; then
        print_error "Missing required prerequisites"
        exit 1
    fi

    echo ""
}

# Build the .NET application for a specific platform
build_dotnet() {
    local runtime="$1"
    local output_dir="$2"

    echo "Building for $runtime..."
    cd "$PROJECT_ROOT/OnlyT.Avalonia"
    dotnet publish -c Release -r "$runtime" --self-contained true -p:PublishSingleFile=false -o "$output_dir"
    print_success "Built for $runtime"
}

# Build WPF Classic version for Windows (requires Windows SDK)
build_windows_wpf() {
    echo "Building WPF Classic version..."
    local wpf_publish_dir="$PROJECT_ROOT/publish/win-x64-wpf"

    cd "$PROJECT_ROOT"
    if dotnet publish OnlyT/OnlyT.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o "$wpf_publish_dir" 2>/dev/null; then
        print_success "Built WPF Classic version"
    else
        print_warning "WPF Classic build failed (requires Windows SDK - skipping)"
        print_warning "The installer will still work without the Classic version"
    fi
}

# Build for Windows
build_windows() {
    print_header "Building for Windows"

    local publish_dir="$PROJECT_ROOT/publish/win-x64"

    # Build Avalonia (cross-platform) version
    build_dotnet "win-x64" "$publish_dir"

    # Build WPF Classic version (optional - only works on Windows)
    build_windows_wpf

    # Check for Inno Setup
    if command -v iscc &> /dev/null; then
        echo "Running Inno Setup Compiler..."
        mkdir -p "$DIST_DIR/Windows"
        iscc "$SCRIPT_DIR/Windows/OnlyT-Setup.iss"
        print_success "Windows installer created"
    else
        print_warning "Inno Setup not found, creating portable ZIP instead"
        mkdir -p "$DIST_DIR/Windows"
        cd "$publish_dir"
        zip -r "$DIST_DIR/Windows/$APP_NAME-$APP_VERSION-win-x64-portable.zip" .

        # Copy StreamDeck plugin
        if [ -d "$PROJECT_ROOT/StreamDeck/com.onlyt.timer.sdPlugin" ]; then
            cd "$PROJECT_ROOT/StreamDeck"
            zip -r "$DIST_DIR/Windows/OnlyT-StreamDeck-Plugin.streamDeckPlugin" "com.onlyt.timer.sdPlugin"
        fi
        print_success "Windows portable ZIP created"
    fi
}

# Build for macOS
build_macos() {
    print_header "Building for macOS"

    local platform=$(detect_platform)

    if [ "$platform" != "macos" ]; then
        print_warning "macOS builds require running on macOS"
        print_warning "Skipping macOS build (cross-compilation not supported for signing/notarization)"
        return 0
    fi

    # Determine architecture
    local arch=$(uname -m)
    local runtime="osx-x64"
    if [ "$arch" = "arm64" ]; then
        runtime="osx-arm64"
    fi

    # Run the macOS build script
    if [ -f "$SCRIPT_DIR/macOS/build-macos.sh" ]; then
        echo "Running macOS build script..."
        chmod +x "$SCRIPT_DIR/macOS/build-macos.sh"
        RUNTIME_ID="$runtime" "$SCRIPT_DIR/macOS/build-macos.sh" || {
            print_warning "macOS build completed with warnings (signing/notarization may have failed)"
            print_warning "Check if you have configured your Apple Developer credentials"
        }
    else
        print_error "macOS build script not found"
        return 1
    fi
}

# Build for Linux
build_linux() {
    print_header "Building for Linux"

    local platform=$(detect_platform)

    if [ "$platform" != "linux" ]; then
        # Can still cross-compile, but won't be able to run appimagetool
        print_warning "Linux AppImage creation requires running on Linux"
        print_warning "Creating portable archive only..."

        local publish_dir="$PROJECT_ROOT/publish/linux-x64"
        build_dotnet "linux-x64" "$publish_dir"

        mkdir -p "$DIST_DIR/Linux"
        cd "$PROJECT_ROOT/publish"
        tar -czvf "$DIST_DIR/Linux/$APP_NAME-$APP_VERSION-linux-x64.tar.gz" "linux-x64"
        print_success "Linux portable archive created"
        return 0
    fi

    # Run the Linux build script
    if [ -f "$SCRIPT_DIR/Linux/build-appimage.sh" ]; then
        echo "Running Linux build script..."
        chmod +x "$SCRIPT_DIR/Linux/build-appimage.sh"
        "$SCRIPT_DIR/Linux/build-appimage.sh"
    else
        print_error "Linux build script not found"
        return 1
    fi
}

# Package StreamDeck plugin
build_streamdeck() {
    print_header "Packaging StreamDeck Plugin"

    if [ -f "$SCRIPT_DIR/StreamDeck/pack-streamdeck.sh" ]; then
        chmod +x "$SCRIPT_DIR/StreamDeck/pack-streamdeck.sh"
        "$SCRIPT_DIR/StreamDeck/pack-streamdeck.sh"
    else
        # Fallback to manual packaging
        if [ -d "$PROJECT_ROOT/StreamDeck/com.onlyt.timer.sdPlugin" ]; then
            mkdir -p "$DIST_DIR/StreamDeck"
            cd "$PROJECT_ROOT/StreamDeck"
            zip -r "$DIST_DIR/StreamDeck/com.onlyt.timer-$APP_VERSION.streamDeckPlugin" "com.onlyt.timer.sdPlugin" \
                -x "*.DS_Store" \
                -x "*__MACOSX*"
            print_success "StreamDeck plugin packaged"
        else
            print_warning "StreamDeck plugin source not found"
        fi
    fi
}

# Print usage
usage() {
    echo "OnlyT Build Script"
    echo ""
    echo "Usage: $0 [options] [platforms...]"
    echo ""
    echo "Platforms:"
    echo "  windows     Build Windows installer"
    echo "  macos       Build macOS DMG (requires macOS)"
    echo "  linux       Build Linux AppImage (requires Linux for AppImage)"
    echo "  streamdeck  Package StreamDeck plugin"
    echo "  all         Build for all platforms (default)"
    echo ""
    echo "Options:"
    echo "  -h, --help     Show this help message"
    echo "  -v, --version  Show version"
    echo "  --skip-check   Skip prerequisite checks"
    echo ""
    echo "Examples:"
    echo "  $0                  # Build for all platforms"
    echo "  $0 windows          # Build Windows only"
    echo "  $0 macos linux      # Build macOS and Linux"
    echo ""
}

# Main
main() {
    local skip_check=0
    local platforms=()

    # Parse arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            -h|--help)
                usage
                exit 0
                ;;
            -v|--version)
                echo "$APP_NAME Build Script v$APP_VERSION"
                exit 0
                ;;
            --skip-check)
                skip_check=1
                shift
                ;;
            windows|macos|linux|streamdeck|all)
                platforms+=("$1")
                shift
                ;;
            *)
                print_error "Unknown option: $1"
                usage
                exit 1
                ;;
        esac
    done

    # Default to all platforms
    if [ ${#platforms[@]} -eq 0 ]; then
        platforms=("all")
    fi

    # Expand "all" to individual platforms
    if [[ " ${platforms[*]} " =~ " all " ]]; then
        platforms=("windows" "macos" "linux" "streamdeck")
    fi

    print_header "OnlyT Build Script v$APP_VERSION"
    echo "Platforms to build: ${platforms[*]}"
    echo "Current platform: $(detect_platform)"

    # Check prerequisites
    if [ $skip_check -eq 0 ]; then
        check_prerequisites
    fi

    # Clean dist directory
    echo "Cleaning dist directory..."
    rm -rf "$DIST_DIR"
    mkdir -p "$DIST_DIR"

    # Build each platform
    local failed=()

    for platform in "${platforms[@]}"; do
        case $platform in
            windows)
                build_windows || failed+=("windows")
                ;;
            macos)
                build_macos || failed+=("macos")
                ;;
            linux)
                build_linux || failed+=("linux")
                ;;
            streamdeck)
                build_streamdeck || failed+=("streamdeck")
                ;;
        esac
    done

    # Summary
    print_header "Build Summary"

    echo "Output directory: $DIST_DIR"
    echo ""

    if [ -d "$DIST_DIR/Windows" ]; then
        echo "Windows:"
        ls -la "$DIST_DIR/Windows/" 2>/dev/null || echo "  (empty)"
    fi

    if [ -d "$DIST_DIR/macOS" ]; then
        echo "macOS:"
        ls -la "$DIST_DIR/macOS/" 2>/dev/null || echo "  (empty)"
    fi

    if [ -d "$DIST_DIR/Linux" ]; then
        echo "Linux:"
        ls -la "$DIST_DIR/Linux/" 2>/dev/null || echo "  (empty)"
    fi

    if [ -d "$DIST_DIR/StreamDeck" ]; then
        echo "StreamDeck:"
        ls -la "$DIST_DIR/StreamDeck/" 2>/dev/null || echo "  (empty)"
    fi

    echo ""

    if [ ${#failed[@]} -gt 0 ]; then
        print_warning "Some builds failed: ${failed[*]}"
        echo "Check the output above for details."
        exit 1
    else
        print_success "All builds completed successfully!"
    fi
}

# Run main
main "$@"
