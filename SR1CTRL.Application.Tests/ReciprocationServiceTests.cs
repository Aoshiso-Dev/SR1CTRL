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
        var sut = new ReciprocationService(serial);

        await sut.StartAsync();
        await sut.StartAsync();
        await sut.StopAsync();
    }

    [Fact]
    public async Task ConfigureLinear_WhileRunning_UsesLatestImmediateCommand()
    {
        var serial = new RecordingSerialConnection();
        var sut = new ReciprocationService(serial);

        await sut.StartAsync();

        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.2, 0.7, 1.0), applyImmediately: true);
        sut.ConfigureLinear(new AxisMotionSettings(AxisType.L, 0, 0.1, 0.9, 1.0), applyImmediately: true);

        await Task.Delay(120);
        await sut.StopAsync();

        Assert.Single(serial.SentLines);
        Assert.Contains("L09000", serial.SentLines[0]);
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
    }
}
