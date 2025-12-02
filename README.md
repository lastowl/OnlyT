# OnlyT <img src="https://ci.appveyor.com/api/projects/status/d0wra2jk7o23fagx?svg=true">

Windows Meeting Timer using C#, WPF and custom analogue clock control. Designed for use in Kingdom Halls where the meeting format is predefined, but has a "manual" and "file-based" mode that can be used to configure the timers as required (and so can be used in other settings too).

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

If you just want to install the application, please download the [OnlyTSetup.exe](https://github.com/AntonyCorbett/OnlyT/releases/latest) file (there is also a portable version if you'd prefer to just copy a folder).

### Help

See the [wiki](https://github.com/AntonyCorbett/OnlyT/wiki) for basic instructions and for information on where to get further help.

See the [FAQ](https://github.com/AntonyCorbett/OnlyT/wiki/FAQ) for frequently asked questions.

### License, etc

OnlyT is Copyright &copy; 2018, 2024 Antony Corbett and other contributors under the [MIT license](LICENSE).

NAudio (Mark Heath) is used under the Microsoft Public License (Ms-PL). MaterialDesign themes (James Willock, Mulholland Software and Contributors) is used under the MIT license. NUglify, Copyright (c) 2016, Alexandre Mutel. QRCode (Raffael Herrmann) is used under MIT. Serilog is used under the Apache License Version 2.0, January 2004. LiteDB (Mauricio David) is used under the MIT License. PDFSharp is used under the MIT License.

With thanks to:
* Crowdin.com for localisation tools
* GitHub for project management tools
* [JetBrains](https://jb.gg/OpenSourceSupport) for Resharper tools
* A team of over 20 translators who have helped localise OnlyT
