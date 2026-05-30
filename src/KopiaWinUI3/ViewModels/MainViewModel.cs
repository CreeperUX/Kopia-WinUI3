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
        IFolderPickerService folderPicker)
    {
        _locator = locator;
        _commands = commands;
        _folderPicker = folderPicker;
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
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ConnectRepositoryAsync()
    {
        await RunOperationAsync("连接仓库", async () =>
        {
            var result = await _commands.RunAsync(BuildRepositoryArguments("connect"));

            RepositoryStatus = result.Succeeded ? "已连接仓库" : "连接仓库失败";
            CommandOutput = result.DisplayText;
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

            var args = new List<string> { "--progress", "snapshot", "create" };
            if (!string.IsNullOrWhiteSpace(SnapshotDescription))
            {
                args.Add($"--description={SnapshotDescription}");
            }

            args.Add(BackupSourcePath);

            var result = await _commands.RunStreamingAsync(args, output);
            SnapshotSummary = result.Succeeded ? "备份完成" : "备份失败";
            CommandOutput = result.DisplayText;
            StatusText = result.Succeeded ? "备份完成" : "备份失败";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ListSnapshotsAsync()
    {
        await RunOperationAsync("读取快照", async () =>
        {
            var result = await _commands.RunAsync(["snapshot", "list"]);
            SnapshotSummary = result.Succeeded ? "已加载快照列表" : "当前没有可用仓库";
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

            CommandOutput = result.DisplayText;
            StatusText = result.Succeeded ? "恢复完成" : "恢复失败";
        });
    }

    private async Task CheckRepositoryCoreAsync()
    {
        var result = await _commands.RunAsync(["repository", "status"]);
        RepositoryStatus = result.Succeeded ? "已连接仓库" : "未连接仓库";
        CommandOutput = result.DisplayText;
    }

    partial void OnRepositoryProviderIndexChanged(int value)
    {
        OnPropertyChanged(nameof(FilesystemDestinationVisibility));
        OnPropertyChanged(nameof(S3DestinationVisibility));
        OnPropertyChanged(nameof(SftpDestinationVisibility));
        OnPropertyChanged(nameof(WebDavDestinationVisibility));
        OnPropertyChanged(nameof(B2DestinationVisibility));
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
            CommandOutput = ex.Message;
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
            AppendOutput(ex.Message);
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
