namespace KopiaWinUI3.Services;

public interface IKopiaLocator
{
    string? FindKopiaExecutable();

    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);
}
