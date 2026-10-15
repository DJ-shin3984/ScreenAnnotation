; Screen Annotation 설치 프로그램 (Inno Setup 6). build\make-installer.ps1 이 호출한다.
; 사용자 데이터(%APPDATA%\ScreenAnnotation 설정, %LOCALAPPDATA%\ScreenAnnotation\logs)는 제거해도 남긴다.
#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#define AppName "Screen Annotation"
#define AppExe "ScreenAnnotation.exe"

[Setup]
AppId={{7E3C2A9B-5D14-4F6A-9C1B-2A8E6F0D3B57}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=ScreenAnnotation
DefaultDirName={autopf}\ScreenAnnotation
DefaultGroupName={#AppName}
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=..\dist-installer
OutputBaseFilename=ScreenAnnotation-Setup-{#AppVersion}
SetupIconFile=..\src\ScreenAnnotation.App\Assets\app.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
PrivilegesRequired=admin
CloseApplications=yes
; 코드 서명 자리: Inno Setup IDE → Tools → Configure Sign Tools 에 "signtool" 등록 후 아래 주석 해제
; SignTool=signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /a $f

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "바탕화면 바로가기 만들기"; GroupDescription: "추가 작업:"
Name: "autostart"; Description: "Windows 시작 시 자동 실행"; GroupDescription: "추가 작업:"

[Files]
Source: "..\dist\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\{#AppName} 제거"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "ScreenAnnotation"; ValueData: """{app}\{#AppExe}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#AppExe}"; Description: "{#AppName} 실행"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "KillApp"
Filename: "taskkill"; Parameters: "/IM ScreenAnnotation.Watchdog.exe /F"; Flags: runhidden; RunOnceId: "KillWatchdog"

[Code]
// framework-dependent 게시이므로 .NET 8 Desktop 런타임이 있는지 확인. 없어도 설치는 진행하고 안내만 한다.
function HasDotnet8Desktop(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '8.' then Result := True;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if not HasDotnet8Desktop() then
    MsgBox('.NET 8 Desktop Runtime이 설치되어 있지 않습니다.' + #13#10 +
           '설치 후 https://dotnet.microsoft.com/download/dotnet/8.0 에서 "Desktop Runtime (x64)"를 설치하세요.',
           mbInformation, MB_OK);
end;
