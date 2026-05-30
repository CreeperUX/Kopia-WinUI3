namespace KopiaWinUI3.Services;

public interface IKopiaProcessService
{
    event EventHandler<string>? LogReceived;

    Uri? ServerUri { get; }

    bool IsRunning { get; }

    Task<Uri> StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
