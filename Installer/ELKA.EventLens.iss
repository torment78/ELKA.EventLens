#define MyAppName "ELKA EventLens"
#define MyAppPublisher "ElkaSoft"
#define MyAppExeName "ELKA.EventLens.App.exe"
#define MyCompanyFolderName "ElkaSoft"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef ReleaseVersion
  #define ReleaseVersion "0.1.0-dev.4"
#endif
#ifndef SourcePublishDir
  #error SourcePublishDir must point to a clean installed-mode publish folder.
#endif
#ifndef InstallerOutputDir
  #error InstallerOutputDir is required.
#endif
#ifndef WebViewBootstrapper
  #error WebViewBootstrapper must point to Microsoft's signed Evergreen bootstrapper.
#endif
#if Ver < EncodeVer(6, 6, 0)
  #error This installer requires Inno Setup 6.6 or newer.
#endif

[Setup]
AppId={{B536C27A-C350-4B80-9C78-E3AC1AEE58DA}
AppName={#MyAppName}
AppVersion={#ReleaseVersion}
AppVerName={#MyAppName} {#ReleaseVersion} - Development Preview
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/torment78/ELKA.EventLens
AppSupportURL=https://github.com/torment78/ELKA.EventLens/issues
AppUpdatesURL=https://github.com/torment78/ELKA.EventLens/releases
VersionInfoVersion={#AppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Development Preview Installer
VersionInfoProductName={#MyAppName}
DefaultDirName={autopf}\{#MyCompanyFolderName}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
DisableWelcomePage=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
UsePreviousLanguage=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=ELKA_EventLens_Setup_{#ReleaseVersion}
SetupIconFile=..\src\ELKA.EventLens.App\Assets\EventLens-v1.ico
WizardImageFile=Branding\wizard-left.png
WizardSmallImageFile=Branding\wizard-small.png
WizardStyle=modern dark polar includetitlebar
WizardSizePercent=120,120
Compression=lzma2/ultra64
SolidCompression=yes
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} (Development Preview)
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked
Name: "runafter"; Description: "Launch ELKA EventLens after installation"; GroupDescription: "Post-install:"

[Files]
Source: "Branding\ElkaSoft.png"; Flags: dontcopy
Source: "{#WebViewBootstrapper}"; DestName: "MicrosoftEdgeWebview2Setup.exe"; Flags: dontcopy
Source: "{#SourcePublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch ELKA EventLens"; Flags: nowait postinstall skipifsilent runasoriginaluser; Tasks: runafter; Check: not IsAutoUpdate
Filename: "{app}\{#MyAppExeName}"; Flags: nowait runasoriginaluser; Check: IsAutoUpdate

[UninstallDelete]
Type: dirifempty; Name: "{app}"

[Code]
var
  WelcomeLogo: TBitmapImage;
  FinishedLogo: TBitmapImage;
  LegacyUninstaller: String;
  LegacyRetired: Boolean;

function OpenProcess(DesiredAccess: LongWord; InheritHandle: Boolean; ProcessId: LongWord): THandle;
  external 'OpenProcess@kernel32.dll stdcall';
function WaitForSingleObject(Handle: THandle; Milliseconds: LongWord): LongWord;
  external 'WaitForSingleObject@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';
function GetFileAttributes(FileName: String): LongWord;
  external 'GetFileAttributesW@kernel32.dll stdcall';
function CreateFile(FileName: String; DesiredAccess, ShareMode, SecurityAttributes,
  CreationDisposition, FlagsAndAttributes: LongWord; TemplateFile: THandle): THandle;
  external 'CreateFileW@kernel32.dll stdcall';

function LegacyInstallDir: String;
begin
  Result := ExpandConstant('{autopf}\Elka Software\{#MyAppName}');
end;

function SameDirectory(LeftPath, RightPath: String): Boolean;
begin
  Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(LeftPath)),
    RemoveBackslashUnlessRoot(ExpandFileName(RightPath))) = 0;
end;

function IsPlainPath(Path: String): Boolean;
var
  Parent: String;
  Attributes: LongWord;
begin
  Result := False;
  Path := RemoveBackslashUnlessRoot(ExpandFileName(Path));
  while Path <> '' do
  begin
    Attributes := GetFileAttributes(Path);
    if (Attributes <> $FFFFFFFF) and ((Attributes and $400) <> 0) then Exit;
    Parent := ExtractFileDir(Path);
    if Parent = Path then Break;
    Path := Parent;
  end;
  Result := True;
end;

procedure SelectCurrentCompanyFolder;
var
  PreviousDir, UninstallCommand, Expected: String;
  Key: String;
begin
  { UsePreviousAppDir and older updaters' /DIR both selected the obsolete path. }
  if SameDirectory(WizardDirValue, LegacyInstallDir) then
    WizardForm.DirEdit.Text := ExpandConstant('{autopf}\{#MyCompanyFolderName}\{#MyAppName}');

  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{B536C27A-C350-4B80-9C78-E3AC1AEE58DA}_is1';
  Expected := AddBackslash(LegacyInstallDir) + 'unins000.exe';
  if RegQueryStringValue(HKA, Key, 'InstallLocation', PreviousDir) and
     SameDirectory(PreviousDir, LegacyInstallDir) and
     RegQueryStringValue(HKA, Key, 'UninstallString', UninstallCommand) and
     ((CompareText(UninstallCommand, Expected) = 0) or (CompareText(UninstallCommand, '"' + Expected + '"') = 0)) and
     FileExists(Expected) and IsPlainPath(Expected) then
    LegacyUninstaller := Expected;
end;

function RetireLegacyInstallation: String;
var
  ExitCode: Integer;
  FileHandle: THandle;
  OldExecutable: String;
begin
  Result := '';
  if LegacyRetired or (LegacyUninstaller = '') or SameDirectory(WizardDirValue, LegacyInstallDir) then Exit;
  if not IsPlainPath(LegacyUninstaller) then
  begin
    Result := 'The old installation contains a linked path. It was left untouched. Uninstall that copy manually before continuing.';
    Exit;
  end;
  { PrepareToInstall precedes Inno's normal close-applications check. }
  OldExecutable := AddBackslash(LegacyInstallDir) + '{#MyAppExeName}';
  if FileExists(OldExecutable) then
  begin
    FileHandle := CreateFile(OldExecutable, $40000000, 7, 0, 3, 0, 0);
    if FileHandle = THandle(-1) then
    begin
      Result := 'Close the old EventLens from its tray menu before retrying Setup. Its program file is in use or cannot be accessed.';
      Exit;
    end;
    CloseHandle(FileHandle);
  end;
  { Use its own uninstaller, never recursively delete Program Files or the company folder. }
  if not Exec(LegacyUninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'The previous EventLens version could not be uninstalled. Close it from the tray menu and retry.';
    Exit;
  end;
  if (ExitCode <> 0) or FileExists(AddBackslash(LegacyInstallDir) + '{#MyAppExeName}') then
  begin
    Result := 'The previous EventLens files are still in use. Close EventLens from its tray menu and retry Setup.';
    Exit;
  end;
  LegacyRetired := True;
  RemoveDir(LegacyInstallDir);
  RemoveDir(ExtractFileDir(LegacyInstallDir));
  Log('Retired the registered legacy EventLens installation; AppData and unrelated files were preserved.');
end;

function IsAutoUpdate: Boolean;
begin
  Result := ExpandConstant('{param:AUTOUPDATE|0}') = '1';
end;

function WaitForEventLensExit: Boolean;
var
  ProcessId: Integer;
  ProcessHandle: THandle;
begin
  Result := True;
  if not IsAutoUpdate then Exit;
  ProcessId := StrToIntDef(ExpandConstant('{param:ELKAPID|0}'), 0);
  if ProcessId <= 0 then
  begin
    Result := False;
    Exit;
  end;
  ProcessHandle := OpenProcess($00100000, False, ProcessId);
  if ProcessHandle = 0 then
  begin
    { ERROR_INVALID_PARAMETER means the launching process has already exited. }
    Result := DLLGetLastError = 87;
    Exit;
  end;
  try
    Result := WaitForSingleObject(ProcessHandle, 30000) = 0;
  finally
    CloseHandle(ProcessHandle);
  end;
end;

function HasWebView2Runtime: Boolean;
var
  Version: String;
  Key: String;
begin
  Key := 'Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}';
  Result := (RegQueryStringValue(HKLM32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0')) or
            (RegQueryStringValue(HKCU32, Key, 'pv', Version) and (Version <> '') and (Version <> '0.0.0.0'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
begin
  Result := '';
  if not WaitForEventLensExit then
  begin
    Result := 'EventLens has not exited yet. Close EventLens from its tray menu before retrying the update.';
    Exit;
  end;
  if HasWebView2Runtime then
  begin
    Result := RetireLegacyInstallation;
    Exit;
  end;
  WizardForm.StatusLabel.Caption := 'Installing Microsoft Edge WebView2 Runtime...';
  ExtractTemporaryFile('MicrosoftEdgeWebview2Setup.exe');
  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
  begin
    Result := 'Microsoft WebView2 could not be started. Check your internet connection and try again.';
    Exit;
  end;
  if ExitCode = 3010 then
    NeedsRestart := True;
  if not HasWebView2Runtime then
    Result := 'Microsoft WebView2 could not be installed (code ' + IntToStr(ExitCode) + '). Check your internet connection, or install the Evergreen WebView2 Runtime from Microsoft, then retry Setup.'
  else
    Result := RetireLegacyInstallation;
end;

procedure AddBrandLogo(var Logo: TBitmapImage; ParentPage: TNewNotebookPage; LeftEdge: Integer);
begin
  Logo := TBitmapImage.Create(WizardForm);
  Logo.Parent := ParentPage;
  Logo.SetBounds(LeftEdge, ParentPage.Height - ScaleY(120), ScaleX(108), ScaleY(108));
  Logo.Stretch := True;
  Logo.PngImage.LoadFromFile(ExpandConstant('{tmp}\ElkaSoft.png'));
end;

procedure InitializeWizard;
begin
  SelectCurrentCompanyFolder;
  ExtractTemporaryFile('ElkaSoft.png');
  WizardForm.WelcomeLabel1.Caption := 'ELKA EventLens' + #13#10 + 'Development Preview';
  WizardForm.WelcomeLabel1.Height := ScaleY(62);
  WizardForm.WelcomeLabel2.Top := WizardForm.WelcomeLabel1.Top + WizardForm.WelcomeLabel1.Height + ScaleY(12);
  WizardForm.WelcomeLabel2.Caption :=
    'Monitor Windows events, understand warnings, and investigate problems from one desktop tool.' + #13#10 + #13#10 +
    'This development preview is intended for testing. .NET is included; Microsoft WebView2 is installed if needed (internet required).' + #13#10 + #13#10 +
    'Version {#ReleaseVersion}  |  Windows x64';
  WizardForm.FinishedHeadingLabel.Caption := 'EventLens is ready to test';
  WizardForm.FinishedLabel.Caption :=
    'ELKA EventLens Development Preview has been installed.' + #13#10 + #13#10 +
    'Use the tray icon to reopen the app. Choose Exit from the tray menu to quit.';
  AddBrandLogo(WelcomeLogo, WizardForm.WelcomePage, WizardForm.WelcomeLabel1.Left);
  AddBrandLogo(FinishedLogo, WizardForm.FinishedPage, WizardForm.FinishedHeadingLabel.Left);
end;
