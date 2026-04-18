using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.UseCases;

namespace SR1CTRL.Application.Services;

internal sealed class DeviceSession : IAsyncDisposable
{
    public DeviceSession(ISerialConnection serial)
    {
        Serial = serial;
        Query = new DeviceQueryService(serial);
        Reciprocation = new ReciprocationService(serial);
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
