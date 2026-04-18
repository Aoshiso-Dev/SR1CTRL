namespace SR1CTRL.Application.Models;

public sealed record DeviceRuntimeState(
    bool IsConnected,
    bool IsRunning);
