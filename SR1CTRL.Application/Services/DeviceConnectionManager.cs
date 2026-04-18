using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Application.Services;

internal sealed class DeviceConnectionManager
{
    private readonly ISerialConnectionFactory _serialConnectionFactory;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    private DeviceSession? _session;

    public DeviceConnectionManager(ISerialConnectionFactory serialConnectionFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(serialConnectionFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _serialConnectionFactory = serialConnectionFactory;
        _timeProvider = timeProvider;
    }

    public bool IsConnected
    {
        get
        {
            lock (_gate)
            {
                return _session?.Serial.IsOpen == true;
            }
        }
    }

    public async Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            throw new InvalidOperationException("Select a COM port.");
        }

        if (IsConnected)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var serial = _serialConnectionFactory.Create(portName, baudRate);
        await serial.OpenAsync().ConfigureAwait(false);

        var created = new DeviceSession(serial, _timeProvider);
        try
        {
            await EnsureDeviceRespondingAsync(created, cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                if (_session is null)
                {
                    _session = created;
                    return;
                }
            }
        }
        catch
        {
            await created.DisposeAsync().ConfigureAwait(false);
            throw;
        }

        await created.DisposeAsync().ConfigureAwait(false);
    }

    public DeviceSession RequireSession()
    {
        lock (_gate)
        {
            return _session ?? throw new InvalidOperationException("Not connected.");
        }
    }

    public DeviceSession? TryGetSession()
    {
        lock (_gate)
        {
            return _session;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        DeviceSession? current;

        lock (_gate)
        {
            current = _session;
            _session = null;
        }

        if (current is null)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        await current.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task EnsureDeviceRespondingAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(1));

        var response = await session.Query.QueryD0Async(timeoutCts.Token).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(response))
        {
            return;
        }

        throw new InvalidOperationException("No response from the connected device. Verify COM port settings and the target device.");
    }
}

