using SR1CTRL.Application.Abstractions;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.UseCases;

public sealed class ReciprocationService : IDisposable
{
    private readonly ISerialConnection _serial;
    private readonly object _gate = new();

    private AxisReciprocator? _lin;
    private AxisReciprocator? _rot;

    private CancellationTokenSource? _cts;
    private Task? _task;

    private readonly Queue<string> _pendingCommands = new();

    public ReciprocationService(ISerialConnection serial)
    {
        _serial = serial;
    }

    public void ConfigureLinear(AxisMotionSettings settings, bool applyImmediately = true)
    {
        lock (_gate)
        {
            if (_lin is null) _lin = new AxisReciprocator(settings);
            else _lin.UpdateSettings(settings, applyImmediately);

            if (applyImmediately && _cts is not null)
            {
                var command = _lin.Reapply(DateTime.UtcNow);
                _pendingCommands.Enqueue(command);
            }
        }
    }

    public void ConfigureRotate(AxisMotionSettings settings, bool applyImmediately = true)
    {
        lock (_gate)
        {
            if (_rot is null) _rot = new AxisReciprocator(settings);
            else _rot.UpdateSettings(settings, applyImmediately);

            if (applyImmediately && _cts is not null)
            {
                var command = _rot.Reapply(DateTime.UtcNow);
                _pendingCommands.Enqueue(command);
            }
        }
    }

    public Task StartAsync()
    {
        if (_cts is not null) throw new InvalidOperationException("Already started.");

        _cts = new CancellationTokenSource();
        _task = RunAsync(_cts.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        _cts.Cancel();

        try
        {
            if (_task is not null)
            {
                await _task.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            _task = null;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            string? pending = null;
            lock (_gate)
            {
                if (_pendingCommands.Count > 0)
                {
                    pending = _pendingCommands.Dequeue();
                }
            }

            if (pending is not null)
            {
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
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                continue;
            }

            var now = DateTime.UtcNow;
            var next = DateTime.MaxValue;
            if (lin is not null) next = Min(next, lin.NextAtUtc);
            if (rot is not null) next = Min(next, rot.NextAtUtc);

            var delay = next - now;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }

            now = DateTime.UtcNow;

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

            var line = (linearCommand is not null && rotateCommand is not null)
                ? $"{linearCommand} {rotateCommand}"
                : (linearCommand ?? rotateCommand!);

            await _serial.SendLineAsync(line, cancellationToken).ConfigureAwait(false);
        }
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;
}
