; Inno Setup: 단일 ONITON.exe 를 설치하는 ONITON-Setup.exe 를 만든다 (관리자 권한 불필요)
[Setup]
AppName=ONITON
AppVersion=1.0.2
AppPublisher=MRX
DefaultDirName={autopf}\ONITON
DefaultGroupName=ONITON
OutputDir=..\dist
OutputBaseFilename=ONITON-Setup
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\ONITON.exe
PrivilegesRequired=lowest
Compression=lzma2
SolidCompression=yes

[Tasks]
Name: "desktopicon"; Description: "바탕화면 바로가기 / Desktop shortcut"; Flags: unchecked

[Files]
Source: "..\dist\ONITON.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\ONITON"; Filename: "{app}\ONITON.exe"
Name: "{autodesktop}\ONITON"; Filename: "{app}\ONITON.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\ONITON.exe"; Description: "ONITON 실행 / Launch"; Flags: nowait postinstall skipifsilent
