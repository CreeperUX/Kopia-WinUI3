using System.Diagnostics;

namespace KopiaWinUI3.Services;

public sealed class KopiaCommandService : IKopiaCommandService
{
    private readonly IKopiaLocator _locator;

    public KopiaCommandService(IKopiaLocator locator)
    {
        _locator = locator;
    }

    public async Task<KopiaCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        return await RunStreamingAsync(arguments, _ => { }, cancellationToken);
    }

    public async Task<KopiaCommandResult> RunStreamingAsync(
        IReadOnlyList<string> arguments,
        Action<string> outputReceived,
        CancellationToken cancellationToken = default)
    {
        var executable = _locator.FindKopiaExecutable()
            ?? throw new FileNotFoundException("未找到 kopia.exe。请先运行 scripts/Get-Kopia.ps1 下载 Kopia 本体。");

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--disable-file-logging");

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

        process.Start();

        if (process is null)
        {
            throw new InvalidOperationException("无法启动 kopia.exe。");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(outputDone.Task, errorDone.Task);

        return new KopiaCommandResult(
            process.ExitCode,
            string.Join(Environment.NewLine, output),
            string.Join(Environment.NewLine, error));
    }
}
