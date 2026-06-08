# OnlyT Build & Release Instructions

This document describes how to build, maintain, and release OnlyT for all platforms.

## Project Overview

OnlyT is a meeting timer application with both WPF (Windows-only) and Avalonia (cross-platform) versions. This fork maintains the Avalonia port for macOS, Linux, and Windows.

- **Upstream**: https://github.com/AntonyCorbett/OnlyT
- **Fork**: https://github.com/lastowl/OnlyT
- **Branch**: `avalonia-port`

## Prerequisites

- .NET SDK 10.0+
- macOS (for signing/notarizing Mac builds)
- CrossOver with Inno Setup installed (for Windows installer on Mac)
- GitHub CLI (`gh`) for creating releases

## Quick Start - Full Release

```bash
# 1. Load build secrets
source .build-secrets

# 2. Set version
VERSION="2.4.0.17"

# 3. Update version in all files (see Version Update section)

# 4. Run full build (see Full Release Build section)

# 5. Create GitHub release (see GitHub Release section)
```

---

## Version Update

Before building a release, update the version in these files:

### SolutionInfo.cs (main version)
```bash
# File: SolutionInfo.cs
[assembly: AssemblyVersion("2.4.0.16")]
```

### Build Scripts
```bash
# File: Installer/build-all.sh
APP_VERSION="2.4.0.16"

# File: Installer/macOS/build-macos.sh
APP_VERSION="2.4.0.16"
```

### Windows Installer
```bash
# File: Installer/Windows/OnlyT-Setup.iss
#define MyAppVersion "2.4.0.16"
#define MyAppURL "https://github.com/lastowl/OnlyT"
```

### Quick sed commands to update all at once:
```bash
VERSION="2.4.0.17"

sed -i '' "s/AssemblyVersion(\".*\")/AssemblyVersion(\"${VERSION}\")/" SolutionInfo.cs
sed -i '' "s/APP_VERSION=\".*\"/APP_VERSION=\"${VERSION}\"/" Installer/build-all.sh
sed -i '' "s/APP_VERSION=\".*\"/APP_VERSION=\"${VERSION}\"/" Installer/macOS/build-macos.sh
sed -i '' "s/#define MyAppVersion \".*\"/#define MyAppVersion \"${VERSION}\"/" Installer/Windows/OnlyT-Setup.iss
```

---

## Build Commands

### Load Secrets (Required for macOS signing)

```bash
source .build-secrets
```

This loads:
- `DEVELOPER_ID_APP` - Apple Developer certificate
- `APPLE_ID` - Apple ID email
- `TEAM_ID` - Apple Developer Team ID
- `APP_SPECIFIC_PASSWORD` - App-specific password for notarization

### macOS Universal DMG (Signed & Notarized)

```bash
export BUILD_TYPE="universal"
./Installer/macOS/build-macos.sh
```

**Output**: `dist/macOS/OnlyT-{version}-universal.dmg`

**What it does**:
1. Builds for both osx-x64 and osx-arm64
2. Creates universal binaries using `lipo`
3. Creates app bundle with Info.plist and icon
4. Signs all binaries with Developer ID
5. Creates DMG with app and StreamDeck plugin
6. Submits to Apple for notarization
7. Staples notarization ticket to DMG

### Linux x64

```bash
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=false -o publish/linux-x64

mkdir -p dist/Linux
cd publish && tar -czvf ../dist/Linux/OnlyT-${VERSION}-linux-x64.tar.gz linux-x64 && cd ..
```

**Output**: `dist/Linux/OnlyT-{version}-linux-x64.tar.gz`

### Linux ARM64

```bash
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj \
  -c Release -r linux-arm64 --self-contained true \
  -p:PublishSingleFile=false -o publish/linux-arm64

cd publish && tar -czvf ../dist/Linux/OnlyT-${VERSION}-linux-arm64.tar.gz linux-arm64 && cd ..
```

**Output**: `dist/Linux/OnlyT-{version}-linux-arm64.tar.gz`

### Windows Portable

```bash
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=false -o publish/win-x64

mkdir -p dist/Windows
cd publish && zip -r ../dist/Windows/OnlyT-${VERSION}-win-x64-portable.zip win-x64 && cd ..
```

**Output**: `dist/Windows/OnlyT-{version}-win-x64-portable.zip`

### Windows ARM64 Portable

```bash
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj \
  -c Release -r win-arm64 --self-contained true \
  -p:PublishSingleFile=false -o publish/win-arm64

mkdir -p dist/Windows
cd publish && zip -r ../dist/Windows/OnlyT-${VERSION}-win-arm64-portable.zip win-arm64 && cd ..
```

