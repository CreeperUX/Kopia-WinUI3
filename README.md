# Kopia WinUI3

Windows-native shell for Kopia built with WinUI 3.

## Current scope

This repository starts with a practical MVP:

- Bundle `kopia.exe` with a WinUI 3 desktop app.
- Start `kopia server start` on a dynamic `127.0.0.1` port.
- Host Kopia's existing HTML UI in WebView2.
- Show local diagnostics: Kopia path, version, server URL, and process logs.
- Keep the installer path open for a single setup executable that installs both the GUI and Kopia itself.

## Prerequisites

- Windows 10 1809 or newer.
- .NET 8 SDK.
- Visual Studio 2022 with WinUI/Windows App SDK components. A plain .NET SDK install is not enough for a full WinUI build because the XAML/PRI packaging tasks are provided by the Visual Studio Windows app tooling.
- WebView2 Runtime on the target machine.

## Prepare Kopia

Download the Windows Kopia CLI binary into `third_party/kopia`:

```powershell
.\scripts\Get-Kopia.ps1 -Version 0.23.0 -Architecture x64
```

The app also searches for `kopia.exe` on `PATH`, but the installer build should use the bundled copy.

## Build

```powershell
$env:DOTNET_CLI_HOME = Join-Path (Get-Location) '.dotnet-home'
dotnet restore .\src\KopiaWinUI3\KopiaWinUI3.csproj
dotnet build .\src\KopiaWinUI3\KopiaWinUI3.csproj -c Debug -p:Platform=x64
```

If build fails with a missing `Microsoft.Build.Packaging.Pri.Tasks.dll`, install the Visual Studio workload/component set for Windows App SDK and WinUI development, then build again from Developer PowerShell or Visual Studio.

## Publish

```powershell
dotnet publish .\src\KopiaWinUI3\KopiaWinUI3.csproj -c Release -r win-x64 -p:Platform=x64
```

## Repository layout

```text
src/KopiaWinUI3/          WinUI 3 app
scripts/                 Developer scripts
installer/wix/           WiX packaging notes and starter files
third_party/kopia/        Local Kopia binary cache, not committed
```
