# In-App Updates

## User Flow

1. Open Settings and choose **Check for updates**.
2. When a newer version is available, choose **Download and install**.
3. The package is downloaded from this repository and checked against its SHA-256 checksum. Confirm installation when prompted.
4. Windows launches Setup, requesting elevation if needed. Only a successful launch lets EventLens close. Cancelling the elevation prompt keeps the app running.
5. Pending Settings changes and event history are saved, browsers are disposed, and the app exits instead of hiding in the tray.
6. Setup waits up to 30 seconds for the launching process to exit and reopens EventLens after success. Custom directories stay unchanged; the old standard `Elka Software\ELKA EventLens` installation is uninstalled and replaced under `ElkaSoft\ELKA EventLens`. It does not automatically reboot Windows.

Portable mode uses **Download portable ZIP**, stores the ZIP under the app's `Updates` folder, and opens Explorer with the file selected. It never installs to Program Files or closes the portable app automatically. Exit before extracting the update into the existing portable folder, preserving its `Saves`, `Logs`, and `WebView2Profile` directories.

## Release Selection and Verification

- Public [GitHub release listing](https://docs.github.com/en/rest/releases/releases) is used without a token or account login. The latest 100 releases are considered; drafts, incomplete uploads, unsupported tags, and older versions are ignored.
- Development builds accept stable versions and `vX.Y.Z-dev.N` releases. A stable build ignores prereleases. Numeric comparison handles `dev.10` correctly after `dev.9`.
- Exact repository asset URLs, expected filenames, reported package size, and matching SHA-256 checksums are required. Download size and metadata are bounded, and partial files are removed on cancellation/failure.
- The installer is rehashed before launch and kept read-locked through Windows accepting that launch. Checksums detect corruption; they are not a substitute for a code-signing certificate. Preview installers remain unsigned and Windows can warn about them.
- Normal downloads live under `%LocalAppData%\ElkaSoft\ELKA EventLens\Updates`. Downloads and app data are excluded from source control and release packages.
- A cancelled or failed installation may require reopening the existing app manually. No installer is silently retried or forced past an error.

Installer arguments follow [Inno Setup's command-line parameters](https://jrsoftware.org/ishelp/topic_setupcmdline.htm). `/AUTOUPDATE=1` and `/ELKAPID` are EventLens-specific: they select the process-exit wait and post-install restart. Ordinary manual installs keep their existing wizard behavior.

## Verification

```powershell
dotnet run --project tests/ELKA.EventLens.Checks/ELKA.EventLens.Checks.csproj
dotnet run --project tests/ELKA.EventLens.Checks/ELKA.EventLens.Checks.csproj -- --live-update-check --live-update-download
```

Default checks use synthetic metadata, packages, and UI fixtures. The optional live check fetches the public feed and verifies a real release download but never executes it. UAC approval itself remains a user action.

The installer wait/install/restart sequence is exercised with a separate test AppId and harmless executable in a workspace folder. `scripts/Test-LegacyInstaller.ps1` covers remembered old paths, old updater `/DIR` arguments, running-app protection, custom directories, and unrelated-file preservation. Test installation and uninstall do not touch the real EventLens installation.

## Applied Changes

- SEARCH `Updates` in `Views/SettingsWindow.xaml`. ADD UNDER the appearance control: version, update buttons, status, cancellation, and bounded progress.
- SEARCH `UpdateViewModel` for command state, asynchronous checks, downloads, and cancellation.
- SEARCH `AppUpdateService` for strict release selection, bounded HTTP downloads, checksums, and installer launch.
- SEARCH `ShutdownForUpdate` in `App.xaml.cs` for the real exit path.
- SEARCH `WaitForEventLensExit` in `Installer/ELKA.EventLens.iss` for the installer handoff.