**Output**: `dist/Windows/OnlyT-{version}-win-arm64-portable.zip`

### Windows Installer (via CrossOver)

Requires Inno Setup installed in CrossOver bottle "Test".

```bash
/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine --bottle Test \
  "$HOME/Library/Application Support/CrossOver/Bottles/Test/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe" \
  "Z:$(pwd)/Installer/Windows/OnlyT-Setup.iss"
```

**Output**: `dist/Windows/OnlyT-Setup-{version}.exe`

### StreamDeck Plugin

```bash
cd StreamDeck
zip -r ../dist/com.onlyt.timer-${VERSION}.streamDeckPlugin com.onlyt.timer.sdPlugin -x "*.DS_Store"
cd ..
```

**Output**: `dist/com.onlyt.timer-{version}.streamDeckPlugin`

---

## Full Release Build (All Platforms)

```bash
# Load secrets
source .build-secrets

# Set version
VERSION="2.4.0.16"

# Clean previous builds
rm -rf dist publish

# 1. macOS (signed & notarized) - ~3-5 minutes
export BUILD_TYPE="universal"
./Installer/macOS/build-macos.sh

# 2. Linux x64
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=false -o publish/linux-x64
mkdir -p dist/Linux
cd publish && tar -czvf ../dist/Linux/OnlyT-${VERSION}-linux-x64.tar.gz linux-x64 && cd ..

# 3. Linux ARM64
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r linux-arm64 --self-contained true -p:PublishSingleFile=false -o publish/linux-arm64
cd publish && tar -czvf ../dist/Linux/OnlyT-${VERSION}-linux-arm64.tar.gz linux-arm64 && cd ..

# 4. Windows portable (x64)
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64
mkdir -p dist/Windows
cd publish && zip -r ../dist/Windows/OnlyT-${VERSION}-win-x64-portable.zip win-x64 && cd ..

# 4b. Windows portable (ARM64)
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=false -o publish/win-arm64
cd publish && zip -r ../dist/Windows/OnlyT-${VERSION}-win-arm64-portable.zip win-arm64 && cd ..

# 5. Windows installer (via CrossOver)
/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine --bottle Test \
  "$HOME/Library/Application Support/CrossOver/Bottles/Test/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe" \
  "Z:$(pwd)/Installer/Windows/OnlyT-Setup.iss"

# 6. StreamDeck plugin
cd StreamDeck && zip -r ../dist/com.onlyt.timer-${VERSION}.streamDeckPlugin com.onlyt.timer.sdPlugin -x "*.DS_Store" && cd ..

# 7. Verify all artifacts
ls -lh dist/macOS/*.dmg dist/Linux/*.tar.gz dist/Windows/*.exe dist/Windows/*.zip dist/*.streamDeckPlugin
# (Windows ZIPs: both win-x64-portable.zip and win-arm64-portable.zip)
```

---

## GitHub Release

```bash
VERSION="2.4.0.16"

gh release create v${VERSION} \
  --repo lastowl/OnlyT \
  --title "OnlyT v${VERSION} - Cross-Platform Avalonia Port" \
  --notes "$(cat <<'EOF'
# OnlyT v${VERSION} Release Notes

## Changes in this Release

- [List changes here]

### Platform Downloads

| Platform | File | Notes |
|----------|------|-------|
| Windows | OnlyT-Setup-${VERSION}.exe | Installer with optional Stream Deck plugin |
| Windows | OnlyT-${VERSION}-win-x64-portable.zip | Portable archive (x64) |
| Windows (ARM64) | OnlyT-${VERSION}-win-arm64-portable.zip | Portable archive for ARM (Surface, Snapdragon, etc.) |
| macOS (Universal) | OnlyT-${VERSION}-universal.dmg | Intel + Apple Silicon, signed & notarized |
| Linux (x64) | OnlyT-${VERSION}-linux-x64.tar.gz | Portable archive |
| Linux (ARM64) | OnlyT-${VERSION}-linux-arm64.tar.gz | Portable archive for ARM |
| Stream Deck | com.onlyt.timer-${VERSION}.streamDeckPlugin | Stream Deck plugin |

### Requirements
- **Windows**: Windows 10 or later (x64)
- **macOS**: macOS 10.14 Mojave or later
- **Linux**: x64 or ARM64 with glibc 2.17+

---
For more information: https://github.com/lastowl/OnlyT
EOF
)" \
  dist/Windows/OnlyT-Setup-${VERSION}.exe \
  dist/macOS/OnlyT-${VERSION}-universal.dmg \
  dist/Linux/OnlyT-${VERSION}-linux-x64.tar.gz \
  dist/Linux/OnlyT-${VERSION}-linux-arm64.tar.gz \
  dist/Windows/OnlyT-${VERSION}-win-x64-portable.zip \
  dist/Windows/OnlyT-${VERSION}-win-arm64-portable.zip \
  dist/com.onlyt.timer-${VERSION}.streamDeckPlugin
```

