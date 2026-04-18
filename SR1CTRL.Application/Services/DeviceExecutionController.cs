using SR1CTRL.Domain;

namespace SR1CTRL.Application.Services;

internal sealed class DeviceExecutionController
{
    private readonly object _gate = new();

    private bool _isRunning;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _isRunning;
            }
        }
    }

    public async Task StartAsync(DeviceSession session, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_isRunning)
            {
                return;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        await session.Reciprocation.StartAsync().ConfigureAwait(false);

        lock (_gate)
        {
            _isRunning = true;
        }
    }

    public async Task StopAsync(DeviceSession? session, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (!_isRunning)
            {
                return;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (session is not null)
        {
            await session.Reciprocation.StopAsync().ConfigureAwait(false);
        }

        lock (_gate)
        {
            _isRunning = false;
        }
    }

    public void ConfigureLinear(DeviceSession? session, AxisMotionSettings settings, bool applyImmediately)
    {
        session?.Reciprocation.ConfigureLinear(settings, applyImmediately);
    }

    public void ConfigureRotate(DeviceSession? session, AxisMotionSettings settings, bool applyImmediately)
    {
        session?.Reciprocation.ConfigureRotate(settings, applyImmediately);
    }

    public void ResetState()
    {
        lock (_gate)
        {
            _isRunning = false;
        }
    }
}

