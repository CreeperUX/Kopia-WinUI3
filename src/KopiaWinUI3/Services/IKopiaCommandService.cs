namespace KopiaWinUI3.Services;

public interface IKopiaCommandService
{
    Task<KopiaCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);

    Task<KopiaCommandResult> RunStreamingAsync(
        IReadOnlyList<string> arguments,
        Action<string> outputReceived,
        CancellationToken cancellationToken = default);
}
