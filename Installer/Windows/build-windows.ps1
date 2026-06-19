# Build (and optionally sign) the OnlyT Windows installer on a Windows machine.
# Run from the repo root (remote-build.sh does this). Steps:
#   1. derive the version from SolutionInfo.cs (no manual bumping of the .iss)
#   2. dotnet publish the Avalonia + WPF apps (win-x64, self-contained)
#   3. sign the launched app exes BEFORE packaging  (so the installed app is signed)
#   4. Inno Setup -> OnlyT-Setup-<ver>.exe  (version passed via /D)
#   5. sign the installer
#
# Signing uses Azure Artifact Signing and only happens when the signing config is present:
#   - a config script that sets AZURE_* creds + VCAM_SIGN_{ACCOUNT,PROFILE,ENDPOINT} + DOTNET_ROOT_X64
#     (path via $env:SIGN_CONFIG, default C:\Users\build\vcam-signing.ps1 — shared with VirtualCamApp)
#   - the signtool dlib (path via $env:SIGN_DLIB, default the VirtualCamApp-cached Azure.CodeSigning.Dlib.dll)
# If the config/dlib aren't found, it builds UNSIGNED (and says so) rather than failing.
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path

# 1. version from SolutionInfo.cs
$m = Select-String -Path (Join-Path $root 'SolutionInfo.cs') -Pattern 'AssemblyVersion\("([0-9.]+)"\)'
if (-not $m) { throw "Could not read AssemblyVersion from SolutionInfo.cs" }
$ver = $m.Matches[0].Groups[1].Value
Write-Host "=== OnlyT Windows build $ver ===" -ForegroundColor Cyan

# 2. publish
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64
dotnet publish OnlyT/OnlyT.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64-wpf

# 3. signing setup (optional)
$signCfg = if ($env:SIGN_CONFIG) { $env:SIGN_CONFIG } else { 'C:\Users\build\vcam-signing.ps1' }
$dlib    = if ($env:SIGN_DLIB)   { $env:SIGN_DLIB }   else { 'C:\Users\build\VirtualCamApp\src\native\win\tools\TrustedSigning\bin\x64\Azure.CodeSigning.Dlib.dll' }
$canSign = $false
if ((Test-Path $signCfg) -and (Test-Path $dlib)) {
    . $signCfg
    if ($env:VCAM_SIGN_PROFILE -and $env:AZURE_CLIENT_ID) { $canSign = $true }
}
$signtool = (Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin' -Recurse -Filter signtool.exe -EA SilentlyContinue |
             Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1).FullName
$md = Join-Path $env:TEMP 'onlyt-sign-md.json'
if ($canSign) {
    @{ Endpoint=$env:VCAM_SIGN_ENDPOINT; CodeSigningAccountName=$env:VCAM_SIGN_ACCOUNT; CertificateProfileName=$env:VCAM_SIGN_PROFILE } |
        ConvertTo-Json | Set-Content $md -Encoding ascii
    Write-Host "Signing enabled (profile $env:VCAM_SIGN_PROFILE)" -ForegroundColor Green
} else {
    Write-Host "Signing config/dlib not found - building UNSIGNED" -ForegroundColor Yellow
}
function Sign-File($path) {
    if (-not $canSign -or -not (Test-Path $path)) { return }
    & $signtool sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib $dlib /dmdf $md $path
    if ($LASTEXITCODE -ne 0) { throw "signtool failed for $path" }
}

# 4. sign the app exes BEFORE packaging (covers OnlyT.exe / OnlyT.Avalonia.exe whatever they're named)
Get-ChildItem "publish\win-x64\*.exe","publish\win-x64-wpf\*.exe" -EA SilentlyContinue |
    ForEach-Object { Write-Host "signing $($_.Name)"; Sign-File $_.FullName }

# 5. Inno Setup with the derived version
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' "/DMyAppVersion=$ver" "Installer\Windows\OnlyT-Setup.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

# 6. sign the installer
$inst = Get-ChildItem "dist\Windows\OnlyT-Setup-*.exe" | Sort-Object LastWriteTime | Select-Object -Last 1
Sign-File $inst.FullName

# 7. portable zips (Avalonia x64 + arm64) with signed app exes
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=false -o publish/win-arm64
Get-ChildItem "publish\win-arm64\*.exe" -EA SilentlyContinue | ForEach-Object { Write-Host "signing $($_.Name)"; Sign-File $_.FullName }
Compress-Archive -Path "publish\win-x64\*"   -DestinationPath "dist\Windows\OnlyT-$ver-win-x64-portable.zip"   -Force
Compress-Archive -Path "publish\win-arm64\*" -DestinationPath "dist\Windows\OnlyT-$ver-win-arm64-portable.zip" -Force

Write-Host "=== DONE: installer + portable zips for $ver (signed=$canSign) ===" -ForegroundColor Green
Get-ChildItem "dist\Windows" | Select-Object Name,Length | Format-Table -AutoSize | Out-String | Write-Host
