# publish.ps1 → Inno Setup → dist-installer\ScreenAnnotation-Setup-<version>.exe
param([string]$Version = "0.1.0", [switch]$SelfContained)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
& "$PSScriptRoot\publish.ps1" -NoShortcut -SelfContained:$SelfContained

$iscc = @("C:\Program Files (x86)\Inno Setup 6\ISCC.exe", "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe 없음: winget install --id JRSoftware.InnoSetup -e" }

New-Item -ItemType Directory -Force "$root\dist-installer" | Out-Null
& $iscc "/DAppVersion=$Version" "$PSScriptRoot\installer.iss" | Select-Object -Last 3
$out = "$root\dist-installer\ScreenAnnotation-Setup-$Version.exe"
Write-Host "installer: $out ($([Math]::Round((Get-Item $out).Length / 1MB, 1)) MB)"
