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

    private CancellationTokenSource? _cts;
    private Task? _task;
    private bool _disposed;

    private string? _pendingLinearCommand;
    private string? _pendingRotateCommand;

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

            if (_lin is null) _lin = new AxisReciprocator(settings);
            else _lin.UpdateSettings(settings, applyImmediately);

            if (applyImmediately && _cts is not null)
            {
                var command = _lin.Reapply(_timeProvider.GetUtcNow());
                _pendingLinearCommand = command;
                SignalLoop();
            }
        }
    }

    public void ConfigureRotate(AxisMotionSettings settings, bool applyImmediately = true)
    {
        lock (_gate)
        {
            ThrowIfDisposed();

            if (_rot is null) _rot = new AxisReciprocator(settings);
            else _rot.UpdateSettings(settings, applyImmediately);

            if (applyImmediately && _cts is not null)
            {
                var command = _rot.Reapply(_timeProvider.GetUtcNow());
                _pendingRotateCommand = command;
                SignalLoop();
            }
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
        }

        cts?.Cancel();
        cts?.Dispose();
        _wakeUpSignal.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? pendingLinear;
            string? pendingRotate;

            lock (_gate)
            {
                pendingLinear = _pendingLinearCommand;
                pendingRotate = _pendingRotateCommand;
                _pendingLinearCommand = null;
                _pendingRotateCommand = null;
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
            lock (_gate)
            {
                lin = _lin;
                rot = _rot;
            }

            if (lin is null && rot is null)
            {
                await WaitForWakeOrDelayAsync(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                continue;
            }

            var now = _timeProvider.GetUtcNow();
            var next = DateTimeOffset.MaxValue;
            if (lin is not null) next = Min(next, lin.NextAtUtc);
            if (rot is not null) next = Min(next, rot.NextAtUtc);

            var delay = next - now;
            if (delay > TimeSpan.Zero)
            {
                await WaitForWakeOrDelayAsync(delay, cancellationToken).ConfigureAwait(false);
            }

            now = _timeProvider.GetUtcNow();

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

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ReciprocationService));
        }
    }

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a <= b ? a : b;
}
