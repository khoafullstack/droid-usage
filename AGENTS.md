# Repository Guidelines

## Project Structure & Module Organization
The repo holds one app: a WinUI 3 (.NET 8) floating widget that shows Factory Droid usage limits. All code lives under `windows/`.

- `windows/DroidUsage/` is the only project (`DroidUsage.csproj`, solution at `windows/DroidUsage.sln`).
- Layers: `Composition/` (DI in `ServiceRegistration.cs`), `Configuration/` (paths, endpoints, `AppInfo`), `Models/`, `Presentation/` (formatting logic), `ViewModels/`, `Views/` (XAML), `Platform/` (Win32 tray, window, Credential Manager), `Logging/`, and `Services/` (`Abstractions/` interfaces, plus `Api/`, `Auth/`, `Security/`, `Settings/`, `Update/`).
- `windows/DroidUsage/Assets/` holds icons. `Program.cs` is the custom entry point; it must call `VelopackApp.Build().Run()` first.
- `windows/scripts/build-installer.ps1` builds installers. `windows/artifacts/` and `windows/releases/` are generated and git-ignored.

## Build, Test, and Development Commands
Run from `windows/`:

- `dotnet build DroidUsage\DroidUsage.csproj -c Release -p:Platform=x64` builds the app (use `-p:Platform=ARM64 -r win-arm64` for ARM64).
- `.\scripts\build-installer.ps1 -Version 0.1.0 -SkipTagCheck` builds x64 and ARM64 Velopack installers into `windows\releases\`.
- `.\scripts\build-installer.ps1` (no flags) builds from the `vX.Y.Z` tag on HEAD and requires a clean tree. Add `-Publish` with `GITHUB_TOKEN` to upload to GitHub Releases.

## Coding Style & Naming Conventions
C# with `Nullable` and `ImplicitUsings` enabled, 4-space indentation, file-scoped namespaces matching the folder (`DroidUsage.Services.Auth`). Types and members use PascalCase, private fields `_camelCase`, interfaces `IName`. ViewModels use CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`). Register new services through `Composition/ServiceRegistration.cs` and depend on interfaces from `Services/Abstractions/`. No formatter or linter is configured; match surrounding code.

## Testing Guidelines
There is no test project yet. Verify changes by building both x64 and ARM64 with zero warnings and running the app. If you add tests, create `windows/DroidUsage.Tests/` and name tests `Method_Scenario_Result`.

## Commit & Pull Request Guidelines
Use Conventional Commits with a scope, as in history: `feat(windows): ...`, `refactor(windows): ...`. Keep the subject short and explain the why in the body. PRs should describe the change, list the checks you ran, and include a screenshot for UI changes.

## Security & Configuration Tips
Never log or commit tokens. Manual API keys are stored via DPAPI/Credential Manager, and Factory auth is read from `~/.factory`. Settings and logs live in `%LocalAppData%\DroidUsage`; the installed app lives in `%LocalAppData%\FactoryDroidUsage`, so keep those two names distinct. Set `FactoryEndpoints.ReleasesRepoUrl` before publishing a release.
