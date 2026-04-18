namespace SR1CTRL.Application.Models;

public sealed record LinearMotionRequest(
    int Channel,
    double Min,
    double Max,
    double SpeedPerSecond);
