using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.Models;
using SR1CTRL.Application.Services;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.UseCases;

public sealed class DeviceController : IDeviceController
{
    private readonly DeviceConnectionManager _connections;
    private readonly DeviceExecutionController _execution;

    public DeviceController(ISerialConnectionFactory serialConnectionFactory, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(serialConnectionFactory);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _connections = new DeviceConnectionManager(serialConnectionFactory, timeProvider);
        _execution = new DeviceExecutionController();
    }

    public bool IsConnected => _connections.IsConnected;

    public bool IsRunning => _execution.IsRunning;

    public async Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken)
    {
        await _connections.ConnectAsync(portName, baudRate, cancellationToken).ConfigureAwait(false);
        _execution.ResetState();
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken)
    {
        var session = _connections.TryGetSession();

        await _execution.StopAsync(session, cancellationToken).ConfigureAwait(false);
        await _connections.DisconnectAsync(cancellationToken).ConfigureAwait(false);

        _execution.ResetState();
    }

    public async Task<DeviceInfo> QueryDeviceInfoAsync(CancellationToken cancellationToken)
    {
        var session = _connections.RequireSession();

        var d0 = await session.Query.QueryD0Async(cancellationToken).ConfigureAwait(false);
        var d1 = await session.Query.QueryD1Async(cancellationToken).ConfigureAwait(false);
        var d2 = await session.Query.QueryD2Async(cancellationToken).ConfigureAwait(false);

        return new DeviceInfo(d0, d1, d2);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var session = _connections.RequireSession();
        await _execution.StartAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _execution.StopAsync(_connections.TryGetSession(), cancellationToken).ConfigureAwait(false);
    }

    public void ConfigureLinear(AxisMotionSettings settings, bool applyImmediately = true)
    {
        _execution.ConfigureLinear(_connections.TryGetSession(), settings, applyImmediately);
    }

    public void ConfigureRotate(AxisMotionSettings settings, bool applyImmediately = true)
    {
        _execution.ConfigureRotate(_connections.TryGetSession(), settings, applyImmediately);
    }

    public void ConfigureMotionProfile(MotionProfileSettings settings, bool applyImmediately = true)
    {
        _execution.ConfigureMotionProfile(_connections.TryGetSession(), settings, applyImmediately);
    }
}

