using System.Security.Cryptography;

namespace KopiaWinUI3.Services;

public sealed class VerifiedCopyService : IVerifiedCopyService
{
    private const int BufferSize = 1024 * 1024;

    public async Task<VerifiedCopyResult> CopyAsync(
        string sourcePath,
        string destinationRoot,
        IProgress<VerifiedCopyProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var sourceDirectory = new DirectoryInfo(sourcePath);
        if (!sourceDirectory.Exists)
        {
            throw new DirectoryNotFoundException($"找不到备份源路径：{sourcePath}");
        }

        var destinationDirectory = Path.Combine(destinationRoot, sourceDirectory.Name);
        GuardDestination(sourceDirectory.FullName, destinationDirectory);
        Directory.CreateDirectory(destinationDirectory);

        progress.Report(new VerifiedCopyProgress("正在扫描文件...", 0, 0));

        var files = sourceDirectory.EnumerateFiles("*", SearchOption.AllDirectories)
            .Select(file => new CopyItem(
                file.FullName,
                Path.Combine(destinationDirectory, Path.GetRelativePath(sourceDirectory.FullName, file.FullName)),
                file.Length))
            .ToList();

        var totalBytes = files.Sum(file => file.Length);
        var copiedBytes = 0L;
        progress.Report(new VerifiedCopyProgress($"发现 {files.Count} 个文件，准备复制。", 0, totalBytes));

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destinationFile = new FileInfo(file.DestinationPath);
            destinationFile.Directory?.Create();

            await CopyFileAsync(file, copiedBytes, totalBytes, progress, cancellationToken);
            copiedBytes += file.Length;

            progress.Report(new VerifiedCopyProgress($"正在校验 {Path.GetFileName(file.SourcePath)}", copiedBytes, totalBytes, file.SourcePath));
            await VerifyFileAsync(file.SourcePath, file.DestinationPath, cancellationToken);
        }

        progress.Report(new VerifiedCopyProgress($"校验拷贝完成：{destinationDirectory}", totalBytes, totalBytes));
        return new VerifiedCopyResult(destinationDirectory, files.Count, totalBytes);
    }

    private static void GuardDestination(string sourcePath, string destinationPath)
    {
        var normalizedSource = Path.GetFullPath(sourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalizedDestination = Path.GetFullPath(destinationPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (normalizedDestination.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalizedDestination, normalizedSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("目标路径不能位于源路径内部，请选择一个独立的备份目标文件夹。");
        }
    }

    private static async Task CopyFileAsync(
        CopyItem item,
        long copiedBeforeFile,
        long totalBytes,
        IProgress<VerifiedCopyProgress> progress,
        CancellationToken cancellationToken)
    {
        var copiedInFile = 0L;
        await using var source = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        await using var destination = new FileStream(item.DestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);
        var buffer = new byte[BufferSize];

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copiedInFile += read;
            progress.Report(new VerifiedCopyProgress(
                $"正在复制 {Path.GetFileName(item.SourcePath)}",
                copiedBeforeFile + copiedInFile,
                totalBytes,
                item.SourcePath));
        }
    }

    private static async Task VerifyFileAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken)
    {
        var sourceHash = await ComputeHashAsync(sourcePath, cancellationToken);
        var destinationHash = await ComputeHashAsync(destinationPath, cancellationToken);

        if (!sourceHash.SequenceEqual(destinationHash))
        {
            throw new InvalidOperationException($"文件校验失败：{sourcePath}");
        }
    }

    private static async Task<byte[]> ComputeHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    private sealed record CopyItem(string SourcePath, string DestinationPath, long Length);
}
