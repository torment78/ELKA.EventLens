# ELKA Installation and Storage Convention

Confirmed September 29, 2026. Use the exact company-folder spelling **ElkaSoft** for future ELKA applications.

## Installed Applications

Each app gets its own folder inside the company folder:

```text
%ProgramFiles%\ElkaSoft\<App Name>\
%AppData%\ElkaSoft\<App Name>\Saves\
%LocalAppData%\ElkaSoft\<App Name>\Logs\
%LocalAppData%\ElkaSoft\<App Name>\WebView2Profile\
```

For EventLens, `<App Name>` is `ELKA EventLens`. The usual installer location is `C:\Program Files\ElkaSoft\ELKA EventLens`. Use Windows known folders rather than hard-coding the drive letter. Keep user data out of Program Files; uninstall preserves it.

Starting with `0.1.0-dev.4`, Setup redirects the old standard `Elka Software\ELKA EventLens` directory to `ElkaSoft\ELKA EventLens`, including a remembered directory or an old updater's `/DIR` argument. It runs the registered old copy's uninstaller before installing the replacement. Exit EventLens from its tray menu first; an in-use executable blocks removal. User data is outside the program folder and survives uninstall. Genuine custom installation directories remain unchanged.

Only the recognized EventLens installation is retired. Setup does not recursively delete either company folder, other applications, or unrecognized files. Empty old program/company folders are removed where possible. A linked path or a failed uninstall requires manual attention, rather than unsafe deletion.

## Existing EventLens Data

Before reading settings or starting a browser, an installed build migrates:

| Previous location | New location |
| --- | --- |
| `%AppData%\ELKA.EventLens` | `%AppData%\ElkaSoft\ELKA EventLens\Saves` |
| `%LocalAppData%\ELKA\EventLens\Logs` | `%LocalAppData%\ElkaSoft\ELKA EventLens\Logs` |
| `%LocalAppData%\ELKA\EventLens\WebView2Profile` | `%LocalAppData%\ElkaSoft\ELKA EventLens\WebView2Profile` |

The migration moves each entire directory, preserving files without copying a partial browser profile. It never overwrites or merges into an existing destination. If another EventLens copy is running, or a move fails because a folder is locked or inaccessible, the app keeps using that old folder for the current launch and records a note in the activity log. Exit old copies from their tray menu and restart to retry. Do not run an older build after migration; it still expects the previous paths.

Moving browser storage follows [Microsoft's WebView2 user-data-folder guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder). Close old EventLens/browser sessions before upgrading. Website sessions may still expire or require a new login.

Starting with `0.1.0-dev.3`, successful migration writes `%LocalAppData%\ElkaSoft\ELKA EventLens\storage-layout-v1.complete`. Subsequent launches use only the ElkaSoft paths, even if an old copy recreates its legacy folder. A blocked migration does not write this marker. Builds that already migrated with `dev.2` adopt the same marker without moving the data again.

Starting with `0.1.0-dev.4`, after the main window initializes successfully (including start-minimized mode), leftover legacy directories are retired. Conflicting or extra files move intact into a unique `LegacyRecovery` folder under the corresponding `%AppData%` or `%LocalAppData%\ElkaSoft\ELKA EventLens` app root. This is a whole-directory, same-volume move, not a copy followed by piecemeal deletion. Current settings and browser profiles are never overwritten or merged. Original file contents and timestamps are preserved, and recovery locations are recorded in the activity log. Empty old directories are removed; sibling apps are left alone.

Cleanup skips portable copies, incomplete migrations, other running EventLens copies, and linked paths. Failed moves retain their source and retry on a later launch. `legacy-cleanup-v2.complete` records successful retirement. Recovery folders are not automatically deleted; inspect them before discarding anything you may need.

## Portable Applications

Portable storage remains unchanged and never migrates installed data:

```text
ElkaEventLensPort\
  ELKA.EventLens.App.exe
  ELKA.EventLens.portable
  Saves\
  Logs\
  WebView2Profile\
```

## Applied Changes

No manual replacement is needed.

- SEARCH `MyCompanyFolderName` in `Installer/ELKA.EventLens.iss`. REPLACE BLOCK: the default company folder is now `ElkaSoft`.
- SEARCH `DataFolder` in `Services/AppStorageService.cs`. REPLACE BLOCK: resolve per-app company folders and migrate the prior layout. The previous path properties remain commented for reference.
- SEARCH `new ActivityLogService` in `App.xaml.cs`. ADD UNDER: record migration results in the activity log.
- SEARCH `CheckStorage` in the checks project for isolated migration, conflict, retry, and portable tests.
- SEARCH `SelectCurrentCompanyFolder` and `RetireLegacyInstallation` in the installer for the legacy upgrade fix.
- SEARCH `UpgradeInstallFolder` in `AppUpdateService` for remapping older installed paths.
- SEARCH `await viewModel.InitializeAsync()` in `MainWindow.xaml.cs`. ADD UNDER: one-time background cleanup after initialization.
- SEARCH `LegacyStorageCleanup` and `LegacyCleanupChecks` for recovery, completion, and data-safety behavior.
- RUN `./scripts/Test-LegacyInstaller.ps1` for isolated old-folder upgrade tests. It uses the fetched `v0.1.0-dev.3` tag, a random AppId, workspace-only paths, and a harmless executable. It never installs the production app or touches real saves.
