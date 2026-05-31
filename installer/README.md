# Installer

Rclone WinUI3 uses a custom per-user .NET installer because the app is currently an unpackaged WinUI 3 desktop application.

Build the installer:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build-Installer.ps1 -Configuration Release -Runtime win-x64
```

The installer is created at:

```text
artifacts\installer\RcloneWinUI3-Setup-win-x64.exe
```

Install behavior:

- Installs to `%LOCALAPPDATA%\Programs\Rclone WinUI3`.
- Includes the published WinUI app and bundled `bin\rclone.exe`.
- Shows a small setup wizard with option selection, installation progress, and a completion page.
- Lets the user choose whether to create a Start menu shortcut.
- Lets the user choose whether to create a desktop shortcut.
- Registers an uninstall entry under the current user's Windows uninstall registry.
- Adds an App Paths entry for `RcloneWinUI3.exe`.
- Supports quiet installation through the generated installer with `/Q`.

This installer does not require administrator rights. It is generated as a single self-contained `.exe` with the application payload embedded inside it. A future MSI/WiX package can switch to a machine-wide `%ProgramFiles%` install if needed.
