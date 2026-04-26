using SR1CTRL.Application.Abstractions;
using SR1CTRL.Application.UseCases;

namespace SR1CTRL.Application.Tests;

public sealed class DeviceControllerTests
{
    [Fact]
    public async Task ConnectAndDisconnect_ChangesConnectionState_AndDisposesConnection()
    {
        var factory = new FakeSerialConnectionFactory();
        factory.EnqueueResponse("D0", "device");
        var sut = new DeviceController(factory, TimeProvider.System);

        await sut.ConnectAsync("COM7", 9600, CancellationToken.None);

        Assert.True(sut.IsConnected);
        Assert.NotNull(factory.LastConnection);
        Assert.True(factory.LastConnection!.OpenCalled);

        await sut.DisconnectAsync(CancellationToken.None);

        Assert.False(sut.IsConnected);
        Assert.False(sut.IsRunning);
        Assert.True(factory.LastConnection.CloseCalled);
        Assert.True(factory.LastConnection.DisposeCalled);
    }

    [Fact]
    public async Task QueryDeviceInfoAsync_ReturnsExpectedValues()
    {
        var factory = new FakeSerialConnectionFactory();
        factory.EnqueueResponse("D0", "device");
        factory.EnqueueResponse("D0", "device");
        factory.EnqueueResponse("D1", "tcode");
        factory.EnqueueResponse("D2", "L0\nR0");

        var sut = new DeviceController(factory, TimeProvider.System);
        await sut.ConnectAsync("COM7", 9600, CancellationToken.None);

        var info = await sut.QueryDeviceInfoAsync(CancellationToken.None);

        Assert.Equal("device", info.D0);
        Assert.Equal("tcode", info.D1);
        Assert.Equal($"L0{Environment.NewLine}R0", info.D2);
    }

    [Fact]
    public async Task StartAsync_CanBeCalledTwice_WithoutFailure()
    {
        var factory = new FakeSerialConnectionFactory();
        factory.EnqueueResponse("D0", "device");
        var sut = new DeviceController(factory, TimeProvider.System);
        await sut.ConnectAsync("COM7", 9600, CancellationToken.None);

        await sut.StartAsync(CancellationToken.None);
        await sut.StartAsync(CancellationToken.None);

        Assert.True(sut.IsRunning);

        await sut.StopAsync(CancellationToken.None);
        Assert.False(sut.IsRunning);
    }

    [Fact]
    public async Task ConnectAsync_WhenDeviceDoesNotRespond_ThrowsAndDisposesConnection()
    {
        var factory = new FakeSerialConnectionFactory();
        var sut = new DeviceController(factory, TimeProvider.System);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ConnectAsync("COM7", 9600, CancellationToken.None));

        Assert.Contains("No response", ex.Message);
        Assert.False(sut.IsConnected);
        Assert.NotNull(factory.LastConnection);
        Assert.True(factory.LastConnection!.OpenCalled);
        Assert.True(factory.LastConnection.CloseCalled);
        Assert.True(factory.LastConnection.DisposeCalled);
    }

    [Fact]
    public async Task ConnectAsync_WhenDeviceRespondsAfterRetry_Connects()
    {
        var factory = new FakeSerialConnectionFactory
        {
            D0AttemptsBeforeResponse = 2,
            DelayedD0Response = "device"
        };
        var sut = new DeviceController(factory, TimeProvider.System);

        await sut.ConnectAsync("COM7", 9600, CancellationToken.None);

        Assert.True(sut.IsConnected);
        Assert.Equal(2, factory.LastConnection?.D0SendCount);
    }

    private sealed class FakeSerialConnectionFactory : ISerialConnectionFactory
    {
        private readonly Dictionary<string, Queue<string>> _responses = new(StringComparer.OrdinalIgnoreCase);

        public FakeSerialConnection? LastConnection { get; private set; }
        public int D0AttemptsBeforeResponse { get; init; }
        public string? DelayedD0Response { get; init; }

        public void EnqueueResponse(string command, string response)
        {
            if (!_responses.TryGetValue(command, out var queue))
            {
                queue = new Queue<string>();
                _responses[command] = queue;
            }

            queue.Enqueue(response);
        }

        public ISerialConnection Create(string portName, int baudRate)
        {
            LastConnection = new FakeSerialConnection(_responses, D0AttemptsBeforeResponse, DelayedD0Response);
            return LastConnection;
        }
    }

    private sealed class FakeSerialConnection : ISerialConnection
    {
        private readonly Dictionary<string, Queue<string>> _responses;
        private readonly Queue<string> _readQueue = new();
        private readonly int _d0AttemptsBeforeResponse;
        private readonly string? _delayedD0Response;

        public FakeSerialConnection(
            Dictionary<string, Queue<string>> responses,
            int d0AttemptsBeforeResponse,
            string? delayedD0Response)
        {
            _responses = responses;
            _d0AttemptsBeforeResponse = d0AttemptsBeforeResponse;
            _delayedD0Response = delayedD0Response;
        }

        public bool IsOpen { get; private set; }

        public bool OpenCalled { get; private set; }
        public bool CloseCalled { get; private set; }
        public bool DisposeCalled { get; private set; }
        public int D0SendCount { get; private set; }

        public Task OpenAsync()
        {
            OpenCalled = true;
            IsOpen = true;
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            CloseCalled = true;
            IsOpen = false;
            return Task.CompletedTask;
        }

        public Task SendLineAsync(string line, CancellationToken cancellationToken)
        {
            if (string.Equals(line, "D0", StringComparison.OrdinalIgnoreCase)
                && _delayedD0Response is not null)
            {
                D0SendCount++;
                if (D0SendCount >= _d0AttemptsBeforeResponse)
                {
                    _readQueue.Enqueue(_delayedD0Response);
                }

                return Task.CompletedTask;
            }

            if (_responses.TryGetValue(line, out var queue) && queue.Count > 0)
            {
                foreach (var split in queue.Dequeue().Split('\n'))
                {
                    _readQueue.Enqueue(split);
                }
            }

            return Task.CompletedTask;
        }

        public Task<string?> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            if (_readQueue.Count == 0)
            {
                return Task.FromResult<string?>(null);
            }

            return Task.FromResult<string?>(_readQueue.Dequeue());
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            IsOpen = false;
            return ValueTask.CompletedTask;
        }
    }
}
