#define MyAppName "ELKA EventLens"
#define MyAppPublisher "ElkaSoft"
#define MyAppExeName "ELKA.EventLens.App.exe"
#define MyCompanyFolderName "Elka Software"

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef ReleaseVersion
  #define ReleaseVersion "0.1.0-dev.1"
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
Filename: "{app}\{#MyAppExeName}"; Description: "Launch ELKA EventLens"; Flags: nowait postinstall skipifsilent runasoriginaluser; Tasks: runafter

[UninstallDelete]
Type: dirifempty; Name: "{app}"

[Code]
var
  WelcomeLogo: TBitmapImage;
  FinishedLogo: TBitmapImage;

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
  if HasWebView2Runtime then
    Exit;
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
    Result := 'Microsoft WebView2 could not be installed (code ' + IntToStr(ExitCode) + '). Check your internet connection, or install the Evergreen WebView2 Runtime from Microsoft, then retry Setup.';
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
