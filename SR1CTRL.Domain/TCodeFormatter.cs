namespace SR1CTRL.Domain;

public static class TCodeFormatter
{
    public static string AxisWithInterval(AxisMotionSettings settings, double target, int intervalMs)
    {
        settings = settings.Normalize();

        var axisChar = settings.Axis == AxisType.L ? "L" : "R";
        var magnitude = ToMagnitude4(target);

        return $"{axisChar}{settings.Channel}{magnitude:0000}I{intervalMs}";
    }

    private static int ToMagnitude4(double value)
    {
        value = Math.Max(0.0, Math.Min(0.9999, value));

        var magnitude = (int)Math.Round(value * 10000.0, MidpointRounding.AwayFromZero);
        if (magnitude >= 10000) magnitude = 9999;
        if (magnitude < 0) magnitude = 0;

        return magnitude;
    }
}
