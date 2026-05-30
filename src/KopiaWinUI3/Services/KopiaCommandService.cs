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

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("无法启动 kopia.exe。");
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new KopiaCommandResult(
            process.ExitCode,
            await outputTask,
            await errorTask);
    }
}
