#define AppVersion "1.6.2"
[Setup]
AppId={{878E5A03-3413-49D4-933B-117D78A41555}
AppName=Tomatotodo
AppVersion={#AppVersion}
AppPublisher=Tomatotodo
DefaultDirName={localappdata}\Programs\Tomatotodo
DisableDirPage=no
DisableWelcomePage=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
WizardStyle=modern
SetupIconFile=..\Assets\AppIcon.ico
UninstallDisplayIcon={app}\Tomatotodo.Windows.exe
LicenseFile=Trial-Notice.txt
OutputDir=output
OutputBaseFilename=Tomatotodo-Setup-{#AppVersion}
VersionInfoVersion=1.6.2.0
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayName=Tomatotodo

[Languages]
Name: "chinesesimp"; MessagesFile: "ChineseSimplified.isl"

[Files]
Source: "publish-1.6.2-final\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"
Source: "..\Assets\AppIcon.ico"; DestDir: "{app}"

[Icons]
Name: "{userprograms}\Tomatotodo"; Filename: "{app}\Tomatotodo.Windows.exe"; WorkingDir: "{app}"; IconFilename: "{app}\AppIcon.ico"

[Run]
Filename: "{app}\Tomatotodo.Windows.exe"; Description: "立即打开 Tomatotodo"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent unchecked

[UninstallDelete]
Type: files; Name: "{userdesktop}\Tomatotodo.lnk"

[Code]
var DesktopOption: TNewCheckBox;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Tomatotodo', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\Tomatotodo.Windows.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Tomatotodo');
end;

procedure InitializeWizard;
begin
  DesktopOption := TNewCheckBox.Create(WizardForm);
  DesktopOption.Parent := WizardForm.FinishedPage;
  DesktopOption.Caption := '创建桌面快捷方式';
  DesktopOption.Left := WizardForm.RunList.Left;
  DesktopOption.Top := WizardForm.RunList.Top + ScaleY(32);
  DesktopOption.Width := ScaleX(300);
  DesktopOption.Height := ScaleY(24);
  DesktopOption.Checked := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssDone) and (not WizardSilent) and DesktopOption.Checked then
    CreateShellLink(ExpandConstant('{userdesktop}\Tomatotodo.lnk'), 'Tomatotodo',
      ExpandConstant('{app}\Tomatotodo.Windows.exe'), '', ExpandConstant('{app}'),
      ExpandConstant('{app}\AppIcon.ico'), 0, SW_SHOWNORMAL);
end;
