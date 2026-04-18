using SR1CTRL.Application.Models;
using SR1CTRL.Application.UseCases;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.Tests;

public sealed class DeviceControlUseCaseTests
{
    [Fact]
    public async Task ConnectAsync_ReturnsConnectedState()
    {
        var fake = new FakeDeviceController();
        var sut = new DeviceControlUseCase(fake);

        var state = await sut.ConnectAsync("COM3", 115200, CancellationToken.None);

        Assert.True(state.IsConnected);
        Assert.False(state.IsRunning);
        Assert.Equal("COM3", fake.LastPortName);
        Assert.Equal(115200, fake.LastBaudRate);
    }

    [Fact]
    public async Task StartAndStopAsync_UpdatesRunningState()
    {
        var fake = new FakeDeviceController { IsConnected = true };
        var sut = new DeviceControlUseCase(fake);

        var started = await sut.StartAsync(CancellationToken.None);
        var stopped = await sut.StopAsync(CancellationToken.None);

        Assert.True(started.IsRunning);
        Assert.False(stopped.IsRunning);
    }

    [Fact]
    public void ApplyLinear_ClampsTooSmallSpeed()
    {
        var fake = new FakeDeviceController { IsConnected = true };
        var sut = new DeviceControlUseCase(fake);

        sut.ApplyLinear(new LinearMotionRequest(
            Channel: 0,
            Min: 0.2,
            Max: 0.8,
            SpeedPerSecond: 0));

        Assert.NotNull(fake.LastLinear);
        Assert.True(fake.LastLinear!.SpeedPerSecond >= 0.0001);
    }

    private sealed class FakeDeviceController : IDeviceController
    {
        public bool IsConnected { get; set; }
        public bool IsRunning { get; set; }

        public string? LastPortName { get; private set; }
        public int LastBaudRate { get; private set; }

        public AxisMotionSettings? LastLinear { get; private set; }
        public AxisMotionSettings? LastRotate { get; private set; }

        public Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken)
        {
            LastPortName = portName;
            LastBaudRate = baudRate;
            IsConnected = true;
            IsRunning = false;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken)
        {
            IsConnected = false;
            IsRunning = false;
            return Task.CompletedTask;
        }

        public Task<DeviceInfo> QueryDeviceInfoAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeviceInfo("d0", "d1", "d2"));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public void ConfigureLinear(AxisMotionSettings settings, bool applyImmediately = true)
        {
            LastLinear = settings;
        }

        public void ConfigureRotate(AxisMotionSettings settings, bool applyImmediately = true)
        {
            LastRotate = settings;
        }
    }
}
