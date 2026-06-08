; OnlyT Installer Script for Inno Setup
; This script creates a Windows installer that bundles OnlyT and the StreamDeck plugin

#define MyAppName "OnlyT"
#define MyAppVersion "2.5.0.9"
#define MyAppPublisher "OnlyT"
#define MyAppURL "https://github.com/lastowl/OnlyT"
#define MyAppExeName "OnlyT.exe"

[Setup]
; Application info
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; Installation directories
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Output settings
OutputDir=..\..\dist\Windows
OutputBaseFilename=OnlyT-Setup-{#MyAppVersion}
SetupIconFile=..\..\OnlyT.Avalonia\Assets\onlyt.ico
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes

; Windows version requirements
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Privileges
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Appearance
WizardStyle=modern
WizardSizePercent=100

; Signing (uncomment and configure for production)
; SignTool=signtool sign /tr http://timestamp.digicert.com /td sha256 /fd sha256 /a $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "german"; MessagesFile: "compiler:Languages\German.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\Portuguese.isl"
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "dutch"; MessagesFile: "compiler:Languages\Dutch.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "classicversion"; Description: "Install OnlyT Classic (WPF version)"; GroupDescription: "Additional components:"; Flags: checkedonce
Name: "streamdeck"; Description: "Install Stream Deck plugin"; GroupDescription: "Additional components:"; Flags: checkedonce

[Files]
; OnlyT Avalonia application files (from OnlyT.Avalonia project publish output)
; The AssemblyName is set to "OnlyT" in the csproj, so the executable is OnlyT.exe
Source: "..\..\publish\win-x64\OnlyT.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\publish\win-x64\OnlyT.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\publish\win-x64\OnlyT.pdb"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\publish\win-x64\*.json"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
Source: "..\..\publish\win-x64\wwwroot\*"; DestDir: "{app}\wwwroot"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; OnlyT Classic (WPF) application files - installed to Classic subdirectory
; Only included if WPF was built (skipifsourcedoesntexist handles missing files gracefully)
Source: "..\..\publish\win-x64-wpf\OnlyT.exe"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\*.dll"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\*.pdb"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\*.json"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\*.mp3"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\*.ico"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\*.txt"; DestDir: "{app}\Classic"; Tasks: classicversion; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\..\publish\win-x64-wpf\runtimes\*"; DestDir: "{app}\Classic\runtimes"; Tasks: classicversion; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

; StreamDeck plugin - only install if user selects it AND StreamDeck is installed
Source: "..\..\StreamDeck\com.onlyt.timer.sdPlugin\*"; DestDir: "{localappdata}\Elgato\StreamDeck\Plugins\com.onlyt.timer.sdPlugin"; Tasks: streamdeck; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Avalonia version shortcuts
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; WPF Classic version shortcuts — only created when the Classic exe was
; actually installed. The Classic (WPF) build requires Windows and is absent
; from the cross-platform release pipeline, so without this guard the shortcut
; would dangle and show a blank/broken icon.
Name: "{autoprograms}\{#MyAppName} Classic"; Filename: "{app}\Classic\{#MyAppExeName}"; Tasks: classicversion; Check: ClassicExeExists
Name: "{autodesktop}\{#MyAppName} Classic"; Filename: "{app}\Classic\{#MyAppExeName}"; Tasks: desktopicon and classicversion; Check: ClassicExeExists

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Check if Stream Deck is installed
function IsStreamDeckInstalled(): Boolean;
begin
  Result := DirExists(ExpandConstant('{localappdata}\Elgato\StreamDeck'));
end;

// True only if the WPF "Classic" executable was actually installed. Used to
// suppress dangling Classic shortcuts when the Classic build isn't shipped.
function ClassicExeExists(): Boolean;
begin
  Result := FileExists(ExpandConstant('{app}\Classic\{#MyAppExeName}'));
end;

// Called when wizard page changes - use this to modify task list when tasks page is shown
procedure CurPageChanged(CurPageID: Integer);
begin
  // When we reach the tasks page, disable StreamDeck task if not installed
  if CurPageID = wpSelectTasks then
  begin
    if not IsStreamDeckInstalled() then
    begin
      // Index 0 = desktopicon, Index 1 = classicversion, Index 2 = streamdeck
      WizardForm.TasksList.Checked[2] := False;
      WizardForm.TasksList.ItemEnabled[2] := False;
    end;
  end;
end;

// Show message about StreamDeck if not installed
function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = wpSelectTasks then
  begin
    if not IsStreamDeckInstalled() and WizardIsTaskSelected('streamdeck') then
    begin
      MsgBox('Stream Deck software is not installed. The Stream Deck plugin will not be installed.', mbInformation, MB_OK);
      Result := True;
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\Elgato\StreamDeck\Plugins\com.onlyt.timer.sdPlugin"
