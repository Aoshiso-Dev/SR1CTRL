namespace SR1CTRL.Application.Models;

public sealed record AppStateSnapshot
{
    public string? SelectedPort { get; init; }

    public int? BaudRate { get; init; }

    public double? L_Min { get; init; }

    public double? L_Max { get; init; }

    public double? L_Speed { get; init; }

    public double? R_Min { get; init; }

    public double? R_Max { get; init; }

    public double? R_Speed { get; init; }
}
