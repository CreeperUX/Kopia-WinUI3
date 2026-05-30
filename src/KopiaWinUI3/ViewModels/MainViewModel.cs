using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KopiaWinUI3.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace KopiaWinUI3.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly Regex SpeedRegex = new(
        @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?B)/s",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IKopiaLocator _locator;
    private readonly IKopiaCommandService _commands;
    private readonly IFolderPickerService _folderPicker;
    private readonly INotificationDialogService _dialogs;
    private readonly IVerifiedCopyService _verifiedCopy;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckRepositoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateRepositoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectRepositoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickBackupSourcePathCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickRepositoryPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateSnapshotCommand))]
    [NotifyCanExecuteChangedFor(nameof(ListSnapshotsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ListPoliciesCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreSnapshotCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "正在初始化...";

    [ObservableProperty]
    private string kopiaVersion = "未检测";

    [ObservableProperty]
    private string kopiaPath = "未检测";

    [ObservableProperty]
    private string repositoryStatus = "未检测";

    [ObservableProperty]
    private string snapshotSummary = "未加载";

    [ObservableProperty]
    private string policySummary = "未加载";

    [ObservableProperty]
    private string repositoryPath = string.Empty;

    [ObservableProperty]
    private string repositoryPassword = string.Empty;

    [ObservableProperty]
    private int backupModeIndex;

    [ObservableProperty]
    private int repositoryProviderIndex;

    [ObservableProperty]
    private string s3Bucket = string.Empty;

    [ObservableProperty]
    private string s3Endpoint = "s3.amazonaws.com";

    [ObservableProperty]
    private string s3Region = string.Empty;

    [ObservableProperty]
    private string s3AccessKey = string.Empty;

    [ObservableProperty]
    private string s3SecretAccessKey = string.Empty;

    [ObservableProperty]
    private string s3Prefix = string.Empty;

    [ObservableProperty]
    private string sftpHost = string.Empty;

    [ObservableProperty]
    private string sftpPort = "22";

    [ObservableProperty]
    private string sftpUsername = string.Empty;

    [ObservableProperty]
    private string sftpPassword = string.Empty;

    [ObservableProperty]
    private string sftpPath = string.Empty;

    [ObservableProperty]
    private string webDavUrl = string.Empty;

    [ObservableProperty]
    private string webDavUsername = string.Empty;

    [ObservableProperty]
    private string webDavPassword = string.Empty;

    [ObservableProperty]
    private string b2Bucket = string.Empty;

    [ObservableProperty]
    private string b2KeyId = string.Empty;

    [ObservableProperty]
    private string b2Key = string.Empty;

    [ObservableProperty]
    private string b2Prefix = string.Empty;

    [ObservableProperty]
    private string backupSourcePath = string.Empty;

    [ObservableProperty]
    private string snapshotDescription = string.Empty;

    [ObservableProperty]
    private string kopiaParallelFiles = "8";

    [ObservableProperty]
    private string verifiedCopyParallelFiles = "2";

    [ObservableProperty]
    private string verifiedCopyBufferSizeMb = "8";

    [ObservableProperty]
    private bool verifiedCopyVerifyAfterCopy = true;

    [ObservableProperty]
    private string restoreSource = string.Empty;

    [ObservableProperty]
    private string restoreTargetPath = string.Empty;

    [ObservableProperty]
    private string commandOutput = string.Empty;

    [ObservableProperty]
    private bool isTaskRunning;

    [ObservableProperty]
    private bool isProgressIndeterminate;

    [ObservableProperty]
    private double progressValue;

    [ObservableProperty]
    private string taskProgressText = "空闲";

    [ObservableProperty]
    private string transferSpeedText = "--";

    [ObservableProperty]
    private string elapsedTimeText = "00:00";

    [ObservableProperty]
    private string activeTaskName = "无任务";

    public MainViewModel(
        IKopiaLocator locator,
        IKopiaCommandService commands,
        IFolderPickerService folderPicker,
        INotificationDialogService dialogs,
        IVerifiedCopyService verifiedCopy)
    {
        _locator = locator;
        _commands = commands;
        _folderPicker = folderPicker;
        _dialogs = dialogs;
        _verifiedCopy = verifiedCopy;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    public Visibility FilesystemDestinationVisibility => RepositoryProviderIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility S3DestinationVisibility => RepositoryProviderIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SftpDestinationVisibility => RepositoryProviderIndex == 2 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WebDavDestinationVisibility => RepositoryProviderIndex == 3 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility B2DestinationVisibility => RepositoryProviderIndex == 4 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility KopiaRepositorySetupVisibility => BackupModeIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility VerifiedCopyNoticeVisibility => BackupModeIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task RefreshAsync()
    {
        await RunOperationAsync("刷新状态", async () =>
        {
            var executable = _locator.FindKopiaExecutable();
            KopiaPath = executable ?? "未找到 kopia.exe";
            KopiaVersion = await _locator.GetVersionAsync();

            if (executable is null)
            {
                RepositoryStatus = "Kopia 本体缺失";
                StatusText = "请先下载 Kopia 本体";
                return;
            }

            await CheckRepositoryCoreAsync();
            StatusText = "准备就绪";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task CheckRepositoryAsync()
    {
        await RunOperationAsync("检查仓库", CheckRepositoryCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task CreateRepositoryAsync()
    {
        await RunOperationAsync("创建仓库", async () =>
        {
            var result = await _commands.RunAsync(BuildRepositoryArguments("create"));

            RepositoryStatus = result.Succeeded ? "已创建并连接仓库" : "创建仓库失败";
            ThrowIfCommandFailed(result, "创建仓库失败");
            CommandOutput = "仓库已创建并连接。";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ConnectRepositoryAsync()
    {
        await RunOperationAsync("连接仓库", async () =>
        {
            var result = await _commands.RunAsync(BuildRepositoryArguments("connect"));

            RepositoryStatus = result.Succeeded ? "已连接仓库" : "连接仓库失败";
            ThrowIfCommandFailed(result, "连接仓库失败");
            CommandOutput = "仓库已连接。";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task PickBackupSourcePathAsync()
    {
        var path = await _folderPicker.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            BackupSourcePath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task PickRepositoryPathAsync()
    {
        var path = await _folderPicker.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            RepositoryPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task CreateSnapshotAsync()
    {
        await RunMonitoredOperationAsync("开始备份", async output =>
        {
            ValidateDirectoryText(BackupSourcePath, "备份源路径");
            if (BackupModeIndex == 1)
            {
                await RunVerifiedCopyAsync();
                return;
            }

            await EnsureRepositoryReadyForBackupAsync(output);

            var args = new List<string> { "--progress", "snapshot", "create" };
            var kopiaParallel = ParsePositiveInt(KopiaParallelFiles, "Kopia 并行文件数", 1, 64);
            args.Add($"--parallel={kopiaParallel}");

            if (!string.IsNullOrWhiteSpace(SnapshotDescription))
            {
                args.Add($"--description={SnapshotDescription}");
            }

            args.Add(BackupSourcePath);

            var result = await _commands.RunStreamingAsync(args, output);
            if (!result.Succeeded)
            {
                SnapshotSummary = "备份失败";
                StatusText = "备份失败";
                throw new InvalidOperationException(result.DisplayText);
            }

            SnapshotSummary = "备份完成";
            StatusText = "备份完成";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ListSnapshotsAsync()
    {
        await RunOperationAsync("读取快照", async () =>
        {
            var result = await _commands.RunAsync(["snapshot", "list"]);
            SnapshotSummary = result.Succeeded ? "已加载快照列表" : "当前没有可用仓库";
            ThrowIfCommandFailed(result, "读取快照列表失败");
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ListPoliciesAsync()
    {
        await RunOperationAsync("读取策略", async () =>
        {
            var result = await _commands.RunAsync(["policy", "list"]);
            PolicySummary = result.Succeeded ? "已加载策略列表" : "当前没有可用仓库";
            ThrowIfCommandFailed(result, "读取策略列表失败");
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task RestoreSnapshotAsync()
    {
        await RunMonitoredOperationAsync("恢复快照", async output =>
        {
            ValidateText(RestoreSource, "恢复源对象");
            ValidateText(RestoreTargetPath, "恢复目标路径");

            var result = await _commands.RunStreamingAsync([
                "--progress",
                "restore",
                RestoreSource,
                RestoreTargetPath
            ], output);

            if (!result.Succeeded)
            {
                StatusText = "恢复失败";
                throw new InvalidOperationException(result.DisplayText);
            }

            StatusText = "恢复完成";
        });
    }

    private async Task CheckRepositoryCoreAsync()
    {
        var result = await _commands.RunAsync(["repository", "status"]);
        RepositoryStatus = result.Succeeded ? "已连接仓库" : "未连接仓库";
        CommandOutput = result.DisplayText;
    }

    private static void ThrowIfCommandFailed(KopiaCommandResult result, string fallbackMessage)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(ToFriendlyError(result.DisplayText, fallbackMessage));
        }
    }

    private static string ToFriendlyError(string message, string fallbackMessage = "操作失败")
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return fallbackMessage;
        }

        if (message.Contains("repository is not connected", StringComparison.OrdinalIgnoreCase))
        {
            return "当前没有连接 Kopia 仓库。请先在右侧“仓库初始化”中创建或连接仓库，然后再开始备份。";
        }

        if (message.Contains("invalid password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("incorrect password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("wrong password", StringComparison.OrdinalIgnoreCase))
        {
            return "Kopia 加密密码不正确。请确认这是创建该仓库时使用的密码。";
        }

        if (message.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return "没有足够权限访问当前路径或仓库。请确认源路径、目标路径可读写，必要时换到用户目录下的文件夹。";
        }

        if (message.Contains("no such file or directory", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot find the path", StringComparison.OrdinalIgnoreCase)
            || message.Contains("The system cannot find", StringComparison.OrdinalIgnoreCase))
        {
            return "找不到指定路径。请重新选择源路径或目标路径后再试。";
        }

        return message.Trim();
    }

    private async Task EnsureRepositoryReadyForBackupAsync(Action<string> output)
    {
        var status = await _commands.RunAsync(["repository", "status"]);
        if (status.Succeeded)
        {
            RepositoryStatus = "已连接仓库";
            return;
        }

        RepositoryStatus = "未连接仓库";

        if (RepositoryProviderIndex != 0)
        {
            throw new InvalidOperationException(
                "当前没有连接 Kopia 仓库。请先在右侧“仓库初始化”中创建或连接仓库，然后再开始备份。");
        }

        ValidateDirectoryText(RepositoryPath, "本地仓库路径");
        ValidatePassword();

        output("当前未连接 Kopia 仓库，正在尝试连接本地仓库...");
        var connectResult = await _commands.RunAsync(BuildRepositoryArguments("connect"));
        if (connectResult.Succeeded)
        {
            RepositoryStatus = "已连接仓库";
            output("已连接本地仓库。");
            return;
        }

        output("本地仓库连接失败，正在尝试创建新的本地仓库...");
        var createResult = await _commands.RunAsync(BuildRepositoryArguments("create"));
        if (createResult.Succeeded)
        {
            RepositoryStatus = "已创建并连接仓库";
            output("已创建并连接本地仓库。");
            return;
        }

        throw new InvalidOperationException(
            "无法自动准备本地 Kopia 仓库。请确认右侧目标路径可写，并填写 Kopia 加密密码。\n\n"
            + "连接输出：\n"
            + connectResult.DisplayText
            + "\n\n创建输出：\n"
            + createResult.DisplayText);
    }

    partial void OnRepositoryProviderIndexChanged(int value)
    {
        OnPropertyChanged(nameof(FilesystemDestinationVisibility));
        OnPropertyChanged(nameof(S3DestinationVisibility));
        OnPropertyChanged(nameof(SftpDestinationVisibility));
        OnPropertyChanged(nameof(WebDavDestinationVisibility));
        OnPropertyChanged(nameof(B2DestinationVisibility));
    }

    partial void OnBackupModeIndexChanged(int value)
    {
        if (value == 1 && RepositoryProviderIndex != 0)
        {
            RepositoryProviderIndex = 0;
        }

        OnPropertyChanged(nameof(KopiaRepositorySetupVisibility));
        OnPropertyChanged(nameof(VerifiedCopyNoticeVisibility));
    }

    private async Task RunVerifiedCopyAsync()
    {
        if (RepositoryProviderIndex != 0)
        {
            throw new InvalidOperationException("校验文件拷贝目前只支持本地文件夹目的地。");
        }

        ValidateDirectoryText(RepositoryPath, "本地目标路径");

        var stopwatch = Stopwatch.StartNew();
        AppendOutput("开始校验文件拷贝...");

        var progress = new Progress<VerifiedCopyProgress>(copyProgress =>
        {
            IsProgressIndeterminate = copyProgress.TotalBytes <= 0;
            if (copyProgress.TotalBytes > 0)
            {
                ProgressValue = Math.Clamp(copyProgress.BytesCopied * 100.0 / copyProgress.TotalBytes, 0, 100);
            }

            TaskProgressText = copyProgress.Message;
            if (copyProgress.BytesCopied > 0 && stopwatch.Elapsed.TotalSeconds > 0.5)
            {
                TransferSpeedText = $"{FormatBytes((long)(copyProgress.BytesCopied / stopwatch.Elapsed.TotalSeconds))}/s";
            }
        });

        var options = new VerifiedCopyOptions(
            ParsePositiveInt(VerifiedCopyParallelFiles, "校验拷贝并发文件数", 1, 32),
            ParsePositiveInt(VerifiedCopyBufferSizeMb, "校验拷贝缓冲区", 1, 64) * 1024 * 1024,
            VerifiedCopyVerifyAfterCopy);

        var result = await _verifiedCopy.CopyAsync(BackupSourcePath, RepositoryPath, options, progress);
        SnapshotSummary = $"校验拷贝完成：{result.FileCount} 个文件";
        StatusText = "校验拷贝完成";
        var mode = VerifiedCopyVerifyAfterCopy ? "校验拷贝完成。" : "文件拷贝完成，已跳过 SHA-256 校验。";
        CommandOutput = $"{mode}{Environment.NewLine}目标路径：{result.DestinationPath}{Environment.NewLine}文件数量：{result.FileCount}{Environment.NewLine}总大小：{FormatBytes(result.TotalBytes)}";
    }

    private IReadOnlyList<string> BuildRepositoryArguments(string operation)
    {
        ValidatePassword();

        var args = new List<string>
        {
            $"--password={RepositoryPassword}",
            "repository",
            operation
        };

        switch (RepositoryProviderIndex)
        {
            case 0:
                ValidateDirectoryText(RepositoryPath, "本地仓库路径");
                if (operation == "create")
                {
                    Directory.CreateDirectory(RepositoryPath);
                }

                args.Add("filesystem");
                args.Add($"--path={RepositoryPath}");
                break;

            case 1:
                ValidateText(S3Bucket, "S3 Bucket");
                ValidateText(S3AccessKey, "S3 Access Key");
                ValidateText(S3SecretAccessKey, "S3 Secret Access Key");

                args.Add("s3");
                args.Add($"--bucket={S3Bucket}");
                AddOptional(args, "--endpoint", S3Endpoint);
                AddOptional(args, "--region", S3Region);
                AddOptional(args, "--prefix", S3Prefix);
                args.Add($"--access-key={S3AccessKey}");
                args.Add($"--secret-access-key={S3SecretAccessKey}");
                break;

            case 2:
                ValidateText(SftpHost, "SFTP 主机");
                ValidateText(SftpUsername, "SFTP 用户名");
                ValidateText(SftpPath, "SFTP 仓库路径");

                args.Add("sftp");
                args.Add($"--host={SftpHost}");
                AddOptional(args, "--port", SftpPort);
                args.Add($"--username={SftpUsername}");
                AddOptional(args, "--sftp-password", SftpPassword);
                args.Add($"--path={SftpPath}");
                break;

            case 3:
                ValidateText(WebDavUrl, "WebDAV 地址");

                args.Add("webdav");
                args.Add($"--url={WebDavUrl}");
                AddOptional(args, "--webdav-username", WebDavUsername);
                AddOptional(args, "--webdav-password", WebDavPassword);
                break;

            case 4:
                ValidateText(B2Bucket, "B2 Bucket");
                ValidateText(B2KeyId, "B2 Key ID");
                ValidateText(B2Key, "B2 Application Key");

                args.Add("b2");
                args.Add($"--bucket={B2Bucket}");
                args.Add($"--key-id={B2KeyId}");
                args.Add($"--key={B2Key}");
                AddOptional(args, "--prefix", B2Prefix);
                break;

            default:
                throw new InvalidOperationException("请选择仓库目的地类型。");
        }

        return args;
    }

    private static void AddOptional(List<string> args, string optionName, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            args.Add($"{optionName}={value}");
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{bytes} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }

    private static int ParsePositiveInt(string value, string label, int minimum, int maximum)
    {
        if (!int.TryParse(value, out var parsed))
        {
            throw new InvalidOperationException($"{label}必须是数字。");
        }

        if (parsed < minimum || parsed > maximum)
        {
            throw new InvalidOperationException($"{label}必须在 {minimum} 到 {maximum} 之间。");
        }

        return parsed;
    }

    private async Task RunOperationAsync(string operationName, Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = $"{operationName}中...";
            await operation();
            if (StatusText.EndsWith("中...", StringComparison.Ordinal))
            {
                StatusText = $"{operationName}完成";
            }
        }
        catch (Exception ex)
        {
            StatusText = "操作失败";
            CommandOutput = "操作失败，错误详情已通过弹窗显示。";
            await _dialogs.ShowErrorAsync(operationName, ToFriendlyError(ex.Message));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RunMonitoredOperationAsync(string operationName, Func<Action<string>, Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        using var timerCts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            IsBusy = true;
            IsTaskRunning = true;
            IsProgressIndeterminate = true;
            ProgressValue = 0;
            ActiveTaskName = operationName;
            TaskProgressText = "正在启动";
            TransferSpeedText = "等待数据";
            ElapsedTimeText = "00:00";
            CommandOutput = string.Empty;
            StatusText = $"{operationName}中...";

            _ = UpdateElapsedTimeAsync(stopwatch, timerCts.Token);

            await operation(line => EnqueueOutput(line));

            IsProgressIndeterminate = false;
            ProgressValue = 100;
            TaskProgressText = "完成";
        }
        catch (Exception ex)
        {
            IsProgressIndeterminate = false;
            TaskProgressText = "失败";
            StatusText = "操作失败";
            AppendOutput("操作失败，错误详情已通过弹窗显示。");
            await _dialogs.ShowErrorAsync(operationName, ToFriendlyError(ex.Message));
        }
        finally
        {
            timerCts.Cancel();
            IsTaskRunning = false;
            IsBusy = false;
            stopwatch.Stop();
        }
    }

    private async Task UpdateElapsedTimeAsync(Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500, cancellationToken).ContinueWith(_ => { }, TaskScheduler.Default);
            Enqueue(() =>
            {
                var elapsed = stopwatch.Elapsed;
                ElapsedTimeText = elapsed.TotalHours >= 1
                    ? elapsed.ToString(@"hh\:mm\:ss")
                    : elapsed.ToString(@"mm\:ss");
            });
        }
    }

    private void EnqueueOutput(string line)
    {
        Enqueue(() =>
        {
            AppendOutput(line);
            TaskProgressText = "运行中";

            var speedMatch = SpeedRegex.Match(line);
            if (speedMatch.Success)
            {
                TransferSpeedText = $"{speedMatch.Groups["value"].Value} {speedMatch.Groups["unit"].Value}/s";
            }
        });
    }

    private void Enqueue(Action action)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            action();
            return;
        }

        _dispatcherQueue.TryEnqueue(() => action());
    }

    private void AppendOutput(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        CommandOutput = string.IsNullOrWhiteSpace(CommandOutput)
            ? message
            : $"{CommandOutput}{Environment.NewLine}{message}";
    }

    private void ValidateDirectoryText(string value, string label)
    {
        ValidateText(value, label);

        if (value.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            throw new InvalidOperationException($"{label}包含非法字符。");
        }
    }

    private static void ValidateText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"请填写{label}。");
        }
    }

    private void ValidatePassword()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPassword))
        {
            throw new InvalidOperationException("请填写仓库密码。");
        }
    }

    private bool CanRunCommand()
    {
        return !IsBusy;
    }
}
