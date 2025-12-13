# OnlyT (Cross-Platform Fork)

> **This is a fork of [OnlyT by Antony Corbett](https://github.com/AntonyCorbett/OnlyT)**. The original project is an excellent Windows-only meeting timer. This fork extends it with cross-platform support and removes some of the restrictions present in the original version.

A cross-platform meeting timer built with C# and Avalonia UI (with the original WPF version still included). Features both analogue and digital clock displays. Designed for use in Kingdom Halls where the meeting format is predefined, but includes "manual" and "file-based" modes that can be used to configure timers as required for other settings.

### Key Differences from Original
* **Cross-platform support** - Runs on Windows, macOS, and Linux
* **Configurable countdown duration** - Pre-meeting countdown is no longer fixed at 5 minutes
* **Countdown on web clock** - Pre-meeting countdown can now be displayed on the web clock
* **Live language switching** - Language can be changed without restarting the application
* **Maintained separately** - This fork may diverge from the upstream project

![Main Window](http://cv8.org.uk/soundbox/OnlyT/Images/MainWindow2.png)

![Timer Display](http://cv8.org.uk/soundbox/OnlyT/Images/Monitor02.png)

### System Requirements

**Windows (WPF Version):**
* Windows 10
* 2GB RAM
* 20MB Hard disk space
* Internet connection (for "Automatic" Operating Mode only)

**Cross-Platform (Avalonia Version):**
* Windows 10 or later / macOS 10.15 or later / Linux with GTK3
* 2GB RAM
* 30MB Hard disk space
* Internet connection (for "Automatic" Operating Mode only)

### Cross-Platform Support

OnlyT now includes a cross-platform version built with Avalonia UI that runs on **Windows, macOS, and Linux**!

The original Windows WPF version (`OnlyT` project) remains unchanged and fully supported. The cross-platform version (`OnlyT.Avalonia` project) provides the same functionality across all platforms while sharing most of the business logic.

**Building the Cross-Platform Version:**

```bash
# On Linux/macOS
./build-cross-platform.sh

# On Windows
build-cross-platform.cmd
```

This will build versions for Windows, macOS (both Intel and Apple Silicon), and Linux.

For more information about the cross-platform version, see [OnlyT.Avalonia/README.md](OnlyT.Avalonia/README.md).

### Download

Download the latest release from this fork's [Releases page](../../releases/latest).

For the original Windows-only version, see the [original OnlyT releases](https://github.com/AntonyCorbett/OnlyT/releases/latest).

### Help

See the [original wiki](https://github.com/AntonyCorbett/OnlyT/wiki) for basic instructions and for information on where to get further help.

See the [FAQ](https://github.com/AntonyCorbett/OnlyT/wiki/FAQ) for frequently asked questions.

### License, etc

This fork is based on OnlyT, Copyright &copy; 2018, 2024 Antony Corbett and other contributors under the [MIT license](LICENSE). Fork modifications are also released under the MIT license.

NAudio (Mark Heath) is used under the Microsoft Public License (Ms-PL). MaterialDesign themes (James Willock, Mulholland Software and Contributors) is used under the MIT license. NUglify, Copyright (c) 2016, Alexandre Mutel. QRCode (Raffael Herrmann) is used under MIT. Serilog is used under the Apache License Version 2.0, January 2004. LiteDB (Mauricio David) is used under the MIT License. PDFSharp is used under the MIT License.

With thanks to:
* Crowdin.com for localisation tools
* GitHub for project management tools
* [JetBrains](https://jb.gg/OpenSourceSupport) for Resharper tools
* A team of over 20 translators who have helped localise OnlyT
