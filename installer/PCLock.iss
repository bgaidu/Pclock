; Inno Setup script for PCLock (requires Inno Setup 6 to compile).
; Build the EXE first:  build.bat Release
; Then compile:         ISCC.exe installer\PCLock.iss
; Output:               installer\output\PCLockSetup.exe
; NOTE: keep this file ASCII-only where possible; the installer itself does not
; create the scheduled task - PCLock.exe does that on its first elevated run.

#define MyAppName "PCLock"
#define MyAppExeName "PCLock.exe"
#define MySource "..\bin\Release"

[Setup]
AppId={{B7A1C0DE-4C3E-4C8A-9E11-PCLOCK000001}
AppName={#MyAppName}
AppVersion=1.6.1
DefaultDirName={pf}\PCLock
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=output
OutputBaseFilename=PCLockSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#MySource}\PCLock.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MySource}\PCLock.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MySource}\AppIcon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MySource}\chinese.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "{#MySource}\english.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist
Source: "..\README.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Run]
Filename: "{app}\PCLock.exe"; Description: "Run PCLock once (creates the startup task)"; Flags: postinstall runascurrentuser nowait

; IMPORTANT: do NOT add an [UninstallRun] entry that launches PCLock.exe.
; Uninstall must go through Settings -> Uninstall (uninstall password), which
; removes the scheduled task, registry key and program directory. Launching the
; protected exe during uninstall would leave the watchdog running.

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    MsgBox('Run PCLock once as administrator. It creates the startup task and asks you to set the parent PIN and uninstall password.', mbInformation, MB_OK);
end;
