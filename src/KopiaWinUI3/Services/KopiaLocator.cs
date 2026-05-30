using System.Diagnostics;

namespace KopiaWinUI3.Services;

public sealed class KopiaLocator : IKopiaLocator
{
    private const string ExecutableName = "kopia.exe";

    public string? FindKopiaExecutable()
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
        var executable = FindKopiaExecutable();
        if (executable is null)
        {
            return "未找到 kopia.exe";
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--version");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return "无法启动 kopia.exe";
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = (await outputTask).Trim();
        var error = (await errorTask).Trim();

        if (!string.IsNullOrWhiteSpace(output))
        {
            return output;
        }

        return string.IsNullOrWhiteSpace(error) ? $"kopia.exe exited with code {process.ExitCode}" : error;
    }

    private static IEnumerable<string> GetCandidates()
    {
        var baseDirectory = AppContext.BaseDirectory;

        yield return Path.Combine(baseDirectory, "bin", ExecutableName);
        yield return Path.Combine(baseDirectory, ExecutableName);

        var current = new DirectoryInfo(baseDirectory);
        while (current is not null)
        {
            yield return Path.Combine(current.FullName, "third_party", "kopia", ExecutableName);
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
