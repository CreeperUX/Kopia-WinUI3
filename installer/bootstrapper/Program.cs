using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Win32;

namespace RcloneWinUI3.Setup;

internal static class Program
{
    private const string AppName = "Rclone WinUI3";
    private const string AppId = "RcloneWinUI3";
    private const string ExeName = "RcloneWinUI3.exe";
    private const string Version = "1.0.0";
    private const string Publisher = "CreeperUX";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        var options = InstallOptions.Parse(args);
        try
        {
            if (options.Quiet)
            {
                InstallerCore.Install(options, static (_, _) => { });
                return 0;
            }

            Application.Run(new InstallerForm(options));
            return 0;
        }
        catch (Exception ex)
        {
            if (options.Quiet)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }

            MessageBox.Show(ex.Message, $"{AppName} Setup", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private sealed class InstallerForm : Form
    {
        private readonly InstallOptions _options;
        private readonly Panel _welcomePanel = new();
        private readonly Panel _progressPanel = new();
        private readonly Panel _finishPanel = new();
        private readonly CheckBox _startMenuCheck = new();
        private readonly CheckBox _desktopCheck = new();
        private readonly ProgressBar _progressBar = new();
        private readonly Label _statusLabel = new();
        private readonly Label _finishTitle = new();
        private readonly Label _finishMessage = new();

        public InstallerForm(InstallOptions options)
        {
            _options = options;
            Text = $"{AppName} Setup";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(700, 440);
            Icon = TryLoadIcon();

            BuildWelcomePanel();
            BuildProgressPanel();
            BuildFinishPanel();
        }

        private static Icon? TryLoadIcon()
        {
            try
            {
                using var stream = typeof(Program).Assembly.GetManifestResourceStream("AppIcon.ico");
                return stream is null ? null : new Icon(stream);
            }
            catch
            {
                return null;
            }
        }

        private void BuildWelcomePanel()
        {
            _welcomePanel.Dock = DockStyle.Fill;
            Controls.Add(_welcomePanel);

            var title = NewLabel("安装 Rclone WinUI3", 30, 26, 620, 36, 17, FontStyle.Bold);
            _welcomePanel.Controls.Add(title);

            var description = NewLabel("安装器会将应用和内置 rclone 安装到当前用户目录，并注册到 Windows 应用卸载列表。", 32, 78, 620, 48);
            _welcomePanel.Controls.Add(description);

            var pathLabel = NewLabel("安装位置：", 32, 140, 620, 24, 10, FontStyle.Bold);
            _welcomePanel.Controls.Add(pathLabel);

            var pathBox = new TextBox
            {
                Text = InstallerCore.InstallDir,
                Location = new Point(32, 168),
                Size = new Size(620, 28),
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle
            };
            _welcomePanel.Controls.Add(pathBox);

            _startMenuCheck.Text = "添加到开始菜单";
            _startMenuCheck.Checked = true;
            _startMenuCheck.AutoSize = true;
            _startMenuCheck.Location = new Point(34, 222);
            _welcomePanel.Controls.Add(_startMenuCheck);

            _desktopCheck.Text = "创建桌面快捷方式";
            _desktopCheck.Checked = false;
            _desktopCheck.AutoSize = true;
            _desktopCheck.Location = new Point(34, 256);
            _welcomePanel.Controls.Add(_desktopCheck);

            var cancelButton = NewButton("取消", 474, 374);
            cancelButton.Click += (_, _) => Close();
            _welcomePanel.Controls.Add(cancelButton);

            var installButton = NewButton("安装", 576, 374);
            installButton.Click += async (_, _) => await InstallAsync();
            _welcomePanel.Controls.Add(installButton);
        }

        private void BuildProgressPanel()
        {
            _progressPanel.Dock = DockStyle.Fill;
            _progressPanel.Visible = false;
            Controls.Add(_progressPanel);

            _progressPanel.Controls.Add(NewLabel("正在安装", 30, 26, 620, 36, 17, FontStyle.Bold));

            _statusLabel.Text = "准备开始...";
            _statusLabel.Location = new Point(32, 102);
            _statusLabel.Size = new Size(620, 48);
            _progressPanel.Controls.Add(_statusLabel);

            _progressBar.Minimum = 0;
            _progressBar.Maximum = 100;
            _progressBar.Location = new Point(32, 166);
            _progressBar.Size = new Size(620, 24);
            _progressPanel.Controls.Add(_progressBar);
        }

        private void BuildFinishPanel()
        {
            _finishPanel.Dock = DockStyle.Fill;
            _finishPanel.Visible = false;
            Controls.Add(_finishPanel);

            _finishTitle.Location = new Point(30, 26);
            _finishTitle.Size = new Size(620, 36);
            _finishTitle.Font = new Font("Segoe UI", 17, FontStyle.Bold);
            _finishPanel.Controls.Add(_finishTitle);

            _finishMessage.Location = new Point(32, 92);
            _finishMessage.Size = new Size(620, 160);
            _finishPanel.Controls.Add(_finishMessage);

            var finishButton = NewButton("完成", 576, 374);
            finishButton.Click += (_, _) => Close();
            _finishPanel.Controls.Add(finishButton);
        }

        private async Task InstallAsync()
        {
            _welcomePanel.Visible = false;
            _progressPanel.Visible = true;

            var installOptions = _options with
            {
                CreateStartMenuShortcut = _startMenuCheck.Checked,
                CreateDesktopShortcut = _desktopCheck.Checked
            };

            try
            {
                await Task.Run(() => InstallerCore.Install(installOptions, ReportProgress));
                ShowFinish("安装完成", "Rclone WinUI3 已安装完成。");
            }
            catch (Exception ex)
            {
                ShowFinish("安装失败", ex.Message);
            }
        }

        private void ReportProgress(int percent, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ReportProgress(percent, message));
                return;
            }

            _progressBar.Value = Math.Clamp(percent, 0, 100);
            _statusLabel.Text = message;
        }

