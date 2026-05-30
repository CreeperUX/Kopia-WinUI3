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
    [NotifyCanExecuteChangedFor(nameof(ListSnapshotsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ListPoliciesCommand))]
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
        }
        catch (Exception ex)
        {
            StatusText = "操作失败";
            CommandOutput = ex.ToString();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRunCommand()
    {
        return !IsBusy;
    }
}
