; Compile after: ./Build.ps1 -Publish
; Sign release binaries and the resulting installer before public distribution.
[Setup]
AppId={{8E4AEB29-249B-443B-A795-7E9E0E2AB063}
AppName=SpeakerMesh
AppVersion=0.5.0
AppPublisher=SpeakerMesh
DefaultDirName={localappdata}\Programs\SpeakerMesh
DefaultGroupName=SpeakerMesh
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer
OutputBaseFilename=SpeakerMesh-0.5.0-preview-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayIcon={app}\SpeakerMesh.exe
LicenseFile=..\LICENSE.txt

[Files]
Source: "..\artifacts\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked
Name: "startup"; Description: "Open SpeakerMesh when I sign in (playback starts manually)"; Flags: unchecked

[Icons]
Name: "{group}\SpeakerMesh"; Filename: "{app}\SpeakerMesh.exe"
Name: "{autodesktop}\SpeakerMesh"; Filename: "{app}\SpeakerMesh.exe"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "SpeakerMesh"; ValueData: """{app}\SpeakerMesh.exe"""; Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\SpeakerMesh.exe"; Description: "Open SpeakerMesh"; Flags: nowait postinstall skipifsilent
