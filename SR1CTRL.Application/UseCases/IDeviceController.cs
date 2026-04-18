using SR1CTRL.Application.Models;
using SR1CTRL.Domain;

namespace SR1CTRL.Application.UseCases;

public interface IDeviceController
{
    bool IsConnected { get; }
    bool IsRunning { get; }

    Task ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken);
    Task DisconnectAsync(CancellationToken cancellationToken);
    Task<DeviceInfo> QueryDeviceInfoAsync(CancellationToken cancellationToken);
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    void ConfigureLinear(AxisMotionSettings settings, bool applyImmediately = true);
    void ConfigureRotate(AxisMotionSettings settings, bool applyImmediately = true);
}
