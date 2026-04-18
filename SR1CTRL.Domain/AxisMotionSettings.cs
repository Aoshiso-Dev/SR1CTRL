namespace SR1CTRL.Domain;

public sealed record AxisMotionSettings(
    AxisType Axis,
    int Channel,
    double Min,
    double Max,
    double SpeedPerSecond)
{
    public AxisMotionSettings Normalize()
    {
        if (Channel is < 0 or > 9) throw new ArgumentOutOfRangeException(nameof(Channel));

        var min = Clamp01Exclusive(Min);
        var max = Clamp01Exclusive(Max);

        if (Math.Abs(max - min) < 1e-12) throw new ArgumentException("Min and Max must be different.");
        if (SpeedPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(SpeedPerSecond));
        if (max < min) (min, max) = (max, min);

        return this with { Min = min, Max = max };
    }

    public int OneWayRampMs()
    {
        var normalized = Normalize();
        var dist = Math.Abs(normalized.Max - normalized.Min);
        var seconds = dist / normalized.SpeedPerSecond;
        var ms = (int)Math.Round(seconds * 1000.0, MidpointRounding.AwayFromZero);

        return Math.Max(ms, 1);
    }

    private static double Clamp01Exclusive(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return 0.0;
        if (value < 0.0) return 0.0;
        if (value >= 0.9999) return 0.9999;

        return value;
    }
}
