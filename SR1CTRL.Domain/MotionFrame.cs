namespace SR1CTRL.Domain;

public sealed record MotionFrame(
    double LinearTarget,
    double RotateTarget,
    int IntervalMs);
