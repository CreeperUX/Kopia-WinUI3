namespace KopiaWinUI3.Services;

public sealed record VerifiedCopyOptions(
    int Parallelism,
    int BufferSizeBytes,
    bool VerifyAfterCopy);
