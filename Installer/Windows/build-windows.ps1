# Build (and optionally sign) the OnlyT Windows installer on a Windows machine.
# Run from the repo root (remote-build.sh does this). Steps:
#   1. derive the version from SolutionInfo.cs (no manual bumping of the .iss)
#   2. dotnet publish the Avalonia + WPF apps (win-x64, self-contained)
#   3. sign the launched app exes BEFORE packaging  (so the installed app is signed)
#   4. Inno Setup -> OnlyT-Setup-<ver>.exe  (version passed via /D)
#   5. sign the installer
#
# Signing uses Azure Artifact Signing and only happens when the signing config is present:
#   - a config script that sets VCAM_SIGN_{ACCOUNT,PROFILE,ENDPOINT} plus either AZURE_* service-principal
#     creds or VCAM_SIGN_USE_AZURE_CLI=1 (use the current `az login` session)
#     (path via $env:SIGN_CONFIG, default ~\vcam-signing.ps1 — shared with VirtualCamApp)
#   - the signtool dlib (path via $env:SIGN_DLIB, default the VirtualCamApp-cached Azure.CodeSigning.Dlib.dll)
# If the config/dlib aren't found, it builds UNSIGNED (and says so) rather than failing.
# A .NET 10 SDK installed per-user in ~\.dotnet (dotnet-install.ps1) is used ahead of the machine-wide one.
$ErrorActionPreference = 'Stop'
$root = (Get-Location).Path

$userDotnet = Join-Path $env:USERPROFILE '.dotnet'
if (Test-Path (Join-Path $userDotnet 'sdk\10.*')) { $env:PATH = "$userDotnet;$env:PATH" }

# 1. version from SolutionInfo.cs
$m = Select-String -Path (Join-Path $root 'SolutionInfo.cs') -Pattern 'AssemblyVersion\("([0-9.]+)"\)'
if (-not $m) { throw "Could not read AssemblyVersion from SolutionInfo.cs" }
$ver = $m.Matches[0].Groups[1].Value
Write-Host "=== OnlyT Windows build $ver ===" -ForegroundColor Cyan

# 2. publish ($ErrorActionPreference doesn't cover native exes, so check exit codes explicitly)
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64
if ($LASTEXITCODE -ne 0) { throw "publish failed (Avalonia win-x64)" }
dotnet publish OnlyT/OnlyT.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64-wpf
if ($LASTEXITCODE -ne 0) { throw "publish failed (WPF win-x64)" }

# 3. signing setup (optional)
$signCfg = if ($env:SIGN_CONFIG) { $env:SIGN_CONFIG } else { Join-Path $env:USERPROFILE 'vcam-signing.ps1' }
$dlib    = if ($env:SIGN_DLIB)   { $env:SIGN_DLIB }   else { Join-Path $env:USERPROFILE 'VirtualCamApp\src\native\win\tools\TrustedSigning\bin\x64\Azure.CodeSigning.Dlib.dll' }
# Newest SDK first: older signtool versions don't support /dlib and fall back to the certificate store.
$signtool = (Get-ChildItem 'C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\signtool.exe' -EA SilentlyContinue |
             Sort-Object { [version]$_.Directory.Parent.Name } -Descending | Select-Object -First 1).FullName
$canSign = $false
$useAzureCli = $false
if ((Test-Path $signCfg) -and (Test-Path $dlib)) {
    . $signCfg
    $useAzureCli = $env:VCAM_SIGN_USE_AZURE_CLI -in @('1', 'true', 'yes')
    if ($env:VCAM_SIGN_PROFILE -and ($env:AZURE_CLIENT_ID -or $useAzureCli)) {
        if (-not $signtool) { throw "Signing config present but signtool.exe not found under Windows Kits" }
        if ($useAzureCli) {
            az account show --output none
            if ($LASTEXITCODE -ne 0) { throw "Signing uses the Azure CLI, but it isn't logged in; run az login" }
        }
        $canSign = $true
    }
}
$md = Join-Path $env:TEMP 'onlyt-sign-md.json'
if ($canSign) {
    $metadata = @{ Endpoint=$env:VCAM_SIGN_ENDPOINT; CodeSigningAccountName=$env:VCAM_SIGN_ACCOUNT; CertificateProfileName=$env:VCAM_SIGN_PROFILE }
    if ($useAzureCli) {
        # Keep AzureCliCredential; skip credentials that precede it or may open an interactive prompt.
        $metadata.ExcludeCredentials = @('EnvironmentCredential', 'WorkloadIdentityCredential', 'ManagedIdentityCredential',
            'SharedTokenCacheCredential', 'VisualStudioCredential', 'VisualStudioCodeCredential',
            'AzurePowerShellCredential', 'AzureDeveloperCliCredential', 'InteractiveBrowserCredential')
    }
    $metadata | ConvertTo-Json | Set-Content $md -Encoding ascii
    Write-Host "Signing enabled (profile $env:VCAM_SIGN_PROFILE)" -ForegroundColor Green
} else {
    Write-Host "Signing config/dlib not found - building UNSIGNED" -ForegroundColor Yellow
}
# The signing dlib runs on the x64 .NET 8 runtime, which a per-user SDK install doesn't include.
$signDotnetRoot = if ($env:DOTNET_ROOT_X64) { $env:DOTNET_ROOT_X64 } else {
    @((Join-Path $env:ProgramFiles 'dotnet\x64'), (Join-Path $env:ProgramFiles 'dotnet')) |
        Where-Object { Test-Path (Join-Path $_ 'shared\Microsoft.NETCore.App\8.*') } | Select-Object -First 1
}
function Sign-File($path) {
    if (-not $canSign -or -not (Test-Path $path)) { return }
    $oldRoot, $oldRootX64 = $env:DOTNET_ROOT, $env:DOTNET_ROOT_X64
    try {
        if ($signDotnetRoot) { $env:DOTNET_ROOT = $signDotnetRoot; $env:DOTNET_ROOT_X64 = $signDotnetRoot }
        & $signtool sign /fd SHA256 /tr http://timestamp.acs.microsoft.com /td SHA256 /dlib $dlib /dmdf $md $path
        if ($LASTEXITCODE -ne 0) { throw "signtool failed for $path" }
    } finally {
        $env:DOTNET_ROOT, $env:DOTNET_ROOT_X64 = $oldRoot, $oldRootX64
    }
}

