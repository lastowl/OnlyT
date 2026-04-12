# OnlyT (Cross-Platform Fork)

> **This is a fork of [OnlyT by Antony Corbett](https://github.com/AntonyCorbett/OnlyT)**. The original project is an excellent Windows-only meeting timer. This fork extends it with cross-platform support, live settings, and removes some of the restrictions present in the original version.

A cross-platform meeting timer built with C# and Avalonia UI (with the original WPF version still included). Features both analogue and digital clock displays. Designed for use in Kingdom Halls where the meeting format is predefined, but includes "manual" and "file-based" modes that can be used to configure timers as required for other settings.

### Key Differences from Original
* **Cross-platform support** - Runs on Windows, macOS, and Linux
* **All settings apply live** - Every setting takes effect immediately without restarting the application
* **Live language switching** - Language can be changed without restarting; 140+ languages supported
* **Full dark mode** - Entire application UI themes dark, not just title bars
* **Classic UI mode** - Optional toggle to match the original WPF operator page layout
* **Configurable countdown duration** - Pre-meeting countdown is no longer fixed at 5 minutes
* **Countdown on web clock** - Pre-meeting countdown can be displayed on the web clock
* **Stream Deck plugin** - Control timers and bell from an Elgato Stream Deck
* **Side-by-side install** - Can be installed alongside the original WPF OnlyT without conflict (separate install directory, config file, and mutex)
* **Maintained separately** - This fork may diverge from the upstream project

> **Note:** The screenshots below show the original WPF version. The Avalonia version uses Material Design theming and has a slightly different visual style.

![Main Window](http://cv8.org.uk/soundbox/OnlyT/Images/MainWindow2.png)

![Timer Display](http://cv8.org.uk/soundbox/OnlyT/Images/Monitor02.png)

### System Requirements

**Windows (WPF Version):**
* Windows 10
* 2GB RAM
* 20MB Hard disk space
* Internet connection (for "Automatic" Operating Mode only)

**Cross-Platform (Avalonia Version):**
* Windows 10 or later / macOS 10.14 or later / Linux with X11 or Wayland
* .NET 10.0 runtime (included in self-contained builds)
* 2GB RAM
* 30MB Hard disk space
* Internet connection (for "Automatic" Operating Mode only)

### Cross-Platform Support

OnlyT includes a cross-platform version built with Avalonia UI that runs on **Windows, macOS, and Linux**.

The original Windows WPF version (`OnlyT` project) remains included. The cross-platform version (`OnlyT.Avalonia` project) provides the same core functionality across all platforms while adding features not present in the WPF version (live settings, dark mode, classic UI toggle, Stream Deck plugin).

**Building the Cross-Platform Version:**

```bash
# Full release build (all platforms)
./Installer/build-all.sh

# macOS only (signed + notarized universal binary)
./Installer/macOS/build-macos.sh

# Or build directly with dotnet
dotnet build OnlyT.Avalonia/OnlyT.Avalonia.csproj
```

See [CLAUDE.md](CLAUDE.md) for detailed build and release instructions.

### Configuration

The Avalonia version stores its settings in `options.avalonia.json` (separate from the WPF `options.json`). On first launch, if an existing WPF options file is found, settings are automatically migrated. Unknown fields from either version are safely ignored.

### Download

Download the latest release from this fork's [Releases page](https://github.com/lastowl/OnlyT/releases/latest).

For the original Windows-only version, see the [original OnlyT releases](https://github.com/AntonyCorbett/OnlyT/releases/latest).

### Help

See the [original wiki](https://github.com/AntonyCorbett/OnlyT/wiki) for basic instructions and for information on where to get further help.

See the [FAQ](https://github.com/AntonyCorbett/OnlyT/wiki/FAQ) for frequently asked questions.

### License, etc

This fork is based on OnlyT, Copyright &copy; 2018, 2024 Antony Corbett and other contributors under the [MIT license](LICENSE). Fork modifications are also released under the MIT license.

NAudio (Mark Heath) is used under the Microsoft Public License (Ms-PL). MaterialDesign themes (James Willock, Mulholland Software and Contributors) is used under the MIT license. NUglify, Copyright (c) 2016, Alexandre Mutel. QRCode (Raffael Herrmann) is used under MIT. Serilog is used under the Apache License Version 2.0, January 2004. LiteDB (Mauricio David) is used under the MIT License. PDFSharp is used under the MIT License.

With thanks to:
* [Antony Corbett](https://github.com/AntonyCorbett) for creating the original OnlyT project
* The original OnlyT translators and [Crowdin.com](https://crowdin.com) contributors from the upstream project
* GitHub for project management tools
* [JetBrains](https://jb.gg/OpenSourceSupport) for Resharper tools

This fork has expanded translations to 140+ languages. Crowdin is not used for this fork; translations are managed directly in the repository.
