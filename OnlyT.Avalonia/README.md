# OnlyT.Avalonia - Cross-Platform Version

> This is part of the [OnlyT fork](https://github.com/lastowl/OnlyT) which extends the original with cross-platform support and additional features.

This is the cross-platform version of OnlyT Meeting Timer built with Avalonia UI. It supports Windows, macOS, and Linux while the original OnlyT project remains a Windows-only WPF application.

## Project Structure

```
OnlyT.Avalonia/
├── Platform/              # Platform-specific implementations
│   ├── Windows/          # Windows-specific code
│   ├── MacOS/            # macOS-specific code
│   └── Linux/            # Linux-specific code
├── Views/                # Avalonia UI views (.axaml files)
├── Assets/               # Images, icons, and resources
├── App.axaml             # Application definition
├── App.axaml.cs          # Application code-behind
└── Program.cs            # Entry point
```

## Architecture

### Platform Abstraction

The project uses a platform abstraction layer defined in `OnlyT.Core` that includes:

- **IMonitorService**: Multi-monitor detection and management
- **IWindowService**: Window placement, always-on-top, and window management
- **ISystemIntegration**: System-level features (dark mode, icons, single instance)

Each platform (Windows, macOS, Linux) provides its own implementation of these interfaces.

### Code Sharing

Most business logic is shared from the original `OnlyT` project using file linking in the `.csproj`:

- Services (Timer, Bell, Options, Schedule, etc.)
- Models
- ViewModels
- Utilities (except Windows-specific native methods)

This approach allows the Windows WPF version to remain untouched while enabling cross-platform support.

## Building

### Prerequisites

- .NET 10.0 SDK or later
- Platform-specific requirements:
  - **Windows**: No additional requirements
  - **macOS**: Xcode command line tools
  - **Linux**: No additional requirements (Avalonia 11 uses X11/Wayland directly)

### Build Commands

```bash
# Build for current platform
dotnet build OnlyT.Avalonia/OnlyT.Avalonia.csproj

# Publish for Windows
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-x64 --self-contained

# Publish for macOS
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r osx-x64 --self-contained

# Publish for Linux
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r linux-x64 --self-contained
```

## Platform-Specific Notes

### Windows

- Settings stored in `options.avalonia.json` (separate from WPF's `options.json`; first-run seeds from WPF if present)
- Supports Windows 10 and later
- DPI awareness enabled

### macOS

- Data stored in `~/Library/Application Support/OnlyT/`
- Lock files in temporary directory
- Supports macOS 10.14 (Mojave) and later

### Linux

- Configuration stored in `~/.config/OnlyT/` (or `$XDG_CONFIG_HOME/OnlyT/`)
- Lock files in `$XDG_RUNTIME_DIR` or `/tmp`
- Tested on Ubuntu, Fedora, and other common distributions
- Avalonia 11 uses X11/Wayland directly (no GTK3 dependency)

## Differences from WPF Version

1. **UI Framework**: Uses Avalonia UI with Material Design theming instead of WPF
2. **Platform APIs**: Abstracted through `IPlatformServices`
3. **Theming**: Material.Avalonia with runtime dark/light switching
4. **Monitor Detection**: Uses Avalonia's cross-platform screen API
5. **Settings**: All settings apply live without restart; stored in separate `options.avalonia.json`
6. **Classic UI mode**: Optional toggle to match the original WPF operator page layout
7. **Stream Deck plugin**: Bundled plugin for Elgato Stream Deck hardware control
8. **Side-by-side install**: Can coexist with the original WPF OnlyT on the same machine

## Development

When making changes:

1. Keep platform-specific code in the `Platform/` directories
2. Shared business logic should go in the linked files from `OnlyT` project
3. UI views in `.axaml` files use Avalonia XAML syntax (similar but not identical to WPF)
4. Test on all platforms before releasing

## Future Work

- Port remaining UI views from WPF to Avalonia
- Implement full feature parity with WPF version
- Add platform-specific packaging (MSI, DMG, DEB/RPM)
- Improve dark mode support across platforms
