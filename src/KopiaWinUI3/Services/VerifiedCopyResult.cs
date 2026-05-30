namespace KopiaWinUI3.Services;

public sealed record VerifiedCopyResult(
    string DestinationPath,
    int FileCount,
    long TotalBytes);
