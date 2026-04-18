using SR1CTRL.Application.Abstractions;

namespace SR1CTRL.Application.Services;

internal sealed class DeviceConnectionManager
{
    private readonly ISerialConnectionFactory _serialConnectionFactory;
    private readonly object _gate = new();

    private DeviceSession? _session;

    public DeviceConnectionManager(ISerialConnectionFactory serialConnectionFactory)
    {
        _serialConnectionFactory = serialConnectionFactory;
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
            throw new InvalidOperationException("COMポートを選択してください。");
        }

        if (IsConnected)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var serial = _serialConnectionFactory.Create(portName, baudRate);
        await serial.OpenAsync().ConfigureAwait(false);

        var created = new DeviceSession(serial);
        var keepCreated = false;

        lock (_gate)
        {
            if (_session is null)
            {
                _session = created;
                keepCreated = true;
            }
        }

        if (!keepCreated)
        {
            await created.DisposeAsync().ConfigureAwait(false);
        }
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
}

