namespace KopiaWinUI3.Services;

public interface IKopiaCommandService
{
    Task<KopiaCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
