namespace SR1CTRL.Application.Models;

public sealed record AppStateSnapshot
{
    public string? HotkeyDeviceName { get; init; }
    public string? HotkeyStartStopKey { get; init; }
    public string? HotkeyLinearSpeedDownKey { get; init; }
    public string? HotkeyLinearSpeedUpKey { get; init; }
    public string? HotkeyRotateSpeedDownKey { get; init; }
    public string? HotkeyRotateSpeedUpKey { get; init; }

    public string? SelectedPort { get; init; }

    public int? BaudRate { get; init; }

    public double? L_Min { get; init; }

    public double? L_Max { get; init; }

    public double? L_Speed { get; init; }

    public double? R_Min { get; init; }

    public double? R_Max { get; init; }

    public double? R_Speed { get; init; }

    public string? MotionProfile { get; init; }

    public double? MotionIntensity { get; init; }
}

