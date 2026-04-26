using SR1CTRL.Application.Abstractions;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.UseCases;

public sealed class ReciprocationService : IDisposable
{
    private readonly ISerialConnection _serial;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _wakeUpSignal = new(0, 1);

    private AxisReciprocator? _lin;
    private AxisReciprocator? _rot;
    private CoordinatedMotionPlanner? _coordinated;
    private AxisMotionSettings? _linearSettings;
    private AxisMotionSettings? _rotateSettings;
    private MotionProfileSettings _motionProfile = new(MotionProfileKind.IndependentLoop, 0.5);

    private CancellationTokenSource? _cts;
    private Task? _task;
    private bool _disposed;

    private string? _pendingLinearCommand;
    private string? _pendingRotateCommand;
    private string? _pendingCoordinatedCommand;

    public ReciprocationService(ISerialConnection serial, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(serial);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _serial = serial;
        _timeProvider = timeProvider;
    }

    public void ConfigureLinear(AxisMotionSettings settings, bool applyImmediately = true)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            _linearSettings = settings.Normalize();

            if (IsCoordinatedMotion)
            {
                UpdateCoordinatedPlannerLocked(applyImmediately);
                QueueCoordinatedImmediateLocked(applyImmediately);
                return;
            }

            _coordinated = null;
            if (_lin is null) _lin = new AxisReciprocator(_linearSettings);
            else _lin.UpdateSettings(_linearSettings, applyImmediately);

