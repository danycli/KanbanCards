[Setup]
AppName=KanbanCards
AppVersion=1.1.0
DefaultDirName={autopf}\KanbanCards
DefaultGroupName=KanbanCards
OutputDir=Output
OutputBaseFilename=KanbanCardsSetup
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
SetupIconFile=..\AppIcon.ico
UninstallDisplayIcon={app}\KanbanCards.exe

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KanbanCards"; Filename: "{app}\KanbanCards.exe"; IconFilename: "{app}\KanbanCards.exe"
Name: "{autodesktop}\KanbanCards"; Filename: "{app}\KanbanCards.exe"; Tasks: desktopicon; IconFilename: "{app}\KanbanCards.exe"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"
Name: "startupicon"; Description: "Start KanbanCards when Windows starts"; GroupDescription: "Additional icons:"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KanbanCards"; ValueData: """{app}\KanbanCards.exe"""; Tasks: startupicon
