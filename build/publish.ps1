# dist\ScreenAnnotation.exe 를 만든다 (framework-dependent, .NET 8 런타임 필요). 바탕화면 바로가기도 갱신.
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot
Get-Process ScreenAnnotation, ScreenAnnotation.Watchdog -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet publish "$root\src\ScreenAnnotation.Watchdog" -c Release -r win-x64 --self-contained false -o "$root\dist" | Out-Null
dotnet publish "$root\src\ScreenAnnotation.App" -c Release -r win-x64 --self-contained false -o "$root\dist" | Out-Null
$exe = "$root\dist\ScreenAnnotation.exe"
$sc = (New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path ([Environment]::GetFolderPath("Desktop")) "Screen Annotation.lnk"))
$sc.TargetPath = $exe; $sc.WorkingDirectory = "$root\dist"; $sc.Description = "Ctrl+Shift+Space로 화면 정지·판서"; $sc.Save()
Write-Host "published: $exe"
