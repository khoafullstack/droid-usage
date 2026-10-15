# Factory Droid Usage (Windows)

## Releasing

Versions come from git tags (`vX.Y.Z`). Local builds without a tag report `0.0.0-dev`.

```powershell
git tag v1.2.3
.\windows\scripts\build-installer.ps1            # x64 + arm64 into windows\releases\
.\windows\scripts\build-installer.ps1 -Arch x64  # one architecture
$env:GITHUB_TOKEN = "<token>"
.\windows\scripts\build-installer.ps1 -Publish   # also upload to GitHub Releases
```

Before the first publish, set `FactoryEndpoints.ReleasesRepoUrl` to the real repository.
For local testing without a tag: `-Version 0.1.0 -SkipTagCheck`.

Each architecture is published on its own Velopack channel (`win-x64`, `win-arm64`), and the app
picks the channel that matches its process architecture. Keep `windows\releases\` between builds
if you want delta packages.

## Install locations

- App: `%LocalAppData%\FactoryDroidUsage` (removed on uninstall)
- Settings and logs: `%LocalAppData%\DroidUsage` (kept on uninstall)

Installers are unsigned, so SmartScreen will warn until a code-signing certificate is added
(`vpk pack --signParams ...`).
