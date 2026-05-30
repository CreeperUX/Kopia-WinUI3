# Kopia WinUI3

Windows-native shell for Kopia built with WinUI 3.

## Current scope

This repository starts with a practical MVP:

- Bundle `kopia.exe` with a WinUI 3 desktop app.
- Start `kopia server start` on a dynamic `127.0.0.1` port.
- Reserve the main content area for Kopia's existing HTML UI. WebView2 is temporarily disabled in the runnable demo because the current lightweight WinUI CLI toolchain exposes a WebView2/WinRT projection conflict that needs a separate compatibility pass.
- Show local diagnostics: Kopia path, version, server URL, and process logs.
- Keep the installer path open for a single setup executable that installs both the GUI and Kopia itself.

## Prerequisites

- Windows 10 1809 or newer.
- .NET 8 SDK.
- WinUI CLI templates:

```powershell
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
```

- WebView2 Runtime on the target machine.

Visual Studio 2022 is optional. The project uses `Microsoft.Windows.SDK.BuildTools.WinApp` so the basic app can be built from the .NET CLI.

## Prepare Kopia

Download the Windows Kopia CLI binary into `third_party/kopia`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Get-Kopia.ps1 -Version 0.23.0 -Architecture x64
```

The app also searches for `kopia.exe` on `PATH`, but the installer build should use the bundled copy. After build, the local output should contain:

```text
src\KopiaWinUI3\bin\Debug\net8.0-windows10.0.22621.0\win-x64\bin\kopia.exe
```

## Build

```powershell
dotnet restore .\src\KopiaWinUI3\KopiaWinUI3.csproj
dotnet build .\src\KopiaWinUI3\KopiaWinUI3.csproj -c Debug
dotnet run --project .\src\KopiaWinUI3\KopiaWinUI3.csproj -c Debug --no-build
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
