[Setup]
AppId=KanbanCards
AppName=KanbanCards
AppVersion=1.3.0
AppPublisher=danycli
DefaultDirName={autopf}\KanbanCards
DefaultGroupName=KanbanCards
OutputDir=Output
OutputBaseFilename=KanbanCardsSetup
Compression=lzma2/max
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\AppIcon.ico
UninstallDisplayIcon={app}\KanbanCards.exe

; Enable upgrading over previous installations seamlessly
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
DisableDirPage=auto
DisableProgramGroupPage=auto

; Automatic handling of running application via Windows Restart Manager
CloseApplications=force
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"
Name: "startupicon"; Description: "Start KanbanCards when Windows starts"; GroupDescription: "Additional icons:"

[Files]
Source: "..\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\KanbanCards"; Filename: "{app}\KanbanCards.exe"; IconFilename: "{app}\KanbanCards.exe"
Name: "{autodesktop}\KanbanCards"; Filename: "{app}\KanbanCards.exe"; Tasks: desktopicon; IconFilename: "{app}\KanbanCards.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "KanbanCards"; ValueData: """{app}\KanbanCards.exe"""; Tasks: startupicon; Flags: uninsdeletevalue

[Run]
Filename: "{app}\KanbanCards.exe"; Description: "{cm:LaunchProgram,KanbanCards}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/f /im KanbanCards.exe"; Flags: runhidden; RunOnceId: "StopKanbanCardsOnUninstall"

[Code]
// Proactively terminate any running instance before file extraction begins
procedure StopRunningApp();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im KanbanCards.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(400); // Allow OS to release all open file handles
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopRunningApp();
  Result := '';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssDone then
  begin
    Exec(ExpandConstant('{app}\KanbanCards.exe'), '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
  end;
end;

function InitializeUninstall(): Boolean;
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/f /im KanbanCards.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Sleep(400);
  Result := True;
end;
