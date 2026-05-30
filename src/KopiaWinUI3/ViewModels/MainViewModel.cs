using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KopiaWinUI3.Services;

namespace KopiaWinUI3.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IKopiaLocator _locator;
    private readonly IKopiaCommandService _commands;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckRepositoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateRepositoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectRepositoryCommand))]
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
    private string backupSourcePath = string.Empty;

    [ObservableProperty]
    private string snapshotDescription = string.Empty;

    [ObservableProperty]
    private string restoreSource = string.Empty;

    [ObservableProperty]
    private string restoreTargetPath = string.Empty;

    [ObservableProperty]
    private string commandOutput = string.Empty;

    public MainViewModel(IKopiaLocator locator, IKopiaCommandService commands)
    {
        _locator = locator;
        _commands = commands;
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task RefreshAsync()
    {
        await RunNativeOperationAsync("刷新状态", async () =>
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
        await RunNativeOperationAsync("检查仓库", CheckRepositoryCoreAsync);
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task CreateRepositoryAsync()
    {
        await RunNativeOperationAsync("创建仓库", async () =>
        {
            ValidateDirectoryText(RepositoryPath, "仓库目的路径");
            ValidatePassword();
            Directory.CreateDirectory(RepositoryPath);

            var result = await _commands.RunAsync([
                $"--password={RepositoryPassword}",
                "repository",
                "create",
                "filesystem",
                $"--path={RepositoryPath}"
            ]);

            RepositoryStatus = result.Succeeded ? "已创建并连接仓库" : "创建仓库失败";
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ConnectRepositoryAsync()
    {
        await RunNativeOperationAsync("连接仓库", async () =>
        {
            ValidateDirectoryText(RepositoryPath, "仓库目的路径");
            ValidatePassword();

            var result = await _commands.RunAsync([
                $"--password={RepositoryPassword}",
                "repository",
                "connect",
                "filesystem",
                $"--path={RepositoryPath}"
            ]);

            RepositoryStatus = result.Succeeded ? "已连接仓库" : "连接仓库失败";
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task CreateSnapshotAsync()
    {
        await RunNativeOperationAsync("创建快照", async () =>
        {
            ValidateDirectoryText(BackupSourcePath, "备份源路径");

            var args = new List<string> { "snapshot", "create" };
            if (!string.IsNullOrWhiteSpace(SnapshotDescription))
            {
                args.Add($"--description={SnapshotDescription}");
            }

            args.Add(BackupSourcePath);

            var result = await _commands.RunAsync(args);
            SnapshotSummary = result.Succeeded ? "快照创建完成" : "创建快照失败";
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ListSnapshotsAsync()
    {
        await RunNativeOperationAsync("读取快照", async () =>
        {
            var result = await _commands.RunAsync(["snapshot", "list"]);
            SnapshotSummary = result.Succeeded ? "已加载快照列表" : "当前没有可用仓库";
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task ListPoliciesAsync()
    {
        await RunNativeOperationAsync("读取策略", async () =>
        {
            var result = await _commands.RunAsync(["policy", "list"]);
            PolicySummary = result.Succeeded ? "已加载策略列表" : "当前没有可用仓库";
            CommandOutput = result.DisplayText;
        });
    }

    [RelayCommand(CanExecute = nameof(CanRunCommand))]
    public async Task RestoreSnapshotAsync()
    {
        await RunNativeOperationAsync("恢复快照", async () =>
        {
            ValidateText(RestoreSource, "恢复源对象");
            ValidateText(RestoreTargetPath, "恢复目标路径");

            var result = await _commands.RunAsync([
                "restore",
                RestoreSource,
                RestoreTargetPath
            ]);

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

    private async Task RunNativeOperationAsync(string operationName, Func<Task> operation)
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
