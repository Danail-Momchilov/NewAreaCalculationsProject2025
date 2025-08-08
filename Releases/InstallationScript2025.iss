#define MyAppName "IPA-AreaCalculations"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "IPA Architecture and More"
#define MyAppExeName "MyProg-x64.exe"

[Setup]
AppId={{75B951C9-A0E1-43AA-BB76-DDEAF1781D38}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
CreateAppDir=yes
DefaultDirName=C:\Program Files\IPA\AreaCalculations2.0
DisableDirPage=yes
DisableProgramGroupPage=yes
LicenseFile=B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\LICENSE.txt
OutputDir=B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\Releases
OutputBaseFilename=IPA-AreaCalculationsV2.0.0
SetupIconFile=B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\installerIcon.ico
Password=ipaMipa
Encryption=yes
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
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\AreaCalculations.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\AreaCalculations.deps.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\AreaCalculations.pdb"; DestDir: "{app}"; Flags: ignoreversion

; All support DLLs from screenshot
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\ClosedXML.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\ClosedXML.Parser.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\DocumentFormat.OpenXml.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\DocumentFormat.OpenXml.Framework.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\ExcelNumberFormat.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\RBush.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\SixLabors.Fonts.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\bin\Release\net8.0-windows\System.IO.Packaging.dll"; DestDir: "{app}"; Flags: ignoreversion

; Add-in files for 2025 and 2026
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\AreaCalculations.addin"; DestDir: "C:\ProgramData\Autodesk\Revit\Addins\2025"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\AreaCalculations.addin"; DestDir: "C:\ProgramData\Autodesk\Revit\Addins\2026"; Flags: ignoreversion

; Icons and images
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\areacIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\areaIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\excelIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\plotIcon.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\education.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\info.png"; DestDir: "{app}"; Flags: ignoreversion
Source: "B:\06. BIM AUTOMATION\02. C#\AREA CALCULATIONS\NewAreaCalculationsProject2025\img\version.png"; DestDir: "{app}"; Flags: ignoreversion

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: files; Name: "C:\ProgramData\Autodesk\Revit\Addins\2025\AreaCalculations.addin"
Type: files; Name: "C:\ProgramData\Autodesk\Revit\Addins\2026\AreaCalculations.addin"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations_is1"; Flags: deletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{#MyAppName}"; Flags: deletekey

Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{{75B951C9-A0E1-43AA-BB76-DDEAF1781D38}_is1"; ValueType: string; ValueName: "DisplayName"; ValueData: "{#MyAppName} {#MyAppVersion}"; Flags: uninsdeletekey
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{{75B951C9-A0E1-43AA-BB76-DDEAF1781D38}_is1"; ValueType: string; ValueName: "UninstallString"; ValueData: "{uninstallexe}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{{75B951C9-A0E1-43AA-BB76-DDEAF1781D38}_is1"; ValueType: string; ValueName: "DisplayVersion"; ValueData: "{#MyAppVersion}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{{75B951C9-A0E1-43AA-BB76-DDEAF1781D38}_is1"; ValueType: string; ValueName: "Publisher"; ValueData: "{#MyAppPublisher}"
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Uninstall\{{75B951C9-A0E1-43AA-BB76-DDEAF1781D38}_is1"; ValueType: string; ValueName: "DisplayIcon"; ValueData: "{app}\installerIcon.ico"

[Code]
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations_is1') then
  begin
    Exec(ExpandConstant('{sys}\msiexec.exe'), '/x{75B951C9-A0E1-43AA-BB76-DDEAF1781D38} /qn', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations');
    RegDeleteKeyIncludingSubkeys(HKEY_LOCAL_MACHINE, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\IPA-AreaCalculations_is1');
  end;
end;

[InstallDelete]
Type: filesandordirs; Name: "{app}"
