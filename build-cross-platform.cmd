@echo off
REM Build script for OnlyT.Avalonia cross-platform versions
REM This script builds the cross-platform version for Windows, macOS, and Linux

setlocal

echo ======================================
echo OnlyT.Avalonia Cross-Platform Builder
echo ======================================
echo.

set PROJECT_DIR=OnlyT.Avalonia
set OUTPUT_DIR=dist
set CONFIG=Release

REM Clean previous builds
echo Cleaning previous builds...
if exist "%OUTPUT_DIR%" rmdir /s /q "%OUTPUT_DIR%"
dotnet clean "%PROJECT_DIR%\OnlyT.Avalonia.csproj" -c "%CONFIG%"

REM Create output directory
mkdir "%OUTPUT_DIR%"

REM Build for Windows x64
echo.
echo Building for Windows (x64)...
dotnet publish "%PROJECT_DIR%\OnlyT.Avalonia.csproj" ^
    -c "%CONFIG%" ^
    -r win-x64 ^
    --self-contained ^
    -o "%OUTPUT_DIR%\win-x64" ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true

REM Build for macOS x64
echo.
echo Building for macOS (x64)...
dotnet publish "%PROJECT_DIR%\OnlyT.Avalonia.csproj" ^
    -c "%CONFIG%" ^
    -r osx-x64 ^
    --self-contained ^
    -o "%OUTPUT_DIR%\osx-x64" ^
    -p:PublishSingleFile=true

REM Build for macOS ARM64 (Apple Silicon)
echo.
echo Building for macOS (ARM64)...
dotnet publish "%PROJECT_DIR%\OnlyT.Avalonia.csproj" ^
    -c "%CONFIG%" ^
    -r osx-arm64 ^
    --self-contained ^
    -o "%OUTPUT_DIR%\osx-arm64" ^
    -p:PublishSingleFile=true

REM Build for Linux x64
echo.
echo Building for Linux (x64)...
dotnet publish "%PROJECT_DIR%\OnlyT.Avalonia.csproj" ^
    -c "%CONFIG%" ^
    -r linux-x64 ^
    --self-contained ^
    -o "%OUTPUT_DIR%\linux-x64" ^
    -p:PublishSingleFile=true

echo.
echo ======================================
echo Build completed successfully!
echo ======================================
echo.
echo Output directory: %OUTPUT_DIR%\
echo.
echo Built for platforms:
echo   - Windows (x64): %OUTPUT_DIR%\win-x64\
echo   - macOS (x64): %OUTPUT_DIR%\osx-x64\
echo   - macOS (ARM64): %OUTPUT_DIR%\osx-arm64\
echo   - Linux (x64): %OUTPUT_DIR%\linux-x64\
echo.
echo Note: You can create archives manually or use 7-Zip/tar
echo.

endlocal
pause
