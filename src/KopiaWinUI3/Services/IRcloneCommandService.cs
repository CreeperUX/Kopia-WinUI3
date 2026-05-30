namespace KopiaWinUI3.Services;

public interface IRcloneCommandService
{
    Task<RcloneCommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);

    Task<RcloneCommandResult> RunStreamingAsync(
        IReadOnlyList<string> arguments,
        Action<string> outputReceived,
        CancellationToken cancellationToken = default);
}
