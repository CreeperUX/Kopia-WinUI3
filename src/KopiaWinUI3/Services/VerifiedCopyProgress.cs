namespace KopiaWinUI3.Services;

public sealed record VerifiedCopyProgress(
    string Message,
    long BytesCopied,
    long TotalBytes,
    string? CurrentFile = null);
