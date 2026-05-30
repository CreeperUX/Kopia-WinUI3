using System.Diagnostics;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KopiaWinUI3.Services;
using Microsoft.UI.Dispatching;

namespace KopiaWinUI3.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly Regex SpeedRegex = new(
        @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>[KMGT]?i?B)/s",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PercentRegex = new(
        @",\s*(?<percent>\d+(?:\.\d+)?)%,",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IRcloneLocator _locator;
    private readonly IRcloneCommandService _commands;
    private readonly IFolderPickerService _folderPicker;
    private readonly INotificationDialogService _dialogs;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickSourcePathCommand))]
    [NotifyCanExecuteChangedFor(nameof(PickDestinationPathCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartTransferCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckDestinationCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "正在初始化...";

    [ObservableProperty]
    private string rcloneVersion = "未检测";

    [ObservableProperty]
    private string rclonePath = "未检测";

    [ObservableProperty]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private string destinationPath = string.Empty;

    [ObservableProperty]
    private int operationIndex;

    [ObservableProperty]
    private bool useChecksum = true;

    [ObservableProperty]
    private bool verifyAfterTransfer = true;

    [ObservableProperty]
    private bool dryRun;

    [ObservableProperty]
    private string transfers = "8";

    [ObservableProperty]
    private string checkers = "16";

    [ObservableProperty]
    private string bufferSizeMb = "16";

    [ObservableProperty]
    private string bandwidthLimit = string.Empty;

    [ObservableProperty]
    private string extraArguments = string.Empty;

    [ObservableProperty]
    private string commandOutput = string.Empty;

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
        IRcloneLocator locator,
        IRcloneCommandService commands,
        IFolderPickerService folderPicker,
        INotificationDialogService dialogs)
    {
        _locator = locator;
        _commands = commands;
        _folderPicker = folderPicker;
        _dialogs = dialogs;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task RefreshAsync()
    {
        await RunOperationAsync("刷新状态", async () =>
        {
            var executable = _locator.FindRcloneExecutable();
            RclonePath = executable ?? "未找到 rclone.exe";
            RcloneVersion = await _locator.GetVersionAsync();
            StatusText = executable is null ? "请先下载 rclone 本体" : "准备就绪";
            CommandOutput = RcloneVersion;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task PickSourcePathAsync()
    {
        var path = await _folderPicker.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            SourcePath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task PickDestinationPathAsync()
    {
        var path = await _folderPicker.PickFolderAsync();
        if (!string.IsNullOrWhiteSpace(path))
        {
            DestinationPath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task StartTransferAsync()
    {
        await RunMonitoredOperationAsync(OperationIndex == 0 ? "复制文件" : "同步文件", async output =>
        {
            ValidateTransferPaths();
            var args = BuildTransferArguments(OperationIndex == 0 ? "copy" : "sync");
            var result = await _commands.RunStreamingAsync(args, output);
            ThrowIfCommandFailed(result, OperationIndex == 0 ? "复制失败" : "同步失败");

            if (VerifyAfterTransfer && !DryRun)
            {
                output("传输完成，正在执行 rclone check...");
                var checkResult = await _commands.RunStreamingAsync(BuildCheckArguments(), output);
                ThrowIfCommandFailed(checkResult, "校验失败");
            }

            StatusText = DryRun ? "预演完成" : "传输完成";
            TaskProgressText = DryRun ? "预演完成" : "完成";
            ProgressValue = 100;
            IsProgressIndeterminate = false;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task CheckDestinationAsync()
    {
        await RunMonitoredOperationAsync("校验目标", async output =>
        {
            ValidateTransferPaths();
            var result = await _commands.RunStreamingAsync(BuildCheckArguments(), output);
            ThrowIfCommandFailed(result, "校验失败");
            StatusText = "校验完成";
            TaskProgressText = "校验完成";
            ProgressValue = 100;
            IsProgressIndeterminate = false;
        });
    }

    private IReadOnlyList<string> BuildTransferArguments(string operation)
    {
        var args = new List<string>
        {
            operation,
            SourcePath,
            DestinationPath,
            "--progress",
            "--stats=1s",
            "--stats-one-line",
            $"--transfers={ParsePositiveInt(Transfers, "并行传输数", 1, 128)}",
            $"--checkers={ParsePositiveInt(Checkers, "并行检查数", 1, 256)}",
            $"--buffer-size={ParsePositiveInt(BufferSizeMb, "缓冲区大小", 1, 1024)}M"
        };

        if (UseChecksum)
        {
            args.Add("--checksum");
        }

        if (DryRun)
        {
            args.Add("--dry-run");
        }

        if (!string.IsNullOrWhiteSpace(BandwidthLimit))
        {
            args.Add($"--bwlimit={BandwidthLimit}");
        }

        AddExtraArguments(args);
        return args;
    }

    private IReadOnlyList<string> BuildCheckArguments()
    {
        var args = new List<string>
        {
            "check",
            SourcePath,
            DestinationPath,
            "--one-way",
            "--progress",
            "--stats=1s",
            "--stats-one-line",
            $"--checkers={ParsePositiveInt(Checkers, "并行检查数", 1, 256)}"
        };

        if (UseChecksum)
        {
            args.Add("--checksum");
        }

        AddExtraArguments(args);
        return args;
    }

    private void AddExtraArguments(List<string> args)
    {
        foreach (var argument in SplitArguments(ExtraArguments))
        {
            args.Add(argument);
        }
    }

    private static IEnumerable<string> SplitArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        foreach (var argument in value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return argument;
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
            IsProgressIndeterminate = true;
            ProgressValue = 0;
            ActiveTaskName = operationName;
            TaskProgressText = "正在启动";
            TransferSpeedText = "--";
            ElapsedTimeText = "00:00";
            CommandOutput = string.Empty;
            StatusText = $"{operationName}中...";

            _ = UpdateElapsedTimeAsync(stopwatch, timerCts.Token);

            await operation(line => EnqueueOutput(line));
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
            TaskProgressText = ExtractStatus(line);

            var speedMatch = SpeedRegex.Match(line);
            if (speedMatch.Success)
            {
                TransferSpeedText = $"{speedMatch.Groups["value"].Value} {speedMatch.Groups["unit"].Value}/s";
            }

            var percentMatch = PercentRegex.Match(line);
            if (percentMatch.Success && double.TryParse(percentMatch.Groups["percent"].Value, out var percent))
            {
                IsProgressIndeterminate = false;
                ProgressValue = Math.Clamp(percent, 0, 100);
            }
        });
    }

    private static string ExtractStatus(string line)
    {
        if (line.Contains("Transferred:", StringComparison.OrdinalIgnoreCase))
        {
            return "正在传输";
        }

        if (line.Contains("Checks:", StringComparison.OrdinalIgnoreCase)
            || line.Contains("checking", StringComparison.OrdinalIgnoreCase))
        {
            return "正在校验";
        }

        if (line.Contains("ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return "发生错误";
        }

        return string.IsNullOrWhiteSpace(line) ? "运行中" : line.Trim();
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

        const int maxLength = 60000;
        if (CommandOutput.Length > maxLength)
        {
            CommandOutput = CommandOutput[^maxLength..];
        }
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

    private void ValidateTransferPaths()
    {
        ValidateText(SourcePath, "源路径");
        ValidateText(DestinationPath, "目标路径");
    }

    private static void ValidateText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"请填写{label}。");
        }
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

    private static void ThrowIfCommandFailed(RcloneCommandResult result, string fallbackMessage)
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

        if (message.Contains("command not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("未找到 rclone.exe", StringComparison.OrdinalIgnoreCase))
        {
            return "未找到 rclone.exe。请先运行 scripts/Get-Rclone.ps1 下载 rclone 本体。";
        }

        if (message.Contains("directory not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot find", StringComparison.OrdinalIgnoreCase))
        {
            return "找不到指定路径或 rclone remote。请检查源路径、目标路径或 remote 名称。";
        }

        if (message.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return "没有足够权限访问当前路径。请确认源路径和目标路径可读写。";
        }

        return message.Trim();
    }

    private bool CanRunCommand()
    {
        return !IsBusy;
    }
}
