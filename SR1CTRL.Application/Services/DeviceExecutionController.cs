using SR1CTRL.Domain;

namespace SR1CTRL.Application.Services;

internal sealed class DeviceExecutionController
{
    private readonly object _gate = new();
    private readonly SemaphoreSlim _transitionLock = new(1, 1);

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
        await _transitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
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
        finally
        {
            _transitionLock.Release();
        }
    }

    public async Task StopAsync(DeviceSession? session, CancellationToken cancellationToken)
    {
        await _transitionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
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
        finally
        {
            _transitionLock.Release();
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

    public void ConfigureMotionProfile(DeviceSession? session, MotionProfileSettings settings, bool applyImmediately)
    {
        session?.Reciprocation.ConfigureMotionProfile(settings, applyImmediately);
    }

    public void ResetState()
    {
        lock (_gate)
        {
            _isRunning = false;
        }
    }
}

