namespace SR1CTRL.Application.Models;

public sealed record RotateMotionRequest(
    int Channel,
    double Min,
    double Max,
    double SpeedPerSecond);
