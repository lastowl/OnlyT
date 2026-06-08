#!/bin/bash
# Build the OnlyT Windows installer on a remote Windows VM over SSH, from the Mac, in one command.
# Tars the source cleanly (no macOS AppleDouble ._ files, no bin/obj/.git/publish), scp's it to the VM,
# publishes the Avalonia (win-x64) + WPF Classic (win-x64) apps there, runs Inno Setup, and copies the
# resulting installer .exe back to dist/Windows/.
#
# The VM must have: OpenSSH (key in authorized_keys), .NET 10 SDK, Inno Setup 6 (ISCC.exe).
# See memory: windows-vm-access (shared with VirtualCamApp; user "build", key under /tmp/vcam-ssh).
#
# Usage: VM_HOST=192.168.189.128 SSH_KEY=/tmp/vcam-ssh/id_ed25519 ./remote-build.sh
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
VM_USER="${VM_USER:-build}"
VM_HOST="${VM_HOST:?set VM_HOST to the Windows VM IP}"
SSH_KEY="${SSH_KEY:?set SSH_KEY to the private key path}"
SSH="ssh -i $SSH_KEY -o StrictHostKeyChecking=no -o ServerAliveInterval=30"
SCP="scp -i $SSH_KEY -o StrictHostKeyChecking=no"
DEST='C:/Users/build/OnlyT'

echo "=== packaging source (clean) ==="
COPYFILE_DISABLE=1 tar \
  --exclude='./.git' --exclude='./dist' --exclude='./publish' \
  --exclude='*/bin' --exclude='*/obj' --exclude='*.dmg' --exclude='.build-secrets' \
  -czf /tmp/onlyt-src.tgz -C "$REPO" .

echo "=== copying to $VM_USER@$VM_HOST ==="
$SCP /tmp/onlyt-src.tgz "$VM_USER@$VM_HOST:C:/Users/build/onlyt-src.tgz"

echo "=== extract + build on VM ==="
$SSH "$VM_USER@$VM_HOST" "powershell -NoProfile -ExecutionPolicy Bypass -Command \"if(Test-Path '$DEST'){Remove-Item -Recurse -Force '$DEST'}; New-Item -ItemType Directory -Force '$DEST' | Out-Null; tar -xzf C:/Users/build/onlyt-src.tgz -C '$DEST'; Set-Location '$DEST'; dotnet publish OnlyT.Avalonia/OnlyT.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64; dotnet publish OnlyT/OnlyT.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o publish/win-x64-wpf; & 'C:/Program Files (x86)/Inno Setup 6/ISCC.exe' Installer/Windows/OnlyT-Setup.iss\""

echo "=== copying installer back ==="
mkdir -p "$REPO/dist/Windows"
$SCP "$VM_USER@$VM_HOST:$DEST/dist/Windows/OnlyT-Setup-*.exe" "$REPO/dist/Windows/" || echo "(no installer found to copy)"
ls -lh "$REPO/dist/Windows/"*.exe 2>/dev/null || true
echo "=== DONE ==="
