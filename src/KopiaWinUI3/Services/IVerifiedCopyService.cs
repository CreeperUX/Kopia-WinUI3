namespace KopiaWinUI3.Services;

public interface IVerifiedCopyService
{
    Task<VerifiedCopyResult> CopyAsync(
        string sourcePath,
        string destinationRoot,
        IProgress<VerifiedCopyProgress> progress,
        CancellationToken cancellationToken = default);
}
