# OnlyT.Avalonia - Cross-Platform Version

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

- .NET 8.0 SDK or later
- Platform-specific requirements:
  - **Windows**: No additional requirements
  - **macOS**: Xcode command line tools
  - **Linux**: GTK3 development libraries

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

- Uses the same data storage locations as the WPF version
- Supports Windows 10 and later
- DPI awareness enabled

### macOS

- Data stored in `~/Library/Application Support/OnlyT/`
- Lock files in temporary directory
- Supports macOS 10.15 (Catalina) and later

### Linux

- Configuration stored in `~/.config/OnlyT/` (or `$XDG_CONFIG_HOME/OnlyT/`)
- Lock files in `$XDG_RUNTIME_DIR` or `/tmp`
- Tested on Ubuntu, Fedora, and other common distributions
- Requires GTK3

## Differences from WPF Version

1. **UI Framework**: Uses Avalonia UI instead of WPF
2. **Platform APIs**: Abstracted through `IPlatformServices`
3. **Theming**: Uses Avalonia's Fluent theme system
4. **Monitor Detection**: Uses Avalonia's cross-platform screen API

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
