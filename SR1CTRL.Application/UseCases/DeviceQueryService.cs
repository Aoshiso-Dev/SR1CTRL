using System.Text;
using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Application.UseCases;

public sealed class DeviceQueryService
{
    private readonly ISerialConnection _serial;
    private readonly TimeProvider _timeProvider;

    public DeviceQueryService(ISerialConnection serial, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(serial);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _serial = serial;
        _timeProvider = timeProvider;
    }

    public Task<string> QueryD0Async(CancellationToken cancellationToken) => QuerySingleAsync("D0", cancellationToken);
    public Task<string> QueryD1Async(CancellationToken cancellationToken) => QuerySingleAsync("D1", cancellationToken);
    public Task<string> QueryD2Async(CancellationToken cancellationToken) => QueryMultiAsync("D2", cancellationToken);

    private async Task<string> QuerySingleAsync(string command, CancellationToken cancellationToken)
    {
        await _serial.SendLineAsync(command, cancellationToken).ConfigureAwait(false);

        return await _serial.ReadLineAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false) ?? string.Empty;
    }

    private async Task<string> QueryMultiAsync(string command, CancellationToken cancellationToken)
    {
        await _serial.SendLineAsync(command, cancellationToken).ConfigureAwait(false);

        var sb = new StringBuilder();
        var idle = TimeSpan.FromMilliseconds(200);
        var hard = TimeSpan.FromSeconds(2);

        var start = _timeProvider.GetUtcNow();
        while (_timeProvider.GetUtcNow() - start < hard)
        {
            var line = await _serial.ReadLineAsync(idle, cancellationToken).ConfigureAwait(false);
            if (line is null)
            {
                break;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(line);
        }

        return sb.ToString();
    }
}
