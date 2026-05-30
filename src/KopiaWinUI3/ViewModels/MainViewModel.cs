using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using KopiaWinUI3.Services;

namespace KopiaWinUI3.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IKopiaLocator _locator;
    private readonly IKopiaProcessService _processService;
    private readonly DispatcherQueue _dispatcherQueue;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool isRunning;

    [ObservableProperty]
    private string statusText = "正在初始化...";

    [ObservableProperty]
    private string kopiaVersion = "未检测";

    [ObservableProperty]
    private string kopiaPath = "未检测";

    [ObservableProperty]
    private string serverUrl = "未启动";

    [ObservableProperty]
    private string processState = "未启动";

    [ObservableProperty]
    private string logText = string.Empty;

    public MainViewModel(IKopiaLocator locator, IKopiaProcessService processService)
    {
        _locator = locator;
        _processService = processService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        _processService.LogReceived += OnProcessLogReceived;
    }

    public event EventHandler<Uri>? ServerStarted;

    [RelayCommand]
    public async Task InitializeAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    public async Task StartAsync()
    {
        try
        {
            StatusText = "正在启动 Kopia...";
            var uri = await _processService.StartAsync();
            ServerUrl = uri.ToString();
            IsRunning = true;
            ProcessState = "运行中";
            StatusText = "Kopia UI 已启动";
            ServerStarted?.Invoke(this, uri);
        }
        catch (Exception ex)
        {
            IsRunning = false;
            ProcessState = "启动失败";
            StatusText = ex.Message;
            AppendLog(ex.ToString());
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    public async Task StopAsync()
    {
        try
        {
            StatusText = "正在停止 Kopia...";
            await _processService.StopAsync();
            IsRunning = false;
            ServerUrl = "未启动";
            ProcessState = "未启动";
            StatusText = "Kopia 已停止";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            AppendLog(ex.ToString());
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        var executable = _locator.FindKopiaExecutable();
        KopiaPath = executable ?? "未找到 kopia.exe";
        KopiaVersion = await _locator.GetVersionAsync();

        IsRunning = _processService.IsRunning;
        ServerUrl = _processService.ServerUri?.ToString() ?? "未启动";
        ProcessState = IsRunning ? "运行中" : "未启动";
        StatusText = executable is null ? "请放置 Kopia 二进制后再启动" : "准备就绪";
    }

    private bool CanStart()
    {
        return !IsRunning;
    }

    private bool CanStop()
    {
        return IsRunning;
    }

    private void OnProcessLogReceived(object? sender, string e)
    {
        if (_dispatcherQueue.HasThreadAccess)
        {
            AppendLog(e);
            return;
        }

        _dispatcherQueue.TryEnqueue(() => AppendLog(e));
    }

    private void AppendLog(string message)
    {
        LogText = string.IsNullOrWhiteSpace(LogText) ? message : $"{LogText}{Environment.NewLine}{message}";
    }
}
