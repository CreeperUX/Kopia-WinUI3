using System.Diagnostics;
using System.Text;

namespace RcloneWinUI3.Services;

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
            ?? throw new FileNotFoundException("未找到 rclone.exe。请确认安装包或 third_party/rclone/rclone.exe 已包含 rclone 本体。");

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
        var outputLock = new object();
        var errorLock = new object();

        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动 rclone.exe。");
        }

        var outputTask = ReadProgressStreamAsync(
            process.StandardOutput,
            output,
            outputLock,
            outputReceived,
            cancellationToken);

        var errorTask = ReadProgressStreamAsync(
            process.StandardError,
            error,
            errorLock,
            outputReceived,
            cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(outputTask, errorTask);

        return new RcloneCommandResult(
            process.ExitCode,
            string.Join(Environment.NewLine, output),
            string.Join(Environment.NewLine, error));
    }

    private static async Task ReadProgressStreamAsync(
        StreamReader reader,
        List<string> lines,
        object linesLock,
        Action<string> outputReceived,
        CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var line = new StringBuilder();

        while (true)
        {
            var read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read == 0)
            {
                break;
            }

            for (var index = 0; index < read; index++)
            {
                var character = buffer[index];
                if (character is '\r' or '\n')
                {
                    FlushLine(line, lines, linesLock, outputReceived);
                    continue;
                }

                line.Append(character);
            }
        }

        FlushLine(line, lines, linesLock, outputReceived);
    }

    private static void FlushLine(
        StringBuilder line,
        List<string> lines,
        object linesLock,
        Action<string> outputReceived)
    {
        if (line.Length == 0)
        {
            return;
        }

        var value = line.ToString();
        line.Clear();

        lock (linesLock)
        {
            lines.Add(value);
        }

        outputReceived(value);
    }
}
