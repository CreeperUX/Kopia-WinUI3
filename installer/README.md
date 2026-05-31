# Installer

Rclone WinUI3 uses a custom machine-wide .NET installer because the app is currently an unpackaged WinUI 3 desktop application.

Build the installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1 -Configuration Release -Runtime win-x64
```

The installer is created at:

```text
artifacts\installer\RcloneWinUI3-Setup-win-x64.exe
```

Install behavior:

- Installs to `%ProgramFiles%\Rclone WinUI3`.
- Includes the published WinUI app and bundled `bin\rclone.exe`.
- Removes the previous per-user `%LOCALAPPDATA%\Programs\Rclone WinUI3` install if it exists.
- Shows a small setup wizard with option selection, installation progress, and a completion page.
- Requires administrator rights and prompts through UAC.
- Lets the user choose whether to create a Start menu shortcut for all users.
- Lets the user choose whether to create a desktop shortcut for all users.
- Registers an uninstall entry under the machine-wide Windows uninstall registry.
- Adds a machine-wide App Paths entry for `RcloneWinUI3.exe`.
- Supports quiet installation through the generated installer with `/Q`.

It is generated as a single self-contained `.exe` with the application payload embedded inside it. A future MSI/WiX package can replace this bootstrapper if deeper Windows Installer integration is needed.
