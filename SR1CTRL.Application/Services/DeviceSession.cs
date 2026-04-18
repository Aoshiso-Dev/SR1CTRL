using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.UseCases;

namespace SR1CTRL.Application.Services;

internal sealed class DeviceSession : IAsyncDisposable
{
    public DeviceSession(ISerialConnection serial, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(serial);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Serial = serial;
        Query = new DeviceQueryService(serial, timeProvider);
        Reciprocation = new ReciprocationService(serial, timeProvider);
    }

    public ISerialConnection Serial { get; }
    public DeviceQueryService Query { get; }
    public ReciprocationService Reciprocation { get; }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Reciprocation.StopAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }

        Reciprocation.Dispose();
        await Serial.CloseAsync().ConfigureAwait(false);
        await Serial.DisposeAsync().ConfigureAwait(false);
    }
}
