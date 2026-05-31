# Rclone WinUI3

Windows-native desktop shell for common rclone transfer workflows, built with WinUI 3 and .NET 8.

The app wraps `rclone.exe` instead of reimplementing transfer logic. It focuses on making everyday copy, sync, and check operations easier to start and monitor from a desktop UI while still exposing the important rclone knobs.

## Current scope

This repository currently contains a practical MVP:

- Locate a bundled `rclone.exe` from the app output or `third_party/rclone`, with a fallback to `PATH`.
- Show local diagnostics, including rclone path, version, task status, progress, speed, elapsed time, ETA, and CLI output.
- Pick local source and destination folders from the UI.
- Accept direct rclone paths and remotes, such as `remote:backup/project`.
- Run `rclone copy` or `rclone sync`.
- Optionally run with `--checksum`, `--dry-run`, and post-transfer `rclone check`.
- Tune `--transfers`, `--checkers`, `--buffer-size`, `--bwlimit`, and additional rclone arguments.
- Keep the installer path open for a single setup executable that installs both the GUI and rclone.

## Prerequisites

- Windows 10 version 1809 or newer.
- .NET 8 SDK.
- Visual Studio 2022 with Windows desktop and WinUI / Windows App SDK build components, or an equivalent CLI build environment.

The project references Windows App SDK packages directly, so the WinUI templates are not required just to build this existing project.

## Bundled rclone

This project is intended to ship with `rclone.exe` directly bundled in the repository under:

```text
third_party\rclone\rclone.exe
third_party\rclone\VERSION
```

The current bundled binary is:

```text
rclone v1.74.2, windows/amd64
```

During build and publish, the app copies the bundled executable to `bin\rclone.exe` in the output directory. The app still has a `PATH` fallback for developer machines, but release builds should rely on the bundled copy.

## Build and run

```powershell
dotnet restore .\src\RcloneWinUI3\RcloneWinUI3.csproj
dotnet build .\src\RcloneWinUI3\RcloneWinUI3.csproj -c Debug
dotnet run --project .\src\RcloneWinUI3\RcloneWinUI3.csproj -c Debug --no-build
```

For a typical x64 debug build, the bundled rclone executable should be copied to a path like:

```text
src\RcloneWinUI3\bin\Debug\net8.0-windows10.0.22621.0\win-x64\bin\rclone.exe
```

If build fails with a missing Windows App SDK or packaging build task, install the Visual Studio workload/component set for Windows App SDK and WinUI development, then build again from Developer PowerShell or Visual Studio.

## Publish

```powershell
dotnet publish .\src\RcloneWinUI3\RcloneWinUI3.csproj -c Release -r win-x64 -p:Platform=x64
```

Publish output should include the WinUI app, self-contained .NET runtime files, Windows App SDK runtime files, and `bin\rclone.exe`.

## Repository layout

```text
src/RcloneWinUI3/         WinUI 3 desktop app
installer/wix/            WiX packaging notes and starter files
third_party/rclone/       Bundled rclone executable and version marker
demo/                     Static demo assets and experiments
```

## Notes for contributors

- Treat rclone as the source of truth for transfer behavior.
- Prefer adding UI and orchestration around rclone commands instead of duplicating rclone logic.
- Keep user rclone configuration outside the app install directory. On Windows, rclone normally stores config under `%APPDATA%\rclone`.
- Preserve local data and rclone config during uninstall unless the user explicitly asks to remove it.
