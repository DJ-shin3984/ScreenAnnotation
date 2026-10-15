# dist\ 에 App + Watchdog 을 게시한다. 기본은 framework-dependent(.NET 8 Desktop 런타임 필요).
# -SelfContained : 런타임 포함 단일 파일 (크기 비교는 docs/checks/T61.md)
# -NoShortcut    : 바탕화면 바로가기 생략 (설치 프로그램 빌드용)
param([switch]$SelfContained, [switch]$NoShortcut)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
$dist = "$root\dist"
Get-Process ScreenAnnotation, ScreenAnnotation.Watchdog -ErrorAction SilentlyContinue | Stop-Process -Force
if (Test-Path $dist) { Remove-Item -Recurse -Force $dist }

$common = @("-c", "Release", "-r", "win-x64", "-o", $dist)
if ($SelfContained) {
    $opts = $common + @("--self-contained", "true", "-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true")
} else {
    $opts = $common + @("--self-contained", "false")
}
dotnet publish "$root\src\ScreenAnnotation.Watchdog" @opts | Out-Null
dotnet publish "$root\src\ScreenAnnotation.App" @opts | Out-Null

$exe = "$dist\ScreenAnnotation.exe"
if (-not $NoShortcut) {
    $sc = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "Screen Annotation.lnk"))
    $sc.TargetPath = $exe; $sc.WorkingDirectory = $dist; $sc.Description = "Ctrl+Shift+Space로 화면 정지·판서"; $sc.Save()
}
$mb = [Math]::Round(((Get-ChildItem $dist -Recurse -File | Measure-Object Length -Sum).Sum / 1MB), 1)
Write-Host "published: $exe ($mb MB, selfContained=$SelfContained)"
