# Factory Droid Usage

A small always-on-top Windows widget that shows your [Factory](https://factory.ai) Droid usage limits at a glance, so you don't have to open the dashboard.

Built with WinUI 3 and .NET 8. Runs on Windows 10 (1809+) and Windows 11, x64 and ARM64.

## Features

- **Compact pill or expanded card.** Collapse to a tiny pill showing one metric, or expand for the full view.
- **All your limits.** 5-hour, weekly and monthly usage with progress bars and reset times, for both the Standard and Droid Core pools.
- **Plan badge.** Shows your current Factory plan.
- **System tray.** Hide the widget to the tray and bring it back any time.
- **Configurable.** Choose which metric the pill shows and how often usage refreshes (15 seconds to 10 minutes).
- **Automatic sign-in.** Reads the credentials the Factory CLI already stored in `~/.factory`, and refreshes tokens when needed. You can paste a token or API key manually instead.
- **Auto-update.** Checks GitHub Releases and shows an "Update" button when a new version is available.

## Install

1. Download the installer for your machine from the [latest release](https://github.com/khoafullstack/droid-usage/releases/latest):
   - `FactoryDroidUsage-win-x64-Setup.exe` for Intel/AMD PCs
   - `FactoryDroidUsage-win-arm64-Setup.exe` for ARM PCs (for example Snapdragon)
2. Run it. The app installs per-user, so no admin rights are needed.

The installers are not code-signed yet, so Windows SmartScreen may warn you. Click **More info**, then **Run anyway**.

A `Portable.zip` is also attached to each release if you prefer no installer.

## Usage

1. Sign in to Factory with the CLI (`droid`) once, so credentials exist in `~/.factory`. Or open the settings (gear icon) and enter a token manually.
2. Launch **Factory Droid Usage** from the Start menu. Drag the widget wherever you like; it remembers its position.
3. Use the gear icon to change the pill metric and refresh interval. Use **Hide to Tray** to get it out of the way.

Settings and logs are stored in `%LocalAppData%\DroidUsage`. Manual tokens are encrypted with Windows DPAPI and kept in Windows Credential Manager. They are never written in plain text.

## Build from source

Requires the .NET 8 SDK (or newer) and Windows.

```powershell
cd windows
dotnet build DroidUsage\DroidUsage.csproj -c Release -p:Platform=x64
```

Build installers (outputs to `windows\releases\`):

```powershell
.\windows\scripts\build-installer.ps1 -Version 0.1.0 -SkipTagCheck
```

Releasing, versioning from git tags and publishing to GitHub are covered in [windows/README.md](windows/README.md). Contributor notes are in [AGENTS.md](AGENTS.md).

## Project layout

```
windows/DroidUsage/
  Composition/     dependency injection setup
  Configuration/   paths, endpoints, app info
  Models/          data types
  Presentation/    usage formatting logic
  ViewModels/      MVVM view models
  Views/           XAML window
  Platform/        tray icon, window helpers, Credential Manager
  Services/        API client, auth, settings, updates, security
```

## Disclaimer

This is an unofficial community project and is not affiliated with Factory.
