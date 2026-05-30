# WiX installer plan

The first installer target should be a single `KopiaWinUI3-Setup-x64.exe` bootstrapper.

Recommended contents:

- Published WinUI 3 app from `dotnet publish`.
- `bin/kopia.exe` copied from `third_party/kopia`.
- WebView2 Evergreen Runtime bootstrapper or offline runtime.
- Apache-2.0 license and third-party notices.

Recommended behavior:

- Install to `%ProgramFiles%\Kopia WinUI3`.
- Create Start menu shortcut.
- Do not delete `%APPDATA%\kopia` during uninstall by default.
- Keep GUI as normal user. Only the installer should elevate.

The actual WiX authoring should be added once the first publish output path is stable.
