using System.Diagnostics;
using System.Text;

namespace KopiaWinUI3.Services;

public sealed class RcloneCommandService : IRcloneCommandService
{
    private readonly IRcloneLocator _locator;

    public RcloneCommandService(IRcloneLocator locator)
    {
        _locator = locator;
    }

    public async Task<RcloneCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        return await RunStreamingAsync(arguments, _ => { }, cancellationToken);
    }

    public async Task<RcloneCommandResult> RunStreamingAsync(
        IReadOnlyList<string> arguments,
        Action<string> outputReceived,
        CancellationToken cancellationToken = default)
    {
        var executable = _locator.FindRcloneExecutable()
            ?? throw new FileNotFoundException("未找到 rclone.exe。请先运行 scripts/Get-Rclone.ps1 下载 rclone 本体。");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var output = new List<string>();
        var error = new List<string>();
        var outputDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var errorDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                outputDone.TrySetResult();
                return;
            }

            output.Add(e.Data);
            outputReceived(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is null)
            {
                errorDone.TrySetResult();
                return;
            }

            error.Add(e.Data);
            outputReceived(e.Data);
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动 rclone.exe。");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(outputDone.Task, errorDone.Task);

        return new RcloneCommandResult(
            process.ExitCode,
            string.Join(Environment.NewLine, output),
            string.Join(Environment.NewLine, error));
    }
}
