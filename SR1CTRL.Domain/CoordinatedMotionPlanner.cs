namespace SR1CTRL.Domain;

public sealed class CoordinatedMotionPlanner
{
    private const int SegmentCount = 12;

    private AxisMotionSettings _linear;
    private AxisMotionSettings _rotate;
    private MotionProfileSettings _profile;
    private bool _towardMax = true;
    private int _segmentIndex;

    public CoordinatedMotionPlanner(
        AxisMotionSettings linear,
        AxisMotionSettings rotate,
        MotionProfileSettings profile)
    {
        _linear = linear.Normalize();
        _rotate = rotate.Normalize();
        _profile = profile.Normalize();
    }

    public DateTimeOffset NextAtUtc { get; private set; } = DateTimeOffset.MinValue;

    public void Update(
        AxisMotionSettings linear,
        AxisMotionSettings rotate,
        MotionProfileSettings profile,
        bool applyImmediately)
    {
        _linear = linear.Normalize();
        _rotate = rotate.Normalize();
        _profile = profile.Normalize();

        if (applyImmediately)
        {
            NextAtUtc = DateTimeOffset.MinValue;
        }
    }

    public MotionFrame Step(DateTimeOffset nowUtc)
    {
        var intervalMs = SegmentIntervalMs(_linear.OneWayRampMs());
        var t = (double)(_segmentIndex + 1) / SegmentCount;
        var linearProgress = EaseInOut(t, _profile.Intensity);
        var rotateProgress = RotateProgress(t);

        var linearTarget = TargetForProgress(_linear, linearProgress);
        var rotateTarget = TargetForProgress(_rotate, rotateProgress);

        _segmentIndex++;
        if (_segmentIndex >= SegmentCount)
        {
            _segmentIndex = 0;
            _towardMax = !_towardMax;
        }

        NextAtUtc = nowUtc + TimeSpan.FromMilliseconds(intervalMs);
        return new MotionFrame(linearTarget, rotateTarget, intervalMs);
    }

    private double RotateProgress(double t)
    {
        return _profile.Kind switch
        {
            MotionProfileKind.AccentTwist => Accent(t, _profile.Intensity),
            _ => EaseInOut(t, _profile.Intensity)
        };
    }

    private double TargetForProgress(AxisMotionSettings settings, double progress)
    {
        var start = _towardMax ? settings.Min : settings.Max;
        var end = _towardMax ? settings.Max : settings.Min;

        return start + ((end - start) * progress);
    }

    private static int SegmentIntervalMs(int oneWayRampMs)
    {
        return Math.Max(1, (int)Math.Round(oneWayRampMs / (double)SegmentCount, MidpointRounding.AwayFromZero));
    }

    private static double EaseInOut(double t, double intensity)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        intensity = Math.Clamp(intensity, 0.0, 1.0);

        var smooth = t * t * (3.0 - (2.0 * t));
        return Lerp(t, smooth, intensity);
    }

    private static double Accent(double t, double intensity)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        intensity = Math.Clamp(intensity, 0.0, 1.0);

        var threshold = Lerp(0.85, 0.55, intensity);
        if (t <= threshold)
        {
            return Lerp(t, 0.0, intensity);
        }

        var accentT = (t - threshold) / (1.0 - threshold);
        var easedAccent = EaseOutCubic(accentT);

        return Lerp(t, easedAccent, intensity);
    }

    private static double EaseOutCubic(double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        var inverse = 1.0 - t;

        return 1.0 - (inverse * inverse * inverse);
    }

    private static double Lerp(double start, double end, double amount)
    {
        return start + ((end - start) * amount);
    }
}
