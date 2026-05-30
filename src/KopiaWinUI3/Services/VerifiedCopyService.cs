using System.Security.Cryptography;

namespace KopiaWinUI3.Services;

public sealed class VerifiedCopyService : IVerifiedCopyService
{
    public async Task<VerifiedCopyResult> CopyAsync(
        string sourcePath,
        string destinationRoot,
        VerifiedCopyOptions options,
        IProgress<VerifiedCopyProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var parallelism = Math.Clamp(options.Parallelism, 1, 32);
        var bufferSize = Math.Clamp(options.BufferSizeBytes, 1024 * 1024, 64 * 1024 * 1024);
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

        using var gate = new SemaphoreSlim(parallelism);
        var tasks = files.Select(async file =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            await gate.WaitAsync(cancellationToken);
            try
            {
                var destinationFile = new FileInfo(file.DestinationPath);
                destinationFile.Directory?.Create();

                await CopyFileAsync(file, totalBytes, bufferSize, progress, () => Interlocked.Read(ref copiedBytes), copied =>
                {
                    Interlocked.Add(ref copiedBytes, copied);
                }, cancellationToken);

                if (options.VerifyAfterCopy)
                {
                    progress.Report(new VerifiedCopyProgress(
                        $"正在校验 {Path.GetFileName(file.SourcePath)}",
                        Interlocked.Read(ref copiedBytes),
                        totalBytes,
                        file.SourcePath));
                    await VerifyFileAsync(file.SourcePath, file.DestinationPath, bufferSize, cancellationToken);
                }
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        var doneMessage = options.VerifyAfterCopy
            ? $"校验拷贝完成：{destinationDirectory}"
            : $"文件拷贝完成：{destinationDirectory}";
        progress.Report(new VerifiedCopyProgress(doneMessage, totalBytes, totalBytes));
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
        long totalBytes,
        int bufferSize,
        IProgress<VerifiedCopyProgress> progress,
        Func<long> getCopiedBytes,
        Action<long> addCopiedBytes,
        CancellationToken cancellationToken)
    {
        await using var source = new FileStream(item.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using var destination = new FileStream(item.DestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var buffer = new byte[bufferSize];

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            addCopiedBytes(read);
            progress.Report(new VerifiedCopyProgress(
                $"正在复制 {Path.GetFileName(item.SourcePath)}",
                getCopiedBytes(),
                totalBytes,
                item.SourcePath));
        }
    }

    private static async Task VerifyFileAsync(string sourcePath, string destinationPath, int bufferSize, CancellationToken cancellationToken)
    {
        var sourceHash = await ComputeHashAsync(sourcePath, bufferSize, cancellationToken);
        var destinationHash = await ComputeHashAsync(destinationPath, bufferSize, cancellationToken);

        if (!sourceHash.SequenceEqual(destinationHash))
        {
            throw new InvalidOperationException($"文件校验失败：{sourcePath}");
        }
    }

    private static async Task<byte[]> ComputeHashAsync(string path, int bufferSize, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    private sealed record CopyItem(string SourcePath, string DestinationPath, long Length);
}
