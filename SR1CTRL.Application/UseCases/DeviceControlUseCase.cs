using SR1CTRL.Application.Models;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.UseCases;

public sealed class DeviceControlUseCase
{
    private readonly IDeviceController _controller;

    public DeviceControlUseCase(IDeviceController controller)
    {
        _controller = controller;
    }

    public DeviceRuntimeState GetState()
    {
        return new DeviceRuntimeState(_controller.IsConnected, _controller.IsRunning);
    }

    public async Task<DeviceRuntimeState> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken)
    {
        await _controller.ConnectAsync(portName, baudRate, cancellationToken).ConfigureAwait(false);
        return GetState();
    }

    public async Task<DeviceRuntimeState> DisconnectAsync(CancellationToken cancellationToken)
    {
        await _controller.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        return GetState();
    }

    public Task<DeviceInfo> QueryDeviceInfoAsync(CancellationToken cancellationToken)
    {
        return _controller.QueryDeviceInfoAsync(cancellationToken);
    }

    public async Task<DeviceRuntimeState> StartAsync(CancellationToken cancellationToken)
    {
        await _controller.StartAsync(cancellationToken).ConfigureAwait(false);
        return GetState();
    }

    public async Task<DeviceRuntimeState> StopAsync(CancellationToken cancellationToken)
    {
        await _controller.StopAsync(cancellationToken).ConfigureAwait(false);
        return GetState();
    }

    public void ApplyLinear(LinearMotionRequest request, bool applyImmediately = true)
    {
        var settings = new AxisMotionSettings(
            Axis: AxisType.L,
            Channel: request.Channel,
            Min: request.Min,
            Max: request.Max,
            SpeedPerSecond: Math.Max(0.0001, request.SpeedPerSecond));

        _controller.ConfigureLinear(settings, applyImmediately);
    }

    public void ApplyRotate(RotateMotionRequest request, bool applyImmediately = true)
    {
        var settings = new AxisMotionSettings(
            Axis: AxisType.R,
            Channel: request.Channel,
            Min: request.Min,
            Max: request.Max,
            SpeedPerSecond: Math.Max(0.0001, request.SpeedPerSecond));

        _controller.ConfigureRotate(settings, applyImmediately);
    }
}
