using System.Diagnostics;
using System.Net.Http;

namespace KopiaWinUI3.Services;

public sealed class KopiaProcessService : IKopiaProcessService
{
    private readonly IKopiaLocator _locator;
    private readonly ILocalPortService _portService;
    private Process? _process;

    public KopiaProcessService(IKopiaLocator locator, ILocalPortService portService)
    {
        _locator = locator;
        _portService = portService;
    }

    public event EventHandler<string>? LogReceived;

    public Uri? ServerUri { get; private set; }

    public bool IsRunning => _process is { HasExited: false };

    public async Task<Uri> StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning && ServerUri is not null)
        {
            return ServerUri;
        }

        var executable = _locator.FindKopiaExecutable()
            ?? throw new FileNotFoundException("未找到 kopia.exe。请把 Kopia Windows 版二进制放到 third_party/kopia/kopia.exe。");

        var port = _portService.GetAvailableLoopbackPort();
        ServerUri = new Uri($"http://127.0.0.1:{port}/");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("server");
        startInfo.ArgumentList.Add("start");
        startInfo.ArgumentList.Add($"--address={ServerUri}");
        startInfo.ArgumentList.Add("--ui");
        startInfo.ArgumentList.Add("--insecure");
        startInfo.ArgumentList.Add("--without-password");
        startInfo.ArgumentList.Add("--shutdown-on-stdin");
        startInfo.ArgumentList.Add("--kopiaui-notifications");

        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };
        _process.OutputDataReceived += OnOutputDataReceived;
        _process.ErrorDataReceived += OnOutputDataReceived;
        _process.Exited += OnExited;

        RaiseLog($"启动 Kopia: {executable}");
        RaiseLog($"服务地址: {ServerUri}");

        if (!_process.Start())
        {
            throw new InvalidOperationException("无法启动 Kopia 进程。");
        }

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitForServerAsync(ServerUri, cancellationToken);
        return ServerUri;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var process = _process;
        if (process is null)
        {
            return;
        }

        if (!process.HasExited)
        {
            try
            {
                process.StandardInput.Close();
            }
            catch (InvalidOperationException)
            {
                // Process already closed stdin.
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                RaiseLog("Kopia 未在 5 秒内退出，强制结束进程。");
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
        }

        process.OutputDataReceived -= OnOutputDataReceived;
        process.ErrorDataReceived -= OnOutputDataReceived;
        process.Exited -= OnExited;
        process.Dispose();

        _process = null;
        ServerUri = null;
        RaiseLog("Kopia 已停止。");
    }

    private static async Task WaitForServerAsync(Uri serverUri, CancellationToken cancellationToken)
    {
        using var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(2)
        };

        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        Exception? lastException = null;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var response = await client.GetAsync(serverUri, cancellationToken);
                if ((int)response.StatusCode < 500)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                lastException = ex;
            }

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException($"等待 Kopia UI 启动超时: {serverUri}", lastException);
    }

    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.Data))
        {
            RaiseLog(e.Data);
        }
    }

    private void OnExited(object? sender, EventArgs e)
    {
        RaiseLog($"Kopia 进程已退出，ExitCode={_process?.ExitCode}");
    }

    private void RaiseLog(string message)
    {
        LogReceived?.Invoke(this, $"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }
}
