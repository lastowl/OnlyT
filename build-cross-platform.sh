#!/bin/bash

# Build script for OnlyT.Avalonia cross-platform versions
# This script builds the cross-platform version for Windows, macOS, and Linux

set -e

echo "======================================"
echo "OnlyT.Avalonia Cross-Platform Builder"
echo "======================================"
echo ""

PROJECT_DIR="OnlyT.Avalonia"
OUTPUT_DIR="dist"
CONFIG="Release"

# Clean previous builds
echo "Cleaning previous builds..."
rm -rf "$OUTPUT_DIR"
dotnet clean "$PROJECT_DIR/OnlyT.Avalonia.csproj" -c "$CONFIG"

# Create output directory
mkdir -p "$OUTPUT_DIR"

# Build for Windows x64
echo ""
echo "Building for Windows (x64)..."
dotnet publish "$PROJECT_DIR/OnlyT.Avalonia.csproj" \
    -c "$CONFIG" \
    -r win-x64 \
    --self-contained \
    -o "$OUTPUT_DIR/win-x64" \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true

# Build for macOS x64
echo ""
echo "Building for macOS (x64)..."
dotnet publish "$PROJECT_DIR/OnlyT.Avalonia.csproj" \
    -c "$CONFIG" \
    -r osx-x64 \
    --self-contained \
    -o "$OUTPUT_DIR/osx-x64" \
    -p:PublishSingleFile=true

# Build for macOS ARM64 (Apple Silicon)
echo ""
echo "Building for macOS (ARM64)..."
dotnet publish "$PROJECT_DIR/OnlyT.Avalonia.csproj" \
    -c "$CONFIG" \
    -r osx-arm64 \
    --self-contained \
    -o "$OUTPUT_DIR/osx-arm64" \
    -p:PublishSingleFile=true

# Build for Linux x64
echo ""
echo "Building for Linux (x64)..."
dotnet publish "$PROJECT_DIR/OnlyT.Avalonia.csproj" \
    -c "$CONFIG" \
    -r linux-x64 \
    --self-contained \
    -o "$OUTPUT_DIR/linux-x64" \
    -p:PublishSingleFile=true

# Create archives
echo ""
echo "Creating distribution archives..."

cd "$OUTPUT_DIR"

# Windows
echo "  - Windows archive..."
zip -r "OnlyT-Avalonia-Windows-x64.zip" win-x64/

# macOS x64
echo "  - macOS x64 archive..."
tar -czf "OnlyT-Avalonia-macOS-x64.tar.gz" osx-x64/

# macOS ARM64
echo "  - macOS ARM64 archive..."
tar -czf "OnlyT-Avalonia-macOS-ARM64.tar.gz" osx-arm64/

# Linux
echo "  - Linux archive..."
tar -czf "OnlyT-Avalonia-Linux-x64.tar.gz" linux-x64/

cd ..

echo ""
echo "======================================"
echo "Build completed successfully!"
echo "======================================"
echo ""
echo "Output directory: $OUTPUT_DIR/"
echo ""
echo "Distribution archives:"
echo "  - OnlyT-Avalonia-Windows-x64.zip"
echo "  - OnlyT-Avalonia-macOS-x64.tar.gz"
echo "  - OnlyT-Avalonia-macOS-ARM64.tar.gz"
echo "  - OnlyT-Avalonia-Linux-x64.tar.gz"
echo ""
