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

Setup remembers an existing installation's chosen directory. For a clean switch from the old `Elka Software` folder, uninstall the old version first (user data is preserved), then run the new installer. Changing only the directory during an upgrade can leave the old program files behind. A custom `/DIR` override remains supported. The default for a fresh installation is always the new company folder.

## Existing EventLens Data

Before reading settings or starting a browser, an installed build migrates:

| Previous location | New location |
| --- | --- |
| `%AppData%\ELKA.EventLens` | `%AppData%\ElkaSoft\ELKA EventLens\Saves` |
| `%LocalAppData%\ELKA\EventLens\Logs` | `%LocalAppData%\ElkaSoft\ELKA EventLens\Logs` |
| `%LocalAppData%\ELKA\EventLens\WebView2Profile` | `%LocalAppData%\ElkaSoft\ELKA EventLens\WebView2Profile` |

The migration moves each entire directory, preserving files without copying a partial browser profile. It never overwrites or merges into an existing destination. If another EventLens copy is running, or a move fails because a folder is locked or inaccessible, the app keeps using that old folder for the current launch and records a note in the activity log. Exit old copies from their tray menu and restart to retry. Do not run an older build after migration; it still expects the previous paths.

Moving browser storage follows [Microsoft's WebView2 user-data-folder guidance](https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/user-data-folder). Close old EventLens/browser sessions before upgrading. Website sessions may still expire or require a new login.

Starting with `0.1.0-dev.3`, successful migration writes `%LocalAppData%\ElkaSoft\ELKA EventLens\storage-layout-v1.complete`. Subsequent launches use only the ElkaSoft paths, even if an old copy recreates its legacy folder. A blocked migration does not write this marker. Old and new folders are never merged over each other; any conflicting old copy is left untouched as recovery data, but is not used after completion. Builds that already migrated with `dev.2` adopt the same marker without moving the data again.

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
