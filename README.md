# ELKA EventLens

![ELKA EventLens](docs/assets/github-social-preview.jpg)

ELKA EventLens is a Windows-only WPF helper for reading Windows Event Viewer errors and warnings in a cleaner card-based UI.

> **Development preview: 0.1.0-dev.4.** Under active development, not a stable release. Intended for testing. Event explanations are troubleshooting suggestions, not confirmed diagnoses.

## Downloads

Get the installer or portable ZIP from the [development preview release](https://github.com/torment78/ELKA.EventLens/releases/tag/v0.1.0-dev.4).

- **Installer:** a dark ELKA-branded setup wizard, with optional desktop shortcut. New installs default to `Program Files\ElkaSoft\ELKA EventLens`. Existing installs retain their selected directory unless changed on the setup directory page.
- **Portable ZIP:** extract the whole `ElkaEventLensPort` folder to a writable location, then run `ELKA.EventLens.App.exe`. Keep its supporting files together.
- Both packages target Windows 10/11 x64 and include .NET. Embedded browsing requires Microsoft Edge WebView2. Setup installs it if missing, requiring internet access; portable users need the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) installed separately.
- Preview binaries are not code-signed. SHA-256 checksums accompany each release.

## Updating

Install this version once to gain **Settings > Check for updates**. The updater checks this public GitHub repository, including development previews, and offers **Download and install** when a newer version exists. After checksum verification and your confirmation, Windows starts Setup, EventLens saves and exits, Setup waits for that exit, then installs and reopens the app. Downloads, network failures, and cancelled elevation do not force the app to close. No automatic Windows reboot is requested.

Portable copies offer **Download portable ZIP** instead. The ZIP is saved beside the portable app under `Updates` and revealed in Explorer. Exit the app before extracting it; keep the existing `Saves`, `Logs`, and `WebView2Profile` folders. A portable update never runs the system installer.

See [updater behavior and validation](docs/UPDATES.md). Older previews without the update button need one manual installation of this release.

## v1 scope

- Loads recent Application and System Critical/Error/Warning events on startup.
- Watches selected logs live with `EventLogWatcher`.
- Handles Security log permission failures without crashing.
- Shows expandable event cards with message, raw XML, technical details, and a plain-language explanation.
- Opens Web, Reddit, Stack Overflow, and Microsoft-focused searches in an integrated WebView2 tab pane.
- Reuses an existing in-app search tab when the same event/search target is clicked again.
- Stores settings and recent history under `%AppData%\ElkaSoft\ELKA EventLens\Saves` for normal installs, or beside the EXE when launched from an `ElkaEventLensPort` portable folder.
- Custom ELKA-styled corner notifications for new Error/Critical events, with repeat throttling and a tray menu.
- Dark theme by default.
- Small, animated pixel scroll steps, wrapping messages, and slim rounded scrollbars.
- Settings > Appearance opens hue wheels for the background, outlines, accents, and severity colors. Changes preview live; Cancel restores the previous palette. Hue rotation preserves perceptual lightness.
- Search engine choices: Brave, Google, DuckDuckGo, and Bing. Reddit and Microsoft searches keep their site restrictions; Stack Overflow keeps its direct site search.
- ChatGPT, Claude, and Microsoft Copilot use the shared browser profile. Sign in from Settings > AI provider > Log in / check session.
- The AI prompt can be expanded or collapsed and collapses after a successful send. Settings > Advanced Chat Prompt provides a customizable copy of the default template and a restore button.
- Dark web pages combines the browser's dark preference with automatic darkening for light-only sites. Changes apply to existing tabs. Each tab retains its browser instance until closed.
- Minimize and close hide to the tray; use the tray's Exit command to quit.

AI auto-send is best-effort because provider websites can change their composer or login flow. When a composer is unavailable, the prompt remains available to copy and send manually. The app does not store account passwords itself; sessions use the existing WebView2 profile.

## Verification

```powershell
dotnet build ELKA.EventLens.sln
dotnet run --project tests/ELKA.EventLens.Checks/ELKA.EventLens.Checks.csproj
```

The Windows-only checks use synthetic events and a local browser page, covering search URLs, settings, hue lightness, wrapping, pixel scrolling, browser darkening, and AI send-once behavior. Rendered screenshots are written to `artifacts/checks`.

Browser darkening uses [Chromium's automatic dark-mode override](https://chromedevtools.github.io/devtools-protocol/tot/Emulation/#method-setAutoDarkModeOverride). Native title-bar colors use [Windows DWM attributes](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwmwindowattribute); color overrides require a supported Windows version.

## Open in Visual Studio

Open `ELKA.EventLens.sln` in Visual Studio 2022 with .NET 8 SDK and the **.NET desktop development** workload. The included `.vsconfig` lists the workload.

Set `ELKA.EventLens.App` as the startup project if Visual Studio does not do it automatically, then press F5. No API keys are required.

## Notes

Security log monitoring usually requires administrator permissions. If the app is not elevated, it should continue running and show a status message instead of crashing.

The integrated search pane uses Microsoft Edge WebView2. Current Windows 10/11 installs usually already have the WebView2 runtime.

## Future roadmap

- Add nearby-event context to AI prompts.
- Add a solution button that prioritizes Microsoft Learn, Microsoft Support, vendor docs, and carefully filtered Reddit/community results.
- Add start with Windows.
- Add stronger grouping for repeated identical errors and optional export/share bundles for troubleshooting.
- Add a proper notification suspend mode for gaming or full-screen work.

## Build Release Packages

On Windows with .NET 8 SDK and Inno Setup 6.6 or newer installed in its default location:

```powershell
./scripts/Build-Release.ps1 -Version 0.1.0-dev.4
```

Outputs are written to `artifacts/installer`: the setup EXE, portable ZIP, and SHA-256 checksums. The build downloads Microsoft's WebView2 bootstrapper and verifies its Microsoft Authenticode signature before bundling it. Only a fresh publish directory is packaged; local settings, history, browser profiles, and logs are excluded.

Run `./scripts/Prepare-Branding.ps1` to regenerate the small web images and installer graphics from the full-size artwork. See [branding](docs/BRANDING.md) and the [artwork gallery](docs/official-cards/README.md).

## Local Data

Normal installations keep settings and event history in `%AppData%\ElkaSoft\ELKA EventLens\Saves`, with logs and the browser profile under `%LocalAppData%\ElkaSoft\ELKA EventLens`. Uninstalling preserves these user data folders.

Existing data is migrated before settings or browsers are opened. The migration never overwrites an existing destination; if blocked, it keeps using the old location for that launch. After success, a completion marker makes the new layout authoritative and legacy folders are no longer consulted. Exit old EventLens copies before upgrading. See the [installation and storage convention](docs/INSTALLATION.md), which is also the default for future ELKA apps. Portable storage does not change.

Portable mode is detected when the app base folder is named `ElkaEventLensPort` or contains `ELKA.EventLens.portable`.

Portable storage is kept next to the EXE:

```text
Saves\settings.json
Saves\events.json
Logs\ELKA.EventLens-*.log
WebView2Profile\
```

The log service reuses the newest log for up to 24 hours, then starts a new timestamped log. Browser sessions may be tied to a Windows account or machine and are not guaranteed to stay signed in when moved to another computer. Treat portable folders as private data; do not publish their contents.
