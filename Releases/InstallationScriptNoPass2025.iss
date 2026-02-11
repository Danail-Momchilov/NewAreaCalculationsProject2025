#define MyAppName "IPA-AreaCalculations"
#define MyAppVersion "2.1.1"
#define MyAppPublisher "IPA Architecture and More"
#define MyAppExeName "MyProg-x64.exe"

[Setup]
AppId={{D5B48238-4161-4AE2-90F6-BAFE9B3B40D3}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
CreateAppDir=yes
DefaultDirName=C:\Program Files\IPA\AreaCalculations2.0
DisableDirPage=yes
DisableProgramGroupPage=yes
LicenseFile=D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\LICENSE.txt
OutputDir=D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\Releases
OutputBaseFilename=IPA-AreaCalculationsNoPassV2.1.1
SetupIconFile=D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\installerIcon.ico
SolidCompression=yes
WizardStyle=modern
AppMutex={#MyAppName}
UninstallDisplayIcon={sys}\SHELL32.dll,4
UninstallDisplayName={#MyAppName} {#MyAppVersion}
UsePreviousAppDir=yes
UsePreviousGroup=yes
CloseApplications=yes
RestartApplications=no
UninstallRestartComputer=no
AllowCancelDuringInstall=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
; Main DLLs and dependencies (from net8.0-windows release folder)
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\AreaCalculations.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\AreaCalculations.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\AreaCalculations.pdb"; DestDir: "{app}"; Flags: ignoreversion

; All support DLLs
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\ClosedXML.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\ClosedXML.Parser.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\DocumentFormat.OpenXml.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\ExcelNumberFormat.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\RBush.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\SixLabors.Fonts.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\System.IO.Packaging.dll"; DestDir: "{app}"; Flags: ignoreversion

; Add-in files for 2025 and 2026
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\AreaCalculations.addin"; DestDir: "C:\ProgramData\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\AreaCalculations.addin"; DestDir: "C:\ProgramData\Autodesk\Revit\Addins\2026"; Flags: ignoreversion

; Icons and images
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\areacIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\areaIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\excelIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\plotIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\education.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\info.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\version.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\settings.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "D:\PROJECTS C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\migrateGroups.png"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: files; Name: "C:\ProgramData\Autodesk\Revit\Addins\2025\AreaCalculations.addin"
Type: files; Name: "C:\ProgramData\Autodesk\Revit\Addins\2026\AreaCalculations.addin"

[Registry]
; Clean up any old or duplicate registry entries first
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations_is1"; Flags: deletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations"; Flags: deletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}"; Flags: deletekey

; Create ONLY the standard Inno Setup registry entry (no custom keys)
; Inno Setup will automatically create the proper registry entries based on AppId

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
  UninstallPath: String;
begin
  // Only uninstall previous 2.x versions using the correct AppId format
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{D5B48238-4161-4AE2-90F6-BAFE9B3B40D3}_is1') then
  begin
    if RegQueryStringValue(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{D5B48238-4161-4AE2-90F6-BAFE9B3B40D3}_is1', 'UninstallString', UninstallPath) then
    begin
      Exec(UninstallPath, '/SILENT', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    end;
  end;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Clean up any old duplicate registry entries that might cause duplicate entries in Add/Remove Programs
    RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations');
    RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations_is1');
  end;
end;

[InstallDelete]
Type: filesandordirs; Name: "{app}"