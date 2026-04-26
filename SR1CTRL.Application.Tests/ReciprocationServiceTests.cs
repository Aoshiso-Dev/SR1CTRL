using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.UseCases;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.Tests;

public sealed class ReciprocationServiceTests
{
    [Fact]
    public async Task StartAsync_CanBeCalledMultipleTimes()
    {
        var serial = new RecordingSerialConnection();
        var sut = new ReciprocationService(serial, TimeProvider.System);

        await sut.StartAsync();
        await sut.StartAsync();
        await sut.StopAsync();
    }

    [Fact]
    public async Task ConfigureLinear_WhileRunning_UsesLatestImmediateCommand()
    {
        var serial = new RecordingSerialConnection();
        var sut = new ReciprocationService(serial, TimeProvider.System);

        await sut.StartAsync();

        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.2, 0.7, 1.0), applyImmediately: true);
        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.1, 0.9, 1.0), applyImmediately: true);

        await Task.Delay(120);
        await sut.StopAsync();

        Assert.Single(serial.SentLines);
        Assert.Contains("L09000", serial.SentLines[0]);
    }

    [Fact]
    public async Task ConfigureLinear_WhileWaiting_ReflectsImmediately()
    {
        var serial = new RecordingSerialConnection();
        var sut = new ReciprocationService(serial, TimeProvider.System);

        await sut.StartAsync();
        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.1, 0.9, 0.1), applyImmediately: true);
        Assert.True(await serial.WaitForSentCountAsync(1, TimeSpan.FromMilliseconds(500)));

        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.2, 0.8, 1.0), applyImmediately: true);
        Assert.True(await serial.WaitForSentCountAsync(2, TimeSpan.FromMilliseconds(500)));

        await sut.StopAsync();
    }

    [Fact]
    public async Task TwistStroke_EmitsCoordinatedLinearAndRotateCommands()
    {
        var serial = new RecordingSerialConnection();
        var sut = new ReciprocationService(serial, TimeProvider.System);

        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.2, 0.8, 4.0), applyImmediately: false);
        sut.ConfigureRotate(new AxisMotionSettings(AxisType.R, 0, 0.3, 0.7, 1.0), applyImmediately: false);
        sut.ConfigureMotionProfile(new MotionProfileSettings(MotionProfileKind.TwistStroke, 1.0), applyImmediately: false);

        await sut.StartAsync();
        Assert.True(await serial.WaitForSentCountAsync(1, TimeSpan.FromMilliseconds(500)));
        await sut.StopAsync();

        Assert.Contains("L0", serial.SentLines[0]);
        Assert.Contains("R0", serial.SentLines[0]);
    }

    private sealed class RecordingSerialConnection : ISerialConnection
    {
        private readonly List<string> _sentLines = new();
        private readonly object _gate = new();

        public bool IsOpen => true;

        public IReadOnlyList<string> SentLines
        {
            get
            {
                lock (_gate)
                {
                    return _sentLines.ToArray();
                }
            }
        }

        public Task OpenAsync() => Task.CompletedTask;

        public Task CloseAsync() => Task.CompletedTask;

        public Task SendLineAsync(string line, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _sentLines.Add(line);
            }

            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async Task<bool> WaitForSentCountAsync(int expectedCount, TimeSpan timeout)
        {
            var deadlineUtc = TimeProvider.System.GetUtcNow() + timeout;

            while (TimeProvider.System.GetUtcNow() < deadlineUtc)
            {
                lock (_gate)
                {
                    if (_sentLines.Count >= expectedCount)
                    {
                        return true;
                    }
                }

                await Task.Delay(10);
            }

            lock (_gate)
            {
                return _sentLines.Count >= expectedCount;
            }
        }
    }
}
