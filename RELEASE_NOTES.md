# OnlyT v2.4.0.14 Release Notes

## Cross-Platform Avalonia Port

This release marks a major milestone: OnlyT has been ported from WPF to Avalonia UI, enabling native support for macOS and Linux alongside Windows.

### New Features

- **Cross-Platform Support**: OnlyT now runs natively on Windows, macOS, and Linux
- **macOS Universal Binary**: Single DMG works on both Intel and Apple Silicon Macs
- **macOS Native Menu**: Proper About and Quit menu integration in the macOS menu bar
- **Linux AppImage**: Easy-to-use AppImage format with desktop integration
- **Stream Deck Plugin**: Included with all platform installers

### Platform Downloads

| Platform | File | Notes |
|----------|------|-------|
| Windows | `OnlyT-Setup-2.4.0.14.exe` | Installer with optional Stream Deck plugin |
| macOS (Universal) | `OnlyT-2.4.0.14-universal.dmg` | Intel + Apple Silicon |
| macOS (Intel) | `OnlyT-2.4.0.14-osx-x64.dmg` | Intel Macs only |
| macOS (Apple Silicon) | `OnlyT-2.4.0.14-osx-arm64.dmg` | M1/M2/M3 Macs only |
| Linux | `OnlyT-2.4.0.14-x86_64.AppImage` | AppImage format |
| Linux | `OnlyT-2.4.0.14-linux-x64.tar.gz` | Portable archive |
| Stream Deck | `com.onlyt.timer-2.4.0.14.streamDeckPlugin` | Standalone plugin |

### macOS Notes

- **Unsigned builds**: If you receive a Gatekeeper warning, right-click the app and select "Open" to bypass it
- **Signed/Notarized builds**: Will open without warnings (requires Apple Developer certificate during build)

### Linux Notes

- Make the AppImage executable: `chmod +x OnlyT-*.AppImage`
- Run directly: `./OnlyT-2.4.0.14-x86_64.AppImage`

### Technical Changes

- Migrated from WPF to Avalonia UI 11.x
- Material.Avalonia theme for consistent cross-platform appearance
- Self-contained .NET deployment (no runtime installation required)
- Hardened runtime support for macOS code signing

### Requirements

- **Windows**: Windows 10 or later (x64)
- **macOS**: macOS 10.14 Mojave or later
- **Linux**: x86_64 with glibc 2.17+ (most modern distributions)

---

For more information, visit: https://github.com/AntonyCorbett/OnlyT
