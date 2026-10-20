; Inno Setup script (free: https://jrsoftware.org/isinfo.php)
; Put this file in <your Unity project>/Installer/ , build the game with Tools > Card Battle > Build Windows (.exe),
; open this file in Inno Setup and press Build > Compile (Ctrl+F9). The installer appears in Installer\Output\.

#define AppName "Card Battle"
#define AppVersion "1.0"
#define Publisher "Your Name"
#define ExeName "CardBattle.exe"
; Folder produced by the Unity build (relative to this .iss file):
#define BuildDir "..\Builds\Windows"

[Setup]
AppId={{8F3B6C1E-5A2D-4E7B-9C31-2D5E8A7B4F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=Output
OutputBaseFilename=CardBattle-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#ExeName}
WizardStyle=modern

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#ExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#ExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#ExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
