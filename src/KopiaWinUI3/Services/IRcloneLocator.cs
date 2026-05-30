namespace KopiaWinUI3.Services;

public interface IRcloneLocator
{
    string? FindRcloneExecutable();

    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);
}
