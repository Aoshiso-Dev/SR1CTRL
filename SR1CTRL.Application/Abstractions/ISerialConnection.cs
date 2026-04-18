namespace SR1CTRL.Application.Abstractions;

public interface ISerialConnection : IAsyncDisposable
{
    bool IsOpen { get; }
    Task OpenAsync();
    Task CloseAsync();
    Task SendLineAsync(string line, CancellationToken cancellationToken);
    Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken);
}