        private void ShowFinish(string title, string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => ShowFinish(title, message));
                return;
            }

            _progressPanel.Visible = false;
            _finishPanel.Visible = true;
            _finishTitle.Text = title;
            _finishMessage.Text = message;
        }

        private static Label NewLabel(string text, int x, int y, int width, int height, float fontSize = 10, FontStyle style = FontStyle.Regular)
        {
            return new Label
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(width, height),
                Font = new Font("Segoe UI", fontSize, style)
            };
        }

        private static Button NewButton(string text, int x, int y)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(92, 32)
            };
        }
    }

    private static class InstallerCore
    {
        private static readonly string InstallRoot = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs"));

        public static readonly string InstallDir = Path.GetFullPath(Path.Combine(InstallRoot, AppName));

        public static void Install(InstallOptions options, Action<int, string> progress)
        {
            if (!InstallDir.StartsWith(InstallRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Install path escaped the expected user Programs directory.");
            }

            progress(5, "读取安装包");
            using var payloadStream = typeof(Program).Assembly.GetManifestResourceStream("payload.zip")
                ?? throw new InvalidOperationException("Installer payload was not found.");

            progress(12, "关闭正在运行的应用");
            StopRunningApplication();

            progress(22, "准备安装目录");
            if (Directory.Exists(InstallDir))
            {
                Directory.Delete(InstallDir, true);
            }
            Directory.CreateDirectory(InstallDir);

            progress(36, "解压应用文件");
            ExtractPayload(payloadStream, InstallDir, progress);

            var exePath = Path.Combine(InstallDir, ExeName);
            var iconPath = Path.Combine(InstallDir, "Assets", "AppIcon.ico");
            var rclonePath = Path.Combine(InstallDir, "bin", "rclone.exe");
            var priPath = Path.Combine(InstallDir, "RcloneWinUI3.pri");
            var uninstallPath = Path.Combine(InstallDir, "Uninstall.ps1");

            progress(70, "校验封装文件");
            RequireFile(exePath, "Installed application executable was not found.");
            RequireFile(rclonePath, "Bundled rclone.exe was not found.");
            RequireFile(priPath, "WinUI resource file was not found.");

            progress(78, "写入卸载程序");
            File.WriteAllText(uninstallPath, BuildUninstallScript(), new System.Text.UTF8Encoding(true));

            progress(84, "创建快捷方式");
            var startMenuShortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft",
                "Windows",
                "Start Menu",
                "Programs",
                $"{AppName}.lnk");
            var desktopShortcut = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                $"{AppName}.lnk");

            if (options.CreateStartMenuShortcut)
            {
                CreateShortcut(startMenuShortcut, exePath, iconPath);
            }
            else
            {
                File.Delete(startMenuShortcut);
            }

            if (options.CreateDesktopShortcut)
            {
                CreateShortcut(desktopShortcut, exePath, iconPath);
            }
            else
            {
                File.Delete(desktopShortcut);
            }

            progress(92, "注册到 Windows");
            RegisterUninstallEntry(exePath, iconPath, uninstallPath);
            RegisterAppPath(exePath);

            progress(100, "安装完成");
        }

        private static void ExtractPayload(Stream payloadStream, string installDir, Action<int, string> progress)
        {
            using var archive = new ZipArchive(payloadStream, ZipArchiveMode.Read);
            var entries = archive.Entries.Where(static entry => !string.IsNullOrEmpty(entry.Name)).ToList();
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var targetPath = Path.GetFullPath(Path.Combine(installDir, entry.FullName));
                if (!targetPath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Payload entry escaped install directory.");
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                entry.ExtractToFile(targetPath, true);

                if (index % 25 == 0 || index == entries.Count - 1)
                {
                    var percent = 36 + (int)Math.Round((index + 1) * 32.0 / entries.Count);
                    progress(percent, $"解压应用文件 ({index + 1}/{entries.Count})");
                }
            }
        }

        private static void StopRunningApplication()
        {
            foreach (var process in Process.GetProcessesByName("RcloneWinUI3"))
            {
                try
                {
                    process.Kill(true);
                    process.WaitForExit(3000);
                }
                catch
                {
                    // Best effort only. The install can still overwrite after the process exits normally.
                }
            }
        }

        private static void RequireFile(string path, string message)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(message, path);
            }
        }

        private static void CreateShortcut(string shortcutPath, string exePath, string iconPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            var shellType = Type.GetTypeFromProgID("WScript.Shell")
                ?? throw new InvalidOperationException("WScript.Shell is not available.");
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = InstallDir;
            shortcut.IconLocation = File.Exists(iconPath) ? iconPath : $"{exePath},0";
            shortcut.Description = AppName;
            shortcut.Save();
        }

        private static void RegisterUninstallEntry(string exePath, string iconPath, string uninstallPath)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{AppId}", true)
                ?? throw new InvalidOperationException("Unable to create uninstall registry key.");

            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", Version);
            key.SetValue("Publisher", Publisher);
            key.SetValue("InstallLocation", InstallDir);
            key.SetValue("DisplayIcon", iconPath);
            key.SetValue("UninstallString", $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallPath}\"");
            key.SetValue("QuietUninstallString", $"powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"{uninstallPath}\" -Silent");
            key.SetValue("URLInfoAbout", "https://github.com/CreeperUX/Rclone-WinUI3");
            key.SetValue("EstimatedSize", Math.Max(1, Directory.GetFiles(InstallDir, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length) / 1024), RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        private static void RegisterAppPath(string exePath)
        {
            using var key = Registry.CurrentUser.CreateSubKey($@"Software\Microsoft\Windows\CurrentVersion\App Paths\{ExeName}", true)
                ?? throw new InvalidOperationException("Unable to create App Paths registry key.");
            key.SetValue("", exePath);
            key.SetValue("Path", InstallDir);
        }

        private static string BuildUninstallScript()
        {
            return """
            param([switch]$Silent)

            $ErrorActionPreference = "SilentlyContinue"

            $appName = "Rclone WinUI3"
            $appId = "RcloneWinUI3"
            $installDir = Split-Path -Parent $MyInvocation.MyCommand.Path
            $startMenuShortcut = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Rclone WinUI3.lnk"
            $desktopShortcut = Join-Path ([Environment]::GetFolderPath("Desktop")) "Rclone WinUI3.lnk"
            $uninstallKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$appId"
            $appPathKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\App Paths\RcloneWinUI3.exe"

            Get-Process -Name "RcloneWinUI3" -ErrorAction SilentlyContinue | Stop-Process -Force
            Remove-Item -LiteralPath $startMenuShortcut -Force
            Remove-Item -LiteralPath $desktopShortcut -Force
            Remove-Item -LiteralPath $uninstallKey -Recurse -Force
            Remove-Item -LiteralPath $appPathKey -Recurse -Force

            $cleanup = Join-Path $env:TEMP ("RcloneWinUI3-cleanup-" + [Guid]::NewGuid() + ".cmd")
            $escapedInstallDir = $installDir.Replace('"', '""')
            $cleanupScript = @"
            @echo off
            ping 127.0.0.1 -n 2 > nul
            rmdir /s /q "$escapedInstallDir"
            del "%~f0"
            "@

            Set-Content -LiteralPath $cleanup -Value $cleanupScript -Encoding ASCII
            Start-Process -FilePath $cleanup -WindowStyle Hidden
            """;
        }
    }

    private sealed record InstallOptions(bool Quiet, bool CreateStartMenuShortcut, bool CreateDesktopShortcut)
    {
        public static InstallOptions Parse(string[] args)
        {
            var quiet = args.Any(static arg => arg.Equals("/Q", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("/quiet", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("-quiet", StringComparison.OrdinalIgnoreCase)
                || arg.Equals("--quiet", StringComparison.OrdinalIgnoreCase));

            var noStartMenu = args.Any(static arg => arg.Equals("--no-start-menu", StringComparison.OrdinalIgnoreCase));
            var desktop = args.Any(static arg => arg.Equals("--desktop-shortcut", StringComparison.OrdinalIgnoreCase));

            return new InstallOptions(quiet, !noStartMenu, desktop);
        }
    }
}
