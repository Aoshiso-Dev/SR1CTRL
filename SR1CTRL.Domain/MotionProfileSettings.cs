namespace SR1CTRL.Domain;

public sealed record MotionProfileSettings(
    MotionProfileKind Kind,
    double Intensity)
{
    public MotionProfileSettings Normalize()
    {
        var intensity = double.IsNaN(Intensity) || double.IsInfinity(Intensity)
            ? 0.5
            : Math.Clamp(Intensity, 0.0, 1.0);

        return this with { Intensity = intensity };
    }
}
