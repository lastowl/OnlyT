#!/bin/bash
# Build (and sign) the OnlyT Windows installer on a remote Windows VM over SSH, from the Mac, in one
# command. Tars the source cleanly (no macOS AppleDouble ._ files, no bin/obj/.git/publish), scp's it to
# the VM, and runs Installer/Windows/build-windows.ps1 there, which derives the version from
# SolutionInfo.cs, publishes the Avalonia + WPF apps, signs the app exes, runs Inno Setup, and signs the
# installer; the resulting .exe is copied back to dist/Windows/.
#
# The VM must have: OpenSSH (key in authorized_keys), .NET 10 SDK, Inno Setup 6 (ISCC.exe). Signing is
# automatic when the Azure Artifact Signing config is present on the VM (see build-windows.ps1); without
# it the build is unsigned.
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

echo "=== extract + build (+sign) on VM ==="
$SSH "$VM_USER@$VM_HOST" "powershell -NoProfile -ExecutionPolicy Bypass -Command \"if(Test-Path '$DEST'){Remove-Item -Recurse -Force '$DEST'}; New-Item -ItemType Directory -Force '$DEST' | Out-Null; tar -xzf C:/Users/build/onlyt-src.tgz -C '$DEST'; Set-Location '$DEST'; & '$DEST/Installer/Windows/build-windows.ps1'\""

echo "=== copying installer + portable zips back ==="
mkdir -p "$REPO/dist/Windows"
$SCP "$VM_USER@$VM_HOST:$DEST/dist/Windows/OnlyT-Setup-*.exe" "$REPO/dist/Windows/" || echo "(no installer found to copy)"
$SCP "$VM_USER@$VM_HOST:$DEST/dist/Windows/OnlyT-*-portable.zip" "$REPO/dist/Windows/" || echo "(no portable zips found to copy)"
ls -lh "$REPO/dist/Windows/"* 2>/dev/null || true
echo "=== DONE ==="
