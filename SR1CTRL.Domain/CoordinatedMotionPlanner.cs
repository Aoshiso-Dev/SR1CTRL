namespace SR1CTRL.Domain;

public sealed class CoordinatedMotionPlanner
{
    private const int MaxSegmentCount = 8;
    private const int MinSegmentIntervalMs = 120;

    private AxisMotionSettings _linear;
    private AxisMotionSettings _rotate;
    private MotionProfileSettings _profile;
    private bool _towardMax = true;
    private int _segmentIndex;
    private int _segmentCount;

    public CoordinatedMotionPlanner(
        AxisMotionSettings linear,
        AxisMotionSettings rotate,
        MotionProfileSettings profile)
    {
        _linear = linear.Normalize();
        _rotate = rotate.Normalize();
        _profile = profile.Normalize();
        _segmentCount = CalculateSegmentCount(_linear.OneWayRampMs());
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
        _segmentCount = CalculateSegmentCount(_linear.OneWayRampMs());
        _segmentIndex = Math.Min(_segmentIndex, _segmentCount - 1);

        if (applyImmediately)
        {
            NextAtUtc = DateTimeOffset.MinValue;
            _segmentIndex = 0;
        }
    }

    public MotionFrame Step(DateTimeOffset nowUtc)
    {
        var intervalMs = SegmentIntervalMs(_linear.OneWayRampMs(), _segmentCount);
        var t = (double)(_segmentIndex + 1) / _segmentCount;
        var linearProgress = EaseInOut(t, _profile.Intensity);
        var rotateProgress = RotateProgress(t);

        var linearTarget = TargetForProgress(_linear, linearProgress);
        var rotateTarget = TargetForProgress(_rotate, rotateProgress);

        _segmentIndex++;
        if (_segmentIndex >= _segmentCount)
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

    private static int CalculateSegmentCount(int oneWayRampMs)
    {
        var count = (int)Math.Floor(oneWayRampMs / (double)MinSegmentIntervalMs);
        return Math.Clamp(count, 1, MaxSegmentCount);
    }

    private static int SegmentIntervalMs(int oneWayRampMs, int segmentCount)
    {
        return Math.Max(1, (int)Math.Round(oneWayRampMs / (double)segmentCount, MidpointRounding.AwayFromZero));
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