---

## Syncing with Upstream

The upstream repo (AntonyCorbett/OnlyT) receives translation updates and bug fixes. Periodically sync these changes.

### Add Upstream Remote (one-time)

```bash
git remote add upstream https://github.com/AntonyCorbett/OnlyT.git
```

### Fetch and Merge Upstream

```bash
git fetch upstream master
git merge upstream/master -m "Merge upstream/master: [description of changes]"
```

### Resolve Conflicts

- **WPF translation files** (`OnlyT/Properties/*.resx`): Usually take upstream (theirs)
- **Avalonia translation files** (`OnlyT.Avalonia/Properties/*.resx`): Keep ours for Avalonia-specific keys
- **Code conflicts**: Review carefully

```bash
# Take upstream for WPF files
git checkout --theirs OnlyT/Properties/Resources.*.resx

# Keep ours for Avalonia files
git checkout --ours OnlyT.Avalonia/Properties/Resources.*.resx

# Add resolved files
git add .
git commit
```

### Sync Avalonia Translations from WPF

After merging upstream, sync updated translations to Avalonia:

```python
# Create sync script or manually update translation values
# Key mapping: WPF locale -> Avalonia locale
# e.g., sl-SI -> sl, ro-RO -> ro-RO
```

Languages to sync: Romanian, Slovenian, Spanish, Portuguese, Italian, Polish (and any others updated upstream)

---

## UI Maintenance

### Spacing Reference (OperatorPage.axaml)

To match the original WPF layout:

| Element | Value |
|---------|-------|
| Main Grid Margin | `10` (uniform) |
| Timer Border Padding | `10` (uniform) |
| Bell Icon Column Width | `15` |
| +/- Button Margins | `0,0,0,2` (top) and `0,2,0,0` (bottom) |
| +/- Icon Size | `24x24` |

---

## CrossOver/Inno Setup Setup

### Install Inno Setup in CrossOver (one-time)

```bash
# Download Inno Setup
curl -L -o /tmp/innosetup.exe "https://jrsoftware.org/download.php/is.exe"

# Install in CrossOver bottle "Test"
/Applications/CrossOver.app/Contents/SharedSupport/CrossOver/bin/wine --bottle Test \
  /tmp/innosetup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-
```

### Verify Installation

```bash
ls "$HOME/Library/Application Support/CrossOver/Bottles/Test/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe"
```

---

## Supported Platforms

### Windows
- Windows 10 or later (x64)

### macOS
- macOS 10.14 Mojave or later
- Universal binary (Intel + Apple Silicon)
- Signed and notarized

### Linux (glibc 2.17+)

**x64**:
- Ubuntu 18.04+
- Debian 10+
- Fedora 33+
- CentOS/RHEL 7+
- openSUSE Leap 15+
- Arch Linux
- Most modern distros

**ARM64**:
- Ubuntu 20.04+ ARM
- Debian 11+ ARM
- Raspberry Pi OS (64-bit)
- Fedora ARM

---

## Troubleshooting

### macOS Notarization Fails

1. Check Apple Developer account is active
2. Verify app-specific password is correct
3. Check certificate is valid: `security find-identity -v -p codesigning`

### CrossOver Wine Errors

1. Ensure bottle "Test" exists
2. Check Inno Setup is installed in the bottle
3. Use full paths with `Z:` drive prefix for Mac paths

### Build Errors

1. Clean and rebuild: `dotnet clean && dotnet build`
2. Check .NET SDK version: `dotnet --version` (need 10.0+)
3. Restore packages: `dotnet restore`

---

## File Locations

| File | Purpose |
|------|---------|
| `SolutionInfo.cs` | Main assembly version |
| `Installer/build-all.sh` | Unified build script |
| `Installer/macOS/build-macos.sh` | macOS build + signing |
| `Installer/Windows/OnlyT-Setup.iss` | Windows installer script |
| `OnlyT.Avalonia/Views/OperatorPage.axaml` | Main UI layout |
| `OnlyT.Avalonia/Properties/Resources.*.resx` | Avalonia translations |
| `OnlyT/Properties/Resources.*.resx` | WPF translations |
| `.build-secrets` | Build credentials (not committed) |
