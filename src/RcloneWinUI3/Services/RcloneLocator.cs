using System.Diagnostics;

namespace RcloneWinUI3.Services;

public sealed class RcloneLocator : IRcloneLocator
{
    private const string ExecutableName = "rclone.exe";

    public string? FindRcloneExecutable()
    {
        foreach (var candidate in GetCandidates())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var executable = FindRcloneExecutable();
        if (executable is null)
        {
            return "未找到 rclone.exe";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("version");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return "无法启动 rclone.exe";
        }

        var outputTask = process.StandardOutput.ReadLineAsync(cancellationToken).AsTask();
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = (await outputTask)?.Trim();
        if (!string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        var error = (await errorTask).Trim();
        return string.IsNullOrWhiteSpace(error) ? $"rclone.exe exited with code {process.ExitCode}" : error;
    }

    private static IEnumerable<string> GetCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;

        yield return Path.Combine(baseDirectory, "bin", ExecutableName);
        yield return Path.Combine(baseDirectory, ExecutableName);

        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            yield return Path.Combine(current.FullName, "third_party", "rclone", ExecutableName);
            current = current.Parent;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return Path.Combine(directory, ExecutableName);
        }
    }
}