# 4. sign the app exes BEFORE packaging (covers OnlyT.exe / OnlyT.Avalonia.exe whatever they're named)
Get-ChildItem "publish\win-x64\*.exe","publish\win-x64-wpf\*.exe" -EA SilentlyContinue |
    ForEach-Object { Write-Host "signing $($_.Name)"; Sign-File $_.FullName }

# 4b. Stream Deck plugin package for the installer: a zip with the plugin folder at its root, with the
# manifest's top-level Version (4-space indent, unlike Nodejs.Version) set to the app version.
# Built with ZipFile rather than Compress-Archive, which writes backslashes into entry names.
$pluginSrc = Join-Path $root 'StreamDeck\com.onlyt.timer.sdPlugin'
$pluginPackage = Join-Path $root 'publish\streamdeck\com.onlyt.timer.streamDeckPlugin'
New-Item -ItemType Directory -Force (Split-Path $pluginPackage) | Out-Null
if (Test-Path $pluginPackage) { Remove-Item $pluginPackage }
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($pluginPackage, 'Create')
try {
    Get-ChildItem $pluginSrc -Recurse -File | Where-Object { $_.Name -ne '.DS_Store' } | ForEach-Object {
        $entryName = 'com.onlyt.timer.sdPlugin/' + $_.FullName.Substring($pluginSrc.Length + 1).Replace('\', '/')
        $stream = $zip.CreateEntry($entryName).Open()
        try {
            if ($entryName -eq 'com.onlyt.timer.sdPlugin/manifest.json') {
                $manifest = [regex]::Replace([IO.File]::ReadAllText($_.FullName), '(?m)^    "Version":\s*"[^"]*"', "    `"Version`": `"$ver`"")
                if ($manifest -notmatch "(?m)^    `"Version`": `"$([regex]::Escape($ver))`"") { throw "Could not set the Stream Deck plugin version" }
                $bytes = (New-Object System.Text.UTF8Encoding $false).GetBytes($manifest)
                $stream.Write($bytes, 0, $bytes.Length)
            } else {
                $file = [IO.File]::OpenRead($_.FullName)
                try { $file.CopyTo($stream) } finally { $file.Dispose() }
            }
        } finally {
            $stream.Dispose()
        }
    }
} finally {
    $zip.Dispose()
}
Write-Host "Stream Deck plugin packaged: $pluginPackage"

# 5. Inno Setup with the derived version
& 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' "/DMyAppVersion=$ver" "Installer\Windows\OnlyT-Setup.iss"
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

# 6. sign the installer
$inst = Get-ChildItem "dist\Windows\OnlyT-Setup-*.exe" | Sort-Object LastWriteTime | Select-Object -Last 1
Sign-File $inst.FullName

# 7. portable zips (Avalonia x64 + arm64) with signed app exes. Zip the directory
# itself (not its contents) so the archive extracts into a win-x64/win-arm64 folder,
# matching previous releases, instead of dumping ~300 files at the extract point.
dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-arm64 --self-contained true -p:PublishSingleFile=false -o publish/win-arm64
if ($LASTEXITCODE -ne 0) { throw "publish failed (Avalonia win-arm64)" }
Get-ChildItem "publish\win-arm64\*.exe" -EA SilentlyContinue | ForEach-Object { Write-Host "signing $($_.Name)"; Sign-File $_.FullName }
Compress-Archive -Path "publish\win-x64"   -DestinationPath "dist\Windows\OnlyT-$ver-win-x64-portable.zip"   -Force
Compress-Archive -Path "publish\win-arm64" -DestinationPath "dist\Windows\OnlyT-$ver-win-arm64-portable.zip" -Force

Write-Host "=== DONE: installer + portable zips for $ver (signed=$canSign) ===" -ForegroundColor Green
Get-ChildItem "dist\Windows" | Select-Object Name,Length | Format-Table -AutoSize | Out-String | Write-Host