            if (applyImmediately && _cts is not null)
            {
                _pendingLinearCommand = _lin.Reapply(_timeProvider.GetUtcNow());
                SignalLoop();
            }
        }
    }

    public void ConfigureRotate(AxisMotionSettings settings, bool applyImmediately = true)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            _rotateSettings = settings.Normalize();

            if (IsCoordinatedMotion)
            {
                UpdateCoordinatedPlannerLocked(applyImmediately);
                QueueCoordinatedImmediateLocked(applyImmediately);
                return;
            }

            _coordinated = null;
            if (_rot is null) _rot = new AxisReciprocator(_rotateSettings);
            else _rot.UpdateSettings(_rotateSettings, applyImmediately);

            if (applyImmediately && _cts is not null)
            {
                _pendingRotateCommand = _rot.Reapply(_timeProvider.GetUtcNow());
                SignalLoop();
            }
        }
    }

    public void ConfigureMotionProfile(MotionProfileSettings settings, bool applyImmediately = true)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            _motionProfile = settings.Normalize();

            if (IsCoordinatedMotion)
            {
                _lin = null;
                _rot = null;
                UpdateCoordinatedPlannerLocked(applyImmediately);
                QueueCoordinatedImmediateLocked(applyImmediately);
                return;
            }

            _coordinated = null;
            RestoreIndependentReciprocatorsLocked(applyImmediately);
            QueueIndependentImmediateLocked(applyImmediately);
        }
    }

    public Task StartAsync()
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            if (_cts is not null)
            {
                return Task.CompletedTask;
            }

            _cts = new CancellationTokenSource();
            _task = RunAsync(_cts.Token);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? task;

        lock (_gate)
        {
            cts = _cts;
            task = _task;
            _cts = null;
            _task = null;

            _pendingLinearCommand = null;
            _pendingRotateCommand = null;
            _pendingCoordinatedCommand = null;
        }

        if (cts is null)
        {
            return;
        }

        cts.Cancel();

        try
        {
            if (task is not null)
            {
                await task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts.Dispose();
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cts;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cts = _cts;
            _cts = null;
            _task = null;
            _pendingLinearCommand = null;
            _pendingRotateCommand = null;
            _pendingCoordinatedCommand = null;
        }

        cts?.Cancel();
        cts?.Dispose();
        _wakeUpSignal.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? pendingCoordinated;
            string? pendingLinear;
            string? pendingRotate;

            lock (_gate)
            {
                pendingCoordinated = _pendingCoordinatedCommand;
                pendingLinear = _pendingLinearCommand;
                pendingRotate = _pendingRotateCommand;
                _pendingCoordinatedCommand = null;
                _pendingLinearCommand = null;
                _pendingRotateCommand = null;
            }

            if (pendingCoordinated is not null)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _serial.SendLineAsync(pendingCoordinated, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (pendingLinear is not null || pendingRotate is not null)
            {
                var pending = CombineCommands(pendingLinear, pendingRotate);
                cancellationToken.ThrowIfCancellationRequested();
                await _serial.SendLineAsync(pending, cancellationToken).ConfigureAwait(false);
                continue;
            }

            AxisReciprocator? lin;
            AxisReciprocator? rot;
            CoordinatedMotionPlanner? coordinated;
            lock (_gate)
            {
                lin = _lin;
                rot = _rot;
                coordinated = _coordinated;
            }

            if (lin is null && rot is null && coordinated is null)
            {
                await WaitForWakeOrDelayAsync(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var now = _timeProvider.GetUtcNow();
            var next = DateTimeOffset.MaxValue;
            if (lin is not null) next = Min(next, lin.NextAtUtc);
            if (rot is not null) next = Min(next, rot.NextAtUtc);
            if (coordinated is not null) next = Min(next, coordinated.NextAtUtc);

            var delay = next - now;
            if (delay > TimeSpan.Zero)
            {
                await WaitForWakeOrDelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }

            now = _timeProvider.GetUtcNow();

            if (coordinated is not null && now >= coordinated.NextAtUtc)
            {
                var coordinatedLine = BuildCoordinatedCommand(coordinated.Step(now));

                cancellationToken.ThrowIfCancellationRequested();
                await _serial.SendLineAsync(coordinatedLine, cancellationToken).ConfigureAwait(false);
                continue;
            }

            string? linearCommand = null;
            string? rotateCommand = null;

            if (lin is not null && now >= lin.NextAtUtc)
            {
                linearCommand = lin.Step(now);
            }

            if (rot is not null && now >= rot.NextAtUtc)
            {
                rotateCommand = rot.Step(now);
            }

            if (linearCommand is null && rotateCommand is null)
            {
                continue;
            }

            var line = CombineCommands(linearCommand, rotateCommand);

            cancellationToken.ThrowIfCancellationRequested();
            await _serial.SendLineAsync(line, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WaitForWakeOrDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(delay, cancellationToken);
        var wakeTask = _wakeUpSignal.WaitAsync(cancellationToken);
        var completed = await Task.WhenAny(delayTask, wakeTask).ConfigureAwait(false);
        await completed.ConfigureAwait(false);
    }

    private void SignalLoop()
    {
        try
        {
            _wakeUpSignal.Release();
        }
        catch (SemaphoreFullException)
        {
        }
    }

    private static string CombineCommands(string? linearCommand, string? rotateCommand)
    {
        return (linearCommand is not null && rotateCommand is not null)
            ? $"{linearCommand} {rotateCommand}"
            : (linearCommand ?? rotateCommand!);
    }

    private bool IsCoordinatedMotion => _motionProfile.Kind is not MotionProfileKind.IndependentLoop;

    private void UpdateCoordinatedPlannerLocked(bool applyImmediately)
    {
        if (_linearSettings is not { } linear || _rotateSettings is not { } rotate)
        {
            _coordinated = null;
            return;
        }

        if (_coordinated is null)
        {
            _coordinated = new CoordinatedMotionPlanner(linear, rotate, _motionProfile);
            return;
        }

        _coordinated.Update(linear, rotate, _motionProfile, applyImmediately);
    }

    private void RestoreIndependentReciprocatorsLocked(bool applyImmediately)
    {
        if (_linearSettings is { } linear)
        {
            if (_lin is null) _lin = new AxisReciprocator(linear);
            else _lin.UpdateSettings(linear, applyImmediately);
        }

        if (_rotateSettings is { } rotate)
        {
            if (_rot is null) _rot = new AxisReciprocator(rotate);
            else _rot.UpdateSettings(rotate, applyImmediately);
        }
    }

    private void QueueCoordinatedImmediateLocked(bool applyImmediately)
    {
        if (!applyImmediately || _cts is null || _coordinated is null)
        {
            return;
        }

        _pendingCoordinatedCommand = BuildCoordinatedCommand(_coordinated.Step(_timeProvider.GetUtcNow()));
        _pendingLinearCommand = null;
        _pendingRotateCommand = null;
        SignalLoop();
    }

    private void QueueIndependentImmediateLocked(bool applyImmediately)
    {
        if (!applyImmediately || _cts is null)
        {
            return;
        }

        var now = _timeProvider.GetUtcNow();
        _pendingCoordinatedCommand = null;
        _pendingLinearCommand = _lin?.Reapply(now);
        _pendingRotateCommand = _rot?.Reapply(now);
        SignalLoop();
    }

    private static string BuildCoordinatedCommand(MotionFrame frame)
    {
        var linear = new AxisMotionSettings(AxisType.L, 0, 0.0, 0.0001, 1.0);
        var rotate = new AxisMotionSettings(AxisType.R, 0, 0.0, 0.0001, 1.0);

        return CombineCommands(
            TCodeFormatter.AxisWithInterval(linear, frame.LinearTarget, frame.IntervalMs),
            TCodeFormatter.AxisWithInterval(rotate, frame.RotateTarget, frame.IntervalMs));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReciprocationService));
        }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
